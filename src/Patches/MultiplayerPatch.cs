using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Playing with friends: 7 Days only lets you invite friends when the game runs as a server ("Servidor" on in
    /// the New / Continue game screen). Test runs used to switch it off and the game kept that, so "Invitar amigos"
    /// did nothing. Every time that screen opens the server starts ON, friends only (unless the player chose public),
    /// with anti-cheat off (the mod cannot run with it: friends join with our launcher too). The player can still
    /// switch it off on that screen.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_NewContinueGameSettings), "ApplyInitialServerEnabledState")]
    static class MultiplayerPatch
    {
        static void Prefix()
        {
            if (MeltyTest.Active) return;
            try
            {
                GamePrefs.Set(EnumGamePrefs.ServerEnabled, true);
                if (GamePrefs.GetInt(EnumGamePrefs.ServerVisibility) == 0) GamePrefs.Set(EnumGamePrefs.ServerVisibility, 1);
                GamePrefs.Set(EnumGamePrefs.ServerEACPeerToPeer, false);
                Debug.Log("[SVD] multiplayer: server on (friends can join), visibility " + GamePrefs.GetInt(EnumGamePrefs.ServerVisibility));
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] multiplayer prefs: " + ex.Message); }
        }
    }
}
