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
                    if (File.Exists(file)) continue;
                    try
                    {
                        var icon = Ryan.Icons.Get(r.id);
                        if (icon == null) { failed++; continue; }
                        File.WriteAllBytes(file, Upscale(icon, 10).EncodeToPNG());
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
