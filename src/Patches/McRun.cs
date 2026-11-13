using HarmonyLib;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Minecraft's sprint (user 2026-11-13: "que no corra en automatico pq es lo que hace esa madre no avanza"):
    /// 7 Days turns a quick tap of the run key into a run that stays on by itself (runToggleActive), and its stamina
    /// then runs out and the player can barely move. Minecraft: you sprint while you hold the key (or after a double
    /// tap of forward, while you keep forward held), only forwards, and there is no stamina (only hunger stops it).
    /// Hunger.Tick keeps 7 Days' stamina full.
    /// </summary>
    [HarmonyPatch(typeof(PlayerMoveController), "Update")]
    static class McRun
    {
        /// <summary>Test hook (MeltyDemo "andar"): 1 walk, 2 run, 3 crouch forward on its own.</summary>
        public static int TestMove;

        static void Postfix(PlayerMoveController __instance)
        {
            try
            {
                __instance.runToggleActive = false;
                var p = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (p == null || p.AttachedToEntity != null) return;          // driving: 7 Days' own controls
                var a = LocalPlayerUI.GetUIForPlayer(p)?.playerInput;
                if (a == null) return;
                if (TestMove > 0) { p.movementInput.moveForward = 1f; p.movementInput.down = TestMove == 3; p.movementInput.running = TestMove == 2; return; }
                bool forward = p.movementInput.moveForward > 0.1f;
                p.movementInput.running = forward && !p.movementInput.down && (a.Run.IsPressed || Survival.DoubleTapSprint.Active);
            }
            catch { }
        }
    }
}
