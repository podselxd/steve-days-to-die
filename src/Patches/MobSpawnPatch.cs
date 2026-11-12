using HarmonyLib;
using SteveDaysToDie.Mobs;

namespace SteveDaysToDie.Patches
{
    [HarmonyPatch(typeof(EntityAlive), "Init")]
    static class MobSpawnPatch
    {
        [HarmonyPostfix]
        static void Postfix(EntityAlive __instance)
        {
            var className = __instance.EntityClass?.entityClassName;
            if (className == null || !MobVisuals.HasMapping(className)) return;

            // Skip if this is a server entity (no visual on server)
            if (!__instance.isEntityRemote && GameManager.IsDedicatedServer) return;

            if (__instance.GetComponent<MobTextureApplier>() != null) return; // already applied

            var applier = __instance.gameObject.AddComponent<MobTextureApplier>();
            applier.EntityClassName = className;
        }
    }
}
