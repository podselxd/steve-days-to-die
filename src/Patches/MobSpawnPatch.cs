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
            if (className == null) return;
            // Minecraft's animals: Minecraft's look built from boxes
            if (Generated.McAnimalsTable.Animals.TryGetValue(className, out var mc))
            {
                if (GameManager.IsDedicatedServer || __instance.GetComponent<McAnimal>() != null) return;
                var a = __instance.gameObject.AddComponent<McAnimal>();
                a.AnimalId = mc.id;
                a.TexturePath = mc.texture;
                if (mc.id == "creeper" && __instance.GetComponent<McCreeper>() == null) __instance.gameObject.AddComponent<McCreeper>();
                if (mc.id == "phantom" && __instance.GetComponent<McPhantom>() == null) __instance.gameObject.AddComponent<McPhantom>();
                return;
            }
            // 7 Days animals keep their own look: the old texture swap (Minecraft skins on 7 Days bodies) is gone
            // (user 2026-11-13); Minecraft animals are real Minecraft models (above)
            return;
#pragma warning disable CS0162
            if (!MobVisuals.HasMapping(className)) return;

            // Skip if this is a server entity (no visual on server)
            if (!__instance.isEntityRemote && GameManager.IsDedicatedServer) return;

            if (__instance.GetComponent<MobTextureApplier>() != null) return; // already applied

            var applier = __instance.gameObject.AddComponent<MobTextureApplier>();
            applier.EntityClassName = className;
        }
    }
}
