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
                if (__instance.GetComponent<McCreeper>() != null || __instance.GetComponent<McAnimal>() != null
                    || __instance.GetComponent<McPhantom>() != null || __instance.GetComponent<McSunBurn>() != null
                    || __instance.GetComponent<McSpider>() != null || __instance.GetComponent<McSlime>() != null
                    || __instance.GetComponent<McRanged>() != null || __instance.GetComponent<McFlyer>() != null) return;
                if (!GameManager.IsDedicatedServer)
                {
                    var a = __instance.gameObject.AddComponent<McAnimal>();
                    a.AnimalId = mc.id;
                    a.TexturePath = mc.texture;
                    a.Scale = mc.scale;
                }
                var go = __instance.gameObject;
                // Minecraft behaviours (sheets/mc_animals.json "ai"): src/Mobs/McMonsters.cs, McFlyers.cs, McCreeper.cs, McPhantom.cs
                foreach (var ai in (mc.ai ?? "").Split(','))
                    switch (ai)
                    {
                        case "creeper": go.AddComponent<McCreeper>(); break;
                        case "phantom": go.AddComponent<McPhantom>(); break;
                        case "ranged": go.AddComponent<McRanged>(); break;
                        case "sun": go.AddComponent<McSunBurn>(); break;
                        case "spider": go.AddComponent<McSpider>(); break;
                        case "slime": go.AddComponent<McSlime>(); break;
                        case "fly": go.AddComponent<McFlyer>(); break;
                        case "wither": go.AddComponent<McWitherTouch>(); go.AddComponent<McSunBurn>(); break;
                    }
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
