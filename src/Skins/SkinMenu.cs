using System;
using UnityEngine;

namespace SteveDaysToDie.Skins
{
    /// <summary>
    /// The skins screen, laid out like the Minecraft launcher's "Skins" tab: the skin in use on the left
    /// with an "add to library" button, the library as a grid on the right (a click puts the skin on at
    /// once) and a "new skin" tile. A "Skins" button shows in the main menu and in the pause menu, so a
    /// skin can be changed in the middle of a game.
    /// </summary>
    public class SkinMenu : MonoBehaviour
    {
        public static SkinMenu Instance { get; private set; }
        /// <summary>The "Skins" button is on screen (main menu or pause menu open).</summary>
        public static bool ButtonVisible { get; private set; }
        public bool IsOpen => _open;

        static readonly Color Bg = new Color(0.07f, 0.07f, 0.07f, 0.98f);
        static readonly Color Edge = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color Hover = new Color(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color Green = new Color(0.24f, 0.52f, 0.15f, 1f);
        static readonly Color GreenHover = new Color(0.31f, 0.62f, 0.2f, 1f);
        static readonly Color Grey = new Color(0.72f, 0.72f, 0.72f, 1f);

        bool _open, _forced;
        Vector2 _scroll;
        string _msg;
        float _msgUntil;
        float _styleScale;
        GUIStyle _title, _head, _name, _btn, _small, _plus;

        void Awake() { Instance = this; }

        void OnDisable() { if (_open) Close(); }

        void OnApplicationFocus(bool focus)
        {
            if (focus && _open) SkinLibrary.Refresh();      // back from the folder: show what was added
        }

        // ---- when the button is shown ----

        static bool Opened(GUIWindowManager wm, string id)
        {
            try { return !string.IsNullOrEmpty(id) && wm.IsWindowOpen(id); }
            catch { return false; }
        }

        // "main" = the game's own menus before entering a world (title screen and its pages);
        // "pause" = the menu opened with Esc while playing
        static string WhichMenu()
        {
            var ui = LocalPlayerUI.primaryUI;
            var wm = ui != null ? ui.windowManager : null;
            if (wm == null) return null;
            if (Opened(wm, XUiC_LoadingScreen.ID)) return null;
            if (Opened(wm, XUiC_InGameMenuWindow.ID)) return "pause";
            var gm = GameManager.Instance;
            bool inWorld = gm != null && gm.World != null;
            if (Opened(wm, XUiC_MainMenu.ID) || (!inWorld && gm != null)) return "main";
            return null;
        }

        string _lastMenu = "-";

        void Update()
        {
            string which = WhichMenu();
            if (which != _lastMenu)
            {
                _lastMenu = which;
                Debug.Log("[SVD] Skins button " + (which == null ? "hidden" : "shown (" + which + " menu)"));
            }
            bool menu = which != null;
            if (_pickRequested)
            {
                _pickRequested = false;
                AddFromFile();
            }
            ButtonVisible = menu && !_open;
            if (_open && !_forced && !menu) Close();               // the menu behind was closed
            if (_open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        // ---- open and close ----

        public void Open()
        {
            SkinLibrary.Refresh();
            _scroll = Vector2.zero;
            _open = true;
            UICamera.ignoreAllEvents = true;     // clicks must not reach the game's menu under this screen
        }

        public void Close()
        {
            _open = false;
            _forced = false;
            UICamera.ignoreAllEvents = false;
        }

        /// <summary>Automatic test: open the screen even though no game menu is open.</summary>
        public void OpenForTest() { _forced = true; Open(); }

        void Say(string text) { _msg = text; _msgUntil = Time.realtimeSinceStartup + 3.5f; }

        bool _pickRequested;

        // "Nuevo aspecto": Windows' window to choose the picture; it goes into the library and is put on
        void AddFromFile()
        {
            string path = FilePicker.ChoosePng("Elige tu skin de Minecraft (PNG)");
            if (path == null) return;
            string file = SkinLibrary.ImportFile(path, out string error);
            if (file == null) { Say(error); return; }
            SkinLibrary.Refresh();
            var e = SkinLibrary.Entries.Find(x => x.Key == file);
            if (e != null && SkinLibrary.Apply(e)) Say("Agregada y puesta: " + e.Name + (Steve.SteveParts.IsSlim(e.Texture) ? " (slim)" : ""));
            else Say("Se agregó " + file + ", pero no se pudo poner");
        }

        // ---- drawing ----

        void OnGUI()
        {
            GUI.depth = -1000;
            float S = Mathf.Clamp(Screen.height / 720f, 0.85f, 2f);
            EnsureStyles(S);

            if (ButtonVisible)
            {
                var r = new Rect(Screen.width - 170 * S - 16, 56 * S, 170 * S, 38 * S);
                if (Button(r, "Skins", Green, GreenHover)) Open();
            }
            if (!_open) return;

            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));
            float pw = Mathf.Min(Screen.width - 40, 1100f * S), ph = Mathf.Min(Screen.height - 40, 680f * S);
            var panel = new Rect((Screen.width - pw) / 2f, (Screen.height - ph) / 2f, pw, ph);
            Fill(new Rect(panel.x - 2, panel.y - 2, pw + 4, ph + 4), Edge);
            Fill(panel, Bg);

            GUI.Label(new Rect(panel.x, panel.y + 12 * S, pw, 34 * S), "Aspectos", _title);
            if (Button(new Rect(panel.xMax - 140 * S, panel.y + 12 * S, 124 * S, 32 * S), "Cerrar", Edge, Hover)) { Close(); return; }

            float leftW = pw * 0.30f;
            DrawCurrent(new Rect(panel.x, panel.y + 56 * S, leftW, ph - 68 * S), S);
            Fill(new Rect(panel.x + leftW, panel.y + 70 * S, 2, ph - 100 * S), Edge);
            DrawLibrary(new Rect(panel.x + leftW + 10 * S, panel.y + 56 * S, pw - leftW - 22 * S, ph - 68 * S), S);
        }

        void DrawCurrent(Rect col, float S)
        {
            GUI.Label(new Rect(col.x, col.y, col.width, 30 * S), "Actual", _head);
            var tex = SteveCharacter.SkinTexture;
            if (tex != null)
            {
                float px = Mathf.Min(col.width * 0.72f / 16f, (col.height - 210 * S) / 32f);
                DrawFront(tex, col.x + (col.width - 16 * px) / 2f, col.y + 44 * S, px);
            }
            var add = new Rect(col.x + 20 * S, col.yMax - 128 * S, col.width - 40 * S, 44 * S);
            if (Button(add, "Agregar a la biblioteca", Green, GreenHover))
            {
                string file = SkinLibrary.SaveCurrent();
                SkinLibrary.Refresh();
                Say(file != null ? "Guardada como " + file : "No se pudo guardar la skin");
            }
            GUI.Label(new Rect(col.x, add.yMax + 6 * S, col.width, 24 * S), Trim(SteveCharacter.SkinName, 28), _small);
            if (_msg != null && Time.realtimeSinceStartup < _msgUntil)
                GUI.Label(new Rect(col.x + 8 * S, col.yMax - 40 * S, col.width - 16 * S, 36 * S), _msg, _small);
        }

        void DrawLibrary(Rect area, float S)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 30 * S), "Biblioteca", _head);
            var view = new Rect(area.x, area.y + 40 * S, area.width, area.height - 40 * S);
            int cols = Mathf.Max(2, Mathf.FloorToInt(view.width / (170 * S)));
            float tileW = (view.width - 18) / cols, tileH = 214 * S;
            int count = SkinLibrary.Entries.Count + 1;
            int rows = (count + cols - 1) / cols;

