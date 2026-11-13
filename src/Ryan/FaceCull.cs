using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Ryan
{
    /// <summary>
    /// The hand's material (Sprites/Default) draws every face with no depth test, in the order the mesh lists them.
    /// So every frame the mesh is given only the faces turned towards the camera, in an order that draws right:
    ///  - which way a face looks comes from its own winding (the old "away from the mesh's centre" guess was wrong
    ///    for shapes with hollows, like a pickaxe: inner side walls were kept and drawn over the flat face);
    ///  - for flat items (extruded sprites) the side walls go first, far ones before near ones, and the flat face
    ///    last, on top - that is what a depth buffer would show, since every wall lies behind the flat face.
    /// </summary>
    public static class FaceCull
    {
        class Data
        {
            public Mesh mesh; public int[] all; public Vector3[] centre; public Vector3[] normal;
            public bool sprite; public float[] dist; public int[] order;
        }
        static readonly Dictionary<MeshFilter, Data> _data = new Dictionary<MeshFilter, Data>();
        static readonly List<int> _tris = new List<int>();
        static readonly List<MeshFilter> _gone = new List<MeshFilter>();

        /// <summary>Give this filter its own copy of the mesh (call when the shown mesh changes).
        /// sprite = a flat item made by MeshKit.ExtrudedSprite (its first two quads are the flat faces).</summary>
        public static void Own(MeshFilter f, Mesh shared, bool sprite = false)
        {
            if (f == null || shared == null) return;
            if (_data.TryGetValue(f, out var old) && old.mesh != null) Object.Destroy(old.mesh);
            if (_data.Count > 8)                                   // filters of hands that no longer exist
            {
                _gone.Clear();
                foreach (var kv in _data) if (kv.Key == null) _gone.Add(kv.Key);
                foreach (var k in _gone) { if (_data[k].mesh != null) Object.Destroy(_data[k].mesh); _data.Remove(k); }
            }
            var m = Object.Instantiate(shared);
            m.name = shared.name + " (culled)";
            f.sharedMesh = m;
            var v = shared.vertices; var all = shared.triangles;
            int quads = all.Length / 6;                            // MeshKit: each face = 4 vertices, 2 triangles
            var d = new Data
            {
                mesh = m, all = all, sprite = sprite && quads >= 2,
                centre = new Vector3[quads], normal = new Vector3[quads], dist = new float[quads], order = new int[quads]
            };
            for (int k = 0; k < quads; k++)
            {
                int i = k * 6;
                Vector3 a = v[all[i]], b = v[all[i + 1]], e = v[all[i + 2]], g = v[all[i + 5]];
                d.centre[k] = (a + b + e + g) * 0.25f;
                d.normal[k] = Vector3.Cross(b - a, e - a);
            }
            // the arm's mesh is mirrored by hand, which turns every winding round: the first quad is always an
            // outer face of a box, so it tells which way this mesh's windings point
            if (quads > 0 && Vector3.Dot(d.normal[0], d.centre[0] - shared.bounds.center) < 0f)
                for (int k = 0; k < quads; k++) d.normal[k] = -d.normal[k];
            _data[f] = d;
        }

        public static void Apply(MeshFilter f, Vector3 camPos)
        {
            if (f == null || !f.gameObject.activeInHierarchy || !_data.TryGetValue(f, out var d) || d.mesh == null) return;
            Vector3 cam = f.transform.InverseTransformPoint(camPos);   // everything in the mesh's own space
            int quads = d.centre.Length;
            _tris.Clear();
            if (!d.sprite)
            {
                for (int k = 0; k < quads; k++)
                    if (Vector3.Dot(d.normal[k], cam - d.centre[k]) > 0f) Add(d, k);
            }
            else
            {
                int n = 0;
                for (int k = 2; k < quads; k++)
                {
                    Vector3 to = cam - d.centre[k];
                    if (Vector3.Dot(d.normal[k], to) <= 0f) continue;   // turned away from the camera
                    d.dist[n] = -to.sqrMagnitude; d.order[n] = k; n++;
                }
                System.Array.Sort(d.dist, d.order, 0, n);              // far walls first
                for (int j = 0; j < n; j++) Add(d, d.order[j]);
                for (int k = 0; k < 2; k++)
                    if (Vector3.Dot(d.normal[k], cam - d.centre[k]) > 0f) Add(d, k);   // the flat face, on top
            }
            d.mesh.SetTriangles(_tris, 0);
        }

        static void Add(Data d, int k)
        {
            int i = k * 6;
            for (int j = 0; j < 6; j++) _tris.Add(d.all[i + j]);
        }
    }
}
