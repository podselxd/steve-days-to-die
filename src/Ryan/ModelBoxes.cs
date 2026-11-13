using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SteveDaysToDie.Ryan
{
    /// <summary>
    /// Any Minecraft block model drawn as Minecraft draws it: its own boxes ("elements"), each face with its texture
    /// piece (uv), element rotations (plants' crossed planes), the blockstate's turn and the 7 Days placement turn.
    /// Used by BlockOverlay for render "model" rows (slabs, stairs, plants, carpets, buttons, lanterns, fence posts...).
    /// Minecraft model space: 0..16 px, x east, y up, z south; ours: 0..1, z mirrored (Ryan's north = +z).
    /// </summary>
    public static class ModelBoxes
    {
        class El { public Vector3 from, to; public Vector3 rotOrigin; public char axis; public float angle; public bool rescale; public (int face, string tex, float[] uv, bool tint)[] faces; }

        static readonly Dictionary<string, List<El>> _cache = new Dictionary<string, List<El>>();
        static readonly Dictionary<string, List<(string model, int x, int y)>> _parts = new Dictionary<string, List<(string, int, int)>>();
        static readonly Color32 Foliage = new Color32(0x77, 0xAB, 0x2F, 255);
        static readonly Color32 White = new Color32(255, 255, 255, 255);

        static List<(string model, int x, int y)> Parts(string special)
        {
            if (string.IsNullOrEmpty(special)) return null;
            if (_parts.TryGetValue(special, out var l)) return l;
            l = new List<(string, int, int)>();
            foreach (var kv in special.Split(';'))
            {
                if (!kv.StartsWith("parts=")) continue;
                foreach (var p in kv.Substring(6).Split(','))
                {
                    var a = p.Split('@');
                    if (a.Length == 3 && int.TryParse(a[1], out int x) && int.TryParse(a[2], out int y)) l.Add((a[0], x, y));
                }
            }
            _parts[special] = l;
            return l;
        }

        static List<El> Elements(string model)
        {
            if (_cache.TryGetValue(model, out var list)) return list;
            list = new List<El>();
            _cache[model] = list;
            var tex = new Dictionary<string, string>();
            JArray elems = null;
            string n = model;
            for (int i = 0; i < 12 && n != null; i++)
            {
                var j = Mc.Model(n.Replace("minecraft:", ""));
                if (j == null) break;
                if (j["textures"] is JObject t)
                    foreach (var p in t.Properties()) if (!tex.ContainsKey(p.Name)) tex[p.Name] = BlockModels.TexValue(p.Value);
                if (elems == null && j["elements"] is JArray e) elems = e;
                n = (string)j["parent"];
            }
            if (elems == null) return list;
            string Ref(string r)
            {
                for (int k = 0; k < 8 && r != null && r.StartsWith("#"); k++) r = tex.TryGetValue(r.Substring(1), out var v) ? v : null;
                return r;
            }
            foreach (var e in elems)
            {
                if (!(e["from"] is JArray f) || !(e["to"] is JArray t2) || !(e["faces"] is JObject faces)) continue;
                var el = new El { from = new Vector3((float)f[0], (float)f[1], (float)f[2]), to = new Vector3((float)t2[0], (float)t2[1], (float)t2[2]) };
                if (e["rotation"] is JObject rot && rot["origin"] is JArray o)
                {
                    el.rotOrigin = new Vector3((float)o[0], (float)o[1], (float)o[2]);
                    el.axis = ((string)rot["axis"] ?? "y")[0];
                    el.angle = (float?)rot["angle"] ?? 0f;
                    el.rescale = (bool?)rot["rescale"] ?? false;
                }
                var fl = new List<(int, string, float[], bool)>();
                foreach (var p in faces.Properties())
                {
                    int idx = System.Array.IndexOf(Face.Names, p.Name);
                    string tx = Ref(BlockModels.TexValue(p.Value["texture"]));
                    if (idx < 0 || tx == null) continue;
                    float[] uv = p.Value["uv"] is JArray u && u.Count == 4 ? new[] { (float)u[0], (float)u[1], (float)u[2], (float)u[3] } : null;
                    fl.Add((idx, tx, uv, p.Value["tintindex"] != null));
                }
                el.faces = fl.ToArray();
                list.Add(el);
            }
            return list;
        }

        // Minecraft's automatic uv for a face when the model gives none (px, v from the top)
        static float[] AutoUv(int face, Vector3 a, Vector3 b)
        {
            switch (face)
            {
                case Face.Up: case Face.Down: return new[] { a.x, a.z, b.x, b.z };
                case Face.North: case Face.South: return new[] { a.x, 16f - b.y, b.x, 16f - a.y };
                default: return new[] { a.z, 16f - b.y, b.z, 16f - a.y };
            }
        }

        /// <summary>Adds the block's boxes to k, block corner at 'at'. facing = 7 Days placement turn (0..3).</summary>
        public static void Append(MeshKit k, Vector3 at, string special, int facing)
        {
            var parts = Parts(special);
            if (parts == null) return;
            foreach (var (model, rx, ry) in parts)
            {
                foreach (var el in Elements(model))
                {
                    int first = k.v.Count;
                    // ours: z mirrored
                    Vector3 min = new Vector3(el.from.x, el.from.y, 16f - el.to.z) / 16f;
                    Vector3 max = new Vector3(el.to.x, el.to.y, 16f - el.from.z) / 16f;
                    // flat elements (plants' planes) get a hair of thickness so both faces exist
                    for (int c = 0; c < 3; c++) if (max[c] - min[c] < 0.0005f) { min[c] -= 0.0002f; max[c] += 0.0002f; }
                    foreach (var (face, tx, uv0, tinted) in el.faces)
                    {
                        Rect tile = Atlas.Uv(new List<FaceLayer> { new FaceLayer { tex = tx, tint = tinted } }, tinted ? Foliage : White);
                        float[] uv = uv0 ?? AutoUv(face, el.from, el.to);
                        var r = new Rect(tile.x + tile.width * uv[0] / 16f, tile.y + tile.height * (16f - uv[3]) / 16f,
                                         tile.width * (uv[2] - uv[0]) / 16f, tile.height * (uv[3] - uv[1]) / 16f);
                        k.Face(face, at + min, at + max, r);
                    }
                    if (el.angle != 0f)
                    {
                        Vector3 piv = at + new Vector3(el.rotOrigin.x, el.rotOrigin.y, 16f - el.rotOrigin.z) / 16f;
                        Vector3 axis = el.axis == 'x' ? Vector3.right : el.axis == 'z' ? Vector3.forward : Vector3.up;
                        float ang = el.axis == 'z' ? el.angle : -el.angle;        // z mirrored turns x and y rotations round
                        if (el.rescale)
                        {
                            float s = 1f / Mathf.Cos(el.angle * Mathf.Deg2Rad);
                            k.Scale(first, new Vector3(el.axis == 'x' ? 1f : s, el.axis == 'y' ? 1f : s, el.axis == 'z' ? 1f : s), piv);
                        }
                        k.Rotate(first, Quaternion.AngleAxis(ang, axis), piv);
                    }
                    Vector3 centre = at + new Vector3(0.5f, 0.5f, 0.5f);
                    if (rx != 0) k.Rotate(first, Quaternion.Euler(rx, 0f, 0f), centre);
                    int turn = ry + facing * 90;
                    if (turn % 360 != 0) k.Rotate(first, Quaternion.Euler(0f, turn, 0f), centre);
                }
            }
        }
    }
}
