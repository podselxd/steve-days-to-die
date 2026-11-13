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
                foreach (var tr in Object.FindObjectsOfType<Transform>())
                {
                    if (tr.parent == null || !tr.name.StartsWith(Prefab) || _seen.Contains(tr.GetInstanceID())) continue;
                    _seen.Add(tr.GetInstanceID());
                    Vector3 w = tr.position + Origin.position;
                    foreach (var off in new[] { Vector3.zero, new Vector3(0.5f, 0.1f, 0.5f), new Vector3(-0.5f, 0.1f, -0.5f), new Vector3(0.5f, 0.1f, -0.5f), new Vector3(-0.5f, 0.1f, 0.5f) })
                    {
                        string n = world.GetBlock(World.worldToBlockPos(w + off)).Block?.GetBlockName();
                        if (n != null && n.StartsWith("mc_") && n.EndsWith("_door")) { _doors.Add(tr); Dressed++; break; }
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
