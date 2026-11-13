using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace SteveDaysToDie.Sound
{
    /// <summary>
    /// Minecraft's player sounds, read from the player's own Minecraft (its assets index points to
    /// the files; nothing is copied). Steve makes no voice sounds of his own: the 7 Days grunts for
    /// jumping, landing, panting and pain are silenced, and when health is damaged (or Steve dies)
    /// Minecraft's "hit" sound plays instead, as in Minecraft.
    /// </summary>
    public class MinecraftSounds : MonoBehaviour
    {
        static readonly List<AudioClip> _hurt = new List<AudioClip>();
        static MinecraftSounds _instance;
        AudioSource _flat;

        public static int HurtLoaded => _hurt.Count;
        public static bool Enabled => Settings.GetBool("sonidos_minecraft", true);

        public static void Init(string mcPath, string version)
        {
            if (_instance != null || string.IsNullOrEmpty(mcPath) || string.IsNullOrEmpty(version)) return;
            var go = new GameObject("SVD_MinecraftSounds");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MinecraftSounds>();
            _instance._flat = go.AddComponent<AudioSource>();
            _instance._flat.spatialBlend = 0f;              // the player's own hurt sound: heard "in the head"
            _instance._flat.playOnAwake = false;
            _instance.StartCoroutine(_instance.Load(mcPath, version));
        }

        IEnumerator Load(string mc, string version)
        {
            string index = FindIndex(mc, version);
            if (index == null) { Debug.LogWarning("[SVD] Minecraft sounds: no assets index for " + version + " (start that version once in the launcher)."); yield break; }
            string json = null;
            try { json = File.ReadAllText(index); } catch (Exception ex) { Debug.LogWarning("[SVD] Minecraft sounds: " + ex.Message); }
            if (json == null) yield break;

            for (int i = 1; i <= 3; i++)
            {
                var m = Regex.Match(json, "\"minecraft/sounds/damage/hit" + i + "\\.ogg\"\\s*:\\s*\\{\\s*\"hash\"\\s*:\\s*\"([0-9a-f]{40})\"");
                if (!m.Success) continue;
                string h = m.Groups[1].Value;
                string path = Path.Combine(mc, "assets", "objects", h.Substring(0, 2), h);
                if (!File.Exists(path)) continue;
                using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(req);
                        if (clip != null) { clip.name = "mc_hit" + i; _hurt.Add(clip); }
                    }
                    else Debug.LogWarning("[SVD] Minecraft sound hit" + i + ": " + req.error);
                }
            }
            Debug.Log("[SVD] Minecraft hurt sounds loaded: " + _hurt.Count + "/3");
            // other Minecraft sounds (creeper fuse, explosion, creeper hurt/death), by their path under sounds/
            var wanted = new List<string> { "random/fuse", "random/explode1", "random/explode2", "random/explode3", "random/explode4",
                                         "mob/creeper/say1", "mob/creeper/say2", "mob/creeper/say3", "mob/creeper/say4", "mob/creeper/death",
                                         "mob/pig/death", "mob/sheep/say1", "mob/sheep/say2", "mob/sheep/say3" };
            foreach (var g in new[] { "stone", "wood", "gravel", "grass", "sand", "cloth", "snow" })
                for (int i = 1; i <= 4; i++) { wanted.Add("dig/" + g + i); wanted.Add("step/" + g + i); }
            for (int i = 1; i <= 4; i++) { wanted.Add("mob/cow/say" + i); wanted.Add("mob/cow/hurt" + i); wanted.Add("mob/pig/say" + i); wanted.Add("mob/chicken/say" + i); wanted.Add("mob/chicken/hurt" + i); }
            foreach (var name in wanted)
            {
                var m = Regex.Match(json, "\"minecraft/sounds/" + Regex.Escape(name) + @"\.ogg""\s*:\s*\{\s*""hash""\s*:\s*""([0-9a-f]{40})""");
                if (!m.Success) continue;
                string h = m.Groups[1].Value;
                string path = Path.Combine(mc, "assets", "objects", h.Substring(0, 2), h);
                if (!File.Exists(path)) continue;
                using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.OGGVORBIS))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(req);
                        if (clip != null) { clip.name = "mc_" + name; _clips[name] = clip; }
                    }
                }
            }
            Debug.Log("[SVD] Minecraft sounds loaded: " + _clips.Count);
        }

        static string FindIndex(string mc, string version)
        {
            try
            {
                string vj = Path.Combine(mc, "versions", version, version + ".json");
                if (!File.Exists(vj)) return null;
                string j = File.ReadAllText(vj);
                var m = Regex.Match(j, "\"assetIndex\"\\s*:\\s*\\{[^}]*?\"id\"\\s*:\\s*\"([^\"]+)\"");
                string id = m.Success ? m.Groups[1].Value : null;
                if (id == null)
                {
                    var m2 = Regex.Match(j, "\"assets\"\\s*:\\s*\"([^\"]+)\"");
                    if (m2.Success) id = m2.Groups[1].Value;
                }
                if (id == null) return null;
                string p = Path.Combine(mc, "assets", "indexes", id + ".json");
                return File.Exists(p) ? p : null;
            }
            catch { return null; }
        }

        // the game's overall volume slider, so Steve is not louder than everything else
        static float Volume()
        {
            try { return Mathf.Clamp01(GamePrefs.GetFloat(EnumGamePrefs.OptionsOverallAudioVolumeLevel)); }
            catch { return 1f; }
        }

        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        static readonly Dictionary<string, List<string>> _groups = new Dictionary<string, List<string>>();

        /// <summary>One of a group's sounds at random (group = path without the number, e.g. "dig/stone", "mob/cow/say").</summary>
        public static void PlayGroup(string group, Vector3 pos, float maxDistance = 16f, float volume = 1f, float pitch = 1f)
        {
            if (!_groups.TryGetValue(group, out var list))
            {
                list = new List<string>();
                foreach (var k in _clips.Keys)
                    if (k.StartsWith(group) && (k.Length == group.Length || char.IsDigit(k[group.Length]))) list.Add(k);
                if (_clips.Count > 0) _groups[group] = list;
            }
            if (list.Count == 0) return;
            PlayAt(list[UnityEngine.Random.Range(0, list.Count)], pos, maxDistance, volume, pitch);
        }

        /// <summary>A Minecraft sound (path under sounds/, e.g. "random/fuse") heard from a place in the world.</summary>
        public static void PlayAt(string name, Vector3 pos, float maxDistance = 16f, float volume = 1f, float pitch = 1f)
        {
            if (_instance == null || !_clips.TryGetValue(name, out var clip) || clip == null) return;
            var go = new GameObject("SVD_sound");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.minDistance = 1f;
            s.maxDistance = maxDistance;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.pitch = pitch;
            s.volume = Volume() * volume;
            s.clip = clip;
            s.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
        }

        public static void PlayHurt(Entity e)
        {
            if (_instance == null || _hurt.Count == 0 || e == null) return;
            var clip = _hurt[UnityEngine.Random.Range(0, _hurt.Count)];
            float pitch = (UnityEngine.Random.value - UnityEngine.Random.value) * 0.2f + 1f;     // Minecraft's own variation
            var local = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
            if (e == local)
            {
                _instance._flat.pitch = pitch;
                _instance._flat.PlayOneShot(clip, Volume());
                return;
            }
            // another player: from where they are
            var go = new GameObject("SVD_hurt");
            go.transform.position = e.transform.position + Vector3.up * 1.5f;
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.minDistance = 1f;
            s.maxDistance = 16f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.pitch = pitch;
            s.volume = Volume();
            s.clip = clip;
            s.Play();
            Destroy(go, clip.length / pitch + 0.2f);
        }

        /// <summary>Which 7 Days player sounds are a voice: pain and death become Minecraft's hit; the rest go quiet.</summary>
        public static bool IsVoice(string clipName, out bool isHurt)
        {
            isHurt = false;
            if (string.IsNullOrEmpty(clipName)) return false;
            string n = clipName.ToLowerInvariant();
            if (!(n.StartsWith("player1") || n.StartsWith("player2"))) return false;    // "PlayerLandThump" etc. are not voices
            isHurt = n.Contains("pain") || n.Contains("death");
            return isHurt || n.Contains("jump") || n.Contains("land") || n.Contains("stamina") || n.Contains("drownsurface");
        }

        [HarmonyPatch(typeof(Entity), "PlayOneShot")]
        static class VoicePatch
        {
            static bool Prefix(Entity __instance, string clipName)
            {
                try
                {
                    if (!Enabled || !(__instance is EntityPlayer)) return true;
                    if (!IsVoice(clipName, out bool hurt)) return true;
                    if (hurt) PlayHurt(__instance);
                    return false;       // the 7 Days voice does not play
                }
                catch { return true; }
            }
        }
    }
}
