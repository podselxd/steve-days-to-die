using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Steve
{
    /// <summary>
    /// Minecraft armour worn on Steve, seen by everybody: Minecraft's armour layer boxes (a little bigger than Steve's,
    /// 1 px for helmet, chestplate and boots, 0.5 px for leggings) with the material's texture from the player's
    /// Minecraft (entity/equipment/humanoid[_leggings]/&lt;material&gt;.png, 64x32 layout). Leather is dyed Minecraft's brown.
    /// Built when the equipment changes (SteveModel.Armor every frame, cheap check).
    /// </summary>
    public static class SteveArmor
    {
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        static string Material(string mcId)
        {
            string m = mcId.Substring(0, mcId.LastIndexOf('_'));
            switch (m)
            {
                case "golden": return "gold";
                case "turtle": return "turtle_scute";
                default: return m;
            }
        }

        static Material Mat(string mat, bool legs)
        {
            string key = mat + (legs ? "_leggings" : "");
            if (_mats.TryGetValue(key, out var m)) return m;
            var t = MinecraftAssets.LoadTexture("assets/minecraft/textures/entity/equipment/humanoid" + (legs ? "_leggings" : "") + "/" + mat + ".png");
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            m = t != null ? Ryan.Mats.Cutout(t) : null;
            if (m != null && mat == "leather") m.color = new Color(0xA0 / 255f, 0x65 / 255f, 0x40 / 255f);
            _mats[key] = m;
            return m;
        }

        /// <summary>Builds the armour pieces for the 4 slots (Minecraft ids or null) under Steve's parts.</summary>
        public static List<GameObject> Build(string head, string chest, string legs, string feet, float px, int layer,
                                             Transform headT, Transform upperT, Transform armR, Transform armL, Transform legR, Transform legL)
        {
            var made = new List<GameObject>();
            void Piece(Transform parent, Material mat, int u, int v, int w, int h, int d, Vector3 centre, float grow, bool mirror)
            {
                if (parent == null || mat == null) return;
                var go = new GameObject("SVD_armor") { layer = layer };
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = Box(u, v, w, h, d, px, centre, grow, mirror, 64f, 32f);
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                made.Add(go);
            }
            if (head != null) Piece(headT, Mat(Material(head), false), 0, 0, 8, 8, 8, new Vector3(0, 4, 0), 1f, false);
            if (chest != null)
            {
                var m = Mat(Material(chest), false);
                Piece(upperT, m, 16, 16, 8, 12, 4, new Vector3(0, -6, 0), 1f, false);
                Piece(armR, m, 40, 16, 4, 12, 4, new Vector3(0, -5, 0), 1f, false);
                Piece(armL, m, 40, 16, 4, 12, 4, new Vector3(0, -5, 0), 1f, true);
            }
            if (legs != null)
            {
                var m = Mat(Material(legs), true);
                Piece(upperT, m, 16, 16, 8, 12, 4, new Vector3(0, -6, 0), 0.5f, false);
                Piece(legR, m, 0, 16, 4, 12, 4, new Vector3(0, -6, 0), 0.5f, false);
                Piece(legL, m, 0, 16, 4, 12, 4, new Vector3(0, -6, 0), 0.5f, true);
            }
            if (feet != null)
            {
                var m = Mat(Material(feet), false);
                Piece(legR, m, 0, 16, 4, 12, 4, new Vector3(0, -6, 0), 1f, false);
                Piece(legL, m, 0, 16, 4, 12, 4, new Vector3(0, -6, 0), 1f, true);
            }
            return made;
        }

        // the same box as Steve's (SteveParts.Box), with a texture size and grown on every side
        static Mesh Box(int u, int v, int w, int h, int d, float px, Vector3 centre, float grow, bool mirror, float tw, float th)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            var half = new Vector3(w, h, d) * 0.5f + Vector3.one * grow;
            void Face(Vector3 n, Vector3 up, int ru, int rv, int rw, int rh)
            {
                Vector3 right = Vector3.Cross(up, -n);
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
            Face(Vector3.up, Vector3.back, u + d, v, w, d);
            Face(Vector3.down, Vector3.forward, u + d + w, v, w, d);
            Face(Vector3.right, Vector3.up, u, v + d, d, h);
            Face(Vector3.forward, Vector3.up, u + d, v + d, w, h);
            Face(Vector3.left, Vector3.up, u + d + w, v + d, d, h);
            Face(Vector3.back, Vector3.up, u + d + w + d, v + d, w, h);
            if (mirror)
            {
                for (int i = 0; i < verts.Count; i++) verts[i] = new Vector3(-verts[i].x, verts[i].y, verts[i].z);
                for (int i = 0; i < tris.Count; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }
            var m = new Mesh { name = "SVD_armor_box" };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
