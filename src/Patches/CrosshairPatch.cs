using HarmonyLib;
using SteveDaysToDie.HUD;

namespace SteveDaysToDie.Patches
{
    // The Minecraft crosshair replaces the 7 Days one (both together drew a star).
    [HarmonyPatch(typeof(EntityPlayerLocal), "guiDrawCrosshair")]
    static class CrosshairPatch
    {
        static bool Prefix() => !(HUDTextures.Loaded && HUDTextures.Crosshair != null);
    }
}
