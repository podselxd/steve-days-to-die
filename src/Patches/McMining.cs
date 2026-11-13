using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Minecraft blocks break in Minecraft's time (user 2026-11-13: "talo tablon de madera de mc con hacha de mc y la
    /// tala lento... y asi con todos"). Each block has its own hardness and right tool (sheets/mc_blocks*.json). Minecraft:
    /// seconds = hardness x (1.5 if the tool can harvest it, else 5) / speed, speed = the tool material's speed (wood 2,
    /// stone 4, copper 5, iron 6, diamond 8, netherite 9, gold 12) when it is the right kind of tool, else 1.
    /// Each swing takes its share of the block's 7 Days health: hits = seconds / SwingSeconds.
    /// </summary>
    public static class McMining
    {
        const float SwingSeconds = 0.8f;      // about one swing of our attack bar
        static readonly Dictionary<string, Ryan.Sheets.BlockRow> _rows = new Dictionary<string, Ryan.Sheets.BlockRow>();
        static readonly Dictionary<string, float> Speed = new Dictionary<string, float>
        { { "wooden", 2f }, { "stone", 4f }, { "copper", 5f }, { "iron", 6f }, { "diamond", 8f }, { "netherite", 9f }, { "golden", 12f } };
        static readonly Dictionary<string, int> Level = new Dictionary<string, int>
        { { "wooden", 0 }, { "golden", 0 }, { "stone", 1 }, { "copper", 1 }, { "iron", 2 }, { "diamond", 3 }, { "netherite", 4 } };

        static Ryan.Sheets.BlockRow? Row(string blockName)
        {
            if (_rows.Count == 0) foreach (var r in Ryan.Sheets.Blocks) _rows[r.block7d] = r;
            return blockName != null && _rows.TryGetValue(blockName, out var row) ? row : (Ryan.Sheets.BlockRow?)null;
        }

        /// <summary>The held tool as Minecraft sees it: kind (pickaxe, axe, shovel, hoe, sword or none) and material.</summary>
        public static (string kind, string mat) Tool(EntityAlive who)
        {
            string n = who?.inventory?.holdingItem?.Name ?? "";
            if (n.StartsWith("mc_"))
            {
                foreach (var kind in new[] { "pickaxe", "axe", "shovel", "hoe", "sword" })
                    if (n.EndsWith("_" + kind)) return (kind, n.Substring(3, n.Length - 3 - kind.Length - 1));
                return ("none", "");
            }
            // 7 Days tools: T0 stone, T1 iron, T2 diamond (steel), T3 netherite
            string mat = n.Contains("T0") ? "stone" : n.Contains("T1") ? "iron" : n.Contains("T2") ? "diamond" : n.Contains("T3") ? "netherite" : "stone";
            if (n.Contains("Pick") || n.Contains("Auger")) return ("pickaxe", mat);
            if (n.Contains("Axe") || n.Contains("Fireaxe") || n.Contains("Chainsaw")) return ("axe", mat);
            if (n.Contains("Shovel")) return ("shovel", mat);
            return ("none", "");
        }

        /// <summary>Damage of one swing on a Minecraft block, or -1 when it is not one.</summary>
        public static int SwingDamage(BlockValue bv, EntityAlive who)
        {
            var b = bv.Block;
            var row = Row(b?.GetBlockName());
            if (row == null) return -1;
            var r = row.Value;
            var (kind, mat) = Tool(who);
            bool right = r.tool == kind || (r.tool == "hoe" && kind == "sword");
            float speed = right && Speed.TryGetValue(mat, out var sp) ? sp : 1f;
            bool needsTool = r.minTier >= 0;
            bool canHarvest = !needsTool || (right && Level.TryGetValue(mat, out var lv) && lv >= r.minTier);
            float seconds = r.hardness <= 0f ? 0f : r.hardness * (canHarvest ? 1.5f : 5f) / speed;
            int hits = Mathf.Max(1, Mathf.CeilToInt(seconds / SwingSeconds));
            return Mathf.CeilToInt((float)b.MaxDamage / hits) + 1;
        }

        [HarmonyPatch(typeof(Block), "DamageBlock")]
        static class DamagePatch
        {
            static void Prefix(BlockValueRef _blockValueRef, BlockValue _blockValue, ref int _damagePoints, int _entityIdThatDamaged)
            {
                try
                {
                    if (_damagePoints <= 0 || _entityIdThatDamaged < 0) return;
                    var ent = GameManager.Instance?.World?.GetEntity(_entityIdThatDamaged);
                    // 7 Days' zombies cannot break Minecraft blocks (user 2026-11-13), day or night
                    if (ent is EntityEnemy && _blockValue.Block?.GetBlockName()?.StartsWith("mc_") == true) { _damagePoints = 0; return; }
                    var who = ent as EntityPlayer;
                    if (who == null) return;
                    int d = SwingDamage(_blockValue, who);
                    if (d > 0)
                    {
                        _damagePoints = d;
                        // Minecraft's sounds: a soft step sound while digging, the dig sound when it breaks
                        var row = Row(_blockValue.Block.GetBlockName());
                        string g = row?.sound;
                        if (string.IsNullOrEmpty(g) || g == "none") g = "stone";
                        Vector3 at = _blockValueRef.BlockPosition.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f);
                        bool breaks = _blockValue.damage + d >= _blockValue.Block.MaxDamage;
                        if (breaks) Sound.MinecraftSounds.PlayGroup("dig/" + g, at, 16f, 1f, 0.8f);
                        else Sound.MinecraftSounds.PlayGroup("step/" + g, at, 8f, 0.25f, 0.5f);
                    }
                }
                catch { }
            }
        }
    }
}
