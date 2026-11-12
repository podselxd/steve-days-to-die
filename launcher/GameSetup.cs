using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace SteveVsZombiesLauncher
{
    /// <summary>Finds the games on this PC, installs the mod and starts 7 Days to Die with it.</summary>
    static class GameSetup
    {
        public static string ModDataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteveDaysToDie");
        public static string MinecraftDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        // ---- 7 Days to Die: every Steam library on this PC ----

        static string SteamFolder()
        {
            string s = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
            return s?.Replace('/', '\\');
        }

        public static string FindGame()
        {
            string saved = ReadSavedGameFolder();
            if (saved != null && File.Exists(Path.Combine(saved, "7DaysToDie.exe"))) return saved;
            var libraries = new List<string>();
            string steam = SteamFolder();
            if (steam != null)
            {
                libraries.Add(steam);
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                        libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            }
            libraries.Add(@"C:\Program Files (x86)\Steam");
            foreach (var lib in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string g = Path.Combine(lib, "steamapps", "common", "7 Days To Die");
                if (File.Exists(Path.Combine(g, "7DaysToDie.exe"))) return g;
            }
            return null;
        }

        static string SavedGameFile => Path.Combine(ModDataDir, "carpeta-del-juego.txt");

        static string ReadSavedGameFolder()
        {
            try { return File.Exists(SavedGameFile) ? File.ReadAllText(SavedGameFile).Trim() : null; } catch { return null; }
        }

        public static void SaveGameFolder(string folder)
        {
            try { Directory.CreateDirectory(ModDataDir); File.WriteAllText(SavedGameFile, folder); } catch { }
        }

        // ---- Minecraft ----

        /// <summary>The newest installed Minecraft version that has its game file (null: none).</summary>
        public static string MinecraftJar()
        {
            string versions = Path.Combine(MinecraftDir, "versions");
            if (!Directory.Exists(versions)) return null;
            return Directory.GetDirectories(versions)
                .Select(v => Path.Combine(v, Path.GetFileName(v) + ".jar"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        // ---- versions ----

        public static Version Parse(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            var m = Regex.Match(v, @"\d+(\.\d+){1,3}");
            return m.Success && Version.TryParse(m.Value, out var r) ? r : null;
        }

        static Version VersionInModInfo(string xml)
        {
            var m = Regex.Match(xml ?? "", "<Version value=\"([^\"]+)\"");
            return m.Success ? Parse(m.Groups[1].Value) : null;
        }

        public static string ModFolder(string game) => Path.Combine(game, "Mods", "SteveDaysToDie");

        public static Version InstalledVersion(string game)
        {
            try
            {
                string mi = Path.Combine(ModFolder(game), "ModInfo.xml");
                return File.Exists(mi) ? VersionInModInfo(File.ReadAllText(mi)) : null;
            }
            catch { return null; }
        }

        /// <summary>The mod this launcher carries inside itself (used when there is no internet).</summary>
        public static Stream EmbeddedPayload() => Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");

        public static Version VersionInZip(Stream zipStream)
        {
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                var e = zip.Entries.FirstOrDefault(x => x.FullName.Replace('\\', '/').Equals("SteveDaysToDie/ModInfo.xml", StringComparison.OrdinalIgnoreCase));
                if (e == null) return null;
                using (var r = new StreamReader(e.Open())) return VersionInModInfo(r.ReadToEnd());
            }
        }

        /// <summary>Puts the mod from a zip (the release's own zip) in the game's Mods folder.</summary>
        public static void InstallFrom(Stream zipStream, string game)
        {
            string dest = ModFolder(game);
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                const string prefix = "SteveDaysToDie/";
                if (!zip.Entries.Any(e => e.FullName.Replace('\\', '/').StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("el archivo descargado no trae el mod");
                foreach (var e in zip.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;
                    string name = e.FullName.Replace('\\', '/');
                    if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string target = Path.Combine(dest, name.Substring(prefix.Length).Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    e.ExtractToFile(target, true);
                }
            }
        }

        public static void Uninstall(string game)
        {
            string dir = ModFolder(game);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        // ---- start ----

        public static bool GameRunning() => Process.GetProcessesByName("7DaysToDie").Length > 0;

        // The game needs Steam open: started without it, it would go through Steam's own Play,
        // which uses the anti-cheat version and skips mods.
        public static void StartSteamIfNeeded()
        {
            if (Process.GetProcessesByName("steam").Length > 0) return;
            string exe = (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null) as string)?.Replace('/', '\\');
            if (exe == null || !File.Exists(exe)) return;
            Process.Start(exe, "-silent");
            for (int i = 0; i < 60 && Process.GetProcessesByName("steamwebhelper").Length == 0; i++) Thread.Sleep(500);
            Thread.Sleep(6000);
        }

        public static void Launch(string game)
        {
            var psi = new ProcessStartInfo(Path.Combine(game, "7DaysToDie.exe"), "-force-d3d11 -nogs")
            {
                WorkingDirectory = game,
                UseShellExecute = false,
            };
            psi.EnvironmentVariables["STEVEDAYS_MC"] = MinecraftDir;
            Process.Start(psi);
        }

        public static void MakeDesktopShortcutOnce()
        {
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Steve Days to Die.lnk");
                if (File.Exists(lnk)) return;
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                var t = sc.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { exe });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(exe) });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { "Steve Days to Die: 7 Days to Die con las reglas de Minecraft" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }
    }
}
