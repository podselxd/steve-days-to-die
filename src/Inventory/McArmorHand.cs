using UnityEngine;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Minecraft: right-click with a piece of armour in your hand puts it on (and what you wore there goes to your hand).
    /// User 2026-11-13: "no puedo poner mi armadura desde la hotbar". Called by Patches.McControls.
    /// </summary>
    public partial class McInventory
    {
        public static bool EquipFromHand(EntityPlayerLocal p)
        {
            var inv = Instance;
            if (inv == null || p == null || inv._p != p) return false;
            try
            {
                int idx = p.inventory.holdingItemIdx;
                var held = p.inventory.GetItem(idx);
                if (held == null || held.IsEmpty() || held.count != 1) return false;
                int slot = inv.ArmorSlot(held);
                if (slot < 0 || slot > 3) return false;
                var worn = p.equipment.GetSlotItem(slot);
                p.equipment.SetSlotItem(slot, held.itemValue.Clone());
                p.inventory.SetItem(idx, worn == null || worn.IsEmpty() ? ItemStack.Empty.Clone() : new ItemStack(worn.Clone(), 1));
                Sound.MinecraftSounds.PlayGroup("item/armor/equip_generic", p.position + Vector3.up, 8f, 1f, 1f);
                return true;
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] armour from hand: " + ex.Message); return false; }
        }
    }
}
