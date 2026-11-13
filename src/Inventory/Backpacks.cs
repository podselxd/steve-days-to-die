using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Inventory size (user 2026-11-13: "que el inventario no sea tan grande, pero que sea una opcion"):
    /// setting inventario = minecraft (default: Minecraft's 27 backpack slots + the bar) or 7days (7 Days' 48).
    /// Backpacks from Sophisticated Backpacks (the player's own jar, read like Minecraft's: icons and look; the mod's
    /// Java code does not run here): worn in the chest slot like that mod, each adds its slots (leather 27, copper 45,
    /// iron 54, gold 81, diamond 108, netherite 120). Items mc_sb_*; recipes added in McRecipes (Crafting.McRecipes).
    /// The game asks EffectManager for BagSize (slots shown) and CarryCapacity (slots usable): both answered here.
    /// </summary>
    public static class Backpacks
    {
        public static readonly Dictionary<string, int> Slots = new Dictionary<string, int>
        {
            { "sb_backpack", 27 }, { "sb_copper_backpack", 45 }, { "sb_iron_backpack", 54 },
            { "sb_gold_backpack", 81 }, { "sb_diamond_backpack", 108 }, { "sb_netherite_backpack", 120 },
        };

        public static bool Small => Settings.Get("inventario", "minecraft").ToLowerInvariant() != "7days";

        public static int Bonus(EntityAlive e)
        {
            try
            {
                for (int i = 0; i < e.equipment.GetSlotCount(); i++)
                {
                    string n = e.equipment.GetSlotItem(i)?.ItemClass?.Name;
                    if (n != null && n.StartsWith("mc_sb_") && Slots.TryGetValue(n.Substring(3), out int s)) return s;
                }
            }
            catch { }
            return 0;
        }

        [HarmonyPatch(typeof(EffectManager), "GetValue")]
        static class BagPatch
        {
            static void Postfix(PassiveEffects _passiveEffect, EntityAlive _entity, ref float __result)
            {
                if (_passiveEffect != PassiveEffects.BagSize && _passiveEffect != PassiveEffects.CarryCapacity) return;
                if (!(_entity is EntityPlayer)) return;
                try
                {
                    int bonus = Bonus(_entity);
                    if (Small) __result = 27 + bonus;
                    else if (bonus > 0) __result += bonus;
                    __result = Mathf.Min(__result, 160f);
                }
                catch { }
            }
        }

        // ---- the mod's jar (icons): .minecraft/mods, or the player's Downloads ----
        static string _jar;
        public static string Jar()
        {
            if (_jar != null) return _jar == "" ? null : _jar;
            _jar = "";
            try
            {
                var dirs = new List<string>
                {
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), ".minecraft", "mods"),
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Downloads"),
                };
                foreach (var d in dirs)
                {
                    if (!Directory.Exists(d)) continue;
                    foreach (var f in Directory.GetFiles(d, "sophisticatedbackpacks*.jar")) { _jar = f; break; }
                    if (_jar != "") break;
                }
            }
            catch { }
            Debug.Log("[SVD] Sophisticated Backpacks jar: " + (_jar == "" ? "not found (backpacks use a plain icon)" : _jar));
            return _jar == "" ? null : _jar;
        }

        static Texture2D Read(ZipArchive z, string path)
        {
            var e = z.GetEntry(path);
            if (e == null) return null;
            using (var s = e.Open()) using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                return t.LoadImage(ms.ToArray()) ? t : null;
            }
        }

        /// <summary>A 16x16 icon for a backpack: the mod's cloth, border and the tier's clips, laid flat.</summary>
        public static Texture2D Icon(string id)
        {
            string jar = Jar();
            var icon = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[256];
            string tier = id == "sb_backpack" ? "leather" : id.Substring(3, id.Length - 3 - "_backpack".Length);
            try
            {
                if (jar != null)
                    using (var z = ZipFile.OpenRead(jar))
                    {
                        var cloth = Read(z, "assets/sophisticatedbackpacks/textures/block/backpack_cloth.png");
                        var border = Read(z, "assets/sophisticatedbackpacks/textures/block/backpack_border.png");
                        var clips = Read(z, "assets/sophisticatedbackpacks/textures/block/" + tier + "_clips.png");
                        for (int y = 0; y < 16; y++)
                            for (int x = 0; x < 16; x++)
                            {
                                bool inside = x >= 2 && x <= 13 && y >= 1 && y <= 14;
                                if (!inside) continue;
                                Color c = cloth != null ? cloth.GetPixelBilinear((x + 0.5f) / 16f, (y + 0.5f) / 16f) : new Color(0.55f, 0.35f, 0.2f);
                                c = new Color(c.r * 0.62f, c.g * 0.4f, c.b * 0.25f, 1f);           // leather brown
                                bool edge = x == 2 || x == 13 || y == 1 || y == 14 || y == 9;
                                if (edge && border != null) c = border.GetPixelBilinear((x + 0.5f) / 16f, (y + 0.5f) / 16f) * 0.8f;
                                if ((y == 9 || y == 10) && (x == 5 || x == 6 || x == 9 || x == 10) && clips != null) c = clips.GetPixelBilinear(0.5f, 0.5f);
                                c.a = 1f;
                                px[y * 16 + x] = c;
                            }
                    }
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] backpack icon: " + ex.Message); }
            icon.SetPixels32(px);
            icon.Apply();
            return icon;
        }
    }
}
