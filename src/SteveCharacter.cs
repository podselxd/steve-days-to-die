using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using SteveDaysToDie.Skins;

namespace SteveDaysToDie
{
    /// <summary>
    /// Which skin the player wears. The "skin" setting says: a file from the skins folder (the Skins
    /// button writes it), "steve", or "auto": skin.png beside the settings, the skin chosen in the
    /// Minecraft launcher, and otherwise Steve from the player's Minecraft.
    /// (The launcher's assets/skins folder is NOT used: it caches other people's skins.)
    /// </summary>
    public static class SteveCharacter
    {
        public static Texture2D SkinTexture;
        public static string SkinName = "Steve";

        /// <summary>Raised when the skin changes while playing: Steve is rebuilt with the new one.</summary>
        public static event Action SkinChanged;

        public static void Init(string mcPath)
        {
            string name = "Steve (Minecraft)";
            Texture2D tex = null;
            string pick = Settings.Get("skin", "auto");
            if (pick.Equals("steve", StringComparison.OrdinalIgnoreCase))
                tex = SkinLibrary.SteveTexture();
            else if (!pick.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                tex = SkinLibrary.LoadFile(SkinLibrary.PathFor(pick));
                if (tex != null) name = Path.GetFileNameWithoutExtension(pick);
                else Debug.LogWarning("[SVD] The skin '" + pick + "' from the settings is missing or not a 64x64 / 64x32 skin; using the automatic one.");
            }
            if (tex == null) tex = LoadOwnFile(ref name) ?? LoadLauncherSkin(mcPath, ref name) ?? SkinLibrary.SteveTexture();

            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                SkinTexture = tex;
                SkinName = name;
                Debug.Log($"[SVD] Skin loaded: {tex.width}x{tex.height} ({name})");
            }
            else
                Debug.LogWarning("[SVD] Skin not loaded: the 7 Days character stays.");
        }

        /// <summary>Changes the skin while playing (the Skins button).</summary>
        public static void SetSkin(Texture2D tex, string name)
        {
            if (tex == null) return;
            tex.filterMode = FilterMode.Point;
            SkinTexture = tex;
            SkinName = name;
            Steve.SteveParts.ResetMaterial();
            Debug.Log($"[SVD] Skin changed to '{name}' ({tex.width}x{tex.height}).");
            Steve.SkinNet.Resend();
            SkinChanged?.Invoke();
        }

        static Texture2D LoadOwnFile(ref string source)
        {
            var t = SkinLibrary.LoadFile(SkinLibrary.PathFor("skin.png"));
            if (t != null) source = "skin.png";
            return t;
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
                    var d = Regex.Match(json.Substring(m.Index, Math.Min(600, json.Length - m.Index)), "\"updated\"\\s*:\\s*\"([^\"]+)\"");
                    string date = d.Success ? d.Groups[1].Value : "";
                    if (best != null && string.CompareOrdinal(date, bestDate) <= 0) continue;
                    var t = SkinLibrary.Decode(Convert.FromBase64String(m.Groups[1].Value));
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
