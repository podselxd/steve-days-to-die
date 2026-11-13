using System;
using System.Collections.Generic;
using SteveDaysToDie.HUD;
using UnityEngine;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Minecraft's inventory screen in place of 7 Days' backpack window. Whenever 7 Days opens its backpack
    /// (inventory key, crafting, character, a container...), its own backpack window is moved out of sight and
    /// this one is drawn where it was, on the right: 9-slot hotbar (the 7 Days toolbelt), the backpack in rows
    /// of 9 (Minecraft shows 3 rows; the wheel scrolls through the rest), the 4 armour slots and Steve.
    /// Everything else of 7 Days (crafting list, character, containers) stays on the left as usual.
    /// It moves the player's REAL 7 Days items: nothing is copied, so nothing can be duplicated.
    /// Minecraft clicks: left takes/places/swaps, right takes half or places one, shift moves between bar and
    /// backpack, 1-9 over a slot swaps with that bar slot, Q drops one (Ctrl+Q the stack), clicking outside
    /// the window throws what you carry. The 2x2 grid crafts with Minecraft's recipes (Crafting/McRecipes.cs);
    /// what it makes is a Minecraft item (mc_<id>).
    /// </summary>
    public partial class McInventory : MonoBehaviour
    {
        const int W = 176, H = 166;                 // Minecraft's inventory.png, in its own pixels
        public static bool Enabled => Settings.GetBool("inventario_minecraft", true);
        public static bool IsOpen { get; private set; }

        EntityPlayerLocal _p;
        // 7 Days windows moved out of sight while ours is open: always its backpack; in the plain inventory view
        // (Tab) also its crafting windows and the page bar on top, whose buttons become our column on the left
        static readonly string[] Always = { "windowBackpack" };
        static readonly string[] InventoryView = { "windowCraftingList", "craftingInfoPanel", "windowCraftingQueue", "windowPagingHeader", "itemInfoPanel", "emptyInfoPanel" };
        readonly Dictionary<string, Vector2i> _home = new Dictionary<string, Vector2i>();
        bool _show7dCrafting;                       // the hammer button: 7 Days' crafting next to ours
        bool InventoryViewOn => !_show7dCrafting && IsPage("crafting");

        // 7 Days' page buttons, in its own order and with its own icons (windows.xml windowPagingHeader)
        static readonly (string page, string sprite)[] Pages =
        {
            ("inventory", "ui_game_symbol_backpack"), ("crafting", "ui_game_symbol_hammer"), ("character", "ui_game_symbol_character"), ("map", "ui_game_symbol_map"),
            ("skills", "ui_game_symbol_skills"), ("quests", "ui_game_symbol_quest"), ("challenges", "ui_game_symbol_challenge"),
            ("players", "ui_game_symbol_players"),
        };
        Texture2D _tex;
        ItemStack _cursor = ItemStack.Empty.Clone();
        int _row;                                   // first backpack row on screen
        float _openedAt;
        Rect _panel;
        int _s;
        bool _blocking;

        enum Kind { Belt, Bag, Armor, Craft, Result, FIn, FFuel, FOut }
        readonly ItemStack[] _craft = { ItemStack.Empty.Clone(), ItemStack.Empty.Clone(), ItemStack.Empty.Clone(), ItemStack.Empty.Clone(),
                                        ItemStack.Empty.Clone(), ItemStack.Empty.Clone(), ItemStack.Empty.Clone(), ItemStack.Empty.Clone(), ItemStack.Empty.Clone() };
        bool _table;                                // Minecraft's crafting table window (3x3) instead of the inventory
        int GridSize => _table ? 3 : 2;
        Texture2D _invTex, _tableTex;

        /// <summary>A crafting table was used: open Minecraft's 3x3 window (an empty 7 Days window gives the cursor).</summary>
        static Vector3i? _furnacePos;
        static string _furnaceKind = "furnace";
        bool InFurnace => _table && _furnacePos != null;
        Crafting.FurnaceState Furnace => _furnacePos != null ? Crafting.McFurnaces.Get(_furnacePos.Value, _furnaceKind) : null;

        /// <summary>A Minecraft furnace / smoker / blast furnace was used: open its window.</summary>
        public static void OpenFurnace(EntityPlayerLocal p, Vector3i pos, string kind)
        {
            _furnacePos = pos; _furnaceKind = kind;
            try { LocalPlayerUI.GetUIForPlayer(p).windowManager.Open("mcScreen", true); }
            catch (Exception ex) { Debug.LogWarning("[SVD] furnace: " + ex.Message); }
        }

        public static void OpenTable(EntityPlayerLocal p)
        {
            _furnacePos = null;
            try { LocalPlayerUI.GetUIForPlayer(p).windowManager.Open("mcScreen", true); }
            catch (Exception ex) { Debug.LogWarning("[SVD] crafting table: " + ex.Message); }
        }

        bool TableOpen() { try { return UI != null && UI.windowManager.IsWindowOpen("mcScreen"); } catch { return false; } }
        Crafting.McRecipes.Recipe _recipe;
        struct Slot { public Kind kind; public int idx; public float x, y; }
        readonly List<Slot> _slots = new List<Slot>();
        // Minecraft's "quick craft" drag: press with something on the cursor, move over slots, release
        int _dragButton = -1;                                   // 0 = share evenly, 1 = one each
        readonly List<Slot> _drag = new List<Slot>();
        bool _shiftDrag;                                        // shift + left held: quick-move every slot passed over
        readonly List<Slot> _shiftDone = new List<Slot>();
        static bool SameSlot(Slot a, Slot b) => a.kind == b.kind && a.idx == b.idx;
        bool InDrag(Slot s) { foreach (var d in _drag) if (SameSlot(d, s)) return true; return false; }
        bool Usable(Slot s) => s.kind != Kind.Result && !(s.kind == Kind.Bag && s.idx >= BagCount);

        bool CanDragInto(Slot s)
        {
            if (!Usable(s) || !Accepts(s, _cursor) || s.kind == Kind.FOut) return false;
            var here = Get(s);
            return here.IsEmpty() || (Same(here, _cursor) && here.count < Max(here));
        }

        // what each dragged slot gets (Minecraft: left = cursor / slots, right = 1 each)
        int DragShare(int n) => _dragButton == 1 ? 1 : Mathf.Max(1, _cursor.count / Mathf.Max(1, n));

        int Room(Slot sl) { var h = Get(sl); return h.IsEmpty() ? Max(_cursor) : Max(h) - h.count; }

        void FinishDrag()
        {
            int n = _drag.Count;
            if (n == 1) Click(_drag[0], _dragButton, false);
            else if (n > 1)
            {
                int share = DragShare(n);
                foreach (var sl in _drag)
                {
                    if (_cursor.IsEmpty()) break;
                    var here = Get(sl).Clone();
                    int put = Mathf.Min(share, Mathf.Min(Room(sl), _cursor.count));
                    if (put <= 0) continue;
                    var st = here.IsEmpty() ? _cursor.Clone() : here;
                    st.count = (here.IsEmpty() ? 0 : here.count) + put;
                    Set(sl, st);
                    _cursor.count -= put;
                    if (_cursor.count <= 0) _cursor = ItemStack.Empty.Clone();
                }
            }
            _drag.Clear();
            _dragButton = -1;
        }

        /// <summary>Test runs: a left click with this stack on the cursor on armour slot idx; returns what the slot holds after.</summary>
        public string TestEquip(ItemStack st, int idx)
        {
            _cursor = st.Clone();
            var slot = new Slot { kind = Kind.Armor, idx = idx };
            string why = Accepts(slot, _cursor) ? "accepted" : "refused (preferred slot " + _p.equipment.PreferredItemSlot(_cursor) + ")";
            Click(slot, 0, false);
            var now = Get(slot);
            string r = (now.IsEmpty() ? "empty" : now.itemValue.ItemClass?.Name) + ", " + why;
            _cursor = ItemStack.Empty.Clone();
            return r;
        }

        // double click: every stack of the same item comes onto the cursor (up to a full stack)
        void Collect()
        {
            if (_cursor.IsEmpty() || Max(_cursor) <= 1) return;
            for (int pass = 0; pass < 2 && _cursor.count < Max(_cursor); pass++)          // small stacks first
                foreach (var sl in _slots)
                {
                    if (!Usable(sl) || sl.kind == Kind.FOut || sl.kind == Kind.Armor) continue;
                    var here = Get(sl).Clone();
                    if (!Same(here, _cursor) || (pass == 0 && here.count >= Max(here))) continue;
                    int take = Mathf.Min(here.count, Max(_cursor) - _cursor.count);
                    here.count -= take; _cursor.count += take;
                    Set(sl, here);
                    if (_cursor.count >= Max(_cursor)) break;
                }
        }

        public static McInventory Instance { get; private set; }
        void Start() { _p = GetComponent<EntityPlayerLocal>(); Instance = this; }

        /// <summary>For the automatic test: put these in the 2x2 grid, take the result like a click. Returns what you carry.</summary>
        public string TestCraft(ItemStack[] four)
        {
            for (int i = 0; i < _craft.Length; i++) _craft[i] = i < four.Length && four[i] != null ? four[i].Clone() : ItemStack.Empty.Clone();
            _recipe = Match();
            string made = _recipe?.result;
            TakeResult(false);
            Debug.Log("[SVD] test craft: recipe " + (made ?? "none") + ", carrying " + (_cursor.IsEmpty() ? "nothing" : _cursor.itemValue.ItemClass.Name + " x" + _cursor.count));
            return _cursor.IsEmpty() ? null : _cursor.itemValue.ItemClass.Name;
        }

        LocalPlayerUI UI => _p != null ? LocalPlayerUI.GetUIForPlayer(_p) : null;

        bool BackpackOpen()
        {
            try { return UI != null && UI.windowManager.IsWindowOpen("backpack"); } catch { return false; }
        }

        void Update()
        {
            if (_p == null) return;
            // ours shows only in the plain inventory view (Tab); any 7 Days page from the column shows 7 Days' own screen
            bool table = Ryan.Mc.Ready && _p.IsAlive() && TableOpen();
            bool want = table || (Enabled && Ryan.Mc.Ready && BackpackOpen() && _p.IsAlive() && InventoryViewOn);
            if (IsOpen && table != _table) OnClose();           // switching window: the grid goes back first
            if (want && !IsOpen) OnOpen();
            else if (!want && IsOpen) OnClose();
            if (IsOpen && !_table) KeepAway();
            if (!AnyPageOpen()) _show7dCrafting = false;     // closed: next time Tab opens the inventory again
        }

        bool AnyPageOpen()
        {
            foreach (var pg in Pages) if (pg.page != "inventory" && IsPage(pg.page)) return true;
            return false;
        }

        /// <summary>The column of 7 Days' page buttons, also on 7 Days' own pages (at the left edge), to come back.</summary>
        void OnGUIColumnOnly()
        {
            if (IsOpen || _p == null || !Enabled || !AnyPageOpen())
            {
                if (!IsOpen && _blocking) { UICamera.ignoreAllEvents = false; _blocking = false; }
                return;
            }
            GUI.depth = -50;
            // same size as the inventory window, never taller than the screen
            int s = Mathf.Clamp(Mathf.Min(MinecraftHUD.CalcScale(), Screen.height / (H + 20)), 1, 6);
            var e = Event.current;
            float px = (Btn + 4) * s, py = Mathf.Round((Screen.height - H * s) / 2f);
            bool over = new Rect(px - (Btn + 2) * s, py, (Btn + 2) * s, H * s).Contains(e.mousePosition);
            if (over != _blocking) { UICamera.ignoreAllEvents = over; _blocking = over; }
            if (e.type == EventType.Repaint) DrawPages(px, py, s, e);
            if (e.type == EventType.MouseDown && e.button == 0 && PageClick(px, py, s, e.mousePosition)) e.Use();
        }

        void OnOpen()
        {
            IsOpen = true;
            _openedAt = Time.time;
            _row = 0;
            _table = TableOpen();
            if (_invTex == null) _invTex = Ryan.Mc.Tex("assets/minecraft/textures/gui/container/inventory.png");
            if (_tableTex == null) _tableTex = Ryan.Mc.Tex("assets/minecraft/textures/gui/container/crafting_table.png");
            _tex = _table ? _tableTex : _invTex;
            if (InFurnace) _tex = Ryan.Mc.Tex("assets/minecraft/textures/gui/container/" + _furnaceKind + ".png") ?? _tableTex;
        }

        void OnClose()
        {
            IsOpen = false;
            for (int i = 0; i < _craft.Length; i++) { if (!_craft[i].IsEmpty()) { _cursorBackup(i); } }
            if (_table) { try { if (TableOpen()) UI.windowManager.Close("mcScreen"); } catch { } _table = false; }
            ReturnCursor();
            RestoreWindows();
            if (_blocking) { UICamera.ignoreAllEvents = false; _blocking = false; }
        }

        void OnDisable() { if (IsOpen) OnClose(); }

        // 7 Days' backpack window goes off screen while ours is open (its controller keeps running)
        void KeepAway()
        {
            try
            {
                var xui = UI?.xui;
                if (xui == null) return;
                bool inv = InventoryViewOn;
                foreach (var n in Always) Away(xui, n, true);
                foreach (var n in InventoryView) Away(xui, n, inv);
            }
            catch { }
        }

        void Away(XUi xui, string name, bool away)
        {
            var w = xui.GetWindow(name);
            if (w == null) return;
            if (!_home.TryGetValue(name, out var home))
            {
                if (!away) return;
                home = w.Position;
                _home[name] = home;
            }
            var want = away ? new Vector2i(home.x, home.y - 4000) : home;
            if (w.Position.x != want.x || w.Position.y != want.y) w.Position = want;
        }

        void RestoreWindows()
        {
            try
            {
                var xui = UI?.xui;
                if (xui != null)
                    foreach (var kv in _home) { var w = xui.GetWindow(kv.Key); if (w != null) w.Position = kv.Value; }
            }
            catch { }
            _home.Clear();
        }

        // ---------- slots ----------

        int BagRows => Mathf.Max(1, Mathf.CeilToInt(BagCount / 9f));
        int BagCount { get { try { return _p.bag.SlotCount; } catch { return 0; } } }

        ItemStack Get(Slot s)
        {
            try
            {
                switch (s.kind)
                {
                    case Kind.Belt: return _p.inventory.GetItem(s.idx) ?? ItemStack.Empty;
                    case Kind.Bag: return s.idx < BagCount ? (_p.bag.ItemGrid[s.idx] ?? ItemStack.Empty) : ItemStack.Empty;
                    case Kind.Craft: return _craft[s.idx] ?? ItemStack.Empty;
                    case Kind.Result: return ResultStack() ?? ItemStack.Empty;
                    case Kind.FIn: return Furnace?.input ?? ItemStack.Empty;
                    case Kind.FFuel: return Furnace?.fuel ?? ItemStack.Empty;
                    case Kind.FOut: return Furnace?.output ?? ItemStack.Empty;
                    default:
                        var v = _p.equipment.GetSlotItem(s.idx);
                        return v == null || v.IsEmpty() ? ItemStack.Empty : new ItemStack(v, 1);
                }
            }
            catch { return ItemStack.Empty; }
        }

        void Set(Slot s, ItemStack st)
        {
            if (st == null || st.count <= 0 || st.IsEmpty()) st = ItemStack.Empty.Clone();
            switch (s.kind)
            {
                case Kind.Belt: _p.inventory.SetItem(s.idx, st); break;
                case Kind.Bag: _p.bag.SetSlot(s.idx, st); break;
                case Kind.Craft: _craft[s.idx] = st; break;
                case Kind.Result: break;
                case Kind.FIn: if (Furnace != null) { Furnace.input = st; Crafting.McFurnaces.Changed(); } break;
                case Kind.FFuel: if (Furnace != null) { Furnace.fuel = st; Crafting.McFurnaces.Changed(); } break;
                case Kind.FOut: if (Furnace != null) { Furnace.output = st; Crafting.McFurnaces.Changed(); } break;
                default: _p.equipment.SetSlotItem(s.idx, st.IsEmpty() ? ItemValue.None.Clone() : st.itemValue.Clone()); break;
            }
        }

        bool Accepts(Slot s, ItemStack st)
        {
            if (st == null || st.IsEmpty()) return true;
            if (s.kind == Kind.Result || s.kind == Kind.FOut) return false;                    // take only
            if (s.kind == Kind.FFuel) return Crafting.McFurnaces.BurnSeconds(st) > 0f;          // only what burns
            if (s.kind != Kind.Armor) return true;
            var ic = st.itemValue.ItemClass;
            return ArmorSlot(st) == s.idx;
        }

        /// <summary>Which armour slot (0 head, 1 chest, 2 legs/hands, 3 feet) a piece goes in, or -1. 7 Days' own
        /// PreferredItemSlot answered -1 for Minecraft pieces (they could not be put on from this inventory).</summary>
        int ArmorSlot(ItemStack st)
        {
            try
            {
                string n = st?.itemValue?.ItemClass?.Name ?? "";
                if (n.StartsWith("mc_"))
                {
                    if (n.EndsWith("_helmet") || n.EndsWith("_head") || n.EndsWith("carved_pumpkin")) return 0;
                    if (n.EndsWith("_chestplate") || n.Contains("_backpack") || n.EndsWith("elytra")) return 1;
                    if (n.EndsWith("_leggings")) return 2;
                    if (n.EndsWith("_boots")) return 3;
                }
                if (st.itemValue.ItemClass is ItemClassArmor a && (int)a.EquipSlot < 4) return (int)a.EquipSlot;
                int pref = _p.equipment.PreferredItemSlot(st);
                return pref >= 0 && pref < 4 ? pref : -1;
            }
            catch { return -1; }
        }

        static int Max(ItemStack st)
        {
            try { return Mathf.Max(1, st.itemValue.ItemClass.Stacknumber.Value); } catch { return 1; }
        }

        static bool Same(ItemStack a, ItemStack b) =>
            a != null && b != null && !a.IsEmpty() && !b.IsEmpty() && a.itemValue.type == b.itemValue.type
            && Max(a) > 1 && !a.itemValue.HasQuality && !b.itemValue.HasQuality;

        // ---------- clicks (Minecraft's rules) ----------

        void Click(Slot s, int button, bool shift)
        {
            if (s.kind == Kind.Result) { TakeResult(shift); return; }
            var here = Get(s).Clone();
            if (shift && button == 0) { QuickMove(s, here); return; }
            bool empty = here.IsEmpty(), carrying = !_cursor.IsEmpty();
            if (button == 0)
            {
                if (!carrying) { if (!empty) { _cursor = here; Set(s, null); } return; }
                if (!Accepts(s, _cursor)) return;
                if (s.kind == Kind.Armor && _cursor.count > 1) return;
                if (empty) { Set(s, _cursor); _cursor = ItemStack.Empty.Clone(); return; }
                if (Same(here, _cursor))
                {
                    int move = Mathf.Min(_cursor.count, Max(here) - here.count);
                    here.count += move; _cursor.count -= move;
                    Set(s, here);
                    if (_cursor.count <= 0) _cursor = ItemStack.Empty.Clone();
                    return;
                }
                Set(s, _cursor); _cursor = here;                         // swap
            }
            else if (button == 1)
            {
                if (!carrying)
                {
                    if (empty) return;
                    int take = (here.count + 1) / 2;
                    _cursor = here.Clone(); _cursor.count = take;
                    here.count -= take;
                    Set(s, here);
                    return;
                }
                if (!Accepts(s, _cursor)) return;
                if (empty || Same(here, _cursor) && here.count < Max(here))
                {
                    var one = _cursor.Clone(); one.count = empty ? 1 : here.count + 1;
                    Set(s, one);
                    _cursor.count -= 1;
                    if (_cursor.count <= 0) _cursor = ItemStack.Empty.Clone();
                }
            }
        }

        // shift-click: bar <-> backpack (armour pieces go onto you first, like Minecraft)
        void QuickMove(Slot from, ItemStack st)
        {
            if (st.IsEmpty()) return;
            var targets = new List<Slot>();
            if (from.kind != Kind.Armor)
            {
                int pref = -1;
                pref = ArmorSlot(st);
                if (pref >= 0 && pref < 4 && Get(new Slot { kind = Kind.Armor, idx = pref }).IsEmpty() && st.count == 1)
                    targets.Add(new Slot { kind = Kind.Armor, idx = pref });
            }
            if (from.kind == Kind.Belt || from.kind == Kind.Armor || from.kind == Kind.Craft) for (int i = 0; i < BagCount; i++) targets.Add(new Slot { kind = Kind.Bag, idx = i });
            if (from.kind == Kind.Bag || from.kind == Kind.Armor || from.kind == Kind.Craft) for (int i = 0; i < 9; i++) targets.Add(new Slot { kind = Kind.Belt, idx = i });

            if (InFurnace && (from.kind == Kind.Bag || from.kind == Kind.Belt))
            {
                var probe = new Crafting.FurnaceState { kind = _furnaceKind, input = st };
                if (Crafting.McFurnaces.SmeltFor(probe) != null) targets.Insert(0, new Slot { kind = Kind.FIn });
                else if (Crafting.McFurnaces.BurnSeconds(st) > 0f) targets.Insert(0, new Slot { kind = Kind.FFuel });
            }
            if (from.kind == Kind.FIn || from.kind == Kind.FFuel || from.kind == Kind.FOut)
            {
                for (int i = 0; i < BagCount; i++) targets.Add(new Slot { kind = Kind.Bag, idx = i });
                for (int i = 0; i < 9; i++) targets.Add(new Slot { kind = Kind.Belt, idx = i });
            }
            var left = st.Clone();
            foreach (var t in targets)                  // first top up matching stacks...
            {
                var h = Get(t).Clone();
                if (!Same(h, left)) continue;
                int m = Mathf.Min(left.count, Max(h) - h.count);
                if (m <= 0) continue;
                h.count += m; left.count -= m; Set(t, h);
                if (left.count <= 0) break;
            }
            foreach (var t in targets)                  // ...then empty slots
            {
                if (left.count <= 0) break;
                if (!Get(t).IsEmpty() || !Accepts(t, left)) continue;
                Set(t, left.Clone()); left.count = 0;
            }
            Set(from, left.count > 0 ? left : null);
        }

        void SwapWithBar(Slot s, int bar)
        {
            var b = new Slot { kind = Kind.Belt, idx = bar };
            if (s.kind == Kind.Belt && s.idx == bar) return;
            var a = Get(s).Clone(); var c = Get(b).Clone();
            if (!Accepts(s, c)) return;
            Set(s, c); Set(b, a);
        }

        void Drop(ItemStack st)
        {
            if (st == null || st.IsEmpty()) return;
            try { GameManager.Instance.ItemDropServer(st.Clone(), _p.GetDropPosition(), Vector3.zero, _p.entityId, 60f); }
            catch (Exception ex) { Debug.LogWarning("[SVD] Inventory drop: " + ex.Message); }
        }

        // what you carry goes back to the backpack when the screen closes (or to the ground when it's full)
        void ReturnCursor()
        {
            if (_cursor == null || _cursor.IsEmpty()) { _cursor = ItemStack.Empty.Clone(); return; }
            try { if (_p.bag.AddItem(_cursor.Clone())) { _cursor = ItemStack.Empty.Clone(); return; } } catch { }
            Drop(_cursor);
            _cursor = ItemStack.Empty.Clone();
        }

        // ---------- the 2x2 grid (Minecraft recipes, see Crafting/McRecipes.cs) ----------

        Crafting.McRecipes.Recipe Match()
        {
            int n = GridSize * GridSize;
            var ids = new string[n];
            for (int i = 0; i < n; i++)
            {
                if (_craft[i].IsEmpty()) continue;
                ids[i] = Crafting.McRecipes.McId(_craft[i]);
                if (ids[i] == null) return null;              // something with no Minecraft version: no recipe
            }
            return Crafting.McRecipes.Match(ids, GridSize);
        }

        ItemStack ResultStack()
        {
            var st = _recipe != null ? Crafting.McItems.Stack(_recipe.result, _recipe.count) : null;
            if (st != null) st.itemValue.SetMetadata(Crafting.RealToggle.Crafted, 1);      // made by a Minecraft recipe: stays Minecraft
            return st;
        }

        // Minecraft: a click takes the result (onto what you carry, if it fits); shift crafts all you can into the bag
        void TakeResult(bool shift)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                _recipe = Match();
                var res = ResultStack();
                if (res == null) return;
                if (shift)
                {
                    if (!_p.bag.AddItem(res.Clone())) return;
                }
                else
                {
                    if (_cursor.IsEmpty()) _cursor = res.Clone();
                    else if (Same(_cursor, res) && _cursor.count + res.count <= Max(res)) _cursor.count += res.count;
                    else return;
                }
                for (int i = 0; i < GridSize * GridSize; i++)
                {
                    if (_craft[i].IsEmpty()) continue;
                    _craft[i].count--;
                    if (_craft[i].count <= 0) _craft[i] = ItemStack.Empty.Clone();
                }
                Ryan.FirstPerson.Swing();
                if (!shift) return;
            }
        }

        void _cursorBackup(int i)
        {
            var st = _craft[i];
            _craft[i] = ItemStack.Empty.Clone();
            try { if (_p.bag.AddItem(st.Clone())) return; } catch { }
            Drop(st);
        }

        // ---------- 7 Days' page buttons, as a column left of the window ----------

        const int Btn = 19, Step = 20;
        Rect PageRect(float px, float py, int s, int i) =>
            new Rect(px - (Btn + 2) * s, py + ((H - Pages.Length * Step + 1) / 2f + i * Step) * s, Btn * s, Btn * s);

        void DrawPages(float px, float py, int s, Event e)
        {
            var xui = UI?.xui;
            for (int i = 0; i < Pages.Length; i++)
            {
                var r = PageRect(px, py, s, i);
                bool on = Pages[i].page == "inventory" ? IsOpen : Pages[i].page == "crafting" ? IsPage("crafting") && !IsOpen : IsPage(Pages[i].page);
                Fill(r, on ? new Color(0.78f, 0.78f, 0.78f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f));
                Fill(new Rect(r.x, r.y, r.width, s), new Color(1f, 1f, 1f, 0.6f));
                Fill(new Rect(r.x, r.yMax - s, r.width, s), new Color(0.2f, 0.2f, 0.2f, 0.8f));
                if (r.Contains(e.mousePosition)) Fill(r, new Color(1f, 1f, 1f, 0.25f));
                var ir = new Rect(r.x + 1.5f * s, r.y + 1.5f * s, 16 * s, 16 * s);
                if (Pages[i].page == "inventory") { var chest = Ryan.Icons.Get("bundle"); if (chest != null) GUI.DrawTexture(ir, chest); }   // Minecraft's chest
                else DrawUiSprite(xui, Pages[i].sprite, ir, on ? Color.white : new Color(0.15f, 0.15f, 0.15f));
            }
        }

        bool PageClick(float px, float py, int s, Vector2 m)
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                if (!PageRect(px, py, s, i).Contains(m)) continue;
                if (Pages[i].page == "inventory")
                {
                    _show7dCrafting = false;                                   // back to the Minecraft inventory
                    if (!IsPage("crafting")) Open("crafting");
                }
                else if (Pages[i].page == "crafting")
                {
                    _show7dCrafting = true;                                    // 7 Days' crafting with its own backpack
                    if (!IsPage("crafting")) Open("crafting");
                }
                else Open(Pages[i].page);
                return true;
            }
            return false;
        }

        bool IsPage(string page) { try { return UI != null && UI.windowManager.IsWindowOpen(page); } catch { return false; } }

        void Open(string page)
        {
            try { XUiC_WindowSelector.OpenSelectorAndWindow(_p, page); }
            catch (Exception ex) { Debug.LogWarning("[SVD] page " + page + ": " + ex.Message); }
        }

        static void DrawUiSprite(XUi xui, string sprite, Rect r, Color tint)
        {
            try
            {
                var atlas = xui?.GetAtlasByName("UIAtlas", sprite);
                var sp = atlas?.GetSprite(sprite);
                var tex = atlas?.texture;
                if (sp == null || tex == null) return;
                float tw = tex.width, th = tex.height;
                var old = GUI.color; GUI.color = tint;
                GUI.DrawTextureWithTexCoords(r, tex, new Rect(sp.x / tw, 1f - (sp.y + sp.height) / th, sp.width / tw, sp.height / th));
                GUI.color = old;
            }
            catch { }
        }

        // ---------- drawing ----------

        void OnGUI()
        {
            if (!IsOpen) { OnGUIColumnOnly(); return; }
            if (_p == null || _tex == null) return;
            GUI.depth = -50;
            // as big as fits in the right part of the screen, leaving 7 Days' own windows on the left visible
            _s = Mathf.Clamp(Mathf.Min(MinecraftHUD.CalcScale(), Mathf.Min(Mathf.FloorToInt(Screen.height / 190f), Mathf.FloorToInt(Screen.width * 0.42f / W))), 1, 6);
            int s = _s;
            float px = _table ? Mathf.Round((Screen.width - W * s) / 2f) : InventoryViewOn ? Mathf.Round((Screen.width - (W + Btn + 2) * s) / 2f) + (Btn + 2) * s   /* window + column, centred together */      // just the inventory: centred
                                       : Screen.width - W * s - Mathf.Max(16, Screen.width / 30);  // next to 7 Days' windows
            float py = Mathf.Round((Screen.height - H * s) / 2f);
            _panel = new Rect(px, py, W * s, H * s);
            var e = Event.current;

            // the game's own menus must not take clicks that land on our window
            bool over = _panel.Contains(e.mousePosition) || !_cursor.IsEmpty() || _bookRect.Contains(e.mousePosition)
                        || new Rect(_panel.x - (Btn + 2) * _s, _panel.y, (Btn + 2) * _s, H * _s).Contains(e.mousePosition);
            if (over != _blocking) { UICamera.ignoreAllEvents = over; _blocking = over; }

            BuildSlots();
            if (e.type == EventType.Layout) _recipe = Match();
            if (e.type == EventType.Repaint)
            {
                GUI.DrawTextureWithTexCoords(_panel, _tex, new Rect(0, 1f - H / 256f, W / 256f, H / 256f));
                if (!_table) InventorySteve.Draw(new Rect(px + 26 * s, py + 8 * s, 51 * s, 72 * s), e.mousePosition, s);     // 3D, follows the mouse
                string title = InFurnace ? Ryan.Mc.T("container." + _furnaceKind, _furnaceKind) : Ryan.Mc.T("container.crafting", "Crafting");
                Label(px + (InFurnace ? 60 : _table ? 28 : 97) * s, py + 6 * s, s, title, new Color(0.25f, 0.25f, 0.25f));
                if (InFurnace && Furnace != null)
                {
                    var f = Furnace;
                    if (f.burnLeft > 0f && f.burnTotal > 0f)
                    {
                        int fh = Mathf.CeilToInt(13f * Mathf.Clamp01(f.burnLeft / f.burnTotal)) + 1;   // the flame, burning down
                        // Minecraft 1.20.2+ keeps these as their own pictures: gui/sprites/container/<kind>/lit_progress.png
                        var lit = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/container/" + _furnaceKind + "/lit_progress.png");
                        if (lit != null) GUI.DrawTextureWithTexCoords(new Rect(px + 56 * s, py + (36 + 14 - fh) * s, 14 * s, fh * s), lit, new Rect(0f, 0f, 1f, fh / 14f));
                        else Sprite(px + 56 * s, py + (36 + 14 - fh) * s, s, 176, 14 - fh, 14, fh);
                    }
                    int aw = Mathf.RoundToInt(24f * Mathf.Clamp01(f.cook / Crafting.McFurnaces.CookSeconds(f.kind)));
                    if (aw > 0)
                    {
                        var arrow = Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/container/" + _furnaceKind + "/burn_progress.png");
                        if (arrow != null) GUI.DrawTextureWithTexCoords(new Rect(px + 79 * s, py + 34 * s, aw * s, 16 * s), arrow, new Rect(0f, 0f, aw / 24f, 1f));
                        else Sprite(px + 79 * s, py + 34 * s, s, 176, 14, aw, 17);                 // older Minecraft: on the sheet
                    }
                }
                if (BagRows > 3)
                {
                    // a slim scrollbar just right of the backpack rows (the wheel moves it)
                    var track = new Rect(px + W * s + s, py + 84 * s, 4 * s, 54 * s);
                    Fill(track, new Color(0.2f, 0.2f, 0.2f, 0.85f));
                    float h = track.height * 3f / BagRows, y = track.y + (track.height - h) * _row / Mathf.Max(1, BagRows - 3);
                    Fill(new Rect(track.x, y, track.width, h), new Color(0.78f, 0.78f, 0.78f, 1f));
                }
                if (!_table) DrawPages(px, py, s, e);
            }
            // Minecraft's recipe book (McRecipeBook.cs): its button on the window, the book on the left
            if (Time.time - _openedAt >= 0.15f || e.type == EventType.Repaint)
                if (RecipeBookGUI(px, py, s, e)) return;

            Slot? hover = null;
            foreach (var sl in _slots)
            {
                var r = new Rect(px + sl.x * s, py + sl.y * s, 16 * s, 16 * s);
                bool isHover = r.Contains(e.mousePosition);
                if (isHover) hover = sl;
                if (e.type == EventType.Repaint)
                {
                    if (sl.kind == Kind.Bag && sl.idx >= BagCount)
                    {
                        Fill(r, new Color(0.35f, 0.35f, 0.35f, 0.9f));     // a slot 7 Days doesn't give you (yet)
                        continue;
                    }
                    var shown = Get(sl);
                    if (_drag.Count > 1 && InDrag(sl))
                    {
                        // Minecraft shows how it will be shared while you drag
                        var pre = shown.IsEmpty() ? _cursor.Clone() : shown.Clone();
                        pre.count = (shown.IsEmpty() ? 0 : shown.count) + Mathf.Min(DragShare(_drag.Count), Room(sl));
                        shown = pre;
                    }
                    DrawStack(shown, r, s);
                    if (isHover || InDrag(sl)) Fill(r, new Color(1f, 1f, 1f, 0.5f));
                }
            }

            if (e.type == EventType.Repaint)
            {
                if (!_cursor.IsEmpty())
                {
                    var left = _cursor;
                    if (_drag.Count > 1)
                    {
                        left = _cursor.Clone(); int share = DragShare(_drag.Count), used = 0;
                        foreach (var d in _drag) used += Mathf.Min(share, Room(d));
                        left.count = Mathf.Max(0, _cursor.count - used);
                    }
                    if (left.count > 0) DrawStack(left, new Rect(e.mousePosition.x - 8 * s, e.mousePosition.y - 8 * s, 16 * s, 16 * s), s);
                }
                else if (hover.HasValue)
                {
                    var st = Get(hover.Value);
                    if (!st.IsEmpty()) Tooltip(Name(st), e.mousePosition, s);
                }
            }

            if (Time.time - _openedAt < 0.15f) return;
            if (_table && e.type == EventType.KeyDown && (e.keyCode == KeyCode.E || e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Tab)) { e.Use(); OnClose(); return; }
            if (e.type == EventType.MouseDown && e.button == 0 && !_table && PageClick(px, py, s, e.mousePosition)) { e.Use(); return; }
            // Minecraft's drag: moving with a button held over slots
            if (e.type == EventType.MouseDrag)
            {
                if (_dragButton >= 0 && hover.HasValue && !InDrag(hover.Value) && CanDragInto(hover.Value) && _drag.Count < _cursor.count)
                    _drag.Add(hover.Value);
                if (_shiftDrag && hover.HasValue && Usable(hover.Value))
                {
                    bool done = false; foreach (var d in _shiftDone) if (SameSlot(d, hover.Value)) done = true;
                    if (!done) { _shiftDone.Add(hover.Value); var st = Get(hover.Value).Clone(); if (!st.IsEmpty()) QuickMove(hover.Value, st); }
                }
                if (_dragButton >= 0 || _shiftDrag) { e.Use(); return; }
            }
            if (e.type == EventType.MouseUp)
            {
                if (_dragButton >= 0 && e.button == _dragButton) { FinishDrag(); e.Use(); return; }
                if (e.button == 0) { _shiftDrag = false; _shiftDone.Clear(); }
            }
            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                if (hover.HasValue)
                {
                    var hv = hover.Value;
                    if (Usable(hv) || hv.kind == Kind.Result)
                    {
                        if (e.button == 0 && e.clickCount >= 2 && !_cursor.IsEmpty() && hv.kind != Kind.Result) Collect();   // double click: gather
                        else if (!_cursor.IsEmpty() && !e.shift && hv.kind != Kind.Result && _dragButton < 0)
                        {
                            _dragButton = e.button; _drag.Clear(); _drag.Add(hv);                                       // start a drag
                        }
                        else
                        {
                            Click(hv, e.button, e.shift);
                            if (e.shift && e.button == 0) { _shiftDrag = true; _shiftDone.Clear(); _shiftDone.Add(hv); }
                        }
                    }
                    e.Use();
                }
                else if (!_panel.Contains(e.mousePosition) && !_cursor.IsEmpty())
                {
                    if (e.button == 0) { Drop(_cursor); _cursor = ItemStack.Empty.Clone(); }
                    else { var one = _cursor.Clone(); one.count = 1; Drop(one); _cursor.count--; if (_cursor.count <= 0) _cursor = ItemStack.Empty.Clone(); }
                    e.Use();
                }
                else if (_panel.Contains(e.mousePosition)) e.Use();
            }
            else if (e.type == EventType.ScrollWheel && _panel.Contains(e.mousePosition) && BagRows > 3)
            {
                _row = Mathf.Clamp(_row + (e.delta.y > 0 ? 1 : -1), 0, BagRows - 3);
                e.Use();
            }
            else if (e.type == EventType.KeyDown && hover.HasValue && !(hover.Value.kind == Kind.Bag && hover.Value.idx >= BagCount))
            {
                if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9) { SwapWithBar(hover.Value, e.keyCode - KeyCode.Alpha1); e.Use(); }
                else if (e.keyCode == KeyCode.Q)
                {
                    var st = Get(hover.Value).Clone();
                    if (!st.IsEmpty())
                    {
                        int n = e.control ? st.count : 1;
                        var drop = st.Clone(); drop.count = n; Drop(drop);
                        st.count -= n; Set(hover.Value, st);
                    }
                    e.Use();
                }
            }
        }

        void BuildSlots()
        {
            _slots.Clear();
            if (!_table) for (int i = 0; i < 4; i++) _slots.Add(new Slot { kind = Kind.Armor, idx = i, x = 8, y = 8 + i * 18 });
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 9; c++)
                    _slots.Add(new Slot { kind = Kind.Bag, idx = (_row + r) * 9 + c, x = 8 + c * 18, y = 84 + r * 18 });
            for (int c = 0; c < 9; c++) _slots.Add(new Slot { kind = Kind.Belt, idx = c, x = 8 + c * 18, y = 142 });
            if (InFurnace)
            {
                _slots.Add(new Slot { kind = Kind.FIn, x = 56, y = 17 });
                _slots.Add(new Slot { kind = Kind.FFuel, x = 56, y = 53 });
                _slots.Add(new Slot { kind = Kind.FOut, x = 116, y = 35 });
            }
            else if (_table)
            {
                for (int n = 0; n < 9; n++) _slots.Add(new Slot { kind = Kind.Craft, idx = n, x = 30 + n % 3 * 18, y = 17 + n / 3 * 18 });
                _slots.Add(new Slot { kind = Kind.Result, idx = 0, x = 124, y = 35 });
            }
            else
            {
                for (int n = 0; n < 4; n++) _slots.Add(new Slot { kind = Kind.Craft, idx = n, x = 98 + n % 2 * 18, y = 18 + n / 2 * 18 });
                _slots.Add(new Slot { kind = Kind.Result, idx = 0, x = 154, y = 28 });
            }
        }

        void DrawStack(ItemStack st, Rect r, int s)
        {
            if (st == null || st.IsEmpty()) return;
            var ic = st.itemValue.ItemClass;
            if (ic == null) return;
            MinecraftHUD.DrawGameIcon(_p, st, ic, r);               // the item's own 7 Days icon
            try
            {
                if (ic.HasAnyTags(FastTags<TagGroup.Global>.Parse("canHaveCosmetic")) || st.itemValue.MaxUseTimes > 0)
                {
                    float left = st.itemValue.MaxUseTimes > 0 ? 1f - st.itemValue.UseTimes / st.itemValue.MaxUseTimes : 1f;
                    if (st.itemValue.MaxUseTimes > 0 && left < 1f)
                    {
                        // Minecraft's durability bar
                        Fill(new Rect(r.x + 2 * s, r.y + 13 * s, 13 * s, 2 * s), Color.black);
                        Fill(new Rect(r.x + 2 * s, r.y + 13 * s, Mathf.Round(13 * left) * s, s), Color.HSVToRGB(left / 3f, 1f, 1f));
                    }
                }
            }
            catch { }
            if (st.count > 1)
            {
                var style = new GUIStyle(GUI.skin.label) { fontSize = 8 * s, alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 0) };
                var rr = new Rect(r.x, r.y, r.width + s, r.height + 2 * s);
                style.normal.textColor = new Color(0.25f, 0.25f, 0.25f);
                GUI.Label(new Rect(rr.x + s, rr.y + s, rr.width, rr.height), st.count.ToString(), style);
                style.normal.textColor = Color.white;
                GUI.Label(rr, st.count.ToString(), style);
            }
        }

        static string Name(ItemStack st)
        {
            try { var n = st.itemValue.ItemClass.GetLocalizedItemName(); return string.IsNullOrEmpty(n) ? st.itemValue.ItemClass.Name : n; }
            catch { return ""; }
        }

        // a piece of the window's own texture (256x256 sheet) at pixel scale s
        void Sprite(float x, float y, int s, int u, int v, int w, int h)
        {
            if (_tex == null || w <= 0 || h <= 0) return;
            GUI.DrawTextureWithTexCoords(new Rect(x, y, w * s, h * s), _tex, new Rect(u / 256f, 1f - (v + h) / 256f, w / 256f, h / 256f));
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Label(float x, float y, int s, string text, Color c)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 7 * s, padding = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = c;
            GUI.Label(new Rect(x, y, 160 * s, 12 * s), text, style);
        }

        static void Tooltip(string text, Vector2 at, int s)
        {
            if (string.IsNullOrEmpty(text)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 8 * s, padding = new RectOffset(3 * s, 3 * s, 2 * s, 2 * s) };
            style.normal.textColor = Color.white;
            var size = style.CalcSize(new GUIContent(text));
            var r = new Rect(at.x + 12 * s, at.y - 12 * s, size.x, size.y);
            Fill(r, new Color(0.06f, 0f, 0.06f, 0.94f));
            var old = GUI.color; GUI.color = new Color(0.31f, 0f, 1f, 0.5f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - s, r.width, s), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(r, text, style);
        }

        // Steve, from the front, in the black box (your skin, both layers)
        static void DrawSteve(float x, float y, int s)
        {
            var t = SteveCharacter.SkinTexture;
            if (t == null) return;
            float p = 2.1f * s;
            float ox = x + (51 * s - 16 * p) / 2f, oy = y + (72 * s - 32 * p) / 2f;
            bool old = t.height < 64, slim = Steve.SteveParts.Slim;
            int aw = slim ? 3 : 4;
            void Part(float dx, float dy, int u, int v, int w, int h, float grow = 0f)
            {
                var r = new Rect(ox + dx * p - grow, oy + dy * p - grow, w * p + 2 * grow, h * p + 2 * grow);
                GUI.DrawTextureWithTexCoords(r, t, new Rect(u / (float)t.width, 1f - (v + h) / (float)t.height, w / (float)t.width, h / (float)t.height));
            }
            Part(4, 0, 8, 8, 8, 8);
            Part(4, 8, 20, 20, 8, 12);
            Part(4 - aw, 8, 44, 20, aw, 12);
            Part(12, 8, old ? 44 : 36, old ? 20 : 52, aw, 12);
            Part(4, 20, 4, 20, 4, 12);
            Part(8, 20, old ? 4 : 20, old ? 20 : 52, 4, 12);
            Part(4, 0, 40, 8, 8, 8, p * 0.5f);
            if (old) return;
            Part(4, 8, 20, 36, 8, 12);
            Part(4 - aw, 8, 44, 36, aw, 12);
            Part(12, 8, 52, 52, aw, 12);
            Part(4, 20, 4, 36, 4, 12);
            Part(8, 20, 4, 52, 4, 12);
        }
    }
}
