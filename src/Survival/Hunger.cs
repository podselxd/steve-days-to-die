using System;
using HarmonyLib;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Survival
{
    /// <summary>
    /// Tiredness the Minecraft way (numbers in sheets/survival.json): there is no stamina bar.
    /// Running, jumping and landing hits build up exhaustion; every 4 points of it cost one point of
    /// saturation (the hidden reserve you fill by eating) and, once that is gone, one food point of 20.
    /// With 6 food points or fewer you can no longer sprint; with 18 or more, health comes back.
    /// Thirst is off by default.
    /// </summary>
    /// <summary>
    /// Minecraft's sprint: tap forward twice quickly and keep holding it. Letting go of forward,
    /// sneaking or opening a menu ends it; hunger still stops it (6 food points or fewer).
    /// The game's own sprint key keeps working too.
    /// </summary>
    public static class DoubleTapSprint
    {
        const float Window = 0.3f;      // Minecraft: 7 ticks
        static float _lastPress = -10f;
        public static bool Active { get; private set; }

        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || !Settings.GetBool("doble_w_corre", true)) { Active = false; return; }
            KeyCode forward = Settings.GetKey("tecla_adelante", KeyCode.W);
            bool playing = Cursor.lockState == CursorLockMode.Locked;
            if (playing && Input.GetKeyDown(forward))
            {
                if (Time.time - _lastPress < Window) Active = true;
                _lastPress = Time.time;
            }
            if (!playing || !Input.GetKey(forward) || p.IsCrouching || !p.IsAlive()) Active = false;
        }
    }

    public static class Hunger
    {
        // Minecraft food eaten and not yet counted in (the game adds the food a little at a time)
        static float _mcFoodPending, _mcSatPending, _heldSince;
        static int _heldSlot = -1, _heldCount;
        static string _heldName;
        static float _exhaustion, _saturation = SurvivalTable.StartSaturation, _lastFood = -1f, _regenTimer;
        static Vector3 _lastPos;
        static bool _hasPos, _wasAirborneUp, _wasOnGround, _hooked, _errorLogged;
        static int _entityId = -1;

        public static bool Enabled => Settings.GetBool("cansancio_minecraft", true);
        public static bool RegenEnabled => Settings.GetBool("regeneracion_minecraft", true);

        /// <summary>True while the player is too hungry to sprint.</summary>
        public static bool SprintBlocked { get; private set; }
        public static float Saturation => _saturation;
        public static float Exhaustion => _exhaustion;
        /// <summary>How many times the sprint switch was actually held off (read by the automatic test).</summary>
        public static int SprintBlocksApplied;

        public static void AddExhaustion(float amount) { _exhaustion += amount; }

        /// <summary>Minecraft's rule, in one place (also used by the test): spends exhaustion on saturation first, then food.</summary>
        public static void Spend(ref float exhaustion, ref float saturation, ref float foodPoints)
        {
            while (exhaustion >= SurvivalTable.ExhaustionPerPoint)
            {
                exhaustion -= SurvivalTable.ExhaustionPerPoint;
                if (saturation > 0f) saturation = Mathf.Max(0f, saturation - 1f);
                else foodPoints = Mathf.Max(0f, foodPoints - 1f);
            }
        }

        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || p.Stats == null || !p.IsAlive()) { _hasPos = false; SprintBlocked = false; return; }
            try
            {
                if (!Settings.GetBool("sed", false) && p.Stats.Water != null)
                    p.Stats.Water.Value = p.Stats.Water.Max;
                // Minecraft has no stamina: 7 Days' runs out while running and leaves you crawling ("no avanza")
                if (p.Stats.Stamina != null && p.Stats.Stamina.Value < p.Stats.Stamina.Max) p.Stats.Stamina.Value = p.Stats.Stamina.Max;
                if (!Enabled) { SprintBlocked = false; return; }

                if (!_hooked) { _hooked = true; Combat.AttackCharge.OnHit += () => _exhaustion += SurvivalTable.ExhaustionAttackHit; }

                var food = p.Stats.Food;
                float point = food.Max / 20f;                       // one Minecraft food point, in 7 Days units
                if (point <= 0f) return;

                if (p.entityId != _entityId || _lastFood < 0f)      // new session or another character
                {
                    _entityId = p.entityId;
                    _lastFood = food.Value;
                    _exhaustion = 0f;
                    _saturation = Mathf.Min(SurvivalTable.StartSaturation, food.Value / point);
                    _hasPos = false;
                }

                // a Minecraft food eaten from the hand (its stack went down by one): its own saturation, as in Minecraft
                try
                {
                    int slot = p.inventory.holdingItemIdx;
                    var st = p.inventory.GetItem(slot);
                    string held = st != null && !st.IsEmpty() ? st.itemValue.ItemClass?.Name : null;
                    int count = held != null ? st.count : 0;
                    if (slot == _heldSlot && _heldName != null && _heldName.StartsWith("mc_") && (held != _heldName || count < _heldCount)
                        && SteveDaysToDie.Generated.McFoodTable.Foods.TryGetValue(_heldName.Substring(3), out var mf)
                        && p.inventory.holdingItem != null && Time.time - _heldSince > 0.5f)
                    {
                        _mcFoodPending += mf.nutrition;
                        _mcSatPending += mf.saturation;
                    }
                    if (slot != _heldSlot || held != _heldName) _heldSince = Time.time;
                    _heldSlot = slot; _heldName = held; _heldCount = count;
                }
                catch { }

                // eating (the game raised the food stat): fill the saturation reserve too
                float gained = (food.Value - _lastFood) / point;
                if (gained > 0.01f)
                {
                    float mcPart = Mathf.Min(gained, _mcFoodPending);       // points that came from a Minecraft food
                    if (mcPart > 0f)
                    {
                        float sat = _mcSatPending * mcPart / _mcFoodPending;
                        _mcSatPending -= sat; _mcFoodPending -= mcPart;
                        _saturation = Mathf.Min(_saturation + sat, food.Value / point);
                    }
                    if (gained - mcPart > 0.01f)
                        _saturation = Mathf.Min(_saturation + (gained - mcPart) * SurvivalTable.SaturationPerFoodPointEaten, food.Value / point);
                }

                // sprinting
                Vector3 pos = p.position;
                if (_hasPos && p.MovementRunning)
                {
                    Vector3 d = pos - _lastPos; d.y = 0f;
                    float m = d.magnitude;
                    if (m < 3f) _exhaustion += m * SurvivalTable.ExhaustionSprintPerMetre;      // ignore teleports
                }
                _lastPos = pos; _hasPos = true;

                // a jump = leaving the ground going up
                bool airborneUp = !p.onGround && p.motion.y > 0.05f;
                if (airborneUp && !_wasAirborneUp && _wasOnGround)
                    _exhaustion += p.MovementRunning ? SurvivalTable.ExhaustionSprintJump : SurvivalTable.ExhaustionJump;
                _wasAirborneUp = airborneUp;
                _wasOnGround = p.onGround;

                // health comes back when well fed, and that makes you hungry
                float foodPoints = food.Value / point;
                if (RegenEnabled && p.Health < p.GetMaxHealth())
                {
                    _regenTimer += Time.deltaTime;
                    float hp = p.GetMaxHealth() / 20f;              // one Minecraft health point (half a heart)
                    if (foodPoints >= 19.75f && _saturation > 0f)
                    {
                        if (_regenTimer >= SurvivalTable.RegenFastSeconds)
                        {
                            _regenTimer = 0f;
                            float use = Mathf.Min(_saturation, SurvivalTable.RegenFastMaxSaturation);
                            p.AddHealth(Mathf.Max(1, Mathf.RoundToInt(hp * use / SurvivalTable.RegenFastMaxSaturation)));
                            _exhaustion += use;
                        }
                    }
                    else if (foodPoints >= SurvivalTable.RegenMinFood)
                    {
                        if (_regenTimer >= SurvivalTable.RegenSlowSeconds)
                        {
                            _regenTimer = 0f;
                            p.AddHealth(Mathf.Max(1, Mathf.RoundToInt(hp)));
                            _exhaustion += SurvivalTable.ExhaustionRegenPerHp;
                        }
                    }
                    else _regenTimer = 0f;
                }
                else _regenTimer = 0f;

                // pay for the tiredness
                float before = foodPoints;
                Spend(ref _exhaustion, ref _saturation, ref foodPoints);
                if (foodPoints < before) food.Value = foodPoints * point;
                _lastFood = food.Value;

                // No stamina bar: always full. Too hungry = no sprinting (see SprintPatch), as in Minecraft.
                p.Stats.Stamina.Value = p.Stats.Stamina.Max;
                SprintBlocked = foodPoints <= SurvivalTable.SprintMinFood;
            }
            catch (Exception ex)
            {
                if (!_errorLogged) { _errorLogged = true; Debug.LogWarning("[SVD] Hunger: " + ex); }
            }
        }

        // The game decides whether you run in MoveByInput and refuses when "exhausted": use that same switch.
        [HarmonyPatch(typeof(EntityPlayerLocal), "MoveByInput")]
        static class SprintPatch
        {
            static void Prefix(EntityPlayerLocal __instance)
            {
                int tm = Patches.McRun.TestMove;
                if (tm > 0) { __instance.movementInput.moveForward = 1f; __instance.movementInput.running = tm == 2; __instance.movementInput.down = tm == 3; }
                if (DoubleTapSprint.Active) __instance.movementInput.running = true;
                if (SprintBlocked && Enabled)
                {
                    __instance.bExhausted = true;
                    SprintBlocksApplied++;
                }
            }
        }
    }
}
