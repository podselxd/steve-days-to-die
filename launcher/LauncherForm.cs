using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteveVsZombiesLauncher
{
    /// <summary>
    /// The launcher window, in the spirit of the Minecraft launcher: tabs (Inicio, Skin, Efectos,
    /// Teclas) and one big green JUGAR button that updates the mod from GitHub and starts the game.
    /// Everything chosen here goes to ajustes.cfg, which the mod reads when the game starts.
    /// </summary>
    class LauncherForm : Form
    {
        internal static readonly Color Bg = Color.FromArgb(32, 32, 32);
        internal static readonly Color Side = Color.FromArgb(20, 20, 20);
        internal static readonly Color Bar = Color.FromArgb(14, 14, 14);
        internal static readonly Color Card = Color.FromArgb(44, 44, 44);
        internal static readonly Color Green = Color.FromArgb(60, 133, 39);
        internal static readonly Color GreenHover = Color.FromArgb(76, 160, 50);
        internal static readonly Color Grey = Color.FromArgb(170, 170, 170);
        internal static readonly Color Ok = Color.FromArgb(108, 196, 80);
        internal static readonly Color Bad = Color.FromArgb(230, 90, 80);

        string _game, _mcJar, _skinName;
        Version _installed, _embedded;
        Updater.Release _online;
        bool _onlineChecked, _busy;
        Bitmap _skin;
        double _progress = -1;

        readonly Panel _side = new Panel(), _bottom = new Panel(), _content = new Panel(), _tabs = new Panel();
        readonly Button _play = new Button();
        readonly Label _status = new Label();
        readonly Dictionary<string, Panel> _pages = new Dictionary<string, Panel>();
        readonly List<Label> _tabLabels = new List<Label>();
        string _page = "Inicio";

        // pages
        readonly Panel _home = new Panel();
        readonly Skin3D _homeSkin = new Skin3D(), _bigSkin = new Skin3D();
        readonly FlowLayoutPanel _skinList = new FlowLayoutPanel();
        readonly Label _skinTitle = new Label();

        public LauncherForm()
        {
            Text = "Steve Days to Die";
            ClientSize = new Size(1060, 650);
            // can be resized, maximized and minimized; F11 = full screen (no borders), F11 or Esc again = back
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true; MinimizeBox = true;
            KeyPreview = true;
            KeyDown += (s, e) =>
            {
                if ((ActiveControl as Button)?.Tag as string == "wait") return;     // the Teclas tab is waiting for a key
                if (e.KeyCode == Keys.F11 || (e.KeyCode == Keys.Escape && FormBorderStyle == FormBorderStyle.None))
                {
                    e.Handled = true;
                    bool full = FormBorderStyle != FormBorderStyle.None;
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = full ? FormBorderStyle.None : FormBorderStyle.Sizable;
                    WindowState = full ? FormWindowState.Maximized : FormWindowState.Normal;
                }
            };
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.None;      // scaled by hand with S (see the end of the constructor)
            DoubleBuffered = true;

            _side.Dock = DockStyle.Left; _side.Width = 240; _side.BackColor = Side;
            _bottom.Dock = DockStyle.Bottom; _bottom.Height = 112; _bottom.BackColor = Bar;
            _content.Dock = DockStyle.Fill; _content.BackColor = Bg;
            _tabs.Dock = DockStyle.Top; _tabs.Height = 52; _tabs.BackColor = Color.FromArgb(26, 26, 26);
            foreach (var p in new[] { _side, _bottom, _content, _home, _tabs }) Buffered(p);
            Controls.Add(_content); Controls.Add(_bottom); Controls.Add(_side);

            _side.Paint += PaintSide;
            _bottom.Paint += PaintBottom;
            _bottom.Resize += (s, e) => LayoutBottom();
            _tabs.Paint += (s, e) =>
            {
                var l = _tabLabels.FirstOrDefault(t => t.Text == _page);
                if (l != null) using (var b = new SolidBrush(Ok)) e.Graphics.FillRectangle(b, l.Left, _tabs.Height - 3, l.Width, 3);
            };

            Style(_play, "JUGAR", 22f);
            _play.Size = new Size(320, 62);
            _play.Click += async (s, e) => await Play();
            _bottom.Controls.Add(_play);

            _status.AutoSize = false; _status.TextAlign = ContentAlignment.MiddleCenter; _status.ForeColor = Grey;
            _status.BackColor = Bar; _status.Font = new Font("Segoe UI", 9.5f);
            _bottom.Controls.Add(_status);

            AddLink("Carpeta de skins", 0, () => { Directory.CreateDirectory(SkinView.SkinsDir); Process.Start("explorer.exe", "\"" + SkinView.SkinsDir + "\""); });
            AddLink("Página del mod en GitHub", 1, () => Process.Start(Updater.Page));
            AddLink("Quitar el mod del juego", 2, Uninstall);

            // the old toggle key was the Spanish keyboard's Ñ, which also switches 7 Days to third person
            try { if (LauncherSettings.Get("tecla_alternar", "Alpha0").Equals("BackQuote", StringComparison.OrdinalIgnoreCase)) LauncherSettings.Set("tecla_alternar", "Alpha0"); } catch { }
            try { if (LauncherSettings.Get("tecla_vista_frontal", "F5").Equals("Insert", StringComparison.OrdinalIgnoreCase)) LauncherSettings.Set("tecla_vista_frontal", "F5"); } catch { }

            BuildHome();
            BuildSkinPage();
            BuildEffectsPage();
            BuildKeysPage();
            int x = 28;
            foreach (var name in new[] { "Inicio", "Skin", "Efectos", "Teclas" })
            {
                var l = new Label { Text = name, AutoSize = true, Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = Grey, BackColor = _tabs.BackColor, Cursor = Cursors.Hand, Location = new Point(x, 14) };
                l.Click += (s, e) => ShowPage(name);
                _tabs.Controls.Add(l);
                _tabLabels.Add(l);
                x += (int)(TextRenderer.MeasureText(name, l.Font).Width / S) + 30;
            }
            _content.Controls.Add(_tabs);
            ShowPage("Inicio");

            // everything above is laid out at 100%: grow it to the screen's scaling (fonts already are)
            if (Math.Abs(S - 1f) > 0.01f) Scale(new SizeF(S, S));
            MinimumSize = new Size(D(940), D(600));
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            if (Height > area.Height || Width > area.Width) WindowState = FormWindowState.Maximized;

            Load += async (s, e) => { Refresh(false); await CheckOnline(); };
        }

        /// <summary>Windows' display scaling (1 = 100%, 1.5 = 150%); layout numbers are written at 100% and go through D().</summary>
        internal static readonly float S = ScreenScale();
        internal static int D(float v) => (int)Math.Round(v * S);

        static float ScreenScale()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1f, g.DpiX / 96f); }
            catch { return 1f; }
        }

        static readonly Bitmap Banner = Resource<Bitmap>("logo_banner.png", s => new Bitmap(s));
        static readonly Icon Logo = Resource<Icon>("logo.ico", s => new Icon(s));
        static readonly Bitmap HomeBg = Resource<Bitmap>("home_bg.jpg", s => new Bitmap(s));

        static T Resource<T>(string name, Func<Stream, T> make) where T : class
        {
            try { using (var s = typeof(LauncherForm).Assembly.GetManifestResourceStream(name)) return s != null ? make(s) : null; }
            catch { return null; }
        }

        static void Buffered(Control p) =>
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(p, true, null);

        Panel Page(string name)
        {
            var p = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Visible = false, AutoScroll = true };
            Buffered(p);
            _pages[name] = p;
            _content.Controls.Add(p);
            return p;
        }

        void ShowPage(string name)
        {
            _page = name;
            foreach (var kv in _pages) kv.Value.Visible = kv.Key == name;
            _pages[name].BringToFront();
            foreach (var l in _tabLabels) l.ForeColor = l.Text == name ? Color.White : Grey;
            _tabs.Invalidate();
            if (name == "Skin") FillSkinList();
        }

        // ---- look ----

        internal static void Style(Button b, string text, float size)
        {
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Green;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", size, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.MouseEnter += (s, e) => { if (b.Enabled) b.BackColor = GreenHover; };
            b.MouseLeave += (s, e) => b.BackColor = b.Enabled ? Green : Color.FromArgb(70, 70, 70);
            b.EnabledChanged += (s, e) => b.BackColor = b.Enabled ? Green : Color.FromArgb(70, 70, 70);
        }

        static Button Plain(string text)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Card, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10f), Height = 36 };
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
            return b;
        }

        void AddLink(string text, int row, Action act)
        {
            var l = new LinkLabel
            {
                Text = text, AutoSize = true, LinkColor = Grey, ActiveLinkColor = Color.White, VisitedLinkColor = Grey,
                LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Side, Font = new Font("Segoe UI", 9.5f),
            };
            l.LinkClicked += (s, e) => { try { act(); } catch (Exception ex) { Status(ex.Message, Bad); } };
            _side.Controls.Add(l);
            _side.Resize += (s, e) => l.Location = new Point(D(24), _side.Height - D(92 - row * 26));
            l.Location = new Point(24, ClientSize.Height - 92 + row * 26);
        }

        void LayoutBottom()
        {
            _play.Location = new Point((_bottom.Width - _play.Width) / 2, D(38));
            _status.SetBounds(0, D(6), _bottom.Width, D(26));
        }

        // for painting inside g.ScaleTransform(S, S): the font in 100%-pixels, so the transform alone sizes it
        internal static void Draw(Graphics g, string s, float size, FontStyle st, Color c, float x, float y)
        {
            using (var f = new Font("Segoe UI", size * 96f / 72f, st, GraphicsUnit.Pixel))
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        static Label Words(string s, float size, FontStyle st, Color c, int x, int y, int w = 0)
        {
            var l = new Label { Text = s, Font = new Font("Segoe UI", size, st), ForeColor = c, BackColor = Color.Transparent, Location = new Point(x, y) };
            if (w > 0) { l.AutoSize = false; l.Size = new Size(w, (int)(TextRenderer.MeasureText(s, l.Font, new Size(D(w), 999), TextFormatFlags.WordBreak).Height / S) + 4); }
            else l.AutoSize = true;
            return l;
        }

        void PaintSide(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(S, S);
            if (Banner != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Banner, new Rectangle(10, 16, 220, 220 * Banner.Height / Banner.Width));
            }
            else
            {
                Draw(g, "STEVE DAYS", 22, FontStyle.Bold, Color.White, 20, 22);
                Draw(g, "TO DIE", 16, FontStyle.Bold, Bad, 23, 62);
            }
            Draw(g, "7 Days to Die + Minecraft", 9, FontStyle.Regular, Grey, 24, 108);

            float y = 150;
            void Line(string label, string value, Color c)
            {
                Draw(g, label, 9, FontStyle.Regular, Grey, 24, y);
                Draw(g, value, 10.5f, FontStyle.Bold, c, 24, y + 17);
                y += 54;
            }
            Line("7 Days to Die", _game != null ? "Encontrado" : "No encontrado", _game != null ? Ok : Bad);
            Line("Minecraft: Java Edition", _mcJar != null ? "Encontrado" : "No encontrado", _mcJar != null ? Ok : Bad);
            Line("Mod instalado", _installed != null ? "v" + _installed : "Todavía no", _installed != null ? Color.White : Grey);
            string online = !_onlineChecked ? "Buscando…" : _online != null ? "v" + _online.Version : "Sin conexión";
            Line("Versión más nueva", online, Color.White);
        }

        // ---- Inicio ----

        void BuildHome()
        {
            _home.Dock = DockStyle.Fill; _home.BackColor = Bg; _home.Visible = false;
            _pages["Inicio"] = _home;
            _content.Controls.Add(_home);
            _home.Paint += PaintHome;
            _homeSkin.Size = new Size(220, 330);
            _home.Controls.Add(_homeSkin);
            _home.Resize += (s, e) => _homeSkin.Location = new Point(_home.Width - D(250), D(24));
            var b = new Button(); Style(b, "Cambiar skin…", 10.5f); b.Size = new Size(180, 38);
            b.Click += (s, e) => ShowPage("Skin");
            _home.Controls.Add(b);
            _home.Resize += (s, e) => b.Location = new Point(_home.Width - D(230), _home.Height - D(56));
        }

        void PaintHome(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.ScaleTransform(S, S);
            int w = (int)Math.Ceiling(_home.Width / S), h = (int)Math.Ceiling(_home.Height / S);
            if (HomeBg != null)
            {
                // the picture fills the page (cropped, never stretched), darker on the left where the text is
                float k = Math.Max(w / (float)HomeBg.Width, h / (float)HomeBg.Height);
                float bw = HomeBg.Width * k, bh = HomeBg.Height * k;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(HomeBg, (w - bw) / 2f, (h - bh) / 2f, bw, bh);
            }
            else
            {
                using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h + 1), Color.FromArgb(38, 48, 36), Bg, 90f)) g.FillRectangle(bg, 0, 0, w, h);
                using (var dot = new SolidBrush(Color.FromArgb(10, 255, 255, 255)))       // a faint pixel grid
                    for (int yy = 0; yy < h; yy += 16)
                        for (int xx = ((yy / 16) % 2) * 8; xx < w; xx += 16) g.FillRectangle(dot, xx, yy, 8, 8);
            }

            Draw(g, _skinName ?? "", 10, FontStyle.Bold, Grey, w - 230, h - 82);
        }

        // ---- Skin ----

        void BuildSkinPage()
        {
            var p = Page("Skin");
            p.AutoScroll = false;
            _bigSkin.SetBounds(30, 14, 300, 360);
            p.Controls.Add(_bigSkin);
            _skinTitle.SetBounds(30, 398, 300, 24); _skinTitle.TextAlign = ContentAlignment.MiddleCenter;
            _skinTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold); _skinTitle.ForeColor = Color.White;
            p.Controls.Add(_skinTitle);
            var hint = Words("Arrastra para girarlo", 9, FontStyle.Italic, Grey, 30, 422, 300); hint.TextAlign = ContentAlignment.TopCenter;
            p.Controls.Add(hint);

            var biblio = Words("Biblioteca", 14, FontStyle.Bold, Color.White, 360, 18);
            p.Controls.Add(biblio);
            var add = new Button(); Style(add, "+  Nuevo aspecto…", 10.5f); add.SetBounds(360, 56, 190, 38);
            add.Click += (s, e) => ChangeSkin();
            p.Controls.Add(add);
            var folder = Plain("Abrir carpeta"); folder.SetBounds(560, 56, 140, 38);
            folder.Click += (s, e) => { Directory.CreateDirectory(SkinView.SkinsDir); Process.Start("explorer.exe", "\"" + SkinView.SkinsDir + "\""); };
            p.Controls.Add(folder);

            _skinList.SetBounds(360, 108, 420, 340);
            _skinList.AutoScroll = true; _skinList.BackColor = Bg;
            p.Controls.Add(_skinList);
            p.Resize += (s, e) =>
            {
                // the 3D Steve grows with the window (full screen shows him big)
                int ph = (int)(p.Height / S), pw = (int)(p.Width / S);         // in 100% units
                int h = Math.Max(260, ph - 30), w = Math.Min(h * 3 / 4, Math.Max(260, pw / 2 - 60));
                _bigSkin.SetBounds(D(30), D(10), D(w), D(h - 50));
                _skinTitle.SetBounds(D(30), D(h - 38), D(w), D(24));
                hint.SetBounds(D(30), D(h - 14), D(w), D(20));
                int lx = w + 60;
                biblio.Left = D(lx); add.Left = D(lx); folder.Left = D(lx + 200);
                _skinList.SetBounds(D(lx), D(108), D(Math.Max(200, pw - lx - 20)), D(Math.Max(120, ph - 118)));
            };
        }

        void FillSkinList()
        {
            _skinList.SuspendLayout();
            var old = _skinList.Controls.Cast<Control>().ToList();
            _skinList.Controls.Clear();
            foreach (var c in old) c.Dispose();
            string current = SkinView.CurrentSetting();
            var entries = new List<(string setting, string name, Bitmap pic)> { ("steve", "Steve", SkinView.SteveSkin()) };
            var own = SkinView.Load(Path.Combine(GameSetup.ModDataDir, "skin.png"));
            if (own != null) entries.Add(("skin.png", "skin.png", own));
            if (Directory.Exists(SkinView.SkinsDir))
                foreach (var f in Directory.GetFiles(SkinView.SkinsDir, "*.png").OrderBy(f => f))
                {
                    var b = SkinView.Load(f);
                    if (b != null) entries.Add((Path.GetFileName(f), Path.GetFileNameWithoutExtension(f), b));
                }
            foreach (var en in entries)
            {
                bool sel = string.Equals(en.setting, current, StringComparison.OrdinalIgnoreCase)
                        || (current.Equals("auto", StringComparison.OrdinalIgnoreCase) && en.name == _skinName);
                var tile = new Panel { Size = new Size(D(118), D(168)), Margin = new Padding(0, 0, D(12), D(12)), BackColor = sel ? Color.FromArgb(52, 70, 46) : Card, Cursor = Cursors.Hand };
                Buffered(tile);
                var pic = en.pic; string setting = en.setting, name = en.name;
                tile.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.ScaleTransform(S, S);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    if (pic != null) SkinView.DrawFront(g, pic, (118 - 16 * 4.2f) / 2f, 8, 4.2f);
                    using (var f = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                    using (var br = new SolidBrush(Color.White))
                        g.DrawString(name, f, br, new RectangleF(4, 146, 110, 18), sf);
                    if (sel) using (var pen = new Pen(Ok, 2)) g.DrawRectangle(pen, 1, 1, 115, 165);
                };
                tile.Click += (s, e) =>
                {
                    SkinView.SetSetting(setting);
                    Refresh(true);
                    Status("Skin: " + name + ". La verás al entrar al juego.", Ok);
                    BeginInvoke(new Action(FillSkinList));
                };
                tile.Disposed += (s, e) => pic?.Dispose();
                _skinList.Controls.Add(tile);
            }
            _skinList.ResumeLayout();
        }

        // ---- Efectos ----

        void BuildEffectsPage()
        {
            var p = Page("Efectos");
            int y = 18;
            p.Controls.Add(Words("Efectos del jugador", 16, FontStyle.Bold, Color.White, 30, y)); y += 40;
            p.Controls.Add(Words("Por defecto juegas como en Minecraft. Enciende los efectos de 7 Days si quieres una experiencia más 7 Days. Se aplica la próxima vez que pulses JUGAR.", 9.5f, FontStyle.Regular, Grey, 32, y, 700)); y += 42;

            Switch Row(LauncherSettings.Toggle t, int x, int yy)
            {
                var sw = new Switch { Location = new Point(x, yy), Width = 375, Label = t.Label, Hint = t.Hint, Checked = LauncherSettings.GetBool(t.Key, t.Default) };
                sw.CheckedChanged += (s, e) => LauncherSettings.SetBool(t.Key, sw.Checked);
                p.Controls.Add(sw);
                return sw;
            }

            p.Controls.Add(Words("Reglas de Minecraft", 11, FontStyle.Bold, Ok, 30, y));
            p.Controls.Add(Words("Efectos de 7 Days", 11, FontStyle.Bold, Color.FromArgb(220, 160, 80), 418, y));
            y += 30;
            var rules = LauncherSettings.Rules; var fx = LauncherSettings.Effects();
            var ruleRows = new List<Switch>(); var fxRows = new List<(Switch sw, LauncherSettings.Toggle t)>();
            for (int i = 0; i < Math.Max(rules.Length, fx.Length); i++)
            {
                if (i < rules.Length) ruleRows.Add(Row(rules[i], 30, y + i * 49));
                if (i < fx.Length) fxRows.Add((Row(fx[i], 418, y + i * 49), fx[i]));
            }
            y = 16;     // the three quick buttons sit at the top, next to the title
            var mc = Plain("Todo como Minecraft"); mc.SetBounds(30, y, 190, 36);
            mc.Left = 300;
            mc.Click += (s, e) => { foreach (var r in ruleRows) r.Checked = true; foreach (var r in fxRows) r.sw.Checked = false; Status("Todo como en Minecraft.", Ok); };
            var sd = Plain("Más 7 Days"); sd.SetBounds(500, y, 130, 36);
            sd.Click += (s, e) => { foreach (var r in fxRows) r.sw.Checked = true; Status("Efectos de 7 Days encendidos.", Ok); };
            var def = Plain("Por defecto"); def.SetBounds(640, y, 130, 36);
            def.Click += (s, e) =>
            {
                for (int i = 0; i < ruleRows.Count; i++) ruleRows[i].Checked = rules[i].Default;
                foreach (var r in fxRows) r.sw.Checked = r.t.Default;
                Status("Efectos por defecto.", Ok);
            };
            p.Controls.Add(mc); p.Controls.Add(sd); p.Controls.Add(def);
        }

        // ---- Teclas ----

        void BuildKeysPage()
        {
            var p = Page("Teclas");
            int y = 18;
            p.Controls.Add(Words("Teclas", 16, FontStyle.Bold, Color.White, 30, y)); y += 40;
            p.Controls.Add(Words("Haz clic en una tecla y pulsa la nueva (Esc cancela). Las demás teclas (moverse, saltar, inventario…) se cambian en las opciones de 7 Days.", 9.5f, FontStyle.Regular, Grey, 32, y, 700)); y += 50;

            var buttons = new List<(Button b, LauncherSettings.KeyBind k)>();
            foreach (var k in LauncherSettings.Keys)
            {
                var kb = k;
                p.Controls.Add(Words(kb.Label, 11, FontStyle.Regular, Color.White, 30, y + 8));
                var b = Plain(LauncherSettings.Pretty(LauncherSettings.Get(kb.Key, kb.Default)));
                b.SetBounds(400, y, 170, 40);
                b.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                string Shown() => LauncherSettings.Pretty(LauncherSettings.Get(kb.Key, kb.Default));
                b.Click += (s, e) => { b.Text = "Pulsa una tecla…"; b.Tag = "wait"; b.Focus(); };
                b.PreviewKeyDown += (s, e) => { if (b.Tag as string == "wait") e.IsInputKey = true; };
                b.KeyDown += (s, e) =>
                {
                    if (b.Tag as string != "wait") return;
                    e.Handled = e.SuppressKeyPress = true;
                    b.Tag = null;
                    if (e.KeyCode == Keys.Escape) { b.Text = Shown(); return; }
                    string name = LauncherSettings.UnityName(e.KeyCode);
                    if (name == null) { b.Text = Shown(); Status("Esa tecla no se puede usar.", Bad); return; }
                    LauncherSettings.Set(kb.Key, name);
                    b.Text = LauncherSettings.Pretty(name);
                    if (LauncherSettings.MayClash(kb.Key, name)) Status("Ojo: " + LauncherSettings.Pretty(name) + " ya hace algo en 7 Days.", Bad);
                    else Status(kb.Label + ": " + LauncherSettings.Pretty(name), Ok);
                };
                b.LostFocus += (s, e) => { if (b.Tag as string == "wait") { b.Tag = null; b.Text = Shown(); } };
                p.Controls.Add(b);
                buttons.Add((b, kb));
                y += 56;
            }
            // the size of the Minecraft bar, hearts and inventory in the game
            y += 14;
            p.Controls.Add(Words("Tamaño de la interfaz (barra, corazones, inventario)", 11, FontStyle.Regular, Color.White, 30, y + 8));
            var sizes = new[] { ("chico", "Chico"), ("mediano", "Mediano"), ("grande", "Grande") };
            var sizeButtons = new List<Button>();
            string cur = LauncherSettings.Get("tamano_hud", "chico").ToLowerInvariant();
            for (int i = 0; i < sizes.Length; i++)
            {
                var (key, label) = sizes[i];
                var sb = Plain(label);
                sb.SetBounds(400 + i * 115, y, 108, 40);
                sb.Tag = key;
                sizeButtons.Add(sb);
                sb.Click += (s2, e2) =>
                {
                    LauncherSettings.Set("tamano_hud", key);
                    foreach (var b2 in sizeButtons) b2.BackColor = (string)b2.Tag == key ? Green : Card;
                    Status("Tamaño de la interfaz: " + label, Ok);
                };
                sb.BackColor = key == cur ? Green : Card;
                p.Controls.Add(sb);
            }
            y += 56;

            var reset = Plain("Restablecer teclas"); reset.SetBounds(30, y + 10, 180, 36);
            reset.Click += (s, e) =>
            {
                foreach (var (b, k) in buttons) { LauncherSettings.Set(k.Key, k.Default); b.Text = LauncherSettings.Pretty(k.Default); }
                Status("Teclas por defecto.", Ok);
            };
            p.Controls.Add(reset);
        }

        void PaintBottom(object sender, PaintEventArgs e)
        {
            if (_progress < 0) return;
            var g = e.Graphics;
            int bw = D(420), x = (_bottom.Width - bw) / 2, y = D(31);
            using (var b = new SolidBrush(Color.FromArgb(50, 50, 50))) g.FillRectangle(b, x, y, bw, D(5));
            using (var b = new SolidBrush(Ok)) g.FillRectangle(b, x, y, (int)(bw * _progress), D(5));
        }

        // ---- state ----

        void Status(string text, Color? c = null)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Status(text, c))); return; }
            _status.Text = text;
            _status.ForeColor = c ?? Grey;
        }

        void Progress(double p)
        {
            if (InvokeRequired) { BeginInvoke(new Action(() => Progress(p))); return; }
            _progress = p;
            _bottom.Invalidate();
        }

        void Refresh(bool keepStatus)
        {
            _game = GameSetup.FindGame();
            _mcJar = GameSetup.MinecraftJar();
            _installed = _game != null ? GameSetup.InstalledVersion(_game) : null;
            try { using (var s = GameSetup.EmbeddedPayload()) _embedded = s != null ? GameSetup.VersionInZip(s) : null; } catch { _embedded = null; }
            var oldSkin = _skin;
            _skin = SkinView.Current(out _skinName);
            _homeSkin.Skin = _skin; _bigSkin.Skin = _skin;
            oldSkin?.Dispose();
            _skinTitle.Text = _skinName ?? "";
            if (Logo != null) Icon = Logo;
            else if (_skin != null)
                try { using (var head = new Bitmap(16, 16)) { using (var g = Graphics.FromImage(head)) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(_skin, new Rectangle(0, 0, 16, 16), new Rectangle(8, 8, 8, 8), GraphicsUnit.Pixel); g.DrawImage(_skin, new Rectangle(0, 0, 16, 16), new Rectangle(40, 8, 8, 8), GraphicsUnit.Pixel); } Icon = Icon.FromHandle(head.GetHicon()); } }
                catch { }

            if (_game == null) { _play.Text = "BUSCAR 7 DAYS"; _play.Enabled = true; }
            else { _play.Text = "JUGAR"; _play.Enabled = _mcJar != null && !_busy; }
            if (!keepStatus)
            {
                if (_game == null) Status("No encontré 7 Days to Die en tu Steam. Pulsa el botón para elegir su carpeta.", Bad);
                else if (_mcJar == null) Status("Necesitas Minecraft: Java Edition instalado y abierto una vez desde su launcher.", Bad);
                else Status("Listo para jugar.");
            }
            _side.Invalidate(); _home.Invalidate();
            LayoutBottom();
        }

        async Task CheckOnline()
        {
            try { _online = await Task.Run(() => Updater.Newest()); }
            catch { _online = null; }
            _onlineChecked = true;
            _side.Invalidate();
            if (_online != null && _game != null && _mcJar != null && (_installed == null || _online.Version > _installed))
                Status("Hay una versión nueva (v" + _online.Version + "): se instala sola al pulsar JUGAR.", Ok);
        }

        // ---- actions ----

        async Task Play()
        {
            if (_game == null)
            {
                using (var d = new FolderBrowserDialog { Description = "Elige la carpeta de 7 Days to Die (la que tiene 7DaysToDie.exe)." })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    if (!File.Exists(Path.Combine(d.SelectedPath, "7DaysToDie.exe"))) { Status("Esa carpeta no tiene 7DaysToDie.exe.", Bad); return; }
                    GameSetup.SaveGameFolder(d.SelectedPath);
                }
                Refresh(false);
                return;
            }
            if (GameSetup.GameRunning()) { Status("7 Days to Die ya está abierto.", Bad); return; }

            _busy = true;
            _play.Enabled = false;
            string error = null;
            await Task.Run(() =>
            {
                try
                {
                    bool updated = false;
                    if (_online != null && (_installed == null || _online.Version > _installed) && (_embedded == null || _online.Version >= _embedded))
                    {
                        try
                        {
                            Status("Descargando la versión " + _online.Version + "…");
                            using (var ms = Updater.Download(_online, Progress)) GameSetup.InstallFrom(ms, _game);
                            updated = true;
                        }
                        catch (Exception ex) { Status("No se pudo descargar (" + ex.Message + "); uso la versión que traigo.", Bad); }
                    }
                    if (!updated && _embedded != null && (_installed == null || _embedded > _installed))
                    {
                        Status("Instalando el mod…");
                        using (var s = GameSetup.EmbeddedPayload()) GameSetup.InstallFrom(s, _game);
                    }
                    Progress(-1);
                    Status("Abriendo Steam…");
                    GameSetup.StartSteamIfNeeded();
                    Status("Abriendo 7 Days to Die…", Ok);
                    GameSetup.Launch(_game);
                }
                catch (UnauthorizedAccessException) { error = "Windows no me dejó escribir en la carpeta del juego. Abre el launcher con clic derecho → Ejecutar como administrador."; }
                catch (Exception ex) { error = "Algo salió mal: " + ex.Message; }
            });
            _busy = false;
            if (error != null) { Status(error, Bad); Refresh(true); return; }
            GameSetup.MakeDesktopShortcutOnce();
            await Task.Delay(2500);
            Close();        // like the Minecraft launcher: out of the way once the game opens
        }

        void ChangeSkin()
        {
            using (var d = new OpenFileDialog { Title = "Elige tu skin de Minecraft", Filter = "Skins de Minecraft (*.png)|*.png" })
            {
                string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(downloads)) d.InitialDirectory = downloads;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string err = SkinView.Add(d.FileName);
                if (err != null) { Status(err, Bad); return; }
                Refresh(true);
                if (_page == "Skin") FillSkinList();
                Status("Skin lista: la verás al entrar al juego.", Ok);
            }
        }

        void Uninstall()
        {
            if (_game == null) return;
            if (GameSetup.GameRunning()) { Status("Cierra 7 Days to Die primero.", Bad); return; }
            if (MessageBox.Show(this, "¿Quitar el mod del juego? Tus skins y ajustes se quedan guardados.", "Steve vs Zombies",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            GameSetup.Uninstall(_game);
            Refresh(false);
            Status("Mod quitado. JUGAR lo vuelve a poner.");
        }
    }

    /// <summary>A setting row: its name, a grey hint, and an on/off switch on the right.</summary>
    class Switch : Control
    {
        bool _on;
        public string Label, Hint;
        public event EventHandler CheckedChanged;

        public Switch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Height = 45; Cursor = Cursors.Hand; BackColor = LauncherForm.Card;
        }

        public bool Checked
        {
            get => _on;
            set { if (_on == value) return; _on = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float S = LauncherForm.S;
            g.ScaleTransform(S, S);                                 // drawn in 100% units
            int W = (int)(Width / S), H = (int)(Height / S);
            using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
            {
                using (var f = new Font("Segoe UI", 13.3f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var b = new SolidBrush(Color.White))
                    g.DrawString(Label, f, b, new RectangleF(12, Hint == null ? 13 : 5, W - 80, 22), sf);
                if (Hint != null)
                    using (var f = new Font("Segoe UI", 10.7f, GraphicsUnit.Pixel))
                    using (var b = new SolidBrush(LauncherForm.Grey))
                        g.DrawString(Hint, f, b, new RectangleF(12, 26, W - 80, 18), sf);
            }
            int w = 44, h = 22, x = W - w - 12, y = (H - h) / 2;
            using (var path = new GraphicsPath())
            {
                path.AddArc(x, y, h, h, 90, 180); path.AddArc(x + w - h, y, h, h, 270, 180); path.CloseFigure();
                using (var b = new SolidBrush(_on ? LauncherForm.Green : Color.FromArgb(90, 90, 90))) g.FillPath(b, path);
            }
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, _on ? x + w - h + 3 : x + 3, y + 3, h - 6, h - 6);
        }
    }
}
