using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>
    /// Things the host does every frame whether or not it has a screen (bug review 2026-11-13: on a dedicated server
    /// the Minecraft biomes' mobs and the bed sleeping only ran from the HUD, which a dedicated server does not have).
    /// Each of them checks it is the server and paces itself.
    /// </summary>
    public class ServerTick : MonoBehaviour
    {
        static ServerTick _inst;

        public static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("SVD_ServerTick");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<ServerTick>();
        }

        void Update()
        {
            if (GameManager.Instance?.World == null) return;
            try { Mobs.McBiomeSpawner.Tick(); } catch { }
            try { Crafting.McBeds.Tick(); } catch { }
        }
    }
}
