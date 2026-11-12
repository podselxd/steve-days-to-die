using System.Collections.Generic;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>
    /// The Minecraft icon of a 7 Days item's pair. Which item pairs with which comes from
    /// sheets/pairs.json (through the generated PairsTable); the picture is read from the player's
    /// own Minecraft jar, lazily, and cached.
    /// </summary>
    public static class ItemIconManager
    {
        // where an item's flat picture can live inside the jar, tried in order
        static readonly string[] Patterns =
        {
            "item/{0}", "block/{0}", "block/{0}_front", "block/{0}_side", "block/{0}_top",
        };

        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D GetIcon(string itemName)
        {
            if (string.IsNullOrEmpty(itemName)) return null;
            if (_cache.TryGetValue(itemName, out var cached)) return cached;

            Texture2D tex = null;
            if (PairsTable.ToMinecraft.TryGetValue(itemName, out var mcId))
            {
                foreach (var p in Patterns)
                {
                    tex = MinecraftAssets.LoadTexture("assets/minecraft/textures/" + string.Format(p, mcId) + ".png");
                    if (tex != null) break;
                }
            }
            _cache[itemName] = tex;     // remember misses too
            return tex;
        }
    }
}
