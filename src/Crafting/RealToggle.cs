using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// Key Ñ: the held item REALLY becomes its pair (user 2026-11-13: "que se quede y se pueda ver en el inventario...
    /// si lo tiras y otro lo agarra, que se quede convertido"). 7 Days material -> its Minecraft item (mc_), same count.
    /// Back to 7 Days only what came from 7 Days (the stack carries the mark "from7d"): a Minecraft item made by
    /// Minecraft's recipes never turns into 7 Days items, so nothing can be duplicated (1 log -> 4 planks...).
    /// It is a normal item change in the inventory, so it is saved, dropped, picked up and seen by friends as it is.
    /// </summary>
    public static class RealToggle
    {
        const string Mark = "from7d";
        static Dictionary<string, string> _back;

        /// <summary>Returns a message for the player, or null when nothing changed.</summary>
        public static string Toggle(EntityPlayerLocal p, int slot)
        {
            var st = p.inventory.GetItem(slot);
            if (st == null || st.IsEmpty()) return null;
            string name = st.itemValue.ItemClass?.Name;
            if (name == null) return null;
            var pairs = Generated.PairsTable.ToMinecraft;
            if (_back == null)
            {
                _back = new Dictionary<string, string>();
                foreach (var kv in pairs) if (Generated.PairsTable.Reversible.Contains(kv.Key) && !_back.ContainsKey(kv.Value)) _back[kv.Value] = kv.Key;
            }
            ItemStack to = null;
            if (!McItems.IsMc(name))
            {
                // 7 Days -> Minecraft
                if (!Generated.PairsTable.Reversible.Contains(name) || !pairs.TryGetValue(name, out var mcId)) return "Este objeto no tiene versión de Minecraft.";
                to = McItems.Stack(mcId, st.count);
                if (to == null) return "Este objeto no tiene versión de Minecraft.";
                to.itemValue.SetMetadata(Mark, 1);
            }
            else
            {
                // Minecraft -> 7 Days: only if it came from 7 Days
                string mcId = McItems.IdOf(name);
                if (!st.itemValue.TryGetMetadata(Mark, out int from7d) || from7d != 1 || !_back.TryGetValue(mcId, out var sd))
                    return "Lo hecho con Minecraft no vuelve a 7 Days.";
                var iv = ItemClass.GetItem(sd);
                if (iv == null || iv.IsEmpty()) return null;
                to = new ItemStack(iv, st.count);
            }
            p.inventory.SetItem(slot, to);
            return null;
        }
    }
}
