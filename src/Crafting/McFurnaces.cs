// Minecraft's furnace, smoker and blast furnace. The state, the smelting tick, the fuel table and saving it next to
// the save game come from Ryan Ussery's "Steve Days to Die" (Machines.cs, FurnaceState, SaveData, Sheets.Fuels;
// MIT, see src/Ryan/LICENSE-SteveDaysToDie-RyanUssery.txt), adapted to real 7 Days items and to the three kinds.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>Furnace / smoker / blast furnace block (Config/blocks.xml, Class "McFurnace, SteveDaysToDie").</summary>
public class BlockMcFurnace : Block
{
    static readonly BlockActivationCommand[] Cmds = { new BlockActivationCommand("open", "hand", true, false, null) };
    string Kind => GetBlockName().StartsWith("mc_") ? GetBlockName().Substring(3) : "furnace";

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => true;
    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => Cmds;
    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
        => "[E] " + (Localization.Get(GetBlockName()) ?? Kind);

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    { SteveDaysToDie.McInv.McInventory.OpenFurnace(_player, _blockPos, Kind); return true; }
    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    { SteveDaysToDie.McInv.McInventory.OpenFurnace(_player, _blockPos, Kind); return true; }

    public override void OnBlockAdded(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    { base.OnBlockAdded(_world, _chunk, _blockPos, _blockValue, _addedByPlayer);  }
    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    { base.OnBlockLoaded(_world, _blockPos, _blockValue);  }
    public override void OnBlockUnloaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    { base.OnBlockUnloaded(_world, _blockPos, _blockValue);  }
    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue); 
        SteveDaysToDie.Crafting.McFurnaces.Broken(_blockPos);          // what was inside falls out, as in Minecraft
    }
}

namespace SteveDaysToDie.Crafting
{
    public class FurnaceState
    {
        public string kind = "furnace";
        public ItemStack input = ItemStack.Empty.Clone(), fuel = ItemStack.Empty.Clone(), output = ItemStack.Empty.Clone();
        public float burnLeft, burnTotal, cook;
    }

    public static class McFurnaces
    {
        // seconds of burning per Minecraft fuel (Ryan's table = Minecraft's ticks / 20); '#' = a Minecraft item tag
        static readonly (string id, float seconds)[] Fuels =
        {
            ("coal", 80), ("charcoal", 80), ("coal_block", 800), ("#planks", 15), ("#logs", 15), ("#wooden_slabs", 7.5f),
            ("stick", 5), ("crafting_table", 15), ("chest", 15), ("bookshelf", 15), ("wooden_sword", 10), ("wooden_pickaxe", 10),
            ("wooden_axe", 10), ("wooden_shovel", 10), ("#wool", 5), ("lava_bucket", 1000), ("blaze_rod", 120), ("dried_kelp_block", 200),
        };
        static Dictionary<string, float> _fuel;

        static readonly Dictionary<Vector3i, FurnaceState> _all = new Dictionary<Vector3i, FurnaceState>();
        static string _loadedFor;
        static float _tickAcc, _saveAt;
        static bool _dirty;

        public static string RecipeKind(string kind) => kind == "smoker" ? "smoking" : kind == "blast_furnace" ? "blasting" : "smelting";
        public static float CookSeconds(string kind) => kind == "furnace" ? 10f : 5f;       // smoker and blast furnace: twice as fast

        public static float BurnSeconds(ItemStack st)
        {
            string id = McRecipes.McId(st);
            if (id == null) return SevenDaysFuel(st);
            if (_fuel == null)
            {
                _fuel = new Dictionary<string, float>();
                foreach (var f in Fuels)
                {
                    if (f.id.StartsWith("#")) { foreach (var t in McRecipes.Tag(f.id.Substring(1))) if (!_fuel.ContainsKey(t)) _fuel[t] = f.seconds; }
                    else _fuel[f.id] = f.seconds;
                }
            }
            return _fuel.TryGetValue(id, out var s) ? s : SevenDaysFuel(st);
        }

        // anything 7 Days itself lets you burn (its FuelValue: wood is 50) burns in a Minecraft furnace too;
        // 50 -> 15 s, the same as a Minecraft log
        static float SevenDaysFuel(ItemStack st)
        {
            try
            {
                var ic = st?.itemValue?.ItemClass;
                if (ic == null) return 0f;
                int v = ic.FuelValue != null ? ic.FuelValue.Value : 0;
                if (v <= 0 && ic is ItemClassBlock && st.itemValue.ToBlockValue().Block != null) v = st.itemValue.ToBlockValue().Block.FuelValue;
                return v > 0 ? v * 0.3f : 0f;
            }
            catch { return 0f; }
        }

        public static McRecipes.Smelt SmeltFor(FurnaceState f) => f.input.IsEmpty() ? null : McRecipes.SmeltFor(McRecipes.McId(f.input), RecipeKind(f.kind));

        public static FurnaceState Get(Vector3i pos, string kind)
        {
            Load();
            if (!_all.TryGetValue(pos, out var f)) { f = new FurnaceState { kind = kind }; _all[pos] = f; }
            return f;
        }

        public static void Changed() { _dirty = true; }

        public static bool Burning(Vector3i pos) => _all.TryGetValue(pos, out var f) && f.burnLeft > 0f;

