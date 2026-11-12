using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Minecraft's pickaxe levels (sheets/harvest_tiers.json): a rock or ore whose level your pickaxe does not
    /// reach breaks slower and gives nothing, like iron ore with a wooden pickaxe. Everything else (wood, dirt,
    /// car metal with the wrench...) keeps 7 Days' rules. Setting picos_minecraft=no turns it off.
    /// </summary>
    public static class HarvestTiers
    {
        public static bool Enabled => Settings.GetBool("picos_minecraft", true);

        static Regex[] _blockRx, _matRx, _toolRx;
        static readonly Dictionary<int, int> _need = new Dictionary<int, int>();       // block type -> min level (-1 = no rule)
        static readonly Dictionary<string, int> _tier = new Dictionary<string, int>();  // item name -> pickaxe level (-1 = not a pickaxe)

        static void Build()
        {
            if (_blockRx != null) return;
            var b = HarvestTable.Blocks;
            _blockRx = new Regex[b.Length]; _matRx = new Regex[b.Length];
            for (int i = 0; i < b.Length; i++)
            {
                _blockRx[i] = b[i].block == "" ? null : new Regex(b[i].block);
                _matRx[i] = b[i].material == "" ? null : new Regex(b[i].material);
            }
            _toolRx = new Regex[HarvestTable.Tools.Length];
            for (int i = 0; i < _toolRx.Length; i++) _toolRx[i] = new Regex(HarvestTable.Tools[i].item);
        }

        /// <summary>The pickaxe level a block needs, or -1 when Minecraft's rule does not apply to it.</summary>
        public static int Needed(BlockValue bv)
        {
            if (bv.isair) return -1;
            if (_need.TryGetValue(bv.type, out int n)) return n;
            Build();
            n = -1;
            try
            {
                Block block = bv.Block;
                string name = block.GetBlockName();
                string mat = block.blockMaterial != null ? block.blockMaterial.id : "";
                // the block's own name first (ores are made of stone too), then its material
                for (int i = 0; i < _blockRx.Length && n < 0; i++)
                    if (_blockRx[i] != null && _blockRx[i].IsMatch(name)) n = HarvestTable.Blocks[i].minTier;
                for (int i = 0; i < _matRx.Length && n < 0; i++)
                    if (_matRx[i] != null && _matRx[i].IsMatch(mat)) n = HarvestTable.Blocks[i].minTier;
            }
            catch { n = -1; }
            _need[bv.type] = n;
            return n;
        }

        /// <summary>The pickaxe level of what the entity holds (its 7 Days name or its Minecraft pair), -1 if none.</summary>
        public static int ToolTier(EntityAlive who)
        {
            string name = who?.inventory?.holdingItem?.Name;
            if (string.IsNullOrEmpty(name)) return -1;
            if (_tier.TryGetValue(name, out int t)) return t;
            Build();
            t = Match(name);
            if (t < 0 && PairsTable.ToMinecraft.TryGetValue(name, out var mc)) t = Match(mc);
            _tier[name] = t;
            return t;
        }

        static int Match(string s)
        {
            for (int i = 0; i < _toolRx.Length; i++)
                if (_toolRx[i].IsMatch(s)) return HarvestTable.Tools[i].tier;
            return -1;
        }

        /// <summary>True when the local player hits a rock its pickaxe can't harvest.</summary>
        public static bool TooWeak(EntityAlive who, BlockValue bv)
        {
            if (!Enabled || !(who is EntityPlayerLocal)) return false;
            int need = Needed(bv);
            return need >= 0 && ToolTier(who) < need;
        }

        // slower, like Minecraft without the right pickaxe
        [HarmonyPatch(typeof(ItemActionAttack), "Hit")]
        static class SlowPatch
        {
            static void Prefix(WorldRayHitInfo hitInfo, int _attackerEntityId, ref float _blockDamage)
            {
                try
                {
                    if (hitInfo == null || hitInfo.tag == null || !GameUtils.IsBlockOrTerrain(hitInfo.tag)) return;
                    var world = GameManager.Instance?.World;
                    var who = world?.GetEntity(_attackerEntityId) as EntityAlive;
                    if (who == null) return;
                    if (TooWeak(who, world.GetBlock(hitInfo.hit.blockPos))) _blockDamage *= HarvestTable.SpeedWithoutTier;
                }
                catch { }
            }
        }

        // and nothing comes out of it
        [HarmonyPatch(typeof(GameUtils), "HarvestOnAttack")]
        static class NoDropPatch
        {
            static bool Prefix(ItemActionData _actionData)
            {
                try
                {
                    var who = _actionData?.invData?.holdingEntity;
                    var details = _actionData?.attackDetails;
                    if (who == null || details == null || !details.bBlockHit) return true;
                    if (!TooWeak(who, details.blockBeingDamaged)) return true;
                    details.blockBeingDamaged = BlockValue.Air;    // what the game does at the end of a harvest
                    return false;
                }
                catch { return true; }
            }
        }
    }
}
