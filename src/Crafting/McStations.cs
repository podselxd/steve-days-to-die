using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minecraft's crafting table as a real 7 Days block (Config/blocks.xml: Class="McCraftingTable, SteveDaysToDie";
/// 7 Days finds the class by the "Block" prefix, so it lives outside any namespace). E opens Minecraft's 3x3
/// crafting window (McInv.McInventory in table mode). It looks like Minecraft's table: the block's faces are
/// drawn with the player's own Minecraft textures on top of it (SteveDaysToDie.Crafting.McBlockLook).
/// Breaking it gives the table back.
/// </summary>
public class BlockMcCraftingTable : Block
{
    static readonly BlockActivationCommand[] Cmds = { new BlockActivationCommand("open", "hand", true, false, null) };

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => true;

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => Cmds;

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return "[E] " + (Localization.Get("mc_crafting_table") ?? "Crafting Table");
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        SteveDaysToDie.McInv.McInventory.OpenTable(_player);
        return true;
    }

    public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        SteveDaysToDie.McInv.McInventory.OpenTable(_player);
        return true;
    }

    public override void OnBlockAdded(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(_world, _chunk, _blockPos, _blockValue, _addedByPlayer); 
        Debug.Log("[SVD] crafting table added at " + _blockPos);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue); 
    }

    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue); 
    }

    public override void OnBlockUnloaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockUnloaded(_world, _blockPos, _blockValue); 
    }
}

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// Draws Minecraft blocks' faces (from the player's own Minecraft) over the 7 Days blocks that stand for them.
    /// Block callbacks can come from the chunk threads, so requests are queued and handled on the main thread.
    /// </summary>
    public static class McBlockLook
    {
        static readonly ConcurrentQueue<(Vector3i pos, string id)> _queue = new ConcurrentQueue<(Vector3i, string)>();
        static readonly Dictionary<Vector3i, GameObject> _shown = new Dictionary<Vector3i, GameObject>();
        static readonly Dictionary<Vector3i, string> _ids = new Dictionary<Vector3i, string>();   // what each one shows now
        static int _made;
        static readonly List<(Vector3i, string)> _relight = new List<(Vector3i, string)>();

        public static void Add(Vector3i pos, string mcId) => _queue.Enqueue((pos, mcId));
        public static void Remove(Vector3i pos) => _queue.Enqueue((pos, null));

        /// <summary>Every frame (main thread): apply the queue and keep each face mesh on its block (the world origin moves).</summary>
        public static void Tick()
        {
            while (_queue.TryDequeue(out var q))
            {
                if (_shown.TryGetValue(q.pos, out var old)) { if (old != null) Object.Destroy(old); _shown.Remove(q.pos); }
                if (q.id == null || !Ryan.Mc.Ready) continue;
                try
                {
                    var go = new GameObject("SVD_McBlock_" + q.id);
                    Object.DontDestroyOnLoad(go);
                    go.AddComponent<MeshFilter>().sharedMesh = Ryan.ItemMeshes.Mesh(q.id);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Ryan.ItemMeshes.Material(q.id, litInWorld: true);
                    go.transform.localScale = Vector3.one * 1.004f;          // just outside the 7 Days block
                    _shown[q.pos] = go;
                    _ids[q.pos] = q.id;
                    if (_made++ < 20) Debug.Log("[SVD] block look: " + q.id + " at " + q.pos + " (mesh " + (go.GetComponent<MeshFilter>().sharedMesh != null) + ", shader " + mr.sharedMaterial?.shader?.name + ")");
                }
                catch (System.Exception ex) { Debug.LogWarning("[SVD] block look " + q.id + ": " + ex.Message); }
            }
            foreach (var kv in _shown)
            {
                if (kv.Value == null) continue;
                kv.Value.transform.position = kv.Key.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f) - Origin.position;
                // furnaces light up while they burn (Minecraft's furnace_on / smoker_on / blast_furnace_on)
                if (_ids.TryGetValue(kv.Key, out var id) && (id.StartsWith("furnace") || id.StartsWith("smoker") || id.StartsWith("blast_furnace")))
                {
                    string kind = id.EndsWith("_on") ? id.Substring(0, id.Length - 3) : id;
                    string want = McFurnaces.Burning(kv.Key) ? kind + "_on" : kind;
                    if (want != id)
                    {
                        try
                        {
                            var mesh = Ryan.ItemMeshes.Mesh(want);
                            if (mesh != null)
                            {
                                kv.Value.GetComponent<MeshFilter>().sharedMesh = mesh;
                                kv.Value.GetComponent<MeshRenderer>().sharedMaterial = Ryan.ItemMeshes.Material(want, litInWorld: true);
                            }
                        }
                        catch { }
                        _relight.Add((kv.Key, want));
                    }
                }
            }
            foreach (var r in _relight) _ids[r.Item1] = r.Item2;
            _relight.Clear();
        }
    }
}

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// How 7 Days shows a Minecraft block outside the world: the white see-through PREVIEW while placing it shows
    /// Minecraft's texture (pale, see-through), and a DROPPED block is a small Minecraft cube instead of 7 Days' sack.
    /// (ItemClassBlock.CreateMesh makes both: purpose Preview / Drop.)
    /// </summary>
    [HarmonyLib.HarmonyPatch(typeof(ItemClassBlock), "CreateMesh")]
    public static class McBlockPreview
    {
        static readonly Dictionary<string, Mesh> _shifted = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> _ghost = new Dictionary<string, Material>();

        static string McId(BlockValue bv)
        {
            try { string n = bv.Block?.GetBlockName(); return n != null && n.StartsWith("mc_") && Ryan.Items.Block(n.Substring(3)) != null ? n.Substring(3) : null; }
            catch { return null; }
        }

        // the cube from 0 to 1, like 7 Days' own block meshes (Ryan's mesh is centred)
        static Mesh Shifted(string id)
        {
            if (_shifted.TryGetValue(id, out var m)) return m;
            var src = Ryan.ItemMeshes.Mesh(id);
            if (src == null) return null;
            m = Object.Instantiate(src);
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] += new Vector3(0.5f, 0.5f, 0.5f);
            m.vertices = v; m.RecalculateBounds();
            _shifted[id] = m;
            return m;
        }

        static void Postfix(BlockValue _blockValue, BlockShape.MeshPurpose _purpose, ref Transform __result)
        {
            if (__result == null || !Ryan.Mc.Ready) return;
            string id = McId(_blockValue);
            if (id == null) return;
            try
            {
                if (_purpose == BlockShape.MeshPurpose.Preview)
                {
                    var mesh = Shifted(id);
                    if (mesh == null) return;
                    if (!_ghost.TryGetValue(id, out var mat))
                        _ghost[id] = mat = Ryan.Mats.Blend(Ryan.Atlas.Texture, new Color(1f, 1f, 1f, 0.55f));    // pale and see-through
                    foreach (var mf in __result.GetComponentsInChildren<MeshFilter>(true))
                    {
                        // a copy of its own: 7 Days writes into the preview's mesh later (moving, turning it);
                        // writing into our shared mesh broke it ("Mesh.vertices is too small" errors)
                        if (mf.sharedMesh != null && mf.sharedMesh.name.StartsWith("SVD_preview")) Object.Destroy(mf.sharedMesh);
                        var own = Object.Instantiate(mesh);
                        own.name = "SVD_preview_" + id;
                        mf.sharedMesh = own;
                        var mr = mf.GetComponent<MeshRenderer>();
                        if (mr != null) mr.sharedMaterial = mat;
                    }
                }
                else if (_purpose == BlockShape.MeshPurpose.Drop)
                {
                    foreach (var r in __result.GetComponentsInChildren<Renderer>(true)) r.enabled = false;   // 7 Days' sack
                    var go = new GameObject("SVD_McDrop_" + id);
                    go.transform.SetParent(__result, false);
                    go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                    go.transform.localScale = Vector3.one * 0.25f;                                       // Minecraft's dropped block size
                    var dm = Object.Instantiate(Ryan.ItemMeshes.Mesh(id));     // its own copy, like the preview
                    dm.name = "SVD_drop_" + id;
                    go.AddComponent<MeshFilter>().sharedMesh = dm;
                    go.AddComponent<MeshRenderer>().sharedMaterial = Ryan.ItemMeshes.Material(id, litInWorld: true);
                    go.AddComponent<McDropLook.Spin>();          // turns, bobs, and keeps 7 Days' sack hidden
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] block preview " + id + ": " + ex.Message); }
        }
    }
}

/// <summary>
/// Thin Minecraft blocks (the torch): 7 Days needs a model for the block's shape, so they use the 7 Days wall torch;
/// its drawing, flame and light are switched off here and only Minecraft's drawing (Ryan's BlockOverlay) shows.
/// Its collider stays, so the torch can be hit and broken. (Config/blocks.xml: Class="McHidden, SteveDaysToDie".)
/// </summary>
public class BlockMcHidden : Block
{
    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        Hide(_ebcd);
    }

    public override void OnBlockEntityTransformBeforeActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformBeforeActivated(_world, _blockPos, _blockValue, _ebcd);
        Hide(_ebcd);
    }

    static void Hide(BlockEntityData d)
    {
        try
        {
            var t = d?.transform;
            if (t == null) return;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var l in t.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var a in t.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
        }
        catch (System.Exception ex) { Debug.LogWarning("[SVD] hidden block: " + ex.Message); }
    }
}
