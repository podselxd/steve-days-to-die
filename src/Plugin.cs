using System;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie
{
    public class Plugin : IModApi
    {
        public static Mod ModInstance { get; private set; }
        public static string MinecraftPath { get; private set; }
        public static string MinecraftVersion { get; private set; }

        public void InitMod(Mod _modInstance)
        {
            ModInstance = _modInstance;
            MeltyTest.Init();
            Settings.Load();
            Debug.Log("[SVD] SteveDaysToDie v1.8.3 loading...");

            MinecraftPath = FindMinecraftPath(_modInstance);

            if (string.IsNullOrEmpty(MinecraftPath))
            {
                MeltyTest.Fail("minecraft-files", "Minecraft folder not found");
                Debug.LogError("[SVD] Minecraft folder not found. Install Minecraft Java Edition and launch it once.");
                return;
            }

            Debug.Log($"[SVD] Minecraft found at: {MinecraftPath}");
            MinecraftVersion = VersionHelper.DetectBestVersion(MinecraftPath);
            Debug.Log($"[SVD] Minecraft version detected: {MinecraftVersion ?? "none"}");

            // Setting: which installed Minecraft version to read everything from.
            string wanted = Settings.Get("version_minecraft", "auto");
            if (!wanted.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                string jar = Path.Combine(MinecraftPath, "versions", wanted, wanted + ".jar");
                if (File.Exists(jar))
                {
                    MinecraftVersion = wanted;
                    Debug.Log("[SVD] Minecraft version chosen in settings: " + wanted);
                }
                else
                    Debug.LogWarning($"[SVD] Settings ask for Minecraft {wanted}, but its files are missing ({jar}). " +
                                     "Start that version once in the Minecraft launcher. Using " + (MinecraftVersion ?? "none") + ".");
            }
            if (MinecraftVersion == null)
                Debug.LogError("[SVD] No Minecraft version with its files was found. Start Minecraft once from its launcher.");

            var versionStatus = VersionHelper.Check(MinecraftVersion, out string versionMsg);
            Debug.Log(versionMsg);
            if (versionStatus == VersionHelper.VersionStatus.TooOld)
            {
                Debug.LogError("[SVD] Minecraft version too old — mod will not load. " + versionMsg);
                return;
            }
            if (versionStatus == VersionHelper.VersionStatus.NewerThanTested)
                Debug.LogWarning(versionMsg);

            if (MinecraftVersion != null)
                MinecraftAssets.Init(MinecraftPath, MinecraftVersion);

            SteveCharacter.Init(MinecraftPath);
            Sound.MinecraftSounds.Init(MinecraftPath, MinecraftVersion);
            // Minecraft models and textures for the first-person hand (Ryan Ussery's reader, MIT)
            try { Ryan.Mc.Init(); Combat.AttackCharge.OnSwing += Ryan.FirstPerson.Swing; Crafting.McItems.Prepare(_modInstance.Path, MinecraftPath, MinecraftVersion); }
            catch (Exception ex) { Debug.LogWarning("[SVD] Minecraft model reader: " + ex.Message); }

            // skins are chosen in the launcher only (the creator's call): no Skins button in the game's menus.
            // Skins/SkinMenu.cs stays in the project, unused, in case it comes back.
            SteveDaysToDie.HUD.HUDTextures.Load();
            if (SteveDaysToDie.HUD.HUDTextures.Loaded && SteveCharacter.SkinTexture != null)
                MeltyTest.Pass("minecraft-files");
            else
                MeltyTest.Fail("minecraft-files", "HUD sprites or skin could not be read from Minecraft " + MinecraftVersion);

            var harmony = new Harmony("com.stevevszombies");
            harmony.PatchAll();
            Debug.Log("[SVD] SteveDaysToDie ready.");
        }

        static string FindMinecraftPath(Mod mod)
        {
            // 1. Melty passes the companion game folder via melty.properties
            try
            {
                string propsPath = Path.Combine(mod.Path, "melty.properties");
                if (File.Exists(propsPath))
                {
                    foreach (var line in File.ReadAllLines(propsPath))
                    {
                        if (line.StartsWith("game:minecraft-java="))
                        {
                            string p = line.Substring("game:minecraft-java=".Length).Trim();
                            if (Directory.Exists(p)) { Debug.Log("[SVD] Minecraft path from melty.properties"); return p; }
                        }
                    }
                }
            }
            catch { }

            // 2. Environment variable (manual testing)
            string env = Environment.GetEnvironmentVariable("STEVEDAYS_MC");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            {
                Debug.Log("[SVD] Minecraft path from STEVEDAYS_MC env var");
                return env;
            }

            // 3. Auto-detect standard locations
            string[] candidates = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", ".minecraft"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minecraft"),
                @"C:\Users\" + Environment.UserName + @"\AppData\Roaming\.minecraft",
            };

            foreach (var c in candidates)
            {
                if (Directory.Exists(c))
                {
                    Debug.Log($"[SVD] Minecraft auto-detected at: {c}");
                    return c;
                }
            }

            return null;
        }
    }
}
