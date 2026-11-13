using UnityEngine;

/// <summary>
/// Minecraft's bed: 7 Days' bedroll inside (placing it sets your respawn point, saved and shared), Minecraft's look
/// drawn by BlockOverlay ("bed" rows), and sleeping: use it at night and, when every player is in bed, the night is
/// skipped to the morning (Minecraft's rule). Sleeping also resets the nights-without-sleep count of the phantoms.
/// (Config/blocks.xml: Class="McBed, SteveDaysToDie" - 7 Days adds the "Block" prefix.)
/// </summary>
public class BlockMcBed : BlockSleepingBag
{
    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_player == null) return false;
        var world = GameManager.Instance.World;
        if (world.IsDaytime())
        {
            GameManager.ShowTooltip(_player, "Solo puedes dormir de noche.");
            return true;
        }
        // in bed: the host checks every player (SteveDaysToDie.Crafting.McBeds.Tick)
        _player.Buffs.SetCustomVar("mcSleepingAt", world.worldTime);
        _player.Buffs.SetCustomVar("mcAwakeSince", world.worldTime);
        GameManager.ShowTooltip(_player, "Durmiendo... (todos los jugadores deben estar en la cama)");
        return true;
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        try { foreach (var r in _ebcd.transform.GetComponentsInChildren<Renderer>(true)) r.enabled = false; } catch { }   // the bedroll: Minecraft's bed is drawn instead
    }
}

namespace SteveDaysToDie.Crafting
{
    public static class McBeds
    {
        static float _next;

        /// <summary>Host, every second: everybody slept in the last 5 in-game minutes at night -> morning.</summary>
        public static void Tick()
        {
            if (Time.time < _next) return;
            _next = Time.time + 1f;
            var world = GameManager.Instance?.World;
            if (world == null || world.IsDaytime()) return;
            var cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm != null && !cm.IsServer) return;
            int players = 0, asleep = 0;
            foreach (var pl in world.Players.list)
            {
                if (pl == null || !pl.IsAlive()) continue;
                players++;
                float at = pl.Buffs.GetCustomVar("mcSleepingAt");
                if (at > 0f && world.worldTime >= (ulong)at && world.worldTime - (ulong)at < 300UL) asleep++;
            }
            if (players == 0 || asleep < players) return;
            // the next morning (6:00), like waking up in Minecraft
            ulong day = world.worldTime / 24000UL + (world.worldTime % 24000UL >= 6000UL ? 1UL : 0UL);
            try { SdtdConsole.Instance.ExecuteSync("settime " + (day + 1) + " 6 0", null); } catch (System.Exception ex) { Debug.LogWarning("[SVD] beds: " + ex.Message); }
            foreach (var pl in world.Players.list) if (pl != null) { pl.Buffs.SetCustomVar("mcSleepingAt", 0f); pl.Buffs.SetCustomVar("mcAwakeSince", world.worldTime); }
            Debug.Log("[SVD] beds: everybody slept - morning");
        }
    }
}
