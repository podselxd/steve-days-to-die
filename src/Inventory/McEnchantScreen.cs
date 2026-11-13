using System.Collections.Generic;
using UnityEngine;
using SteveDaysToDie.Enchanting;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Minecraft's enchanting table screen (gui/container/enchanting_table.png): the item (15,47) and lapis (35,47) slots
    /// are the crafting grid's first two (they go back to the bag when it closes), three offers at (60, 14 + 19 i) with
    /// their level cost on the right (green when you can pay it) and a hint of the first enchantment like Minecraft's
    /// tooltip. Offer i costs i + 1 levels and i + 1 lapis, needs as many levels as its cost. Costs depend on the
    /// bookshelves around the table (McEnchant.Shelves) and on the player's enchanting seed (changes after each use).
    /// </summary>
    public partial class McInventory
    {
        static Vector3i? _enchPos;
        static int _enchShelves;
        bool InEnchant => _table && _enchPos != null;

        public static void OpenEnchant(EntityPlayerLocal p, Vector3i pos)
        {
            _furnacePos = null; _enchPos = pos;
            try { _enchShelves = McEnchant.Shelves(pos); } catch { _enchShelves = 0; }
            try { LocalPlayerUI.GetUIForPlayer(p).windowManager.Open("mcScreen", true); }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] enchanting table: " + ex.Message); }
        }

        /// <summary>Test hook (MeltyDemo "encantar"): put an item and lapis in, enchant with offer i; returns the item's enchantments.</summary>
        public string TestEnchant(string itemId, int offer, bool put)
        {
            if (put)
            {
                _craft[0] = Crafting.McItems.Stack(itemId, 1) ?? ItemStack.Empty.Clone();
                _craft[1] = Crafting.McItems.Stack("lapis_lazuli", 3) ?? ItemStack.Empty.Clone();
                return "";
            }
            string id = McEnchant.McId(_craft[0]?.itemValue);
            int seed = Seed();
            var costs = McEnchant.Costs(_enchShelves, id, seed);
            Debug.Log($"[SVD] enchant test: {id} shelves {_enchShelves} costs {costs[0]}/{costs[1]}/{costs[2]} level {McXp.LevelOf(_p)}");
            if (costs[offer] <= 0) return "no offer";
            Enchant(id, costs[offer], seed + offer, offer + 1);
            var st = _craft[0];
            return (st?.itemValue?.ItemClass?.Name ?? "?") + " " + (st?.itemValue?.GetMetadata(McEnchant.Key) as string) + " level now " + McXp.LevelOf(_p);
        }

        int Seed()
        {
            float v = 0f;
            try { v = _p.Buffs.GetCustomVar("mcEnchSeed"); } catch { }
            if (v == 0f) { v = Random.Range(1, 1000000); try { _p.Buffs.SetCustomVar("mcEnchSeed", v); } catch { } }
            return (int)v;
        }

        static Texture2D ET(string s) => Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/container/enchanting_table/" + s + ".png");

        static bool EnchantableHere(ItemStack st)
        {
            string id = McEnchant.McId(st?.itemValue);
            return id != null && McEnchant.Kind(id) != null && id != "enchanted_book" && !McEnchant.IsEnchanted(st.itemValue);
        }

        void EnchantGUI(float px, float py, int s, Event e)
        {
            var item = _craft[0]; var lapis = _craft[1];
            string id = McEnchant.McId(item?.itemValue);
            bool can = item != null && !item.IsEmpty() && item.count == 1 && EnchantableHere(item);
            int seed = Seed();
            int[] costs = can ? McEnchant.Costs(_enchShelves, id, seed) : new int[3];
            int level = McXp.LevelOf(_p);
            int lap = lapis != null && !lapis.IsEmpty() && McEnchant.McId(lapis.itemValue) == "lapis_lazuli" ? lapis.count : 0;
            var dark = new GUIStyle(GUI.skin.label) { fontSize = 7 * s, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Clip };
            var num = new GUIStyle(dark) { alignment = TextAnchor.LowerRight, fontSize = 8 * s };
            string tip = null;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(px + 60 * s, py + (14 + 19 * i) * s, 108 * s, 19 * s);
                int cost = costs[i];
                bool afford = cost > 0 && level >= cost && lap >= i + 1;
                bool over = r.Contains(e.mousePosition);
                if (e.type == EventType.Repaint)
                {
                    var bg = cost <= 0 ? ET("enchantment_slot_disabled") : afford ? ET(over ? "enchantment_slot_highlighted" : "enchantment_slot") : ET("enchantment_slot_disabled");
                    if (bg != null) GUI.DrawTexture(r, bg);
                    if (cost > 0)
                    {
                        var lv = ET("level_" + (i + 1) + (afford ? "" : "_disabled"));
                        if (lv != null) GUI.DrawTexture(new Rect(r.x + 1 * s, r.y + 1 * s, 16 * s, 16 * s), lv);
                        var picks = McEnchant.Pick(id, cost, seed + i);
                        dark.normal.textColor = afford ? new Color(0.41f, 0.38f, 0.29f) : new Color(0.25f, 0.2f, 0.16f);
                        GUI.Label(new Rect(r.x + 20 * s, r.y + 1 * s, 86 * s, 9 * s), picks.Count > 0 ? McEnchant.Line(picks[0].e.id, picks[0].level) + " ...?" : "", dark);
                        num.normal.textColor = afford ? new Color(0.5f, 1f, 0.125f) : new Color(0.25f, 0.5f, 0.06f);
                        GUI.Label(new Rect(r.x, r.y, r.width - 2 * s, r.height - 1 * s), cost.ToString(), num);
                    }
                }
                if (over && cost > 0)
                {
                    var picks = McEnchant.Pick(id, cost, seed + i);
                    tip = (picks.Count > 0 ? McEnchant.Line(picks[0].e.id, picks[0].level) + " . . . ?" : "") +
                          "\n" + (i + 1) + " lapislázuli" + (lap >= i + 1 ? "" : " (te falta)") +
                          "\n" + (i + 1) + (i == 0 ? " nivel" : " niveles") + " de experiencia" +
                          (level >= cost ? "" : "\nNecesitas nivel " + cost);
                }
                if (over && afford && e.type == EventType.MouseDown && e.button == 0)
                {
                    Enchant(id, cost, seed + i, i + 1);
                    e.Use();
                }
            }
            if (tip != null && e.type == EventType.Repaint) Tooltip(tip, e.mousePosition, s);
        }

        void Enchant(string id, int cost, int seed, int price)
        {
            var picks = McEnchant.Pick(id, cost, seed);
            if (picks.Count == 0) return;
            bool book = id == "book";
            if (book && picks.Count > 1) picks.RemoveAt(new System.Random(seed).Next(picks.Count));   // Minecraft: a book loses one
            ItemStack st = book ? Crafting.McItems.Stack("enchanted_book", 1) : _craft[0].Clone();
            if (st == null) return;
            var d = new Dictionary<string, int>();
            foreach (var (e, l) in picks) d[e.id] = l;
            McEnchant.Write(st.itemValue, d);
            _craft[0] = st;
            var lapis = _craft[1].Clone(); lapis.count -= price;
            _craft[1] = lapis.count > 0 ? lapis : ItemStack.Empty.Clone();
            McXp.SpendLevels(_p, price);
            try { _p.Buffs.SetCustomVar("mcEnchSeed", Random.Range(1, 1000000)); } catch { }
            Sound.MinecraftSounds.PlayGroup("block/enchantment_table/enchant", _p.position + Vector3.up, 8f, 1f, 0.9f + Random.value * 0.2f);
        }

        /// <summary>The tooltip of an item: its name and, when enchanted, Minecraft's grey list of enchantments.</summary>
        static string NameWithEnchantments(ItemStack st)
        {
            string n = Name(st);
            try
            {
                var d = McEnchant.Read(st.itemValue);
                foreach (var kv in d) n += "\n" + McEnchant.Line(kv.Key, kv.Value);
            }
            catch { }
            return n;
        }

        /// <summary>Minecraft's enchantment glint: a purple shimmer over enchanted items.</summary>
        static void Glint(ItemStack st, Rect r)
        {
            try
            {
                if (st == null || st.IsEmpty() || !McEnchant.IsEnchanted(st.itemValue)) return;
                float a = 0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f + r.x * 0.05f);
                Fill(r, new Color(0.62f, 0.3f, 1f, a));
            }
            catch { }
        }
    }
}
