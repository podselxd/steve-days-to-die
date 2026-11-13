using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// 7 Days' GPU occlusion culling switches grass, decorations, chunk layers and distant terrain on and off from a
    /// depth picture of the player's camera. Our extra drawing (the Minecraft hand camera, the mining crack, the
    /// Minecraft block faces) confuses it: dark "plates" appeared around when mining, textures vanished, even showed
    /// up in the sky, getting worse over time. The mod turns it off (setting oclusion_juego=si brings it back).
    /// </summary>
    public static class OcclusionOff
    {
        static bool _logged;

        public static bool Wanted => !Settings.GetBool("oclusion_juego", true);     // on again (faster); =no switches it off

        /// <summary>At game start: the game reads this option when a world loads.</summary>
        public static void Init()
        {
            if (!Wanted) return;
            try { GamePrefs.Set(EnumGamePrefs.OptionsGfxOcclusion, false); } catch { }
        }

        /// <summary>Every frame: if it is on anyway (a world loaded before, or the player switched it on), switch it off.</summary>
        public static void Tick()
        {
            if (!Wanted) return;
            try
            {
                var om = OcclusionManager.Instance;
                if (om == null || !om.GPUCullingEnabled) return;
                om.EnableCulling(false);
                om.SetRenderersEnabled(true);          // show everything it had hidden
                if (!_logged) { _logged = true; Debug.Log("[SVD] 7 Days occlusion culling switched off (it broke grass and decorations with our drawing)"); }
            }
            catch (System.Exception ex) { if (!_logged) { _logged = true; Debug.LogWarning("[SVD] occlusion: " + ex.Message); } }
        }
    }
}
