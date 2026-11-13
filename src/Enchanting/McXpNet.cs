using System;
using UnityEngine;

namespace SteveDaysToDie.Enchanting
{
    /// <summary>
    /// Experience for whoever earned it (bug review 2026-11-13: friends got none for kills and ores). Kills and broken
    /// blocks are decided on the server; it gives the points to its own player directly and sends them to a friend's
    /// game with NetPackageMcXp (the cvar is kept by that player's own game).
    /// </summary>
    public static class McXpNet
    {
        public static void Grant(EntityPlayer p, int points)
        {
            if (p == null || points <= 0) return;
            if (p is EntityPlayerLocal) { McXp.Add(p, points); return; }
            try
            {
                var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
                if (cm != null && cm.IsServer) cm.SendPackage(NetPackageManager.GetPackage<NetPackageMcXp>().Setup(points), false, p.entityId);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] xp send: " + ex.Message); }
        }
    }
}

public class NetPackageMcXp : NetPackage
{
    int _points;

    public NetPackageMcXp Setup(int points) { _points = points; return this; }

    public override void read(PooledBinaryReader _br) { _points = _br.ReadInt32(); }

    public override void write(PooledBinaryWriter _bw)
    {
        base.write(_bw);
        System.IO.BinaryWriter w = _bw;
        w.Write(_points);
    }


    public override void ProcessPackage(World _world, GameManager _callbacks)
    {
        try
        {
            var me = _world?.GetPrimaryPlayer();
            if (me != null && _points > 0 && _points < 10000) SteveDaysToDie.Enchanting.McXp.Add(me, _points);
        }
        catch (Exception ex) { Debug.LogWarning("[SVD] xp package: " + ex.Message); }
    }
}
