using System.Collections.Generic;
using UnityEngine;
using SteveDaysToDie.Enchanting;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Minecraft's anvil (AnvilMenu.createResult), gui/container/anvil.png: left (27,47), right (76,47), result (134,47),
    /// name field (62,24). Right side = an enchanted book or the same item (enchantments merged: equal levels go one up,
    /// different ones keep the higher, capped at the max; incompatible ones cost 1 and are left out; cost = level x
    /// rarity multiplier, halved for books), or the repair material (each one fixes 25%, costs 1). Two of the same item
    /// also add their durability (+12%). Renaming costs 1. The item's prior work penalty (2^n - 1, metadata mcWork) is
    /// added and doubles each use. 40 levels or more: "Too expensive!".
    /// </summary>
    public partial class McInventory
    {
        static Vector3i? _anvilPos;
        bool InAnvil => _table && _anvilPos != null;
        string _rename = "";
        string _renameFor;
        int _anvilCost, _anvilUnits;
        string _anvilError;
        ItemStack _anvilResult;

        public static void OpenAnvil(EntityPlayerLocal p, Vector3i pos)
        {
            _furnacePos = null; _enchPos = null; _anvilPos = pos;
            try { LocalPlayerUI.GetUIForPlayer(p).windowManager.Open("mcScreen", true); }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] anvil: " + ex.Message); }
        }

        /// <summary>Test hook (MeltyDemo "yunque"): sword with Sharpness III + book with Sharpness III, take the result.</summary>
        public string TestAnvil(bool put)
        {
            if (put)
            {
                var sw = Crafting.McItems.Stack("iron_sword", 1); var bk = Crafting.McItems.Stack("enchanted_book", 1);
                if (sw == null || bk == null) return "no items";
                McEnchant.Write(sw.itemValue, new Dictionary<string, int> { { "sharpness", 3 } });
                McEnchant.Write(bk.itemValue, new Dictionary<string, int> { { "sharpness", 3 } });
                _craft[0] = sw; _craft[1] = bk;
                return "";
            }
            AnvilCompute();
            string r = $"cost {_anvilCost} error {_anvilError} result {(_anvilResult?.itemValue?.GetMetadata(McEnchant.Key) as string)} level before {McXp.LevelOf(_p)}";
            AnvilTake(true);
            return r + " level after " + McXp.LevelOf(_p) + " left slot empty " + _craft[0].IsEmpty();
        }

        static int Work(ItemValue iv) { try { return iv != null && iv.TryGetMetadata("mcWork", out int w) ? w : 0; } catch { return 0; } }

        public static string CustomName(ItemValue iv) { try { return iv?.GetMetadata("mcName") as string; } catch { return null; } }

        static string RepairMaterial(string id)
        {
            if (id == null) return null;
            if (id.StartsWith("wooden_")) return "planks";
            if (id.StartsWith("stone_")) return "cobblestone";
            if (id.StartsWith("iron_") || id.StartsWith("chainmail_")) return "iron_ingot";
            if (id.StartsWith("golden_")) return "gold_ingot";
            if (id.StartsWith("diamond_")) return "diamond";
            if (id.StartsWith("netherite_")) return "netherite_ingot";
            if (id.StartsWith("copper_")) return "copper_ingot";
            if (id.StartsWith("leather_")) return "leather";
            if (id.StartsWith("turtle_")) return "turtle_scute";
            if (id == "mace") return "breeze_rod";
            if (id == "elytra") return "phantom_membrane";
            return null;
        }

        static int Rarity(McEnchant.Ench e, bool book)
        {
            int m = e.weight >= 10 ? 1 : e.weight >= 5 ? 2 : e.weight >= 2 ? 4 : 8;
            return book ? Mathf.Max(1, m / 2) : m;
        }

        void AnvilCompute()
        {
            _anvilResult = null; _anvilCost = 0; _anvilUnits = 0; _anvilError = null;
            var left = _craft[0]; var right = _craft[1];
            if (left == null || left.IsEmpty()) return;
            string lid = McEnchant.McId(left.itemValue);
            var res = left.Clone();
            res.itemValue = left.itemValue.Clone();
            int cost = 0;
            bool did = false;
            if (right != null && !right.IsEmpty())
            {
                string rid = McEnchant.McId(right.itemValue);
                string mat = RepairMaterial(lid);
                var riv = right.itemValue;
                if (mat != null && (rid == mat || (mat == "planks" && rid != null && rid.EndsWith("_planks"))) && res.itemValue.MaxUseTimes > 0 && res.itemValue.UseTimes > 0)
                {
                    // the material: 25% a piece
                    float quarter = res.itemValue.MaxUseTimes / 4f;
                    int units = 0;
                    while (units < right.count && res.itemValue.UseTimes > 0) { res.itemValue.UseTimes = Mathf.Max(0f, res.itemValue.UseTimes - quarter); units++; }
                    cost += units; _anvilUnits = units; did = true;
                }
                else if (rid == lid || rid == "enchanted_book")
                {
                    bool book = rid == "enchanted_book";
                    if (!book && res.itemValue.MaxUseTimes > 0 && res.itemValue.UseTimes > 0)
                    {
                        float max = res.itemValue.MaxUseTimes;
                        float left1 = max - res.itemValue.UseTimes, left2 = max - riv.UseTimes;
                        res.itemValue.UseTimes = Mathf.Max(0f, max - (left1 + left2 + max * 0.12f));
                        cost += 2; did = true;
                    }
                    var have = McEnchant.Read(res.itemValue);
                    string kind = McEnchant.Kind(lid);
                    foreach (var kv in McEnchant.Read(riv))
                    {
                        var e = McEnchant.Get(kv.Key);
                        if (e == null) continue;
                        bool fits = kind == "book" || lid == "enchanted_book" || FitsKind(e, kind);
                        bool clash = false;
                        foreach (var h in have.Keys) { var he = McEnchant.Get(h); if (he != null && he != e && e.group != null && e.group == he.group) clash = true; }
                        if (!fits || clash) { cost += 1; continue; }
                        have.TryGetValue(kv.Key, out int cur);
                        int nl = cur == kv.Value ? kv.Value + 1 : Mathf.Max(cur, kv.Value);
                        nl = Mathf.Min(nl, e.max);
                        have[kv.Key] = nl;
                        cost += nl * Rarity(e, book);
                        did = true;
                    }
                    McEnchant.Write(res.itemValue, have);
                    _anvilUnits = 1;
                }
                else { _anvilError = null; return; }
            }
            // renaming
            if (_renameFor != lid + "|" + (CustomName(left.itemValue) ?? "")) { _rename = CustomName(left.itemValue) ?? ""; _renameFor = lid + "|" + (CustomName(left.itemValue) ?? ""); }
            string current = CustomName(left.itemValue) ?? "";
            if (_rename != current && _rename.Trim().Length > 0) { res.itemValue.SetMetadata("mcName", _rename.Trim()); cost += 1; did = true; }
            if (!did) return;
            int prior = Work(left.itemValue) + (right != null && !right.IsEmpty() ? Work(right.itemValue) : 0);
            int total = cost + prior;
            res.itemValue.SetMetadata("mcWork", Mathf.Max(Work(left.itemValue), right != null && !right.IsEmpty() ? Work(right.itemValue) : 0) * 2 + 1);
            _anvilCost = total;
            if (total >= 40) { _anvilError = "¡Demasiado caro!"; return; }
            _anvilResult = res;
        }

        static bool FitsKind(McEnchant.Ench e, string kind)
        {
            switch (e.on)
            {
                case "any": return true;
                case "armor": return kind == "head" || kind == "chest" || kind == "legs" || kind == "feet";
                case "sword": return kind == "sword" || kind == "axe";       // the anvil lets swords' damage go on axes
                case "tool": return kind == "tool" || kind == "axe";
                default: return e.on == kind;
            }
        }

        bool AnvilTake(bool shift)
        {
            AnvilCompute();
            if (_anvilResult == null) return true;
            int level = McXp.LevelOf(_p);
            if (level < _anvilCost) return true;
            if (shift) { if (!_p.bag.AddItem(_anvilResult.Clone())) return true; }
            else { if (!_cursor.IsEmpty()) return true; _cursor = _anvilResult.Clone(); }
            _craft[0] = ItemStack.Empty.Clone();
            if (_craft[1] != null && !_craft[1].IsEmpty())
            {
                var r = _craft[1].Clone(); r.count -= Mathf.Max(1, _anvilUnits);
                _craft[1] = r.count > 0 ? r : ItemStack.Empty.Clone();
            }
            McXp.SpendLevels(_p, _anvilCost);
            _rename = ""; _renameFor = null;
            Sound.MinecraftSounds.PlayGroup("random/anvil_use", _p.position + Vector3.up, 8f, 1f, 0.9f + Random.value * 0.2f);
            return true;
        }

        void AnvilGUI(float px, float py, int s, Event e)
        {
            AnvilCompute();
            // the name field
            var field = new Rect(px + 59 * s, py + 20 * s, 110 * s, 16 * s);
            var ft = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/container/anvil/" + (_craft[0] != null && !_craft[0].IsEmpty() ? "text_field" : "text_field_disabled") + ".png");
            if (e.type == EventType.Repaint && ft != null) GUI.DrawTexture(field, ft);
            if (_craft[0] != null && !_craft[0].IsEmpty())
            {
                var st = new GUIStyle(GUI.skin.textField) { fontSize = 8 * s, padding = new RectOffset(4 * s, 2 * s, 3 * s, 2 * s) };
                st.normal.background = null; st.focused.background = null; st.hover.background = null;
                st.normal.textColor = st.focused.textColor = st.hover.textColor = Color.white;
                GUI.SetNextControlName("mcAnvilName");
                _rename = GUI.TextField(field, _rename ?? "", 50, st);
            }
            // the cost, Minecraft's green (red when you cannot pay), "Too expensive!" in red
            if (e.type == EventType.Repaint && (_anvilCost > 0 || _anvilError != null))
            {
                bool can = _anvilError == null && McXp.LevelOf(_p) >= _anvilCost;
                string t = _anvilError ?? ("Costo de experiencia: " + _anvilCost);
                var st = new GUIStyle(GUI.skin.label) { fontSize = 8 * s, alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 0, 0, 0) };
                st.normal.textColor = can ? new Color(0.5f, 1f, 0.125f) : new Color(1f, 0.38f, 0.38f);
                var r = new Rect(px + 8 * s, py + 67 * s, 160 * s, 12 * s);
                Fill(new Rect(r.xMax - st.CalcSize(new GUIContent(t)).x - 2 * s, r.y, st.CalcSize(new GUIContent(t)).x + 4 * s, r.height), new Color(0f, 0f, 0f, 0.31f));
                GUI.Label(r, t, st);
            }
            if (e.type == EventType.Repaint && _anvilError != null && _craft[1] != null)
            {
                var x = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/container/anvil/error.png");
                if (x != null) GUI.DrawTexture(new Rect(px + 99 * s, py + 45 * s, 28 * s, 21 * s), x);
            }
        }
    }
}
