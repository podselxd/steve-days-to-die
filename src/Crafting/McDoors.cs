using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Crafting
{
    /// <summary>
    /// Minecraft doors: each mc_*_door block extends 7 Days' wooden door (oldWoodDoor), so opening, closing, collision,
    /// saving and multiplayer are 7 Days' own. Here its model is hidden and Minecraft's door (the top and bottom door
    /// textures from the player's Minecraft) is put on the 7 Days door leaf, so it swings with 7 Days' animation.
    /// Doors are found every second (block entities have no event we can use cheaply).
    /// </summary>
    public static class McDoors
    {
        const string Prefab = "oldWoodDoorPrefab";
        static float _next;
        static readonly HashSet<int> _done = new HashSet<int>();
        static readonly Dictionary<string, (string top, string bottom)> _tex = new Dictionary<string, (string, string)>();
        static Material _mat;
        public static int Dressed;

        static bool TexFor(string blockName, out (string top, string bottom) t)
        {
            if (_tex.TryGetValue(blockName, out t)) return t.top != null;
            t = (null, null);
            foreach (var r in Ryan.Sheets.Blocks)
                if (r.block7d == blockName && r.special != null && r.special.StartsWith("door;"))
                {
                    foreach (var kv in r.special.Split(';'))
                    {
                        if (kv.StartsWith("top=")) t.top = kv.Substring(4);
                        if (kv.StartsWith("bottom=")) t.bottom = kv.Substring(7);
                    }
                    break;
                }
            _tex[blockName] = t;
            return t.top != null;
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
                    if (tr.parent == null || !tr.name.StartsWith(Prefab) || _done.Contains(tr.GetInstanceID())) continue;
                    // which block is it? (the door spans two blocks; its pivot sits near the bottom one)
                    Vector3 w = tr.position + Origin.position;
                    string name = null;
                    foreach (var off in new[] { Vector3.zero, new Vector3(0.5f, 0.1f, 0.5f), new Vector3(-0.5f, 0.1f, -0.5f), new Vector3(0.5f, 0.1f, -0.5f), new Vector3(-0.5f, 0.1f, 0.5f) })
                    {
                        var b = world.GetBlock(World.worldToBlockPos(w + off)).Block;
                        string n = b?.GetBlockName();
                        if (n != null && n.StartsWith("mc_") && n.EndsWith("_door")) { name = n; break; }
                    }
                    if (name == null) { _done.Add(tr.GetInstanceID()); continue; }      // a 7 Days door: left alone
                    if (!TexFor(name, out var t)) continue;
                    Dress(tr, t.top, t.bottom);
                    Dressed++;
                    _done.Add(tr.GetInstanceID());
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] doors: " + ex.Message); }
        }

        // hides 7 Days' door and puts Minecraft's on its biggest moving part (the leaf)
        static void Dress(Transform door, string top, string bottom)
        {
            MeshFilter leaf = null; float best = 0f;
            foreach (var mf in door.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                float v = mf.sharedMesh.bounds.size.x * mf.sharedMesh.bounds.size.y + mf.sharedMesh.bounds.size.y * mf.sharedMesh.bounds.size.z + mf.sharedMesh.bounds.size.x * mf.sharedMesh.bounds.size.z;
                if (v > best) { best = v; leaf = mf; }
            }
            foreach (var r in door.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            if (leaf == null) return;
            var bnd = leaf.sharedMesh.bounds;
            Rect rTop = Ryan.Atlas.Uv(new List<Ryan.FaceLayer> { new Ryan.FaceLayer { tex = top } }, new Color32(255, 255, 255, 255));
            Rect rBot = Ryan.Atlas.Uv(new List<Ryan.FaceLayer> { new Ryan.FaceLayer { tex = bottom } }, new Color32(255, 255, 255, 255));
            Ryan.Atlas.Flush();
            if (_mat == null) _mat = Ryan.Mats.Cutout(Ryan.Atlas.Texture);
            var k = new Ryan.MeshKit { shade = true };
            Vector3 min = bnd.min, max = bnd.max, mid = new Vector3(max.x, (min.y + max.y) * 0.5f, max.z);
            for (int f = 0; f < 6; f++) k.Face(f, min, new Vector3(max.x, mid.y, max.z), rBot);
            for (int f = 0; f < 6; f++) k.Face(f, new Vector3(min.x, mid.y, min.z), max, rTop);
            var go = new GameObject("SVD_McDoor");
            go.layer = leaf.gameObject.layer;
            go.transform.SetParent(leaf.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = k.ToMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = _mat;
        }
    }
}
