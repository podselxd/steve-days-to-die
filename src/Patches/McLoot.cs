using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Minecraft things in 7 Days' loot (user 2026-11-13: "en el loot de 7days no veo el loot de mc"). The loot.xml appends
    /// add a few Minecraft items to groups that hold dozens of 7 Days items, so they almost never came out. Now, when a
    /// 7 Days container is filled (the first time someone opens it, on the host), 60% of the time 1-3 Minecraft things
    /// are added too, picked by the kind of container: food in kitchens and fridges, arrows near weapons, materials
    /// elsewhere, and now and then something rare.
    /// </summary>
    [HarmonyPatch(typeof(LootManager), nameof(LootManager.LootContainerOpened))]
    static class McLoot
    {
        static readonly (string id, int min, int max, int w)[] Food = { ("bread", 1, 3, 4), ("apple", 1, 3, 4), ("cooked_beef", 1, 2, 2), ("cooked_porkchop", 1, 2, 2), ("carrot", 1, 4, 3), ("baked_potato", 1, 3, 3), ("cookie", 2, 6, 2), ("pumpkin_pie", 1, 1, 1), ("golden_carrot", 1, 2, 1), ("melon_slice", 1, 5, 2), ("cooked_chicken", 1, 2, 2), ("honey_bottle", 1, 1, 1) };
        static readonly (string id, int min, int max, int w)[] Weapons = { ("arrow", 4, 12, 5), ("bow", 1, 1, 1), ("crossbow", 1, 1, 1), ("iron_sword", 1, 1, 1), ("gunpowder", 1, 4, 3), ("string", 1, 4, 2) };
        static readonly (string id, int min, int max, int w)[] Materials = { ("string", 1, 4, 4), ("stick", 2, 8, 4), ("coal", 1, 6, 4), ("iron_nugget", 2, 9, 3), ("leather", 1, 3, 3), ("paper", 1, 4, 3), ("iron_ingot", 1, 3, 3), ("gold_ingot", 1, 2, 2), ("redstone", 1, 6, 2), ("gunpowder", 1, 3, 2), ("book", 1, 1, 2), ("bone", 1, 4, 3), ("feather", 1, 4, 2), ("flint", 1, 3, 2), ("torch", 2, 8, 3), ("bucket", 1, 1, 1), ("saddle", 1, 1, 1), ("name_tag", 1, 1, 1), ("lapis_lazuli", 1, 6, 1), ("copper_ingot", 1, 4, 2) };
        static readonly (string id, int min, int max, int w)[] Rare = { ("diamond", 1, 2, 3), ("emerald", 1, 3, 3), ("golden_apple", 1, 1, 2), ("ender_pearl", 1, 1, 2), ("enchanted_golden_apple", 1, 1, 1), ("diamond_horse_armor", 1, 1, 1), ("music_disc_cat", 1, 1, 1), ("enchanted_book", 1, 1, 4) };

        static void Prefix(TEFeatureStorage _tileEntity, out bool __state)
        {
            __state = false;
            try { __state = _tileEntity != null && !_tileEntity.ItemGrid.Touched && _tileEntity.IsEmpty(); } catch { }
        }

        static void Postfix(TEFeatureStorage _tileEntity, bool __state)
        {
            if (!__state || _tileEntity == null) return;
            try
            {
                string name = (_tileEntity.lootListName ?? "").ToLowerInvariant();
                if (name.StartsWith("mc")) return;                 // Minecraft's own chests and barrels: not 7 Days loot
                if (_tileEntity.IsEmpty()) return;                  // nothing was spawned (no loot list)
                if (Random.value > 0.6f) return;
                var list = name.Contains("fridge") || name.Contains("cupboard") || name.Contains("food") || name.Contains("cooler") || name.Contains("vending") || name.Contains("kitchen") ? Food
                         : name.Contains("gun") || name.Contains("ammo") || name.Contains("weapon") || name.Contains("military") ? Weapons : Materials;
                int n = Random.Range(1, 4);
                for (int i = 0; i < n; i++)
                {
                    var pick = Pick(Random.value < 0.05f ? Rare : list);
                    var st = Crafting.McItems.Stack(pick.id, Random.Range(pick.min, pick.max + 1));
                    if (st != null && pick.id == "enchanted_book")
                    {
                        // Minecraft's loot books: any enchantment, treasure ones (Mending) included, at a random level
                        var all = Enchanting.McEnchant.All;
                        var e = all[Random.Range(0, all.Length)];
                        Enchanting.McEnchant.Write(st.itemValue, new System.Collections.Generic.Dictionary<string, int> { { e.id, Random.Range(1, e.max + 1) } });
                    }
                    if (st != null) _tileEntity.AddItem(st);
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] Minecraft loot: " + ex.Message); }
        }

        static (string id, int min, int max, int w) Pick((string id, int min, int max, int w)[] l)
        {
            int total = 0; foreach (var x in l) total += x.w;
            int r = Random.Range(0, total);
            foreach (var x in l) { if (r < x.w) return x; r -= x.w; }
            return l[0];
        }
    }
}
