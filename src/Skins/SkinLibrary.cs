using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SteveDaysToDie.Skins
{
    /// <summary>
    /// The player's skins, like the Minecraft launcher's list: Steve (read from the player's Minecraft),
    /// every .png in %LOCALAPPDATA%\SteveDaysToDie\skins, and the skins made in the Minecraft launcher
    /// (copied into that folder the first time, so they keep working if the launcher changes).
    /// </summary>
    public static class SkinLibrary
    {
        public class Entry
        {
            public string Name;         // what the list shows
            public string Key;          // what ajustes.cfg stores: "steve" or a file name
            public string Path;
            public bool IsSteve;
            public Texture2D Texture;   // loaded when first needed
        }

        public static readonly List<Entry> Entries = new List<Entry>();
        static Texture2D _steve;

        public static string MainDir => System.IO.Path.GetDirectoryName(Settings.FilePath) ?? "";
        public static string SkinsDir => System.IO.Path.Combine(MainDir, "skins");

        /// <summary>A Minecraft skin is 64x64, or 64x32 in the old layout.</summary>
        public static Texture2D Decode(byte[] bytes)
        {
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes) || tex.width != 64 || (tex.height != 64 && tex.height != 32))
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.filterMode = FilterMode.Point;
                return tex;
            }
            catch { return null; }
        }

        public static Texture2D LoadFile(string path)
        {
            try { return File.Exists(path) ? Decode(File.ReadAllBytes(path)) : null; }
            catch { return null; }
        }

        public static Texture2D SteveTexture()
        {
            if (_steve == null) _steve = MinecraftAssets.LoadSteveDefaultSkin();
            return _steve;
        }

        public static Texture2D TextureOf(Entry e)
        {
            if (e == null) return null;
            if (e.Texture == null) e.Texture = e.IsSteve ? SteveTexture() : LoadFile(e.Path);
            return e.Texture;
        }

        /// <summary>The file a setting value points to ("skin.png" is the one beside ajustes.cfg).</summary>
        public static string PathFor(string key)
            => key == "skin.png" ? System.IO.Path.Combine(MainDir, "skin.png") : System.IO.Path.Combine(SkinsDir, key);

        public static void Refresh()
        {
            Entries.Clear();
            Entries.Add(new Entry { Name = "Steve (de tu Minecraft)", Key = "steve", IsSteve = true });
            try
            {
                Directory.CreateDirectory(SkinsDir);
                ImportLauncher();
                string own = System.IO.Path.Combine(MainDir, "skin.png");
                if (File.Exists(own)) Entries.Add(new Entry { Name = "skin.png", Key = "skin.png", Path = own });
                var files = Directory.GetFiles(SkinsDir, "*.png");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                    Entries.Add(new Entry { Name = System.IO.Path.GetFileNameWithoutExtension(f), Key = System.IO.Path.GetFileName(f), Path = f });
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] SkinLibrary.Refresh: " + ex.Message); }
        }

        /// <summary>The skin whose key ajustes.cfg holds, or null for "auto".</summary>
        public static Entry Current()
        {
            string key = Settings.Get("skin", "auto");
            return Entries.Find(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public static bool Apply(Entry e)
        {
            var tex = TextureOf(e);
            if (tex == null) return false;
            SteveCharacter.SetSkin(tex, e.Name);
            Settings.Set("skin", e.Key);
            return true;
        }

        /// <summary>Saves the skin in use into the skins folder (the "guardar" of the launcher). Returns the file name.</summary>
        public static string SaveCurrent()
            => SaveTexture(SteveCharacter.SkinTexture, "skin-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        public static string SaveTexture(Texture2D tex, string name)
        {
            try
            {
                if (tex == null) return null;
                Directory.CreateDirectory(SkinsDir);
                string file = Sanitize(name) + ".png";
                File.WriteAllBytes(System.IO.Path.Combine(SkinsDir, file), tex.EncodeToPNG());
                return file;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] SaveTexture: " + ex.Message); return null; }
        }

        /// <summary>
        /// Copies a picture the player chose into the library. Returns its file name, or null with the reason.
        /// The same picture chosen twice is not copied twice.
        /// </summary>
        public static string ImportFile(string path, out string error)
        {
            error = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = Decode(bytes);
                if (tex == null)
                {
                    error = "Esa imagen no es una skin de Minecraft: tiene que ser un PNG de 64x64 (o 64x32).";
                    return null;
                }
                UnityEngine.Object.Destroy(tex);
                Directory.CreateDirectory(SkinsDir);
                string name = Sanitize(System.IO.Path.GetFileNameWithoutExtension(path));
                string file = name + ".png";
                for (int n = 2; File.Exists(System.IO.Path.Combine(SkinsDir, file)); n++)
                {
                    byte[] there = File.ReadAllBytes(System.IO.Path.Combine(SkinsDir, file));
                    if (there.Length == bytes.Length && System.Linq.Enumerable.SequenceEqual(there, bytes)) return file;
                    file = name + "-" + n + ".png";
                }
                File.WriteAllBytes(System.IO.Path.Combine(SkinsDir, file), bytes);
                return file;
            }
            catch (Exception ex)
            {
                error = "No se pudo agregar la skin: " + ex.Message;
                return null;
            }
        }

        public static void Delete(string file)
        {
            try { File.Delete(System.IO.Path.Combine(SkinsDir, file)); } catch { }
        }

        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(SkinsDir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + SkinsDir + "\"");
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] OpenFolder: " + ex.Message); }
        }

        static string Sanitize(string name)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Trim();
            return name.Length == 0 ? "skin" : (name.Length > 60 ? name.Substring(0, 60) : name);
        }

        /// <summary>Copies the launcher's own skins (launcher_custom_skins.json) into the skins folder, once each.</summary>
        public static int ImportLauncher()
        {
            int added = 0;
            try
            {
                string mc = Plugin.MinecraftPath;
                if (string.IsNullOrEmpty(mc)) return 0;
                string p = System.IO.Path.Combine(mc, "launcher_custom_skins.json");
                if (!File.Exists(p)) return 0;
                string json = File.ReadAllText(p);
                foreach (Match m in Regex.Matches(json, "\"skinImage\"\\s*:\\s*\"data:image/png;base64,([A-Za-z0-9+/=]+)\""))
                {
                    int start = Math.Max(0, m.Index - 500);
                    var names = Regex.Matches(json.Substring(start, m.Index - start), "\"name\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    string name = names.Count > 0 ? Regex.Unescape(names[names.Count - 1].Groups[1].Value) : "launcher";
                    string file = System.IO.Path.Combine(SkinsDir, Sanitize("launcher-" + name) + ".png");
                    if (File.Exists(file)) continue;
                    byte[] bytes = Convert.FromBase64String(m.Groups[1].Value);
                    var tex = Decode(bytes);
                    if (tex == null) continue;
                    UnityEngine.Object.Destroy(tex);
                    File.WriteAllBytes(file, bytes);
                    added++;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] ImportLauncher: " + ex.Message); }
            return added;
        }
    }
}
