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
    /// the window throws what you carry. The 2x2 crafting grid is drawn but not in use yet (Minecraft crafting
    /// and the recipe book come later).
    /// </summary>
    public class McInventory : MonoBehaviour
    {
        const int W = 176, H = 166;                 // Minecraft's inventory.png, in its own pixels
        public static bool Enabled => Settings.GetBool("inventario_minecraft", true);
        public static bool IsOpen { get; private set; }

        EntityPlayerLocal _p;
        XUiV_Window _bp;                            // 7 Days' backpack window
        Vector2i _bpHome;
        bool _bpMoved;
        Texture2D _tex;
        ItemStack _cursor = ItemStack.Empty.Clone();
        int _row;                                   // first backpack row on screen
        float _openedAt;
        Rect _panel;
        int _s;
        bool _blocking;

        enum Kind { Belt, Bag, Armor }
        struct Slot { public Kind kind; public int idx; public float x, y; }
        readonly List<Slot> _slots = new List<Slot>();

        void Start() { _p = GetComponent<EntityPlayerLocal>(); }

        LocalPlayerUI UI => _p != null ? LocalPlayerUI.GetUIForPlayer(_p) : null;

        bool BackpackOpen()
        {
            try { return UI != null && UI.windowManager.IsWindowOpen("backpack"); } catch { return false; }
        }

        void Update()
        {
            if (_p == null) return;
            bool want = Enabled && Ryan.Mc.Ready && BackpackOpen() && _p.IsAlive();
            if (want && !IsOpen) OnOpen();
            else if (!want && IsOpen) OnClose();
            if (IsOpen) KeepBackpackAway();
        }

        void OnOpen()
        {
            IsOpen = true;
            _openedAt = Time.time;
            _row = 0;
            if (_tex == null) _tex = Ryan.Mc.Tex("assets/minecraft/textures/gui/container/inventory.png");
        }

        void OnClose()
        {
            IsOpen = false;
            ReturnCursor();
            if (_bp != null && _bpMoved) { _bp.Position = _bpHome; _bpMoved = false; }
            if (_blocking) { UICamera.ignoreAllEvents = false; _blocking = false; }
        }

        void OnDisable() { if (IsOpen) OnClose(); }

        // 7 Days' backpack window goes off screen while ours is open (its controller keeps running)
        void KeepBackpackAway()
        {
            try
            {
                if (_bp == null) _bp = UI?.xui?.GetWindow("windowBackpack");
                if (_bp == null) return;
                if (!_bpMoved) { _bpHome = _bp.Position; _bpMoved = true; }
                var away = new Vector2i(_bpHome.x, _bpHome.y - 4000);
                if (_bp.Position.y != away.y) _bp.Position = away;
            }
            catch { }
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
                default: _p.equipment.SetSlotItem(s.idx, st.IsEmpty() ? ItemValue.None.Clone() : st.itemValue.Clone()); break;
            }
        }

        bool Accepts(Slot s, ItemStack st)
        {
            if (st == null || st.IsEmpty()) return true;
            if (s.kind != Kind.Armor) return true;
            var ic = st.itemValue.ItemClass;
            try { return ic is ItemClassArmor && _p.equipment.PreferredItemSlot(st) == s.idx; } catch { return false; }
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
                try { if (st.itemValue.ItemClass is ItemClassArmor) pref = _p.equipment.PreferredItemSlot(st); } catch { }
                if (pref >= 0 && pref < 4 && Get(new Slot { kind = Kind.Armor, idx = pref }).IsEmpty() && st.count == 1)
                    targets.Add(new Slot { kind = Kind.Armor, idx = pref });
            }
            if (from.kind == Kind.Belt || from.kind == Kind.Armor) for (int i = 0; i < BagCount; i++) targets.Add(new Slot { kind = Kind.Bag, idx = i });
            if (from.kind == Kind.Bag || from.kind == Kind.Armor) for (int i = 0; i < 9; i++) targets.Add(new Slot { kind = Kind.Belt, idx = i });

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

        // ---------- drawing ----------

        void OnGUI()
        {
            if (!IsOpen || _p == null || _tex == null) return;
            GUI.depth = -50;
            _s = Mathf.Clamp(Mathf.Min(MinecraftHUD.CalcScale(), Mathf.FloorToInt(Screen.height / 190f)), 2, 6);
            int s = _s;
            float px = Screen.width - W * s - Mathf.Max(16, Screen.width / 30);       // where 7 Days' backpack sits
            float py = Mathf.Round((Screen.height - H * s) / 2f);
            _panel = new Rect(px, py, W * s, H * s);
            var e = Event.current;

            // the game's own menus must not take clicks that land on our window
            bool over = _panel.Contains(e.mousePosition) || !_cursor.IsEmpty();
            if (over != _blocking) { UICamera.ignoreAllEvents = over; _blocking = over; }

            BuildSlots();
            if (e.type == EventType.Repaint)
            {
                GUI.DrawTextureWithTexCoords(_panel, _tex, new Rect(0, 1f - H / 256f, W / 256f, H / 256f));
                DrawSteve(px + 26 * s, py + 8 * s, s);
                Label(px + 97 * s, py + 6 * s, s, Ryan.Mc.T("container.crafting", "Crafting"), new Color(0.25f, 0.25f, 0.25f));
                if (BagRows > 3)
                    Label(px + 8 * s, py + 72 * s, s, "▲▼ " + (_row + 1) + "-" + Mathf.Min(_row + 3, BagRows) + "/" + BagRows, new Color(0.25f, 0.25f, 0.25f));
            }

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
                    DrawStack(Get(sl), r, s);
                    if (isHover) Fill(r, new Color(1f, 1f, 1f, 0.5f));
                }
            }

            if (e.type == EventType.Repaint)
            {
                if (!_cursor.IsEmpty()) DrawStack(_cursor, new Rect(e.mousePosition.x - 8 * s, e.mousePosition.y - 8 * s, 16 * s, 16 * s), s);
                else if (hover.HasValue)
                {
                    var st = Get(hover.Value);
                    if (!st.IsEmpty()) Tooltip(Name(st), e.mousePosition, s);
                }
            }

            if (Time.time - _openedAt < 0.15f) return;
            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                if (hover.HasValue)
                {
                    if (!(hover.Value.kind == Kind.Bag && hover.Value.idx >= BagCount)) Click(hover.Value, e.button, e.shift);
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
            for (int i = 0; i < 4; i++) _slots.Add(new Slot { kind = Kind.Armor, idx = i, x = 8, y = 8 + i * 18 });
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 9; c++)
                    _slots.Add(new Slot { kind = Kind.Bag, idx = (_row + r) * 9 + c, x = 8 + c * 18, y = 84 + r * 18 });
            for (int c = 0; c < 9; c++) _slots.Add(new Slot { kind = Kind.Belt, idx = c, x = 8 + c * 18, y = 142 });
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
