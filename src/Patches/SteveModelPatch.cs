using HarmonyLib;
using SteveDaysToDie.Steve;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    // Every player's character model (local and remote) gets the Steve body.
    [HarmonyPatch(typeof(EModelSDCS), "Init")]
    static class SteveModelPatch
    {
        static void Postfix(EModelSDCS __instance)
        {
            var go = __instance.gameObject;
            if (go.GetComponent<EntityPlayer>() == null) return;
            if (go.GetComponent<SteveModel>() == null) go.AddComponent<SteveModel>();
            if (go.GetComponent<EntityPlayerLocal>() != null && go.GetComponent<SteveArm>() == null)
                go.AddComponent<SteveArm>();
        }
    }
}
