// Minecraft's recipes, read from the player's own Minecraft. The reader and matcher come from Ryan Ussery's
// "Steve Days to Die" (MIT, see src/Ryan/LICENSE-SteveDaysToDie-RyanUssery.txt), adapted to 7 Days items:
// a 7 Days item takes part as the Minecraft item it pairs with (sheets/pairs.json), a mc_<id> item as itself.
// What comes out is always a Minecraft item (mc_<id>), which never turns back into 7 Days things, so
// crafting can't be used to multiply anything between the two games.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Crafting
{
    public static class McRecipes
    {
        public class Recipe
        {
            public string name;
            public bool shaped;
            public int w, h;
            public HashSet<string>[] grid;
            public List<HashSet<string>> ingredients;
            public string result;
            public int count;
        }

        public class Smelt { public HashSet<string> input; public string result; public int count; public float xp; public string kind; }

        static List<Recipe> _recipes;
        static List<Smelt> _smelts;
        static readonly Dictionary<string, HashSet<string>> _tags = new Dictionary<string, HashSet<string>>();

        public static int Count => _recipes?.Count ?? 0;
        public static IReadOnlyList<Recipe> All { get { Load(); return _recipes; } }

        /// <summary>The Minecraft id a 7 Days stack counts as in Minecraft recipes, or null.</summary>
        public static string McId(ItemStack st)
        {
            if (st == null || st.IsEmpty()) return null;
            string name = st.itemValue.ItemClass?.Name;
            if (name == null) return null;
            if (McItems.IsMc(name)) return McItems.IdOf(name);
            return PairsTable.ToMinecraft.TryGetValue(name, out var mc) ? mc : null;
        }

        static string Strip(string s) => s != null && s.StartsWith("minecraft:") ? s.Substring(10) : s;

        public static HashSet<string> Tag(string name)
        {
            name = Strip(name);
            if (_tags.TryGetValue(name, out var set)) return set;
            set = new HashSet<string>();
            _tags[name] = set;
            if ((Ryan.Mc.Json("data/minecraft/tags/item/" + name + ".json") ?? Ryan.Mc.Json("data/minecraft/tags/items/" + name + ".json"))?["values"] is JArray arr)
                foreach (var v in arr)
                {
                    string id = v is JObject o ? (string)o["id"] : (string)v;
                    if (id == null) continue;
                    if (id.StartsWith("#")) set.UnionWith(Tag(id.Substring(1))); else set.Add(Strip(id));
                }
            return set;
        }

        static HashSet<string> Ingredient(JToken t)
        {
            var s = new HashSet<string>();
            if (t == null) return s;
            if (t is JArray a) { foreach (var x in a) s.UnionWith(Ingredient(x)); return s; }
            if (t is JObject o)
            {
                if (o["item"] != null) s.Add(Strip((string)o["item"]));
                if (o["tag"] != null) s.UnionWith(Tag((string)o["tag"]));
                return s;
            }
            string str = (string)t;
            if (str == null) return s;
            if (str.StartsWith("#")) s.UnionWith(Tag(str.Substring(1))); else s.Add(Strip(str));
            return s;
        }

        static void Result(JToken r, out string id, out int count)
        {
            id = null; count = 1;
            if (r is JObject o) { id = Strip((string)(o["id"] ?? o["item"])); if (o["count"] != null) count = (int)o["count"]; }
            else if (r != null) id = Strip((string)r);
        }

        public static void Load()
        {
            if (_recipes != null) return;
            _recipes = new List<Recipe>();
            _smelts = new List<Smelt>();
            if (!Ryan.Mc.Ready) return;
            var files = Ryan.Mc.List("data/minecraft/recipe/").ToList();
            if (files.Count == 0) files = Ryan.Mc.List("data/minecraft/recipes/").ToList();
            foreach (var f in files)
            {
                if (!f.EndsWith(".json")) continue;
                var j = Ryan.Mc.Json(f);
                if (j == null) continue;
                string type = Strip((string)j["type"]);
                try
                {
                    switch (type)
                    {
                        case "crafting_shaped":
                        {
                            var pattern = ((JArray)j["pattern"]).Select(x => (string)x).ToArray();
                            var key = (JObject)j["key"];
                            int h = pattern.Length, w = pattern.Max(p => p.Length);
                            var r = new Recipe { name = f, shaped = true, w = w, h = h, grid = new HashSet<string>[w * h] };
                            for (int y = 0; y < h; y++)
                                for (int x = 0; x < w; x++)
                                {
                                    char c = x < pattern[y].Length ? pattern[y][x] : ' ';
                                    if (c != ' ') r.grid[y * w + x] = Ingredient(key[c.ToString()]);
                                }
                            Result(j["result"], out r.result, out r.count);
                            if (r.result != null && McItems.Kind(r.result) != null) _recipes.Add(r);
                            break;
                        }
                        case "crafting_shapeless":
                        {
                            var r = new Recipe { name = f, shaped = false, ingredients = ((JArray)j["ingredients"]).Select(Ingredient).ToList() };
                            Result(j["result"], out r.result, out r.count);
                            if (r.result != null && McItems.Kind(r.result) != null) _recipes.Add(r);
                            break;
                        }
                        case "smelting":
                        case "blasting":
                        case "smoking":
                        {
                            var s = new Smelt { input = Ingredient(j["ingredient"]), kind = type, xp = j["experience"] != null ? (float)j["experience"] : 0f };
                            Result(j["result"], out s.result, out s.count);
                            if (s.result != null && McItems.Kind(s.result) != null) _smelts.Add(s);
                            break;
                        }
                    }
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] recipe " + f + ": " + ex.Message); }
            }
            // Sophisticated Backpacks' recipes (its jar's data/sophisticatedbackpacks/recipe), written here
            HashSet<string> S(params string[] ids) => new HashSet<string>(ids);
            void Shaped(string result, string[] pattern, Dictionary<char, HashSet<string>> key)
            {
                int h = pattern.Length, w = pattern[0].Length;
                var r = new Recipe { name = "sophisticatedbackpacks:" + result, shaped = true, w = w, h = h, grid = new HashSet<string>[w * h], result = result, count = 1 };
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { char c = pattern[y][x]; if (c != ' ') r.grid[y * w + x] = key[c]; }
                _recipes.Add(r);
            }
            Shaped("sb_backpack", new[] { "SLS", "SCS", "LLL" }, new Dictionary<char, HashSet<string>> { { 'S', S("string") }, { 'L', S("leather") }, { 'C', S("chest", "trapped_chest", "barrel") } });
            foreach (var (res, from, ingot) in new[] { ("sb_copper_backpack", "sb_backpack", "copper_ingot"), ("sb_iron_backpack", "sb_copper_backpack", "iron_ingot"),
                                                       ("sb_gold_backpack", "sb_iron_backpack", "gold_ingot"), ("sb_diamond_backpack", "sb_gold_backpack", "diamond") })
                Shaped(res, new[] { "III", "IBI", "III" }, new Dictionary<char, HashSet<string>> { { 'I', S(ingot) }, { 'B', S(from) } });
            // the guide book (src/HUD/McGuideBook.cs): a book and a feather
            _recipes.Add(new Recipe { name = "steve:guide_book", shaped = false, ingredients = new List<HashSet<string>> { S("book"), S("feather") }, result = "written_book", count = 1 });
            _recipes.Add(new Recipe { name = "sophisticatedbackpacks:netherite_backpack", shaped = false, ingredients = new List<HashSet<string>> { S("sb_diamond_backpack"), S("netherite_ingot") }, result = "sb_netherite_backpack", count = 1 });
            Debug.Log($"[SVD] Minecraft recipes: {_recipes.Count} crafting, {_smelts.Count} furnace");
        }

        /// <summary>The recipe the grid (size x size, Minecraft ids, null = empty) makes, or null.</summary>
        public static Recipe Match(string[] grid, int size)
        {
            Load();
            int minX = size, minY = size, maxX = -1, maxY = -1;
            var have = new List<string>();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    string id = grid[y * size + x];
                    if (id == null) continue;
                    have.Add(id);
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
            if (have.Count == 0) return null;
            int w = maxX - minX + 1, h = maxY - minY + 1;
            foreach (var r in _recipes)
            {
                if (r.shaped)
                {
                    if (r.w == w && r.h == h && (Fits(r, grid, size, minX, minY, false) || Fits(r, grid, size, minX, minY, true))) return r;
                }
                else if (r.ingredients.Count == have.Count && Shapeless(r.ingredients, have)) return r;
            }
            return null;
        }

        static bool Fits(Recipe r, string[] grid, int size, int ox, int oy, bool mirror)
        {
            for (int y = 0; y < r.h; y++)
                for (int x = 0; x < r.w; x++)
                {
                    var need = r.grid[y * r.w + (mirror ? r.w - 1 - x : x)];
                    string id = grid[(oy + y) * size + ox + x];
                    if (need == null || need.Count == 0) { if (id != null) return false; }
                    else if (id == null || !need.Contains(id)) return false;
                }
            return true;
        }

        static bool Shapeless(List<HashSet<string>> need, List<string> have)
        {
            var used = new bool[have.Count];
            bool Try(int i)
            {
                if (i == need.Count) return true;
                for (int j = 0; j < have.Count; j++)
                {
                    if (used[j] || !need[i].Contains(have[j])) continue;
                    used[j] = true;
                    if (Try(i + 1)) return true;
                    used[j] = false;
                }
                return false;
            }
            return Try(0);
        }

        /// <summary>What a furnace makes from this Minecraft id (normal furnace = "smelting"), or null.</summary>
        public static Smelt SmeltFor(string id, string kind = "smelting")
        {
            Load();
            return id == null ? null : _smelts.FirstOrDefault(s => s.kind == kind && s.input.Contains(id));
        }
    }
}
