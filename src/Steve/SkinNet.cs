using System;
using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Steve
{
    /// <summary>
    /// Multiplayer skins: each player sends their own skin (a 64x64 PNG, a few KB) when they enter the world; the
    /// host keeps everyone's and passes them on, so every player sees every other one with their own skin.
    /// Until a player's skin arrives they are drawn as Minecraft's plain Steve (never with OUR skin).
    /// </summary>
    public static class SkinNet
    {
        static readonly Dictionary<int, byte[]> _png = new Dictionary<int, byte[]>();
        static readonly Dictionary<int, Texture2D> _tex = new Dictionary<int, Texture2D>();
        static Texture2D _steve;
        static int _sentFor = -1;

        /// <summary>The skin to draw another player with: theirs if it arrived, else plain Steve.</summary>
        public static Texture2D SkinOf(int entityId)
        {
            if (_tex.TryGetValue(entityId, out var t) && t != null) return t;
            if (_steve == null)
            {
                _steve = Skins.SkinLibrary.SteveTexture();
                if (_steve != null) _steve.filterMode = FilterMode.Point;
            }
            return _steve;
        }

        /// <summary>Every frame for the local player: send our skin once per world (multiplayer only).</summary>
        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || p.entityId == _sentFor) return;
            try
            {
                var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
                if (cm == null || cm.IsSinglePlayer) return;
                byte[] png = OwnPng();
                _sentFor = p.entityId;
                if (png == null) return;
                if (cm.IsServer)
                {
                    Store(p.entityId, png);
                    cm.SendPackage(NetPackageManager.GetPackage<NetPackageSteveSkin>().Setup(p.entityId, png));
                }
                else cm.SendToServer(NetPackageManager.GetPackage<NetPackageSteveSkin>().Setup(p.entityId, png));
                Debug.Log($"[SVD] skin sent ({png.Length} bytes)");
            }
            catch (Exception ex) { _sentFor = p.entityId; Debug.LogWarning("[SVD] skin send: " + ex.Message); }
        }

        /// <summary>Our skin changed while playing: send it again.</summary>
        public static void Resend() { _sentFor = -1; }

        static byte[] OwnPng()
        {
            var t = SteveCharacter.SkinTexture;
            if (t == null) return null;
            try { return t.EncodeToPNG(); }
            catch
            {
                // not readable: copy it through a render texture
                var rt = RenderTexture.GetTemporary(t.width, t.height, 0, RenderTextureFormat.ARGB32);
                var prev = RenderTexture.active;
                Graphics.Blit(t, rt);
                RenderTexture.active = rt;
                var c = new Texture2D(t.width, t.height, TextureFormat.RGBA32, false);
                c.ReadPixels(new Rect(0, 0, t.width, t.height), 0, 0);
                c.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                return c.EncodeToPNG();
            }
        }

        public static void Store(int entityId, byte[] png)
        {
            if (png == null || png.Length == 0 || png.Length > 200000) return;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(png) || t.width != 64 || (t.height != 64 && t.height != 32)) { UnityEngine.Object.Destroy(t); return; }
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            _png[entityId] = png;
            if (_tex.TryGetValue(entityId, out var old) && old != null) UnityEngine.Object.Destroy(old);
            _tex[entityId] = t;      // SteveModel sees the new texture and rebuilds that player
            Debug.Log($"[SVD] skin received for player {entityId} ({t.width}x{t.height})");
        }

        /// <summary>Host: every skin it knows, for a player who just sent theirs.</summary>
        public static void SendAllTo(ClientInfo to)
        {
            if (to == null) return;
            foreach (var kv in _png)
                if (to.entityId != kv.Key)
                    to.SendPackage(NetPackageManager.GetPackage<NetPackageSteveSkin>().Setup(kv.Key, kv.Value));
        }
    }
}

/// <summary>One player's skin over the network (7 Days finds every NetPackage class by itself).</summary>
public class NetPackageSteveSkin : NetPackage
{
    int _entityId;
    byte[] _png;

    public override bool Compress => true;

    public NetPackageSteveSkin Setup(int entityId, byte[] png)
    {
        _entityId = entityId;
        _png = png;
        return this;
    }

    public override void read(PooledBinaryReader _br)
    {
        _entityId = _br.ReadInt32();
        int n = _br.ReadInt32();
        _png = n > 0 && n <= 200000 ? _br.ReadBytes(n) : new byte[0];
    }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        System.IO.BinaryWriter w = _bw;      // the plain writer's overloads (the game's Span ones need System.Memory)
        w.Write(_entityId);
        w.Write(_png != null ? _png.Length : 0);
        if (_png != null) w.Write(_png);
    }


    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        try
        {
            SteveDaysToDie.Steve.SkinNet.Store(_entityId, _png);
            var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm != null && cm.IsServer && Sender != null)
            {
                // pass it on to everyone else, and give the newcomer everybody's skins (the host's too)
                cm.SendPackage(NetPackageManager.GetPackage<NetPackageSteveSkin>().Setup(_entityId, _png), false, -1, _entityId);
                SteveDaysToDie.Steve.SkinNet.SendAllTo(Sender);
            }
        }
        catch (Exception ex) { Debug.LogWarning("[SVD] skin package: " + ex.Message); }
    }
}
