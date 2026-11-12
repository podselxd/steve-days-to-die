using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Steve
{
    /// <summary>
    /// Rebuilds Steve's shapes (Minecraft ships no model files): six boxes whose faces are cut
    /// from the player's own skin texture with the standard skin layout.
    /// </summary>
    public static class SteveParts
    {
        static Material _material;

        public static bool Legacy => SteveCharacter.SkinTexture != null && SteveCharacter.SkinTexture.height < 64;

        public static Material Material
        {
            get
            {
                if (_material != null) return _material;
                string[] shaders = { "Standard", "Legacy Shaders/Diffuse", "Unlit/Texture", "Sprites/Default" };
                Shader sh = null;
                foreach (var n in shaders) { sh = Shader.Find(n); if (sh != null) break; }
                if (sh == null) { Debug.LogError("[SVD] No shader found for Steve."); return null; }
                _material = new Material(sh) { mainTexture = SteveCharacter.SkinTexture };
                if (_material.HasProperty("_Glossiness")) _material.SetFloat("_Glossiness", 0f);
                if (_material.HasProperty("_Metallic")) _material.SetFloat("_Metallic", 0f);
                Debug.Log("[SVD] Steve material uses shader: " + sh.name);
                return _material;
            }
        }

        /// <summary>
        /// The 7 Days body meshes to hide: the character's own parts sit at the top of the rig.
        /// Anything hanging from the skeleton ("Origin": what the player holds) stays visible.
        /// </summary>
        static readonly System.Collections.Generic.List<SkinnedMeshRenderer> _scan = new System.Collections.Generic.List<SkinnedMeshRenderer>();

        /// <summary>Fills <paramref name="into"/> with the character's own meshes (no allocations; cheap enough to run every frame).</summary>
        public static void BodyMeshes(GameObject rig, System.Collections.Generic.List<SkinnedMeshRenderer> into)
        {
            into.Clear();
            rig.GetComponentsInChildren(true, _scan);
            for (int i = 0; i < _scan.Count; i++)
            {
                var r = _scan[i];
                if (r == null) continue;
                bool underSkeleton = false;
                for (var t = r.transform; t != null && t != rig.transform; t = t.parent)
                    if (t.name == "Origin") { underSkeleton = true; break; }
                // a held item hangs from the skeleton and is not one of the character's own parts
                if (!underSkeleton || BodyPartNames.Contains(r.name.ToLowerInvariant())) into.Add(r);
            }
            _scan.Clear();
        }

        static readonly System.Collections.Generic.HashSet<string> BodyPartNames = new System.Collections.Generic.HashSet<string>
        {
            "head", "eyes", "beard", "hair", "body", "hands", "feet", "helmet", "torso", "gloves", "boots",
        };

        public static GameObject Create(string name, Mesh mesh, int layer)
        {
            var go = new GameObject("SVD_" + name) { layer = layer };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Material;
            return go;
        }

        // Skin rectangles (u, v) of each part's box, sizes in skin pixels (w, h, d).
        public static Mesh Head(float px) => Box(0, 0, 8, 8, 8, px, new Vector3(0, 4, 0), false);
        public static Mesh Body(float px) => Box(16, 16, 8, 12, 4, px, new Vector3(0, -6, 0), false);
        public static Mesh RightArm(float px) => Box(40, 16, 4, 12, 4, px, new Vector3(0, -5, 0), false);
        public static Mesh RightLeg(float px) => Box(0, 16, 4, 12, 4, px, new Vector3(0, -6, 0), false);
        public static Mesh LeftArm(float px) => Legacy ? Box(40, 16, 4, 12, 4, px, new Vector3(0, -5, 0), true)
                                                       : Box(32, 48, 4, 12, 4, px, new Vector3(0, -5, 0), false);
        public static Mesh LeftLeg(float px) => Legacy ? Box(0, 16, 4, 12, 4, px, new Vector3(0, -6, 0), true)
                                                       : Box(16, 48, 4, 12, 4, px, new Vector3(0, -6, 0), false);

        /// <summary>A w x h x d box; centre is its centre relative to the pivot, in skin pixels. +z is the front.</summary>
        static Mesh Box(int u, int v, int w, int h, int d, float px, Vector3 centre, bool mirror)
        {
            var tex = SteveCharacter.SkinTexture;
            float tw = tex != null ? tex.width : 64f, th = tex != null ? tex.height : 64f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var half = new Vector3(w, h, d) * 0.5f;

            void Face(Vector3 n, Vector3 up, int ru, int rv, int rw, int rh)
            {
                Vector3 right = Vector3.Cross(up, -n);   // viewer's right when looking at the face from outside
                float hw = Mathf.Abs(Vector3.Dot(right, half)), hh = Mathf.Abs(Vector3.Dot(up, half));
                Vector3 c = centre + Vector3.Scale(n, half);
                float uL = ru / tw, uR = (ru + rw) / tw, vT = 1f - rv / th, vB = 1f - (rv + rh) / th;
                int i = verts.Count;
                verts.Add((c - right * hw - up * hh) * px); uvs.Add(new Vector2(uL, vB));
                verts.Add((c - right * hw + up * hh) * px); uvs.Add(new Vector2(uL, vT));
                verts.Add((c + right * hw + up * hh) * px); uvs.Add(new Vector2(uR, vT));
                verts.Add((c + right * hw - up * hh) * px); uvs.Add(new Vector2(uR, vB));
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }

            Face(Vector3.up, Vector3.back, u + d, v, w, d);                 // top
            Face(Vector3.down, Vector3.forward, u + d + w, v, w, d);        // bottom
            Face(Vector3.right, Vector3.up, u, v + d, d, h);                // the part's right side
            Face(Vector3.forward, Vector3.up, u + d, v + d, w, h);          // front
            Face(Vector3.left, Vector3.up, u + d + w, v + d, d, h);         // left side
            Face(Vector3.back, Vector3.up, u + d + w + d, v + d, w, h);     // back

            if (mirror)
            {
                for (int i = 0; i < verts.Count; i++) verts[i] = new Vector3(-verts[i].x, verts[i].y, verts[i].z);
                for (int i = 0; i < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }

            var m = new Mesh { name = "SVD_box" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
