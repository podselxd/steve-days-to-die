using System;
using System.Collections.Generic;
using HarmonyLib;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// A 9-slot hotbar, as in Minecraft. 7 Days always has a 10th toolbelt slot, so it is taken out
    /// piece by piece: it can't be selected (the wheel and keys skip it), it is hidden in the game's
    /// own toolbelt, and anything that lands there goes to the backpack (or the ground when full).
    /// </summary>
    public static class NineSlots
    {
        public const int Hidden = 9;        // the 10th slot (0-based)
        static float _nextSweep;

        static bool IsLocal(Inventory inv)
        {
            try { return Traverse.Create(inv).Field("entity").GetValue() is EntityPlayerLocal; }
            catch { return false; }
        }

        /// <summary>Called every frame for the local player: empties the hidden slot.</summary>
        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || p.inventory == null || Time.time < _nextSweep) return;
            _nextSweep = Time.time + 0.25f;
            try
            {
                var stack = p.inventory.GetItem(Hidden);
                if (stack == null || stack.IsEmpty()) return;
                var moving = stack.Clone();
                p.inventory.SetItem(Hidden, ItemStack.Empty.Clone());
                if (p.bag != null && p.bag.CanTakeItem(moving) && p.bag.AddItem(moving))
                {
                    Debug.Log("[SVD] 10th slot: moved " + moving.itemValue.ItemClass?.Name + " to the backpack.");
                    return;
                }
                GameManager.Instance.ItemDropServer(moving, p.GetDropPosition(), Vector3.zero, p.entityId, 60f);
                Debug.Log("[SVD] 10th slot: backpack full, dropped " + moving.itemValue.ItemClass?.Name + ".");
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] NineSlots: " + ex.Message); }
        }

        // the wheel or a key asks for the 10th slot: go round to the other end instead
        [HarmonyPatch(typeof(Inventory), "SetFocusedItemIdx")]
        static class FocusPatch
        {
            static void Prefix(Inventory __instance, ref int _idx)
            {
                if (_idx != Hidden || !IsLocal(__instance)) return;
                int cur = __instance.FocusedSlot;
                _idx = cur == Hidden - 1 ? 0 : cur == 0 ? Hidden - 1 : cur;
            }
        }

        [HarmonyPatch(typeof(Inventory), "SetHoldingItemIdx")]
        static class HoldPatch
        {
            static void Prefix(Inventory __instance, ref int _inventoryIdx)
            {
                if (_inventoryIdx == Hidden && IsLocal(__instance)) _inventoryIdx = __instance.FocusedSlot == Hidden ? 0 : __instance.FocusedSlot;
            }
        }

        [HarmonyPatch(typeof(Inventory), "SetHoldingItemIdxNoHolsterTime")]
        static class HoldNowPatch
        {
            static void Prefix(Inventory __instance, ref int _inventoryIdx)
            {
                if (_inventoryIdx == Hidden && IsLocal(__instance)) _inventoryIdx = __instance.FocusedSlot == Hidden ? 0 : __instance.FocusedSlot;
            }
        }
    }

    /// <summary>
    /// The 7 Days injuries a hit leaves (bleeding, stun and slowness, knockdown, concussion, abrasion)
    /// do not exist in Minecraft: players never get them (sheets/blocked_buffs.json).
    /// </summary>
    public static class NoHitInjuries
    {
        static readonly HashSet<string> _exact = new HashSet<string>(BlockedBuffs.Exact, StringComparer.OrdinalIgnoreCase);

        public static bool Enabled => !Settings.GetBool("heridas_7days", false);

        public static bool Blocked(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (_exact.Contains(name)) return true;
            foreach (var p in BlockedBuffs.Prefixes)
                if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool OnPlayer(EntityBuffs buffs)
        {
            try { return Traverse.Create(buffs).Field("parent").GetValue() is EntityPlayer; }
            catch { return false; }
        }

        [HarmonyPatch(typeof(EntityBuffs), "AddBuff", new[] { typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(float) })]
        static class AddPatch
        {
            static bool Prefix(EntityBuffs __instance, string _name, ref EntityBuffs.BuffStatus __result)
            {
                if (!Enabled || !Blocked(_name) || !OnPlayer(__instance)) return true;
                __result = EntityBuffs.BuffStatus.FailedImmune;
                return false;
            }
        }

        [HarmonyPatch(typeof(EntityBuffs), "AddBuff", new[] { typeof(string), typeof(Vector3i), typeof(int), typeof(bool), typeof(bool), typeof(float) })]
        static class AddAtPatch
        {
            static bool Prefix(EntityBuffs __instance, string _name, ref EntityBuffs.BuffStatus __result)
            {
                if (!Enabled || !Blocked(_name) || !OnPlayer(__instance)) return true;
                __result = EntityBuffs.BuffStatus.FailedImmune;
                return false;
            }
        }
    }
}
