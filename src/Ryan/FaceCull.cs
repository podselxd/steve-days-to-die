using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Ryan
{
    /// <summary>
    /// The hand's material (Sprites/Default) draws every face with no depth test, so a cube's back faces landed
    /// on top of its front ones ("textura rota") and flat items showed their mirrored back. Every frame only the
    /// faces turned towards the camera are kept: a face is visible when it points away from the mesh's centre
    /// and towards the camera, which works for boxes, the arm and the thin extruded items, mirrored or not.
    /// </summary>
    public static class FaceCull
    {
        class Data { public Mesh mesh; public Vector3[] v; public int[] all; public Vector3 center; }
        static readonly Dictionary<MeshFilter, Data> _data = new Dictionary<MeshFilter, Data>();
        static readonly List<int> _tris = new List<int>();

        /// <summary>Give this filter its own copy of the mesh (call when the shown mesh changes).</summary>
        public static void Own(MeshFilter f, Mesh shared)
        {
            if (f == null || shared == null) return;
            if (_data.TryGetValue(f, out var old) && old.mesh != null) Object.Destroy(old.mesh);
            var m = Object.Instantiate(shared);
            m.name = shared.name + " (culled)";
            f.sharedMesh = m;
            _data[f] = new Data { mesh = m, v = shared.vertices, all = shared.triangles, center = shared.bounds.center };
        }

        public static void Apply(MeshFilter f, Vector3 camPos)
        {
            if (f == null || !f.gameObject.activeInHierarchy || !_data.TryGetValue(f, out var d) || d.mesh == null) return;
            var t = f.transform;
            Vector3 c = t.TransformPoint(d.center);
            _tris.Clear();
            for (int i = 0; i + 5 < d.all.Length; i += 6)          // MeshKit: each face = 4 vertices, 2 triangles
            {
                Vector3 a = t.TransformPoint(d.v[d.all[i]]), b = t.TransformPoint(d.v[d.all[i + 1]]), e = t.TransformPoint(d.v[d.all[i + 2]]);
                Vector3 g = t.TransformPoint(d.v[d.all[i + 5]]);
                Vector3 fc = (a + e) * 0.5f;                       // middle of the quad (its diagonal)
                Vector3 n = Vector3.Cross(b - a, g - a);
                if (Vector3.Dot(n, fc - c) < 0f) n = -n;           // make it point outwards
                if (n.sqrMagnitude < 1e-12f) n = fc - c;
                if (Vector3.Dot(n, camPos - fc) <= 0f) continue;   // turned away from the camera
                for (int k = 0; k < 6; k++) _tris.Add(d.all[i + k]);
            }
            d.mesh.SetTriangles(_tris, 0);
        }
    }
}
