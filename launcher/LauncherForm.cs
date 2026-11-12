using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteveVsZombiesLauncher
{
    /// <summary>
    /// The launcher window, in the spirit of the Minecraft launcher: the player's skin, what the mashup
    /// does, and one big green JUGAR button that updates the mod from GitHub and starts the game.
    /// </summary>
    class LauncherForm : Form
    {
        static readonly Color Bg = Color.FromArgb(32, 32, 32);
        static readonly Color Side = Color.FromArgb(20, 20, 20);
        static readonly Color Bar = Color.FromArgb(14, 14, 14);
        static readonly Color Green = Color.FromArgb(60, 133, 39);
        static readonly Color GreenHover = Color.FromArgb(76, 160, 50);
        static readonly Color Grey = Color.FromArgb(170, 170, 170);
        static readonly Color Ok = Color.FromArgb(108, 196, 80);
        static readonly Color Bad = Color.FromArgb(230, 90, 80);

        string _game, _mcJar, _skinName;
        Version _installed, _embedded;
        Updater.Release _online;
        bool _onlineChecked, _busy;
        Bitmap _skin;
        double _progress = -1;

        readonly Panel _side = new Panel(), _bottom = new Panel(), _content = new Panel();
        readonly Button _play = new Button(), _skinBtn = new Button();
        readonly Label _status = new Label();

        public LauncherForm()
        {
            Text = "Steve vs Zombies";
            ClientSize = new Size(980, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10f);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            _side.Dock = DockStyle.Left; _side.Width = 240; _side.BackColor = Side;
            _bottom.Dock = DockStyle.Bottom; _bottom.Height = 128; _bottom.BackColor = Bar;
            _content.Dock = DockStyle.Fill; _content.BackColor = Bg;
            foreach (var p in new[] { _side, _bottom, _content })
                typeof(Panel).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(p, true, null);
            Controls.Add(_content); Controls.Add(_bottom); Controls.Add(_side);

            _side.Paint += PaintSide;
            _content.Paint += PaintContent;
            _bottom.Paint += PaintBottom;
            _bottom.Resize += (s, e) => LayoutBottom();
            _content.Resize += (s, e) => LayoutContent();

            Style(_play, "JUGAR", 22f);
            _play.Size = new Size(320, 66);
            _play.Click += async (s, e) => await Play();
            _bottom.Controls.Add(_play);

            _status.AutoSize = false; _status.TextAlign = ContentAlignment.MiddleCenter; _status.ForeColor = Grey;
            _status.BackColor = Bar; _status.Font = new Font("Segoe UI", 9.5f);
            _bottom.Controls.Add(_status);

            Style(_skinBtn, "Cambiar skinâ€¦", 10.5f);
            _skinBtn.Size = new Size(180, 38);
            _skinBtn.Click += (s, e) => ChangeSkin();
            _content.Controls.Add(_skinBtn);

            AddLink("Carpeta de skins", 0, () => { Directory.CreateDirectory(SkinView.SkinsDir); Process.Start("explorer.exe", "\"" + SkinView.SkinsDir + "\""); });
            AddLink("PÃ¡gina del mod en GitHub", 1, () => Process.Start(Updater.Page));
            AddLink("Quitar el mod del juego", 2, Uninstall);

            Load += async (s, e) => { Refresh(false); await CheckOnline(); };
        }

        // ---- look ----

        void Style(Button b, string text, float size)
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

        void AddLink(string text, int row, Action act)
        {
            var l = new LinkLabel
            {
                Text = text, AutoSize = true, LinkColor = Grey, ActiveLinkColor = Color.White, VisitedLinkColor = Grey,
                LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Side, Font = new Font("Segoe UI", 9.5f),
            };
            l.LinkClicked += (s, e) => { try { act(); } catch (Exception ex) { Status(ex.Message, Bad); } };
            _side.Controls.Add(l);
            _side.Resize += (s, e) => l.Location = new Point(24, _side.Height - 100 + row * 26);
            l.Location = new Point(24, ClientSize.Height - 100 + row * 26);
        }

        void LayoutBottom()
        {
            _play.Location = new Point((_bottom.Width - _play.Width) / 2, 46);
            _status.SetBounds(0, 10, _bottom.Width, 26);
        }

        void LayoutContent()
        {
            _skinBtn.Location = new Point(_content.Width - 60 - 180, _content.Height - 64);
        }

        static void Draw(Graphics g, string s, float size, FontStyle st, Color c, float x, float y)
        {
            using (var f = new Font("Segoe UI", size, st))
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        void PaintSide(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Draw(g, "STEVE", 26, FontStyle.Bold, Color.White, 20, 22);
            Draw(g, "vs ZOMBIES", 16, FontStyle.Bold, Ok, 23, 62);
            Draw(g, "7 Days to Die + Minecraft", 9, FontStyle.Regular, Grey, 24, 96);

            float y = 150;
            void Line(string label, string value, Color c)
            {
                Draw(g, label, 9, FontStyle.Regular, Grey, 24, y);
                Draw(g, value, 10.5f, FontStyle.Bold, c, 24, y + 17);
                y += 54;
            }
            Line("7 Days to Die", _game != null ? "Encontrado" : "No encontrado", _game != null ? Ok : Bad);
            Line("Minecraft: Java Edition", _mcJar != null ? "Encontrado" : "No encontrado", _mcJar != null ? Ok : Bad);
            Line("Mod instalado", _installed != null ? "v" + _installed : "TodavÃ­a no", _installed != null ? Color.White : Grey);
            string online = !_onlineChecked ? "Buscandoâ€¦" : _online != null ? "v" + _online.Version : "Sin conexiÃ³n";
            Line("VersiÃ³n mÃ¡s nueva", online, Color.White);
        }

        void PaintContent(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int w = _content.Width, h = _content.Height;
            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(38, 48, 36), Bg, 90f)) g.FillRectangle(bg, 0, 0, w, h);
            using (var dot = new SolidBrush(Color.FromArgb(10, 255, 255, 255)))       // a faint pixel grid
                for (int yy = 0; yy < h; yy += 16)
                    for (int xx = ((yy / 16) % 2) * 8; xx < w; xx += 16) g.FillRectangle(dot, xx, yy, 8, 8);

            Draw(g, "Sobrevive como Steve", 24, FontStyle.Bold, Color.White, 40, 34);
            Draw(g, "7 Days to Die jugado con las reglas de Minecraft.", 11, FontStyle.Regular, Grey, 42, 80);
            string[] lines =
            {
                "â–   Eres Steve, con tu propia skin (con capas 3D y skins slim)",
                "â–   Corazones, hambre y barra de ataque de Minecraft",
                "â–   Golpe crÃ­tico al caer, y el ritmo de cada arma",
                "â–   Te cansas con el hambre, no con la resistencia",
                "â–   Pantalla de skins dentro del juego (Esc â†’ Skins)",
                "â–   Todo lo de Minecraft se lee de tu propio Minecraft",
            };
            float y = 128;
            foreach (var l in lines) { Draw(g, l, 11, FontStyle.Regular, Color.White, 42, y); y += 32; }
            Draw(g, "Para jugar 7 Days normal, Ã¡brelo desde Steam como siempre.", 9, FontStyle.Italic, Grey, 42, y + 12);

            // the player's skin, big, on the right
            float px = Math.Min(9f, (h - 140) / 32f);
            float sx = w - 60 - 180 + (180 - 16 * px) / 2f, sy = 40;
            if (_skin != null)
            {
                using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(shadow, sx - 6, sy + 32 * px - 8, 16 * px + 12, 18);
                SkinView.DrawFront(g, _skin, sx, sy, px);
            }
            Draw(g, _skinName ?? "", 10, FontStyle.Bold, Grey, w - 60 - 180, h - 92);
        }

        void PaintBottom(object sender, PaintEventArgs e)
        {
            if (_progress < 0) return;
            var g = e.Graphics;
            int bw = 420, x = (_bottom.Width - bw) / 2, y = 38;
            using (var b = new SolidBrush(Color.FromArgb(50, 50, 50))) g.FillRectangle(b, x, y, bw, 5);
            using (var b = new SolidBrush(Ok)) g.FillRectangle(b, x, y, (int)(bw * _progress), 5);
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
            _skin?.Dispose();
            _skin = SkinView.Current(out _skinName);
            if (_skin != null)
                try { using (var head = new Bitmap(16, 16)) { using (var g = Graphics.FromImage(head)) { g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half; g.DrawImage(_skin, new Rectangle(0, 0, 16, 16), new Rectangle(8, 8, 8, 8), GraphicsUnit.Pixel); g.DrawImage(_skin, new Rectangle(0, 0, 16, 16), new Rectangle(40, 8, 8, 8), GraphicsUnit.Pixel); } Icon = Icon.FromHandle(head.GetHicon()); } }
                catch { }

            if (_game == null) { _play.Text = "BUSCAR 7 DAYS"; _play.Enabled = true; }
            else { _play.Text = "JUGAR"; _play.Enabled = _mcJar != null && !_busy; }
            if (!keepStatus)
            {
                if (_game == null) Status("No encontrÃ© 7 Days to Die en tu Steam. Pulsa el botÃ³n para elegir su carpeta.", Bad);
                else if (_mcJar == null) Status("Necesitas Minecraft: Java Edition instalado y abierto una vez desde su launcher.", Bad);
                else Status("Listo para jugar.");
            }
            _side.Invalidate(); _content.Invalidate();
            LayoutBottom(); LayoutContent();
        }

        async Task CheckOnline()
        {
            try { _online = await Task.Run(() => Updater.Newest()); }
            catch { _online = null; }
            _onlineChecked = true;
            _side.Invalidate();
            if (_online != null && _game != null && _mcJar != null && (_installed == null || _online.Version > _installed))
                Status("Hay una versiÃ³n nueva (v" + _online.Version + "): se instala sola al pulsar JUGAR.", Ok);
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
            if (GameSetup.GameRunning()) { Status("7 Days to Die ya estÃ¡ abierto.", Bad); return; }

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
                            Status("Descargando la versiÃ³n " + _online.Version + "â€¦");
                            using (var ms = Updater.Download(_online, Progress)) GameSetup.InstallFrom(ms, _game);
                            updated = true;
                        }
                        catch (Exception ex) { Status("No se pudo descargar (" + ex.Message + "); uso la versiÃ³n que traigo.", Bad); }
                    }
                    if (!updated && _embedded != null && (_installed == null || _embedded > _installed))
                    {
                        Status("Instalando el modâ€¦");
                        using (var s = GameSetup.EmbeddedPayload()) GameSetup.InstallFrom(s, _game);
                    }
                    Progress(-1);
                    Status("Abriendo Steamâ€¦");
                    GameSetup.StartSteamIfNeeded();
                    Status("Abriendo 7 Days to Dieâ€¦", Ok);
                    GameSetup.Launch(_game);
                }
                catch (UnauthorizedAccessException) { error = "Windows no me dejÃ³ escribir en la carpeta del juego. Abre el launcher con clic derecho â†’ Ejecutar como administrador."; }
                catch (Exception ex) { error = "Algo saliÃ³ mal: " + ex.Message; }
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
                Status("Skin lista: la verÃ¡s al entrar al juego.", Ok);
            }
        }

        void Uninstall()
        {
            if (_game == null) return;
            if (GameSetup.GameRunning()) { Status("Cierra 7 Days to Die primero.", Bad); return; }
            if (MessageBox.Show(this, "Â¿Quitar el mod del juego? Tus skins y ajustes se quedan guardados.", "Steve vs Zombies",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            GameSetup.Uninstall(_game);
            Refresh(false);
            Status("Mod quitado. JUGAR lo vuelve a poner.");
        }
    }
}
