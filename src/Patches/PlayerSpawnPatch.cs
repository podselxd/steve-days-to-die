using System;
using HarmonyLib;
using SteveDaysToDie.HUD;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    [HarmonyPatch(typeof(EntityPlayerLocal), "Init")]
    public static class PlayerSpawnPatch
    {
        [HarmonyPostfix]
        static void Postfix(EntityPlayerLocal __instance)
        {
            Attach(__instance);
        }

        public static void Attach(EntityPlayerLocal player)
        {
            if (player == null) return;
            try
            {
                if (player.GetComponent<Steve.SteveModel>() == null)
                    player.gameObject.AddComponent<Steve.SteveModel>();
                if (player.GetComponent<Steve.SteveArm>() == null)
                    player.gameObject.AddComponent<Steve.SteveArm>();

                if (player.GetComponent<MinecraftHUD>() == null)
                    player.gameObject.AddComponent<MinecraftHUD>();

                if (player.GetComponent<FrontView>() == null)
                    player.gameObject.AddComponent<FrontView>();

                if (player.GetComponent<NativeToolbelt>() == null)
                    player.gameObject.AddComponent<NativeToolbelt>();

                Debug.Log("[SVD] Player ready: Minecraft HUD attached.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SVD] Attach failed: " + ex);
            }
        }
    }
}
