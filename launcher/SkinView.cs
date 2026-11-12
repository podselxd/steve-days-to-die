using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteveVsZombiesLauncher
{
    /// <summary>
    /// The player's skin, seen from the front with both layers (the same one the mod uses: the "skin"
    /// line of ajustes.cfg, a file of the skins folder, or Steve read from the player's own Minecraft).
    /// </summary>
    static class SkinView
    {
        public static string SkinsDir => Path.Combine(GameSetup.ModDataDir, "skins");
        static string SettingsFile => Path.Combine(GameSetup.ModDataDir, "ajustes.cfg");

        public static string CurrentSetting()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return "auto";
                foreach (var line in File.ReadAllLines(SettingsFile))
                {
                    var m = Regex.Match(line, @"^\s*skin\s*=\s*(.*)$", RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
            }
            catch { }
            return "auto";
        }

        public static void SetSetting(string value)
        {
            Directory.CreateDirectory(GameSetup.ModDataDir);
            var lines = File.Exists(SettingsFile) ? File.ReadAllLines(SettingsFile).ToList() : new System.Collections.Generic.List<string>();
            int i = lines.FindIndex(l => Regex.IsMatch(l, @"^\s*skin\s*=", RegexOptions.IgnoreCase));
            if (i >= 0) lines[i] = "skin=" + value; else lines.Add("skin=" + value);
            File.WriteAllLines(SettingsFile, lines);
        }

        /// <summary>The skin picture in use, and its name (null when none could be read).</summary>
        public static Bitmap Current(out string name)
        {
            string s = CurrentSetting();
            name = "Steve";
            if (!s.Equals("auto", StringComparison.OrdinalIgnoreCase) && !s.Equals("steve", StringComparison.OrdinalIgnoreCase))
            {
                string p = s == "skin.png" ? Path.Combine(GameSetup.ModDataDir, "skin.png") : Path.Combine(SkinsDir, s);
                var b = Load(p);
                if (b != null) { name = Path.GetFileNameWithoutExtension(s); return b; }
            }
            var own = Load(Path.Combine(GameSetup.ModDataDir, "skin.png"));
            if (own != null && !s.Equals("steve", StringComparison.OrdinalIgnoreCase)) { name = "skin.png"; return own; }
            return Steve();
        }

        public static Bitmap Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using (var fs = File.OpenRead(path)) return Valid(new Bitmap(fs));
            }
            catch { return null; }
        }

        static Bitmap Valid(Bitmap b)
        {
            if (b.Width == 64 && (b.Height == 64 || b.Height == 32)) return new Bitmap(b);
            b.Dispose();
            return null;
        }

        public static Bitmap SteveSkin() => Steve();

        static Bitmap Steve()
        {
            try
            {
                string jar = GameSetup.MinecraftJar();
                if (jar == null) return null;
                using (var zip = ZipFile.OpenRead(jar))
                {
                    var e = zip.GetEntry("assets/minecraft/textures/entity/player/wide/steve.png");
                    if (e == null) return null;
                    using (var s = e.Open())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        ms.Position = 0;
                        return Valid(new Bitmap(ms));
                    }
                }
            }
            catch { return null; }
        }

        /// <summary>Copies a picture into the skins folder and makes it the one in use. Returns the error, or null.</summary>
        public static string Add(string path)
        {
            using (var b = Load(path))
                if (b == null) return "Esa imagen no es una skin de Minecraft: tiene que ser un PNG de 64x64 (o 64x32).";
            Directory.CreateDirectory(SkinsDir);
            string baseName = string.Concat(Path.GetFileNameWithoutExtension(path).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string file = baseName + ".png";
            byte[] bytes = File.ReadAllBytes(path);
            for (int n = 2; File.Exists(Path.Combine(SkinsDir, file)); n++)
            {
                if (File.ReadAllBytes(Path.Combine(SkinsDir, file)).SequenceEqual(bytes)) break;
                file = baseName + "-" + n + ".png";
            }
            File.WriteAllBytes(Path.Combine(SkinsDir, file), bytes);
            SetSetting(file);
            return null;
        }

        public static bool IsSlim(Bitmap t)
        {
            if (t == null || t.Height < 64) return false;
            for (int y = 20; y < 32; y++)
                for (int x = 54; x < 56; x++)
                    if (t.GetPixel(x, y).A > 2) return false;
            return true;
        }

        /// <summary>Draws the skin from the front, both layers, px screen pixels per skin pixel.</summary>
        public static void DrawFront(Graphics g, Bitmap t, float x, float y, float px)
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            bool old = t.Height < 64;
            int aw = IsSlim(t) ? 3 : 4;
            float rx = x + (4 - aw) * px, lx = x + 12 * px, o = 0.5f * px;

            void Part(RectangleF dst, int u, int v, int w, int h, bool flip = false)
            {
                var src = new RectangleF(u, v, w, h);
                if (!flip) { g.DrawImage(t, dst, src, GraphicsUnit.Pixel); return; }
                var state = g.Save();
                g.TranslateTransform(dst.X + dst.Width, dst.Y);
                g.ScaleTransform(-1, 1);
                g.DrawImage(t, new RectangleF(0, 0, dst.Width, dst.Height), src, GraphicsUnit.Pixel);
                g.Restore(state);
            }

            Part(new RectangleF(x + 4 * px, y, 8 * px, 8 * px), 8, 8, 8, 8);
            Part(new RectangleF(x + 4 * px, y + 8 * px, 8 * px, 12 * px), 20, 20, 8, 12);
            Part(new RectangleF(rx, y + 8 * px, aw * px, 12 * px), 44, 20, aw, 12);
            if (old) Part(new RectangleF(lx, y + 8 * px, aw * px, 12 * px), 44, 20, aw, 12, true);
            else Part(new RectangleF(lx, y + 8 * px, aw * px, 12 * px), 36, 52, aw, 12);
            Part(new RectangleF(x + 4 * px, y + 20 * px, 4 * px, 12 * px), 4, 20, 4, 12);
            if (old) Part(new RectangleF(x + 8 * px, y + 20 * px, 4 * px, 12 * px), 4, 20, 4, 12, true);
            else Part(new RectangleF(x + 8 * px, y + 20 * px, 4 * px, 12 * px), 20, 52, 4, 12);

            Part(new RectangleF(x + 4 * px - o, y - o, 8 * px + 2 * o, 8 * px + 2 * o), 40, 8, 8, 8);
            if (old) return;
            Part(new RectangleF(x + 4 * px, y + 8 * px, 8 * px, 12 * px), 20, 36, 8, 12);
            Part(new RectangleF(rx, y + 8 * px, aw * px, 12 * px), 44, 36, aw, 12);
            Part(new RectangleF(lx, y + 8 * px, aw * px, 12 * px), 52, 52, aw, 12);
            Part(new RectangleF(x + 4 * px, y + 20 * px, 4 * px, 12 * px), 4, 36, 4, 12);
            Part(new RectangleF(x + 8 * px, y + 20 * px, 4 * px, 12 * px), 4, 52, 4, 12);
        }
    }
}
