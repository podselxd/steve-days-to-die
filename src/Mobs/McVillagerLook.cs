using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft's villager is drawn in layers (VillagerProfessionLayer): the body (villager/villager.png), the clothes
    /// of its biome type (villager/type/&lt;desert|jungle|plains|savanna|snow|swamp|taiga&gt;.png), its profession
    /// (villager/profession/&lt;armorer...weaponsmith&gt;.png, none when unemployed) and the level badge
    /// (villager/profession_level/stone.png). Here they are laid on top of each other into one picture (same 64x64 layout).
    /// The type comes from where it is (desert, savanna, snow, taiga, jungle, swamp, else plains), the profession is picked
    /// from the entity id, so every player sees the same villager.
    /// </summary>
    public static class McVillagerLook
    {
        static readonly string[] Professions = { "armorer", "butcher", "cartographer", "cleric", "farmer", "fisherman", "fletcher",
                                                 "leatherworker", "librarian", "mason", "nitwit", "shepherd", "toolsmith", "weaponsmith", null };
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        const string Root = "assets/minecraft/textures/entity/villager/";

        public static string TypeAt(Vector3 pos)
        {
            try
            {
                int x = Mathf.FloorToInt(pos.x), z = Mathf.FloorToInt(pos.z);
                string mc = McBiomeSpawner.BiomeAt(x, z, out _);
                switch (mc)
                {
                    case "savanna": return "savanna";
                    case "jungle": return "jungle";
                    case "swamp": return "swamp";
                    case "snowy_taiga": return "taiga";
                    case "ice_spikes": return "snow";
                }
                var b = GameManager.Instance.World.GetBiomeInWorld(x, z)?.m_sBiomeName ?? "";
                if (b == "desert" || b == "wasteland") return "desert";
                if (b == "snow") return "snow";
                if (b == "burnt_forest") return "savanna";
                return "plains";
            }
            catch { return "plains"; }
        }

        public static Texture2D For(EntityAlive e, System.Func<string, Texture2D> load)
        {
            string type = TypeAt(e.position);
            string prof = Professions[Mathf.Abs(e.entityId * 7919) % Professions.Length];
            string key = type + "|" + prof;
            if (_cache.TryGetValue(key, out var done) && done != null) return done;
            var layers = new List<Texture2D> { load(Root + "villager.png"), load(Root + "type/" + type + ".png") };
            if (prof != null) { layers.Add(load(Root + "profession/" + prof + ".png")); if (prof != "nitwit") layers.Add(load(Root + "profession_level/stone.png")); }
            var tex = Compose(layers);
            _cache[key] = tex;
            return tex;
        }

        static Texture2D Compose(List<Texture2D> layers)
        {
            Texture2D baseT = layers[0];
            if (baseT == null) return null;
            int w = baseT.width, h = baseT.height;
            Color32[] px;
            try { px = baseT.GetPixels32(); } catch { return baseT; }
            for (int li = 1; li < layers.Count; li++)
            {
                var l = layers[li];
                if (l == null || l.width != w || l.height != h) continue;
                Color32[] lp;
                try { lp = l.GetPixels32(); } catch { continue; }
                for (int i = 0; i < px.Length; i++)
                {
                    var c = lp[i];
                    if (c.a == 0) continue;
                    if (c.a == 255) { px[i] = c; continue; }
                    float a = c.a / 255f;
                    var d = px[i];
                    px[i] = new Color32((byte)(c.r * a + d.r * (1 - a)), (byte)(c.g * a + d.g * (1 - a)), (byte)(c.b * a + d.b * (1 - a)), (byte)Mathf.Max(c.a, d.a));
                }
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "SVD_villager" };
            t.SetPixels32(px); t.Apply(false, false);
            return t;
        }
    }
}