            _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0, 0, view.width - 18, rows * tileH));
            for (int i = 0; i < count; i++)
            {
                var r = new Rect((i % cols) * tileW, (i / cols) * tileH, tileW, tileH);
                bool over = r.Contains(Event.current.mousePosition);
                if (over) Fill(new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8), Hover);

                if (i == 0)
                {
                    float ring = 62 * S;
                    var rr = new Rect(r.x + (r.width - ring) / 2f, r.y + 56 * S, ring, ring);
                    Fill(rr, Edge);
                    Fill(new Rect(rr.x + 3, rr.y + 3, rr.width - 6, rr.height - 6), over ? Hover : Bg);
                    GUI.Label(rr, "+", _plus);
                    GUI.Label(new Rect(r.x, rr.yMax + 12 * S, r.width, 26 * S), "Nuevo aspecto", _name);
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _pickRequested = true;   // opened next frame, outside the GUI pass
                    continue;
                }

                var e = SkinLibrary.Entries[i - 1];
                var tex = SkinLibrary.TextureOf(e);
                bool current = tex != null && (ReferenceEquals(tex, SteveCharacter.SkinTexture) || e.Name == SteveCharacter.SkinName);
                if (current)
                {
                    Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Green);
                    Fill(new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10), over ? Hover : Bg);
                }
                GUI.Label(new Rect(r.x, r.y + 12 * S, r.width, 26 * S), Trim(e.Name, 18), _name);
                if (tex == null)
                    GUI.Label(new Rect(r.x, r.y + 80 * S, r.width, 40 * S), "no es una skin válida", _small);
                else
                {
                    float px = (tileH - 64 * S) / 32f;
                    DrawFront(tex, r.x + (r.width - 16 * px) / 2f, r.y + 46 * S, px);
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && tex != null)
                {
                    SkinLibrary.Apply(e);
                    Say("Usando: " + e.Name);
                }
            }
            GUI.EndScrollView();
        }

        // ---- a skin seen from the front (head, body, arms, legs: the base layer, as Steve is built) ----

        static void Part(Texture2D t, Rect r, float u, float v, float w, float h, bool flip = false)
        {
            float tw = t.width, th = t.height;
            var uv = flip ? new Rect((u + w) / tw, 1f - (v + h) / th, -w / tw, h / th)
                          : new Rect(u / tw, 1f - (v + h) / th, w / tw, h / th);
            GUI.DrawTextureWithTexCoords(r, t, uv);
        }

        static readonly System.Collections.Generic.Dictionary<Texture2D, bool> _slimCache = new System.Collections.Generic.Dictionary<Texture2D, bool>();

        static bool SlimOf(Texture2D t)
        {
            if (!_slimCache.TryGetValue(t, out bool s)) { s = Steve.SteveParts.IsSlim(t); _slimCache[t] = s; }
            return s;
        }

        /// <summary>Front view of a skin: both layers, and 3-pixel arms for slim skins.</summary>
        public static void DrawFront(Texture2D t, float x, float y, float px)
        {
            bool old = t.height < 64;
            float aw = SlimOf(t) ? 3 : 4;                   // arm width in skin pixels
            float rx = x + (4 - aw) * px;                   // right arm (viewer's left) touches the body
            float lx = x + 12 * px;
            float o = 0.5f * px;                            // the hat sits a little outside the head

            Part(t, new Rect(x + 4 * px, y, 8 * px, 8 * px), 8, 8, 8, 8);                       // head
            Part(t, new Rect(x + 4 * px, y + 8 * px, 8 * px, 12 * px), 20, 20, 8, 12);          // body
            Part(t, new Rect(rx, y + 8 * px, aw * px, 12 * px), 44, 20, aw, 12);                // right arm
            if (old) Part(t, new Rect(lx, y + 8 * px, aw * px, 12 * px), 44, 20, aw, 12, true);
            else Part(t, new Rect(lx, y + 8 * px, aw * px, 12 * px), 36, 52, aw, 12);           // left arm
            Part(t, new Rect(x + 4 * px, y + 20 * px, 4 * px, 12 * px), 4, 20, 4, 12);          // right leg
            if (old) Part(t, new Rect(x + 8 * px, y + 20 * px, 4 * px, 12 * px), 4, 20, 4, 12, true);
            else Part(t, new Rect(x + 8 * px, y + 20 * px, 4 * px, 12 * px), 20, 52, 4, 12);    // left leg

            // second layer
            Part(t, new Rect(x + 4 * px - o, y - o, 8 * px + 2 * o, 8 * px + 2 * o), 40, 8, 8, 8);   // hat
            if (old) return;
            Part(t, new Rect(x + 4 * px, y + 8 * px, 8 * px, 12 * px), 20, 36, 8, 12);          // jacket
            Part(t, new Rect(rx, y + 8 * px, aw * px, 12 * px), 44, 36, aw, 12);                // right sleeve
            Part(t, new Rect(lx, y + 8 * px, aw * px, 12 * px), 52, 52, aw, 12);                // left sleeve
            Part(t, new Rect(x + 4 * px, y + 20 * px, 4 * px, 12 * px), 4, 36, 4, 12);          // right trouser leg
            Part(t, new Rect(x + 8 * px, y + 20 * px, 4 * px, 12 * px), 4, 52, 4, 12);          // left trouser leg
        }

        // ---- small helpers ----

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        bool Button(Rect r, string text, Color bg, Color hover)
        {
            Fill(r, r.Contains(Event.current.mousePosition) ? hover : bg);
            GUI.Label(r, text, _btn);
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        static string Trim(string s, int max) => s == null ? "" : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");

        GUIStyle Style(int size, TextAnchor anchor, Color color, bool bold)
        {
            var st = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(size * _styleScale),
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = true,
                clipping = TextClipping.Clip,
            };
            st.normal.textColor = color;
            return st;
        }

        void EnsureStyles(float S)
        {
            if (_title != null && Mathf.Approximately(_styleScale, S)) return;
            _styleScale = S;
            _title = Style(22, TextAnchor.MiddleCenter, Color.white, true);
            _head = Style(19, TextAnchor.MiddleCenter, Color.white, true);
            _name = Style(15, TextAnchor.MiddleCenter, Color.white, true);
            _btn = Style(16, TextAnchor.MiddleCenter, Color.white, true);
            _small = Style(13, TextAnchor.MiddleCenter, Grey, false);
            _plus = Style(44, TextAnchor.MiddleCenter, Color.white, false);
        }
    }
}
