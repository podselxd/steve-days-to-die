using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Minecraft's recipe book (user 2026-11-13: "añadir el libro de los crafteos al inventario"): the green book button
    /// in the inventory (104,61) and the crafting table (5,34) opens gui/recipe_book.png on the left, 5 x 4 recipes a page
    /// (green = you have the things, red = you don't), the filter button shows only what you can make, page arrows.
    /// Clicking a recipe puts its ingredients into the grid from your bag (Minecraft's "place recipe"); when you lack
    /// something it shows the recipe as a ghost in the grid instead. Only recipes that fit the grid (2x2 / 3x3).
    /// </summary>
    public partial class McInventory
    {
        bool _bookOpen, _bookOnlyCraftable;
        int _bookPage;
        Crafting.McRecipes.Recipe _ghost;
        Rect _bookRect;
        float _bookRefresh;
        readonly List<(Crafting.McRecipes.Recipe r, bool can)> _bookList = new List<(Crafting.McRecipes.Recipe, bool)>();
        const int BookW = 147, BookH = 166;

        /// <summary>Test hook (MeltyDemo): open or close the recipe book.</summary>
        public void TestRecipeBook(bool open) { _bookOpen = open; _bookRefresh = 0f; }

        static Texture2D T(string sprite) => Ryan.Mc.Tex("assets/minecraft/textures/gui/sprites/recipe_book/" + sprite + ".png");

        Rect BookButton(float px, float py, int s) => _table ? new Rect(px + 5 * s, py + 34 * s, 20 * s, 18 * s) : new Rect(px + 104 * s, py + 61 * s, 20 * s, 18 * s);

        Rect BookPanel(float px, float py, int s)
        {
            float left = _table ? px : px - (Btn + 2) * s;           // left of the page column in the inventory view
            float x = Mathf.Max(0f, left - (BookW + 2) * s);
            return new Rect(x, py, BookW * s, BookH * s);
        }

        // what the player carries, counted by Minecraft id (bag, belt and the grid itself)
        Dictionary<string, int> Have()
        {
            var have = new Dictionary<string, int>();
            void Add(ItemStack st)
            {
                string id = Crafting.McRecipes.McId(st);
                if (id == null) return;
                have.TryGetValue(id, out int n); have[id] = n + st.count;
            }
            try { for (int i = 0; i < BagCount; i++) Add(_p.bag.ItemGrid[i]); } catch { }
            try { for (int i = 0; i < 9; i++) Add(_p.inventory.GetItem(i)); } catch { }
            for (int i = 0; i < _craft.Length; i++) Add(_craft[i]);
            return have;
        }

        static List<HashSet<string>> Needs(Crafting.McRecipes.Recipe r)
        {
            var l = new List<HashSet<string>>();
            if (r.shaped) { foreach (var g in r.grid) if (g != null && g.Count > 0) l.Add(g); }
            else if (r.ingredients != null) l.AddRange(r.ingredients);
            return l;
        }

        static bool Fits(Crafting.McRecipes.Recipe r, int size) => r.shaped ? r.w <= size && r.h <= size : r.ingredients != null && r.ingredients.Count <= size * size;

        static bool CanMake(List<HashSet<string>> needs, Dictionary<string, int> have)
        {
            var left = new Dictionary<string, int>(have);
            foreach (var set in needs)
            {
                string got = null;
                foreach (var id in set) if (left.TryGetValue(id, out int n) && n > 0) { got = id; break; }
                if (got == null) return false;
                left[got]--;
            }
            return true;
        }

        void RefreshBook()
        {
            _bookList.Clear();
            var have = Have();
            var seen = new HashSet<string>();
            foreach (var r in Crafting.McRecipes.All)
            {
                if (r.result == null || !Fits(r, GridSize) || Crafting.McItems.Stack(r.result, 1) == null) continue;
                bool can = CanMake(Needs(r), have);
                string key = r.result + (can ? "+" : "-");
                if (!seen.Add(key)) continue;                                // Minecraft groups recipes of the same thing
                if (_bookOnlyCraftable && !can) continue;
                _bookList.Add((r, can));
            }
            _bookList.Sort((a, b) => a.can == b.can ? 0 : a.can ? -1 : 1);   // what you can make first
            // the same result once: keep the craftable one
            var once = new HashSet<string>();
            _bookList.RemoveAll(x => !once.Add(x.r.result));
        }

        /// <summary>Draws the button and the book; true when it used the event (a click on it).</summary>
        bool RecipeBookGUI(float px, float py, int s, Event e)
        {
            if (InFurnace) return false;
            var btn = BookButton(px, py, s);
            bool overBtn = btn.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                var bt = T(overBtn ? "button_highlighted" : "button") ?? T("button");
                if (bt != null) GUI.DrawTexture(btn, bt);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && overBtn)
            {
                _bookOpen = !_bookOpen; _bookPage = 0; _bookRefresh = 0f;
                Sound.MinecraftSounds.PlayGroup("random/click", _p.position, 8f, 0.5f, 1f);
                e.Use(); return true;
            }
            DrawGhost(px, py, s, e);
            if (!_bookOpen) { _bookRect = Rect.zero; return false; }
            _bookRect = BookPanel(px, py, s);
            float bx = _bookRect.x, by = _bookRect.y;
            if (Time.unscaledTime >= _bookRefresh) { _bookRefresh = Time.unscaledTime + 0.5f; RefreshBook(); }
            int pages = Mathf.Max(1, Mathf.CeilToInt(_bookList.Count / 20f));
            _bookPage = Mathf.Clamp(_bookPage, 0, pages - 1);
            var book = Ryan.Mc.Tex("assets/minecraft/textures/gui/recipe_book.png");
            var filter = new Rect(bx + 110 * s, by + 12 * s, 26 * s, 16 * s);
            var back = new Rect(bx + 38 * s, by + 137 * s, 12 * s, 17 * s);
            var fwd = new Rect(bx + 93 * s, by + 137 * s, 12 * s, 17 * s);
            (Crafting.McRecipes.Recipe r, bool can)? hover = null;
            for (int i = 0; i < 20; i++)
            {
                int k = _bookPage * 20 + i;
                if (k >= _bookList.Count) break;
                var cell = new Rect(bx + (11 + i % 5 * 25) * s, by + (31 + i / 5 * 25) * s, 25 * s, 25 * s);
                if (cell.Contains(e.mousePosition)) hover = _bookList[k];
            }
            if (e.type == EventType.Repaint)
            {
                if (book != null) GUI.DrawTextureWithTexCoords(_bookRect, book, new Rect(1f / 256f, 1f - (1f + BookH) / 256f, BookW / 256f, BookH / 256f));
                var ft = T(_bookOnlyCraftable ? (filter.Contains(e.mousePosition) ? "filter_enabled_highlighted" : "filter_enabled") : (filter.Contains(e.mousePosition) ? "filter_disabled_highlighted" : "filter_disabled"));
                if (ft != null) GUI.DrawTexture(filter, ft);
                for (int i = 0; i < 20; i++)
                {
                    int k = _bookPage * 20 + i;
                    if (k >= _bookList.Count) break;
                    var (r, can) = _bookList[k];
                    var cell = new Rect(bx + (11 + i % 5 * 25) * s, by + (31 + i / 5 * 25) * s, 25 * s, 25 * s);
                    var bg = T(can ? "slot_craftable" : "slot_uncraftable");
                    if (bg != null) GUI.DrawTexture(cell, bg); else Fill(cell, can ? new Color(0.5f, 0.6f, 0.5f) : new Color(0.7f, 0.4f, 0.4f));
                    var st = Crafting.McItems.Stack(r.result, r.count);
                    if (st != null) DrawStack(st, new Rect(cell.x + 4 * s, cell.y + 4 * s, 16 * s, 16 * s), s);
                }
                if (pages > 1)
                {
                    var pb = T(back.Contains(e.mousePosition) ? "page_backward_highlighted" : "page_backward");
                    var pf = T(fwd.Contains(e.mousePosition) ? "page_forward_highlighted" : "page_forward");
                    if (_bookPage > 0 && pb != null) GUI.DrawTexture(back, pb);
                    if (_bookPage < pages - 1 && pf != null) GUI.DrawTexture(fwd, pf);
                    var st = new GUIStyle(GUI.skin.label) { fontSize = 8 * s, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(0, 0, 0, 0) };
                    st.normal.textColor = Color.white;
                    GUI.Label(new Rect(bx + 50 * s, by + 137 * s, 43 * s, 17 * s), (_bookPage + 1) + "/" + pages, st);
                }
                if (hover.HasValue)
                {
                    var hs = Crafting.McItems.Stack(hover.Value.r.result, 1);
                    if (hs != null) Tooltip(Name(hs), e.mousePosition, s);
                }
                else if (filter.Contains(e.mousePosition)) Tooltip(_bookOnlyCraftable ? "Mostrando lo que puedes craftear" : "Mostrando todo", e.mousePosition, s);
            }
            if (e.type == EventType.MouseDown && e.button == 0 && _bookRect.Contains(e.mousePosition))
            {
                if (filter.Contains(e.mousePosition)) { _bookOnlyCraftable = !_bookOnlyCraftable; _bookPage = 0; _bookRefresh = 0f; }
                else if (back.Contains(e.mousePosition) && _bookPage > 0) _bookPage--;
                else if (fwd.Contains(e.mousePosition) && _bookPage < pages - 1) _bookPage++;
                else if (hover.HasValue) Place(hover.Value.r, hover.Value.can);
                Sound.MinecraftSounds.PlayGroup("random/click", _p.position, 8f, 0.4f, 1f);
                e.Use(); return true;
            }
            if (e.type == EventType.ScrollWheel && _bookRect.Contains(e.mousePosition))
            {
                _bookPage = Mathf.Clamp(_bookPage + (e.delta.y > 0 ? 1 : -1), 0, pages - 1);
                e.Use(); return true;
            }
            return false;
        }

        // Minecraft's "place recipe": the grid goes back to the bag, then each ingredient is taken from the bag
        void Place(Crafting.McRecipes.Recipe r, bool can)
        {
            _ghost = null;
            if (!_cursor.IsEmpty()) return;
            for (int i = 0; i < _craft.Length; i++) if (!_craft[i].IsEmpty()) _cursorBackup(i);
            if (!can) { _ghost = r; return; }
            int size = GridSize;
            var cells = new List<(int cell, HashSet<string> set)>();
            if (r.shaped)
            {
                for (int y = 0; y < r.h; y++)
                    for (int x = 0; x < r.w; x++)
                    {
                        var set = r.grid[y * r.w + x];
                        if (set != null && set.Count > 0) cells.Add((y * size + x, set));
                    }
            }
            else for (int i = 0; i < r.ingredients.Count; i++) cells.Add((i, r.ingredients[i]));
            foreach (var (cell, set) in cells)
                if (!TakeOne(set, out var one)) { _ghost = r; return; }
                else _craft[cell] = one;
            _bookRefresh = 0f;
        }

        bool TakeOne(HashSet<string> set, out ItemStack one)
        {
            one = null;
            var spots = new List<Slot>();
            for (int i = 0; i < BagCount; i++) spots.Add(new Slot { kind = Kind.Bag, idx = i });
            for (int i = 0; i < 9; i++) spots.Add(new Slot { kind = Kind.Belt, idx = i });
            foreach (var sl in spots)
            {
                var st = Get(sl);
                string id = Crafting.McRecipes.McId(st);
                if (id == null || !set.Contains(id)) continue;
                one = st.Clone(); one.count = 1;
                var left = st.Clone(); left.count--;
                Set(sl, left);
                return true;
            }
            return false;
        }

        // a recipe you can't make yet: shown faded in the empty grid, like Minecraft's ghost recipe
        void DrawGhost(float px, float py, int s, Event e)
        {
            if (_ghost == null) return;
            bool any = false;
            for (int i = 0; i < _craft.Length; i++) if (!_craft[i].IsEmpty()) any = true;
            if (any || !Fits(_ghost, GridSize)) { _ghost = null; return; }
            if (e.type != EventType.Repaint) return;
            int size = GridSize;
            var cells = new List<(int cell, HashSet<string> set)>();
            if (_ghost.shaped) { for (int y = 0; y < _ghost.h; y++) for (int x = 0; x < _ghost.w; x++) { var set = _ghost.grid[y * _ghost.w + x]; if (set != null && set.Count > 0) cells.Add((y * size + x, set)); } }
            else for (int i = 0; i < _ghost.ingredients.Count; i++) cells.Add((i, _ghost.ingredients[i]));
            foreach (var sl in _slots)
            {
                if (sl.kind != Kind.Craft && sl.kind != Kind.Result) continue;
                var r = new Rect(px + sl.x * s, py + sl.y * s, 16 * s, 16 * s);
                ItemStack st = null;
                if (sl.kind == Kind.Result) st = Crafting.McItems.Stack(_ghost.result, _ghost.count);
                else foreach (var (cell, set) in cells)
                        if (cell == sl.idx) { foreach (var id in set) { st = Crafting.McItems.Stack(id, 1); if (st != null) break; } }
                if (st == null) continue;
                DrawStack(st, r, s);
                Fill(r, new Color(0.55f, 0.2f, 0.2f, 0.55f));
            }
        }
    }
}
