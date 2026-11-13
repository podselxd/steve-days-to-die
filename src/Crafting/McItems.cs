using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// Minecraft's items exist in 7 Days as items named mc_&lt;id&gt; (Config/items.xml, generated). Their icons and
    /// names are written here, at game start, from the player's OWN Minecraft, into the mod's folder:
    /// UIAtlases/ItemIconAtlas/mc_*.png and Config/Localization.csv. The game reads both after mods start,
    /// so they are in place in time. Nothing of Minecraft is shipped with the mod.
    /// </summary>
    public static class McItems
    {
        public const string Prefix = "mc_";
        static Dictionary<string, string> _kind;

        public const int ToolQuality = 3;

        /// <summary>Tools made before 1.8.5 came out with quality 0 (useless): give them a quality when held.</summary>
        public static void FixHeld(EntityPlayerLocal p)
        {
            try
            {
                int slot = p.inventory.holdingItemIdx;
                var st = p.inventory.GetItem(slot);
                if (st == null || st.IsEmpty() || !IsMc(st.itemValue.ItemClass?.Name)) return;
                if (!st.itemValue.ItemClass.HasQuality || st.itemValue.Quality > 0) return;
                var fixedStack = new ItemStack(new ItemValue(st.itemValue.type, ToolQuality, ToolQuality, false), st.count);
                p.inventory.SetItem(slot, fixedStack);
            }
            catch { }
        }

        public static bool IsMc(string itemName) => itemName != null && itemName.StartsWith(Prefix);
        public static string IdOf(string itemName) => IsMc(itemName) ? itemName.Substring(Prefix.Length) : null;

        public static string Kind(string id)
        {
            if (_kind == null)
            {
                _kind = new Dictionary<string, string>();
                foreach (var r in McItemsTable.Items) _kind[r.id] = r.kind;
            }
            return id != null && _kind.TryGetValue(id, out var k) ? k : null;
        }

        /// <summary>A stack of the 7 Days item that IS this Minecraft item (mc_&lt;id&gt;), or null.</summary>
        public static ItemStack Stack(string mcId, int count)
        {
            if (string.IsNullOrEmpty(mcId) || Kind(mcId) == null) return null;
            var iv = ItemClass.GetItem(Prefix + mcId);
            if (iv == null || iv.IsEmpty()) return null;
            // Minecraft tools are built on 7 Days tools, which have a quality: 0 means broken (they did not dig)
            if (iv.ItemClass != null && iv.ItemClass.HasQuality) iv = new ItemValue(iv.type, ToolQuality, ToolQuality, false);
            return new ItemStack(iv, count);
        }

        /// <summary>Writes the icons and names (once; existing icons are kept).</summary>
        public static void Prepare(string modPath, string mcPath, string mcVersion)
        {
            if (!Ryan.Mc.Ready || string.IsNullOrEmpty(modPath)) return;
            var t0 = Time.realtimeSinceStartup;
            int made = 0, failed = 0;
            try
            {
                string dir = Path.Combine(modPath, "UIAtlases", "ItemIconAtlas");
                Directory.CreateDirectory(dir);
                foreach (var r in McItemsTable.Items)
                {
                    string file = Path.Combine(dir, Prefix + r.id + ".png");
                    // icons are 128 px at most: 1,400 icons of 640 px overflowed 7 Days' icon atlas (its own icons broke)
                    if (File.Exists(file) && PngWidth(file) <= 160) continue;
                    try
                    {
                        var icon = r.id.StartsWith("sb_") ? McInv.Backpacks.Icon(r.id) : Ryan.Icons.Get(r.id);
                        if (icon == null) { failed++; continue; }
                        int k = Mathf.Max(1, 128 / Mathf.Max(1, icon.width));
                        File.WriteAllBytes(file, Upscale(icon, k).EncodeToPNG());
                        made++;
                    }
                    catch { failed++; }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] Minecraft item icons: " + ex.Message); }

            try { WriteNames(modPath, mcPath, mcVersion); }
            catch (Exception ex) { Debug.LogWarning("[SVD] Minecraft item names: " + ex.Message); }
            Debug.Log($"[SVD] Minecraft items: {McItemsTable.Items.Length} ({made} new icons, {failed} without icon) in {Time.realtimeSinceStartup - t0:0.0}s");
        }

        static int PngWidth(string file)
        {
            try
            {
                using (var f = File.OpenRead(file))
                {
                    var b = new byte[24];
                    if (f.Read(b, 0, 24) < 24) return 0;
                    return (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                }
            }
            catch { return 0; }
        }

        // the game's icons are 160 px; Minecraft's are 16: keep the pixels sharp
        static Texture2D Upscale(Texture2D src, int k)
        {
            int w = src.width, h = src.height;
            var px = src.GetPixels32();
            var dst = new Texture2D(w * k, h * k, TextureFormat.RGBA32, false);
            var o = new Color32[w * k * h * k];
            for (int y = 0; y < h * k; y++)
                for (int x = 0; x < w * k; x++)
                    o[y * w * k + x] = px[(y / k) * w + x / k];
            dst.SetPixels32(o);
            dst.Apply();
            return dst;
        }

        static void WriteNames(string modPath, string mcPath, string mcVersion)
        {
            var en = Ryan.Mc.Json("assets/minecraft/lang/en_us.json");
            var es = SpanishLang(mcPath, mcVersion);
            var sb = new StringBuilder();
            sb.AppendLine("Key,File,Type,UsedInMainMenu,NoTranslate,english,spanish");
            sb.AppendLine("mcItemDesc,items,Item,,,\"Minecraft item: made and used with Minecraft's crafting (inventory, crafting table, furnace).\",\"Objeto de Minecraft: se fabrica y se usa con el crafteo de Minecraft (inventario, mesa de crafteo, horno).\"");
            foreach (var r in McItemsTable.Items)
            {
                string e = Lang(en, r.id) ?? r.id, s = Lang(es, r.id) ?? e;
                sb.Append(Prefix).Append(r.id).Append(",items,Item,,,").Append(Csv(e)).Append(',').Append(Csv(s)).AppendLine();
            }
            string cfg = Path.Combine(modPath, "Config");
            Directory.CreateDirectory(cfg);
            File.WriteAllText(Path.Combine(cfg, "Localization.csv"), sb.ToString(), new UTF8Encoding(false));
        }

        static string Lang(JObject lang, string id)
        {
            if (lang == null) return null;
            return (string)lang["item.minecraft." + id] ?? (string)lang["block.minecraft." + id];
        }

        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

        // Spanish lives in the launcher's asset store, not in the jar (es_mx, else es_es)
        static JObject SpanishLang(string mcPath, string mcVersion)
        {
            try
            {
                var meta = JObject.Parse(File.ReadAllText(Path.Combine(mcPath, "versions", mcVersion, mcVersion + ".json")));
                string idx = (string)meta["assets"] ?? (string)meta["assetIndex"]?["id"];
                var objects = JObject.Parse(File.ReadAllText(Path.Combine(mcPath, "assets", "indexes", idx + ".json")))["objects"];
                foreach (var lang in new[] { "es_mx", "es_es" })
                {
                    string hash = (string)objects?["minecraft/lang/" + lang + ".json"]?["hash"];
                    if (hash == null) continue;
                    string p = Path.Combine(mcPath, "assets", "objects", hash.Substring(0, 2), hash);
                    if (File.Exists(p)) return JObject.Parse(File.ReadAllText(p, Encoding.UTF8));
                }
            }
            catch { }
            return null;
        }
    }
}
