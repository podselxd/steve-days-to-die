using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Enchanting
{
    /// <summary>
    /// Minecraft's enchanting (user 2026-11-13: "Enchantments"). The enchantments with their weights, level ranges
    /// ("modified level" windows: min = a + b x (level-1), max = min + c), what they go on and which exclude each other,
    /// as in Minecraft's Enchantments class; the enchanting table's costs from the bookshelves around it; the item's
    /// enchantability by material; and the pick (EnchantmentHelper.selectEnchantment). Enchantments live on the item as
    /// metadata "mcEnch" = "sharpness=3,unbreaking=2", so they are saved, dropped and traded with the item.
    /// </summary>
    public static class McEnchant
    {
        public const string Key = "mcEnch";

        public class Ench
        {
            public string id, name; public int max, weight, a, b, c; public string on, group;
        }

        // on: sword, axe, tool (pickaxe/shovel/hoe/axe), armor, head, feet, bow, crossbow, trident, mace, any (unbreaking)
        public static readonly Ench[] All =
        {
            E("protection", "Protección", 4, 10, 1, 11, 11, "armor", "prot"),
            E("fire_protection", "Protección contra el fuego", 4, 5, 10, 8, 8, "armor", "prot"),
            E("blast_protection", "Protección contra explosiones", 4, 2, 5, 8, 8, "armor", "prot"),
            E("projectile_protection", "Protección contra proyectiles", 4, 5, 3, 6, 6, "armor", "prot"),
            E("feather_falling", "Caída de pluma", 4, 5, 5, 6, 6, "feet", null),
            E("respiration", "Respiración", 3, 2, 10, 10, 30, "head", null),
            E("aqua_affinity", "Afinidad acuática", 1, 2, 1, 0, 40, "head", null),
            E("thorns", "Espinas", 3, 1, 10, 20, 50, "chest", null),
            E("sharpness", "Filo", 5, 10, 1, 11, 20, "sword", "dmg"),
            E("smite", "Castigo", 5, 5, 5, 8, 20, "sword", "dmg"),
            E("bane_of_arthropods", "Perdición de los artrópodos", 5, 5, 5, 8, 20, "sword", "dmg"),
            E("knockback", "Empuje", 2, 5, 5, 20, 50, "sword", null),
            E("fire_aspect", "Aspecto ígneo", 2, 2, 10, 20, 50, "sword", null),
            E("looting", "Botín", 3, 2, 15, 9, 50, "sword", null),
            E("sweeping_edge", "Filo arrasador", 3, 2, 5, 9, 15, "sword", null),
            E("efficiency", "Eficiencia", 5, 10, 1, 10, 50, "tool", null),
            E("silk_touch", "Toque de seda", 1, 1, 15, 0, 50, "tool", "drop"),
            E("fortune", "Fortuna", 3, 2, 15, 9, 50, "tool", "drop"),
            E("power", "Poder", 5, 10, 1, 10, 15, "bow", null),
            E("punch", "Retroceso", 2, 2, 12, 20, 25, "bow", null),
            E("flame", "Fuego", 1, 2, 20, 0, 30, "bow", null),
            E("infinity", "Infinidad", 1, 1, 20, 0, 30, "bow", null),
            E("quick_charge", "Carga rápida", 3, 5, 12, 20, 50, "crossbow", null),
            E("multishot", "Multidisparo", 1, 2, 20, 0, 30, "crossbow", "xbow"),
            E("piercing", "Perforación", 4, 10, 1, 10, 50, "crossbow", "xbow"),
            E("loyalty", "Lealtad", 3, 5, 12, 7, 50, "trident", null),
            E("impaling", "Empalamiento", 5, 2, 1, 8, 20, "trident", null),
            E("density", "Densidad", 5, 5, 5, 8, 20, "mace", "macedmg"),
            E("breach", "Brecha", 4, 2, 15, 9, 50, "mace", "macedmg"),
            E("unbreaking", "Irrompibilidad", 3, 5, 5, 8, 50, "any", null),
            E("mending", "Reparación", 1, 2, 0, 0, -1, "any", null),            // treasure: never from the table (window max < min)
        };

        static Ench E(string id, string name, int max, int weight, int a, int b, int c, string on, string group)
            => new Ench { id = id, name = name, max = max, weight = weight, a = a, b = b, c = c, on = on, group = group };

        public static Ench Get(string id) { foreach (var e in All) if (e.id == id) return e; return null; }

        // ---- the item ----
        public static string McId(ItemValue iv)
        {
            string n = iv?.ItemClass?.Name;
            return n != null && n.StartsWith("mc_") ? n.Substring(3) : null;
        }

        /// <summary>What kind of thing it is for enchanting, or null when it cannot be enchanted.</summary>
        public static string Kind(string id)
        {
            if (id == null) return null;
            if (id == "book" || id == "enchanted_book") return "book";
            if (id.EndsWith("_sword")) return "sword";
            if (id.EndsWith("_axe")) return "axe";
            if (id.EndsWith("_pickaxe") || id.EndsWith("_shovel") || id.EndsWith("_hoe")) return "tool";
            if (id.EndsWith("_helmet")) return "head";
            if (id.EndsWith("_chestplate")) return "chest";
            if (id.EndsWith("_leggings")) return "legs";
            if (id.EndsWith("_boots")) return "feet";
            if (id == "bow" || id == "crossbow" || id == "trident" || id == "mace") return id;
            if (id == "fishing_rod" || id == "shears" || id == "flint_and_steel" || id == "elytra" || id == "shield") return "other";
            return null;
        }

        static bool Fits(Ench e, string kind)
        {
            if (kind == "book") return true;
            switch (e.on)
            {
                case "any": return true;
                case "armor": return kind == "head" || kind == "chest" || kind == "legs" || kind == "feet";
                case "sword": return kind == "sword";
                case "tool": return kind == "tool" || kind == "axe";
                default: return e.on == kind;
            }
        }

        /// <summary>Minecraft's enchantability by material.</summary>
        public static int Enchantability(string id)
        {
            string k = Kind(id);
            if (k == "book" || k == "bow" || k == "crossbow" || k == "trident" || k == "other") return 1;
            if (k == "mace") return 15;
            bool armor = k == "head" || k == "chest" || k == "legs" || k == "feet";
            if (id.StartsWith("wooden_")) return 15;
            if (id.StartsWith("stone_")) return 5;
            if (id.StartsWith("iron_")) return armor ? 9 : 14;
            if (id.StartsWith("golden_")) return armor ? 25 : 22;
            if (id.StartsWith("diamond_")) return 10;
            if (id.StartsWith("netherite_")) return 15;
            if (id.StartsWith("leather_")) return 15;
            if (id.StartsWith("chainmail_")) return 12;
            if (id.StartsWith("copper_")) return armor ? 8 : 13;
            if (id.StartsWith("turtle_")) return 9;
            return 1;
        }

        public static Dictionary<string, int> Read(ItemValue iv)
        {
            var d = new Dictionary<string, int>();
            try
            {
                if (iv == null || !iv.HasMetadata(Key)) return d;
                string s = iv.GetMetadata(Key) as string;
                if (string.IsNullOrEmpty(s)) return d;
                foreach (var part in s.Split(','))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && int.TryParse(kv[1], out int l)) d[kv[0]] = l;
                }
            }
            catch { }
            return d;
        }

        public static int Level(ItemValue iv, string id) => Read(iv).TryGetValue(id, out int l) ? l : 0;

        public static void Write(ItemValue iv, Dictionary<string, int> d)
        {
            var parts = new List<string>();
            foreach (var kv in d) parts.Add(kv.Key + "=" + kv.Value);
            iv.SetMetadata(Key, string.Join(",", parts));
        }

        public static bool IsEnchanted(ItemValue iv) => Read(iv).Count > 0;

        static readonly string[] Roman = { "", "I", "II", "III", "IV", "V" };
        public static string Line(string id, int level)
        {
            var e = Get(id);
            string n = e != null ? e.name : id;
            return e != null && e.max == 1 ? n : n + " " + (level < Roman.Length ? Roman[level] : level.ToString());
        }

        // ---- the enchanting table (Minecraft's EnchantmentMenu) ----
        /// <summary>The 3 level costs for this many bookshelves (0..15), with the player's enchanting seed.</summary>
        public static int[] Costs(int shelves, string id, int seed)
        {
            var r = new System.Random(seed);
            var costs = new int[3];
            if (Enchantability(id) <= 0) return costs;
            shelves = Mathf.Clamp(shelves, 0, 15);
            for (int i = 0; i < 3; i++)
            {
                int bse = r.Next(8) + 1 + (shelves >> 1) + r.Next(shelves + 1);
                costs[i] = i == 0 ? Mathf.Max(bse / 3, 1) : i == 1 ? bse * 2 / 3 + 1 : Mathf.Max(bse, shelves * 2);
                if (costs[i] < i + 1) costs[i] = 0;
            }
            return costs;
        }

        /// <summary>Minecraft's selectEnchantment: the enchantments a cost gives this item (same seed = same result).</summary>
        public static List<(Ench e, int level)> Pick(string id, int cost, int seed)
        {
            var r = new System.Random(seed + cost * 31);
            var res = new List<(Ench, int)>();
            int ench = Enchantability(id);
            string kind = Kind(id);
            if (ench <= 0 || kind == null) return res;
            int lvl = cost + 1 + r.Next(ench / 4 + 1) + r.Next(ench / 4 + 1);
            float f = ((float)r.NextDouble() + (float)r.NextDouble() - 1f) * 0.15f;
            lvl = Mathf.Clamp(Mathf.RoundToInt(lvl + lvl * f), 1, int.MaxValue);
            var pool = Available(kind, lvl);
            if (pool.Count == 0) return res;
            res.Add(Weighted(pool, r));
            while (r.Next(50) <= lvl)
            {
                pool.RemoveAll(x => x.e == res[res.Count - 1].Item1 || Clash(x.e, res));
                if (pool.Count == 0) break;
                res.Add(Weighted(pool, r));
                lvl /= 2;
            }
            return res;
        }

        static bool Clash(Ench e, List<(Ench e, int level)> have)
        {
            foreach (var h in have)
            {
                if (h.e == e) return true;
                if (e.group != null && e.group == h.e.group) return true;
                if ((e.id == "infinity" && h.e.id == "mending") || (e.id == "mending" && h.e.id == "infinity")) return true;
            }
            return false;
        }

        static List<(Ench e, int level)> Available(string kind, int lvl)
        {
            var l = new List<(Ench, int)>();
            foreach (var e in All)
            {
                if (!Fits(e, kind)) continue;
                for (int k = e.max; k >= 1; k--)
                {
                    int min = e.a + e.b * (k - 1), max = min + e.c;
                    if (lvl >= min && lvl <= max) { l.Add((e, k)); break; }
                }
            }
            return l;
        }

        static (Ench, int) Weighted(List<(Ench e, int level)> pool, System.Random r)
        {
            int total = 0; foreach (var p in pool) total += p.e.weight;
            int x = r.Next(total);
            foreach (var p in pool) { if (x < p.e.weight) return p; x -= p.e.weight; }
            return pool[0];
        }

        /// <summary>Bookshelves that count for the table at pos: Minecraft's ring 2 blocks away, 1-2 high, with air between.</summary>
        public static int Shelves(Vector3i pos)
        {
            var world = GameManager.Instance.World;
            int n = 0;
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    if (Mathf.Abs(dx) < 2 && Mathf.Abs(dz) < 2) continue;
                    for (int dy = 0; dy <= 1; dy++)
                    {
                        var at = pos + new Vector3i(dx, dy, dz);
                        if (world.GetBlock(at).Block?.GetBlockName() != "mc_bookshelf") continue;
                        var mid = pos + new Vector3i(dx / 2, dy, dz / 2);
                        if (world.GetBlock(mid).isair) n++;
                    }
                }
            return Mathf.Min(n, 15);
        }
    }

    /// <summary>
    /// Minecraft's experience, separate from 7 Days' own levels (those are the skill points). Points come from killing
    /// mobs and mining ores; levels follow Minecraft's curve. Kept on the player as the cvar mcXpPoints (saved).
    /// </summary>
    public static class McXp
    {
        public const string Cvar = "mcXpPoints";

        public static int PointsForNext(int level) => level >= 30 ? 9 * level - 158 : level >= 15 ? 5 * level - 38 : 2 * level + 7;

        public static int Total(EntityPlayer p) { try { return Mathf.Max(0, Mathf.RoundToInt(p.Buffs.GetCustomVar(Cvar))); } catch { return 0; } }

        public static void Level(EntityPlayer p, out int level, out float progress)
        {
            int pts = Total(p); level = 0;
            while (pts >= PointsForNext(level)) { pts -= PointsForNext(level); level++; }
            progress = pts / (float)PointsForNext(level);
        }

        public static int LevelOf(EntityPlayer p) { Level(p, out int l, out _); return l; }

        public static void Add(EntityPlayer p, int points)
        {
            if (p == null || points <= 0) return;
            points = Mend(p, points);
            if (points <= 0) return;
            int before = LevelOf(p);
            p.Buffs.SetCustomVar(Cvar, Total(p) + points);
            int after = LevelOf(p);
            Sound.MinecraftSounds.PlayGroup(after > before && after % 5 == 0 ? "random/levelup" : "random/orb", p.position + Vector3.up, 8f, 0.3f, 0.8f + Random.value * 0.5f);
        }

        /// <summary>Minecraft's Mending: experience goes first into repairing a damaged Mending item held or worn (2 a point).</summary>
        static int Mend(EntityPlayer p, int points)
        {
            try
            {
                if (p is EntityPlayerLocal lp)
                {
                    var iv = lp.inventory.holdingItemItemValue;
                    if (McEnchant.Level(iv, "mending") > 0 && iv.UseTimes > 0f)
                    {
                        float fix = Mathf.Min(iv.UseTimes, points * 2f);
                        var nv = iv.Clone(); nv.UseTimes -= fix;
                        int slot = lp.inventory.holdingItemIdx; var st = lp.inventory.GetItem(slot).Clone(); st.itemValue = nv; lp.inventory.SetItem(slot, st);
                        points -= Mathf.CeilToInt(fix / 2f);
                    }
                }
                for (int i = 0; i < 4 && points > 0; i++)
                {
                    var a = p.equipment.GetSlotItem(i);
                    if (a == null || a.IsEmpty() || McEnchant.Level(a, "mending") <= 0 || a.UseTimes <= 0f) continue;
                    float fix = Mathf.Min(a.UseTimes, points * 2f);
                    var nv = a.Clone(); nv.UseTimes -= fix; p.equipment.SetSlotItem(i, nv);
                    points -= Mathf.CeilToInt(fix / 2f);
                }
            }
            catch { }
            return points;
        }

        /// <summary>Takes whole levels away (enchanting), keeping the progress fraction like Minecraft.</summary>
        public static void SpendLevels(EntityPlayer p, int levels)
        {
            Level(p, out int level, out float prog);
            int target = Mathf.Max(0, level - levels);
            int pts = 0;
            for (int i = 0; i < target; i++) pts += PointsForNext(i);
            pts += Mathf.RoundToInt(prog * PointsForNext(target));
            p.Buffs.SetCustomVar(Cvar, pts);
        }

        /// <summary>Minecraft's experience for a kill (orbs): hostiles 5, animals 1-3, bosses more.</summary>
        public static int ForKill(EntityAlive e)
        {
            string c = e?.EntityClass?.entityClassName ?? "";
            if (e is EntityZombie || e is EntityEnemy) return c.Contains("Blaze") || c.Contains("Guardian") ? 10 : 5;
            if (c.StartsWith("mc")) return Random.Range(1, 4);
            if (e is EntityAnimal) return Random.Range(1, 4);
            return 0;
        }

        /// <summary>Minecraft's experience for mining an ore (by what it drops).</summary>
        public static int ForBlock(string blockName)
        {
            if (blockName == null) return 0;
            string n = blockName.ToLowerInvariant();
            if (n.Contains("diamond") || n.Contains("emerald")) return Random.Range(3, 8);
            if (n.Contains("lapis") || n.Contains("quartz")) return Random.Range(2, 6);
            if (n.Contains("redstone") || n.Contains("nitrate") || n.Contains("potassium")) return Random.Range(1, 6);
            if (n.Contains("coal")) return Random.Range(0, 3);
            if (n.Contains("ore") && (n.Contains("iron") || n.Contains("lead") || n.Contains("gold"))) return Random.Range(0, 2);
            return 0;
        }
    }
}
