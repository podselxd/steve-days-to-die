using System;
using HarmonyLib;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Combat
{
    /// <summary>
    /// Where enchantments and effects (version 2) plug in. Minecraft adds enchantment damage on top of
    /// the base damage, scaled by the attack charge; Efficiency adds to mining speed. Enchantments never
    /// change the attack rhythm; effects do (Haste +10% per level, Mining Fatigue -10% per level).
    /// </summary>
    public static class ItemModifiers
    {
        public static Func<ItemValue, float> ExtraDamage = _ => 0f;                     // Sharpness: 0.5 * level + 0.5
        public static Func<ItemValue, float> MiningSpeedBonus = _ => 0f;                // Efficiency: level^2 + 1
        public static Func<EntityPlayerLocal, float> AttackSpeedMultiplier = _ => 1f;   // Haste / Mining Fatigue
    }

    /// <summary>
    /// Minecraft's attack bar. Every weapon and tool has its own recharge time (sheets/combat.json);
    /// a hit does 20% to 100% of its damage depending on how recharged the swing was, and a fully
    /// charged hit landed while falling (and not sprinting) is critical: 150%.
    /// </summary>
    public static class AttackCharge
    {
        public const float CritMultiplier = 1.5f;
        const float HandCooldown = 0.25f;       // fist; in Minecraft anything that is not a weapon also hits at this speed
        const float DefaultCooldown = 0.625f;   // a melee item missing from the table: sword speed

        /// <summary>The local player started a swing.</summary>
        public static event Action OnSwing;
        /// <summary>One of the local player's swings hit a living thing.</summary>
        public static event Action OnHit;

        public static float LastCritTime = -10f;
        // the last hit this rule changed (read by the automatic test)
        public static int HitCount, LastHitBefore, LastHitAfter;
        public static bool LastHitCrit;

        static float _lastAttack = -10f, _pressTime = -10f, _chargeAtPress = 1f, _cooldown = HandCooldown;
        static int _lastSlot = -1;
        static string _lastItem;

        public static bool Enabled => Settings.GetBool("ataque_minecraft", true);

        /// <summary>Seconds the bar takes to refill for this item.</summary>
        public static float Cooldown(ItemClass ic)
        {
            if (ic == null) return HandCooldown;
            if (CombatTable.Cooldown.TryGetValue(ic.Name, out float c)) return c;
            return ic.Name.StartsWith("meleeHand") ? HandCooldown : DefaultCooldown;
        }

        public static float CurrentCooldown => _cooldown;

        /// <summary>0..1: how recharged the next hit is.</summary>
        public static float Charge => Mathf.Clamp01((Time.time - _lastAttack) / Mathf.Max(0.05f, _cooldown));

        /// <summary>Minecraft's damage rule, in one place (also used by the test).</summary>
        public static int Damage(int baseDamage, float charge, bool critical, float enchantDamage)
        {
            float d = baseDamage * (0.2f + 0.8f * charge * charge);
            if (critical) d *= CritMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(d + enchantDamage * charge));
        }

        public static bool IsMelee(ItemClass ic)
        {
            if (ic == null) return true;    // bare hands
            if (ic.Actions == null || ic.Actions.Length == 0 || ic.Actions[0] == null) return false;
            return ic.Actions[0] is ItemActionMelee || ic.Actions[0] is ItemActionDynamicMelee;
        }

        static float _lastY = float.NaN, _vy;

        /// <summary>Vertical speed measured from the player's real position (the game's own "motion" is not reliable for the local player).</summary>
        public static float VerticalSpeed => _vy;

        static void TrackVerticalSpeed(EntityPlayerLocal p)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            float y = p.position.y;
            if (!float.IsNaN(_lastY))
            {
                float dy = y - _lastY;
                _vy = Mathf.Abs(dy) > 8f ? 0f : dy / dt;      // a teleport is not a fall
            }
            _lastY = y;
        }

        /// <summary>Falling the way a Minecraft critical needs: in the air, going down, not in water or on a ladder.</summary>
        public static bool IsFalling(EntityPlayerLocal p)
            => !p.onGround && !p.isInWater && !p.IsInElevator() && (_vy < -0.5f || p.motion.y < -0.01f || p.fallDistance > 0.05f);

        /// <summary>Called every frame for the local player.</summary>
        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || !p.IsAlive()) return;
            TrackVerticalSpeed(p);
            if (!Enabled) return;
            var inv = p.inventory;
            var ic = inv != null ? inv.holdingItem : null;
            bool melee = IsMelee(ic);
            _cooldown = (melee ? Cooldown(ic) : HandCooldown) / Mathf.Max(0.1f, ItemModifiers.AttackSpeedMultiplier(p));

            // Minecraft restarts the bar whenever you change what you hold
            int slot = inv != null ? inv.holdingItemIdx : -1;
            string name = ic != null ? ic.Name : null;
            if (slot != _lastSlot || name != _lastItem)
            {
                if (_lastSlot != -1) _lastAttack = Time.time;
                _lastSlot = slot;
                _lastItem = name;
            }

            if (!melee || Cursor.lockState != CursorLockMode.Locked) return;
            bool pressed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            // holding the button: the game keeps swinging at the item's rhythm, so count each new swing
            bool held = Input.GetMouseButton(0) && Charge >= 1f && Time.time - _pressTime > _cooldown + 0.05f;
            if (pressed || held) Swing();
        }

        public static void Swing()
        {
            _chargeAtPress = Charge;
            _pressTime = _lastAttack = Time.time;
            OnSwing?.Invoke();
        }

        [HarmonyPatch(typeof(EntityAlive), "DamageEntity")]
        static class DamagePatch
        {
            static void Prefix(EntityAlive __instance, DamageSource _damageSource, ref int _strength)
            {
                try
                {
                    if (!Enabled || _strength <= 0) return;
                    var local = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (local == null || __instance == local) return;
                    var src = _damageSource as DamageSourceEntity;
                    if (src == null || src.getEntityId() != local.entityId) return;
                    if (Time.time - _pressTime > 1.2f || !IsMelee(local.inventory?.holdingItem)) return;   // not our melee swing

                    bool crit = _chargeAtPress > 0.9f && IsFalling(local) && !local.MovementRunning;
                    int before = _strength;
                    _strength = Damage(before, _chargeAtPress, crit, ItemModifiers.ExtraDamage(_damageSource.AttackingItem));
                    // Minecraft's mace smash: +4 per block fallen for the first 3, +2 per block for the next 5, +1 after
                    // (Minecraft health; x5 for 7 Days), and the fall itself is forgiven
                    if (local.inventory?.holdingItem?.Name == "mc_mace" && local.fallDistance > 1.5f)
                    {
                        float f = local.fallDistance, bonus = Mathf.Min(f, 3f) * 4f + Mathf.Clamp(f - 3f, 0f, 5f) * 2f + Mathf.Max(0f, f - 8f);
                        _strength += Mathf.RoundToInt(bonus * 5f);
                        local.fallDistance = 0f;
                    }
                    if (crit) LastCritTime = Time.time;
                    HitCount++;
                    LastHitBefore = before; LastHitAfter = _strength; LastHitCrit = crit;
                    Debug.Log($"[SVD] Hit: {before} -> {_strength} (charge {_chargeAtPress:0.00}{(crit ? ", CRITICAL" : "")}).");
                    OnHit?.Invoke();
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] AttackCharge: " + ex.Message); }
            }
        }
    }
}
