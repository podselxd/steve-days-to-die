using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>A Minecraft mob that attacks swings its arm (McAnimal.SwingAt drives Minecraft's attack animation).</summary>
    [HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.Attack))]
    static class McMobAttack
    {
        static void Postfix(EntityAlive __instance, bool __result)
        {
            if (!__result || __instance == null) return;
            var look = __instance.GetComponent<Mobs.McAnimal>();
            if (look != null && Time.time - look.SwingAt > 0.6f) look.SwingAt = Time.time;
        }
    }
}
