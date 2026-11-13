using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// Minecraft doors: each mc_*_door block extends a 7 Days door (opening, closing, collision, saving, multiplayer are
    /// 7 Days' own). Its 7 Days model is hidden here (every second: block entities have no cheap event), and Ryan's
    /// BlockOverlay draws Minecraft's door ("door7d" rows), open or closed as the 7 Days door is (Open below).
    /// </summary>
    public static class McDoors
    {
        const string Prefab = "exteriorHouseDoorPrefab";
        static float _next;
        static readonly List<Transform> _doors = new List<Transform>();
        static readonly HashSet<int> _seen = new HashSet<int>();
        public static int Dressed;
        static readonly List<Vector3i> _pos = new List<Vector3i>();

        /// <summary>Is the 7 Days door at this block open?</summary>
        public static bool Open(World world, Vector3i pos)
        {
            try
            {
                if (world.GetTileEntity(pos) is TileEntityComposite te)
                {
                    var d = te.GetFeature<TEFeatureDoor>();
                    return d != null && d.IsOpen();
                }
            }
            catch { }
            return false;
        }

        public static void Tick()
        {
            if (!Ryan.Mc.Ready || Time.time < _next) return;
            _next = Time.time + 1f;
            var world = GameManager.Instance?.World;
            if (world == null) return;
            try
            {
                // the doors the overlay knows (no scene-wide search: it slowed the game down)
                Ryan.BlockOverlay.PositionsOf("door7d", _pos);
                foreach (var pos in _pos)
                {
                    var chunk = world.GetChunkFromWorldPos(pos) as Chunk;
                    var be = chunk?.GetBlockEntity(pos);
                    var tr = be?.transform;
                    if (tr == null || _seen.Contains(tr.GetInstanceID())) continue;
                    _seen.Add(tr.GetInstanceID());
                    _doors.Add(tr); Dressed++;
                }
                // every Minecraft block whose 7 Days shape must not show (hidden prefab: the blue "?" cube)
                foreach (var kind in new[] { "model", "cutout", "torch" })
                {
                    Ryan.BlockOverlay.PositionsOf(kind, _pos);
                    foreach (var pos in _pos)
                    {
                        if (!(world.GetBlock(pos).Block is BlockMcHidden)) continue;
                        var tr = (world.GetChunkFromWorldPos(pos) as Chunk)?.GetBlockEntity(pos)?.transform;
                        if (tr == null) continue;
                        foreach (var r in tr.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
                    }
                }
                _doors.RemoveAll(t => t == null);
                foreach (var d in _doors)
                    foreach (var r in d.GetComponentsInChildren<Renderer>(true)) if (r.enabled) r.enabled = false;
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] doors: " + ex.Message); }
        }
    }
}