        /// <summary>Every frame (main thread): all furnaces keep smelting, open or not; saved every few seconds.</summary>
        public static void Tick()
        {
            _tickAcc += Time.deltaTime;
            if (_tickAcc < 0.1f) return;
            float dt = _tickAcc; _tickAcc = 0f;
            Load();
            foreach (var f in _all.Values) Tick(f, dt);
            if (_dirty && Time.time > _saveAt) { Save(); _saveAt = Time.time + 5f; }
        }

        static void Tick(FurnaceState f, float dt)
        {
            var smelt = SmeltFor(f);
            ItemStack result = smelt != null ? McItems.Stack(smelt.result, smelt.count) : null;
            bool canSmelt = result != null && (f.output.IsEmpty()
                || (f.output.itemValue.type == result.itemValue.type && f.output.count + result.count <= result.itemValue.ItemClass.Stacknumber.Value));
            if (f.burnLeft <= 0f && canSmelt && !f.fuel.IsEmpty())
            {
                float s = BurnSeconds(f.fuel);
                if (s > 0f)
                {
                    f.burnLeft = f.burnTotal = s;
                    bool lava = McRecipes.McId(f.fuel) == "lava_bucket";
                    f.fuel.count--;
                    if (f.fuel.count <= 0) f.fuel = lava ? (McItems.Stack("bucket", 1) ?? ItemStack.Empty.Clone()) : ItemStack.Empty.Clone();
                    _dirty = true;
                }
            }
            if (f.burnLeft > 0f)
            {
                f.burnLeft -= dt;
                if (canSmelt)
                {
                    f.cook += dt;
                    if (f.cook >= CookSeconds(f.kind))
                    {
                        f.cook = 0f;
                        if (f.output.IsEmpty()) f.output = result.Clone(); else f.output.count += result.count;
                        f.input.count--;
                        if (f.input.count <= 0) f.input = ItemStack.Empty.Clone();
                        _dirty = true;
                    }
                }
                else f.cook = 0f;
            }
            else if (f.cook > 0f) f.cook = Mathf.Max(0f, f.cook - dt * 2f);
        }

        /// <summary>The furnace was broken: its contents drop where it stood.</summary>
        public static void Broken(Vector3i pos)
        {
            Load();
            if (!_all.TryGetValue(pos, out var f)) return;
            _all.Remove(pos);
            _dirty = true;
            try
            {
                foreach (var st in new[] { f.input, f.fuel, f.output })
                    if (st != null && !st.IsEmpty())
                        GameManager.Instance.ItemDropServer(st.Clone(), pos.ToVector3() + new Vector3(0.5f, 0.6f, 0.5f), Vector3.zero, -1, 60f);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] furnace drop: " + ex.Message); }
        }

        // ---- saving next to the save game (Ryan's SaveData idea): SteveDaysToDie_furnaces.json ----

        class Row { public int x, y, z; public string kind, i, fu, o; public int ic, fc, oc; public float burnLeft, burnTotal, cook; }

        static string FilePath()
        {
            string dir = GameIO.GetSaveGameDir();
            return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "SteveDaysToDie_furnaces.json");
        }

        static void Load()
        {
            string path = FilePath();
            if (path == _loadedFor) return;
            _loadedFor = path;
            _all.Clear();
            try
            {
                if (path == null || !File.Exists(path)) return;
                foreach (var r in JsonConvert.DeserializeObject<List<Row>>(File.ReadAllText(path)) ?? new List<Row>())
                    _all[new Vector3i(r.x, r.y, r.z)] = new FurnaceState
                    {
                        kind = r.kind ?? "furnace", input = Make(r.i, r.ic), fuel = Make(r.fu, r.fc), output = Make(r.o, r.oc),
                        burnLeft = r.burnLeft, burnTotal = r.burnTotal, cook = r.cook,
                    };
                Debug.Log("[SVD] furnaces loaded: " + _all.Count);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] furnaces load: " + ex.Message); }
        }

        public static void Save()
        {
            _dirty = false;
            try
            {
                string path = FilePath();
                if (path == null) return;
                var rows = _all.Select(kv => new Row
                {
                    x = kv.Key.x, y = kv.Key.y, z = kv.Key.z, kind = kv.Value.kind,
                    i = Name(kv.Value.input), ic = kv.Value.input.count, fu = Name(kv.Value.fuel), fc = kv.Value.fuel.count,
                    o = Name(kv.Value.output), oc = kv.Value.output.count,
                    burnLeft = kv.Value.burnLeft, burnTotal = kv.Value.burnTotal, cook = kv.Value.cook,
                }).ToList();
                File.WriteAllText(path, JsonConvert.SerializeObject(rows));
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] furnaces save: " + ex.Message); }
        }

        static string Name(ItemStack s) => s == null || s.IsEmpty() ? null : s.itemValue.ItemClass?.Name;

        static ItemStack Make(string name, int count)
        {
            if (string.IsNullOrEmpty(name) || count <= 0) return ItemStack.Empty.Clone();
            var iv = ItemClass.GetItem(name);
            return iv == null || iv.IsEmpty() ? ItemStack.Empty.Clone() : new ItemStack(iv, count);
        }
    }
}
