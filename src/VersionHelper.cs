using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie
{
    public static class VersionHelper
    {
        public const string MIN_MC_VERSION  = "26.1";
        public const string LAST_TESTED_VERSION = "26.1";

        public enum VersionStatus { OK, TooOld, NewerThanTested }

        public static VersionStatus Check(string version, out string message)
        {
            if (string.IsNullOrEmpty(version))
            {
                message = $"[SVD] Could not detect Minecraft version. Min required: {MIN_MC_VERSION}.";
                return VersionStatus.TooOld;
            }

            int cmpMin = CompareVersions(version, MIN_MC_VERSION);
            if (cmpMin < 0)
            {
                message = $"[SVD] Minecraft {version} is too old. Minimum required: {MIN_MC_VERSION}. Update Minecraft and try again.";
                return VersionStatus.TooOld;
            }

            int cmpTested = CompareVersions(version, LAST_TESTED_VERSION);
            if (cmpTested > 0)
            {
                message = $"[SVD] Minecraft {version} is newer than last tested version ({LAST_TESTED_VERSION}). Some features may not work correctly. Proceeding anyway.";
                return VersionStatus.NewerThanTested;
            }

            message = $"[SVD] Minecraft {version} OK.";
            return VersionStatus.OK;
        }

        // Finds the newest installed version >= MIN_MC_VERSION.
        // Falls back to newest available version if none meets minimum.
        public static string DetectBestVersion(string mcPath)
        {
            var versionsDir = Path.Combine(mcPath, "versions");
            if (!Directory.Exists(versionsDir)) return null;

            var candidates = new List<string>();
            foreach (var dir in Directory.GetDirectories(versionsDir))
            {
                var name = Path.GetFileName(dir);
                var jar  = Path.Combine(dir, name + ".jar");
                if (File.Exists(jar)) candidates.Add(name);
            }

            if (candidates.Count == 0) return null;

            // Sort descending so newest is first
            candidates.Sort((a, b) => CompareVersions(b, a));

            // Prefer newest that meets minimum
            foreach (var v in candidates)
                if (CompareVersions(v, MIN_MC_VERSION) >= 0) return v;

            // Nothing meets minimum — return newest anyway (will be rejected by Check())
            return candidates[0];
        }

        // Returns -1, 0, or 1. Handles both "26.1" and legacy "1.21.11" formats.
        // Snapshots like "24w45a" sort before any release.
        public static int CompareVersions(string a, string b)
        {
            var pa = ParseVersion(a);
            var pb = ParseVersion(b);
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int va = i < pa.Length ? pa[i] : 0;
                int vb = i < pb.Length ? pb[i] : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }

        // "26.1" → [26,1]; "1.21.11" → [1,21,11]; snapshots → [0]
        static int[] ParseVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return new[] { 0 };
            // Snapshot pattern like "24w45a" — treat as 0 (too old)
            if (v.Length > 2 && char.IsDigit(v[0]) && v[2] == 'w')
                return new[] { 0 };
            var parts = v.Split('.');
            var result = new List<int>();
            foreach (var p in parts)
            {
                if (int.TryParse(p, out int n)) result.Add(n);
                else result.Add(0);
            }
            return result.ToArray();
        }
    }
}
