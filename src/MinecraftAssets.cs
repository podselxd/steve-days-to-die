using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace SteveDaysToDie
{
    public static class MinecraftAssets
    {
        static string JarPath;

        public static bool Init(string mcPath, string version)
        {
            JarPath = Path.Combine(mcPath, "versions", version, version + ".jar");
            if (!File.Exists(JarPath))
            {
                Debug.LogWarning($"[SVD] MC jar not found: {JarPath}");
                return false;
            }
            Debug.Log($"[SVD] MC jar: {JarPath}");
            return true;
        }

        public static byte[] ReadAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(JarPath) || !File.Exists(JarPath)) return null;
            using (var fs = File.OpenRead(JarPath))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
            {
                var entry = zip.GetEntry(assetPath);
                if (entry == null) return null;
                using (var s = entry.Open())
                {
                    // a compressed stream may hand its bytes over in several reads
                    var buf = new byte[entry.Length];
                    int got = 0;
                    while (got < buf.Length)
                    {
                        int n = s.Read(buf, got, buf.Length - got);
                        if (n <= 0) break;
                        got += n;
                    }
                    return got == buf.Length ? buf : null;
                }
            }
        }

        public static Texture2D LoadTexture(string assetPath)
        {
            var bytes = ReadAsset(assetPath);
            if (bytes == null) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            if (!tex.LoadImage(bytes)) return null;
            return tex;
        }

        public static Texture2D LoadSteveDefaultSkin()
            => LoadTexture("assets/minecraft/textures/entity/player/wide/steve.png");

        public static Texture2D LoadPlayerSkin(string mcPath)
        {
            var skinsDir = Path.Combine(mcPath, "assets", "skins");
            if (!Directory.Exists(skinsDir)) return null;
            FileInfo newest = null;
            foreach (var f in Directory.GetFiles(skinsDir, "*", SearchOption.AllDirectories))
            {
                var fi = new FileInfo(f);
                if (newest == null || fi.LastWriteTimeUtc > newest.LastWriteTimeUtc)
                    newest = fi;
            }
            if (newest == null) return null;
            var bytes = File.ReadAllBytes(newest.FullName);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            return tex.LoadImage(bytes) ? tex : null;
        }
    }
}
