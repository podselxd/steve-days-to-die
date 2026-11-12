// Glue between the files taken from Ryan Ussery's "Steve Days to Die" (MIT, see
// LICENSE-SteveDaysToDie-RyanUssery.txt in this folder) and this mod. His mod had its own Minecraft
// inventory and block tables; here the held item is the player's 7 Days item, shown as its Minecraft pair,
// and a Minecraft item counts as a block when its model is a full cube.
using System.Collections.Generic;
using SteveDaysToDie.Generated;
using UnityEngine;

namespace SteveDaysToDie.Ryan
{
    public static class Sheets
    {
        public struct BlockRow { public string render, model, tint, id; }
    }

    public static class Items
    {
        static readonly Dictionary<string, Sheets.BlockRow?> _blocks = new Dictionary<string, Sheets.BlockRow?>();

        /// <summary>A full-cube block item (oak_planks, cobblestone...) or null (sprites, torches, doors...).</summary>
        public static Sheets.BlockRow? Block(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_blocks.TryGetValue(id, out var row)) return row;
            row = null;
            try
            {
                string m = Icons.ItemModelName(id);
                if (m != null && m.StartsWith("block/"))
                {
                    string name = m.Substring(6);
                    string cur = m;
                    for (int i = 0; i < 12 && cur != null; i++)
                    {
                        if (cur == "block/cube" || cur == "block/cube_all" || cur == "block/cube_column" || cur == "block/cube_bottom_top" || cur == "block/cube_column_horizontal")
                        {
                            row = new Sheets.BlockRow { render = "cube", model = name, tint = "", id = id };
                            break;
                        }
                        var j = Mc.Model(cur);
                        if (j == null) break;
                        cur = ((string)j["parent"])?.Replace("minecraft:", "");
                    }
                }
            }
            catch { row = null; }
            _blocks[id] = row;
            return row;
        }
    }

    public static class Held
    {
        /// <summary>
        /// What the first-person hand shows for the player's held 7 Days item: "" = the bare arm,
        /// a Minecraft id = that item, null = nothing of ours (7 Days' own model stays, e.g. guns).
        /// </summary>
        public static string McId(EntityPlayerLocal p)
        {
            try
            {
                var ic = p.inventory?.holdingItem;
                if (ic == null || ic.Name.StartsWith("meleeHand")) return "";
                if (PairsTable.ToMinecraft.TryGetValue(ic.Name, out var mc) && Icons.ItemModelName(mc) != null) return mc;
            }
            catch { }
            return null;
        }
    }
}
