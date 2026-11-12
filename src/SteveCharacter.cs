using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>
    /// Which skin the player wears. Order: the player's own file (skin.png beside the settings),
    /// the skin chosen in the Minecraft launcher, and otherwise Steve from the player's Minecraft.
    /// (The launcher's assets/skins folder is NOT used: it caches other people's skins.)
    /// </summary>
    public static class SteveCharacter
    {
        public static Texture2D SkinTexture;

        public static void Init(string mcPath)
        {
            string source = "Steve (Minecraft)";
            SkinTexture = LoadOwnFile(ref source) ?? LoadLauncherSkin(mcPath, ref source) ?? MinecraftAssets.LoadSteveDefaultSkin();

            if (SkinTexture != null)
            {
                SkinTexture.filterMode = FilterMode.Point;
                Debug.Log($"[SVD] Skin loaded: {SkinTexture.width}x{SkinTexture.height} from {source}");
            }
            else
                Debug.LogWarning("[SVD] Skin not loaded: the 7 Days character stays.");
        }

        static Texture2D FromBytes(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(bytes) && tex.width == 64 && (tex.height == 64 || tex.height == 32) ? tex : null;
        }

        static Texture2D LoadOwnFile(ref string source)
        {
            try
            {
                string p = Path.Combine(Path.GetDirectoryName(Settings.FilePath) ?? "", "skin.png");
                if (!File.Exists(p)) return null;
                var t = FromBytes(File.ReadAllBytes(p));
                if (t != null) source = "skin.png";
                else Debug.LogWarning("[SVD] skin.png must be a 64x64 or 64x32 Minecraft skin; ignored.");
                return t;
            }
            catch { return null; }
        }

        // launcher_custom_skins.json keeps the skins the player made in the launcher, as base64 PNGs.
        static Texture2D LoadLauncherSkin(string mcPath, ref string source)
        {
            try
            {
                string p = Path.Combine(mcPath, "launcher_custom_skins.json");
                if (!File.Exists(p)) return null;
                string json = File.ReadAllText(p);
                Texture2D best = null;
                string bestDate = "";
                foreach (Match m in Regex.Matches(json, "\"skinImage\"\\s*:\\s*\"data:image/png;base64,([A-Za-z0-9+/=]+)\""))
                {
                    // the entry's "updated" date, when it follows in the same object
                    var d = Regex.Match(json.Substring(m.Index, Math.Min(600, json.Length - m.Index)), "\"updated\"\\s*:\\s*\"([^\"]+)\"");
                    string date = d.Success ? d.Groups[1].Value : "";
                    if (best != null && string.CompareOrdinal(date, bestDate) <= 0) continue;
                    var t = FromBytes(Convert.FromBase64String(m.Groups[1].Value));
                    if (t != null) { best = t; bestDate = date; }
                }
                if (best != null) source = "Minecraft launcher";
                return best;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] launcher skins: " + ex.Message); return null; }
        }

        public static void ApplyToLocalPlayer(EntityPlayerLocal player) { }
    }
}
