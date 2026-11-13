using System.Collections.Generic;
using UnityEngine;

namespace SteveDaysToDie.Mobs
{
    /// <summary>
    /// Minecraft's look for the Minecraft animals (cow, pig, sheep, chicken: sheets/mc_animals.json). Minecraft keeps its
    /// models in Java code, so the boxes are rebuilt here from Minecraft's model layouts (in Minecraft's pixels), with the
    /// texture from the player's own Minecraft. The 7 Days animal underneath gives the body, AI and health; its own
    /// meshes are hidden every frame. Legs swing like Minecraft's quadrupeds while walking; dead animals lie on their side.
    /// </summary>
    public class McAnimal : MonoBehaviour
    {
        // one box: offset (from the part's pivot) and size, Minecraft model space (y down, -z = front), texture u,v
        struct B { public float x, y, z, w, h, d; public int u, v; public float grow; public bool wool, mirror; }
        class Part { public string name; public Vector3 pivot; public float rotX, rotY, rotZ; public B[] boxes; public int leg = -1; public bool head; public int wing; public Part[] kids; public bool glass; public int spiderLeg; }

        static B Bx(float x, float y, float z, int w, int h, int d, int u, int v, float grow = 0f, bool wool = false)
            => new B { x = x, y = y, z = z, w = w, h = h, d = d, u = u, v = v, grow = grow, wool = wool };

        // Minecraft's models (QuadrupedModel / CowModel / PigModel / SheepModel / ChickenModel)
        static Part[] Model(string id)
        {
            switch (id)
            {
                case "cow":
                    // Minecraft 26.x CowModel (read from the game's own code): head with the muzzle (6x3x1 at 1,33), horns
                    return new[]
                    {
                        new Part { name = "head", head = true, pivot = new Vector3(0, 4, -8), boxes = new[] { Bx(-4, -4, -6, 8, 8, 6, 0, 0), Bx(-3, 1, -7, 6, 3, 1, 1, 33), Bx(-5, -5, -5, 1, 3, 1, 22, 0), Bx(4, -5, -5, 1, 3, 1, 22, 0) } },
                        new Part { name = "body", pivot = new Vector3(0, 5, 2), rotX = 90, boxes = new[] { Bx(-6, -10, -7, 12, 18, 10, 18, 4), Bx(-2, 2, -8, 4, 6, 1, 52, 0) } },
                        Leg(0, new Vector3(-4, 12, 7), 12), Leg(1, new Vector3(4, 12, 7), 12), Leg(2, new Vector3(-4, 12, -6), 12), Leg(3, new Vector3(4, 12, -6), 12),
                    };
                case "pig":
                    return new[]
                    {
                        new Part { name = "head", head = true, pivot = new Vector3(0, 12, -6), boxes = new[] { Bx(-4, -4, -8, 8, 8, 8, 0, 0), Bx(-2, 0, -9, 4, 3, 1, 16, 16) } },
                        new Part { name = "body", pivot = new Vector3(0, 11, 2), rotX = 90, boxes = new[] { Bx(-5, -10, -7, 10, 16, 8, 28, 8) } },
                        Leg(0, new Vector3(-3, 18, 7), 6), Leg(1, new Vector3(3, 18, 7), 6), Leg(2, new Vector3(-3, 18, -5), 6), Leg(3, new Vector3(3, 18, -5), 6),
                    };
                case "sheep":
                    return new[]
                    {
                        new Part { name = "head", head = true, pivot = new Vector3(0, 6, -8), boxes = new[] { Bx(-3, -4, -6, 6, 6, 8, 0, 0), Bx(-3, -4, -4, 6, 6, 6, 0, 0, 0.6f, true) } },
                        new Part { name = "body", pivot = new Vector3(0, 5, 2), rotX = 90, boxes = new[] { Bx(-4, -10, -7, 8, 16, 6, 28, 8), Bx(-4, -10, -7, 8, 16, 6, 28, 8, 1.75f, true) } },
                        SheepLeg(0, new Vector3(-3, 12, 7)), SheepLeg(1, new Vector3(3, 12, 7)), SheepLeg(2, new Vector3(-3, 12, -5)), SheepLeg(3, new Vector3(3, 12, -5)),
                    };
                case "creeper":
                    return new[]
                    {
                        new Part { name = "head", head = true, pivot = new Vector3(0, 6, 0), boxes = new[] { Bx(-4, -8, -4, 8, 8, 8, 0, 0) } },
                        new Part { name = "body", pivot = new Vector3(0, 6, 0), boxes = new[] { Bx(-4, 0, -2, 8, 12, 4, 16, 16) } },
                        Leg4(0, new Vector3(-2, 18, 4)), Leg4(1, new Vector3(2, 18, 4)), Leg4(2, new Vector3(-2, 18, -4)), Leg4(3, new Vector3(2, 18, -4)),
                    };
                case "slime":
                    // Minecraft's SlimeModel: a see-through outer cube with the core, eyes and mouth inside (64x32)
                    return new[]
                    {
                        new Part { name = "outer", glass = true, pivot = new Vector3(0, 0, 0), boxes = new[] { Bx(-4, 16, -4, 8, 8, 8, 0, 0) } },
                        new Part { name = "core", pivot = new Vector3(0, 0, 0), boxes = new[] { Bx(-3, 17, -3, 6, 6, 6, 0, 16), Bx(-3.25f, 18, -3.5f, 2, 2, 2, 32, 0), Bx(1.25f, 18, -3.5f, 2, 2, 2, 32, 4), Bx(0, 21, -3.5f, 1, 1, 1, 32, 8) } },
                    };
                case "phantom":
                    // Minecraft's PhantomModel (texture 64x64): body, head, two-part wings that flap, two-part tail
                    return new[]
                    {
                        new Part { name = "body", pivot = new Vector3(0, 12, 0), rotX = -6, boxes = new[] { Bx(-3, -2, -8, 5, 3, 9, 0, 8) },
                                   kids = new[] {
                                       new Part { name = "head", head = true, pivot = new Vector3(0, 1, -7), rotX = 11, boxes = new[] { Bx(-4, -2, -5, 7, 3, 5, 0, 0) } },
                                       new Part { name = "tail", pivot = new Vector3(0, -2, 1), boxes = new[] { Bx(-2, 0, 0, 3, 2, 6, 3, 20), Bx(-1, 0.5f, 6, 1, 1, 6, 4, 29) } },
                                       new Part { name = "wingL", wing = 1, pivot = new Vector3(2, -2, -8), boxes = new[] { Bx(0, 0, 0, 6, 2, 9, 23, 12), Bx(6, 0, 0, 13, 1, 9, 16, 24) } },
                                       new Part { name = "wingR", wing = -1, pivot = new Vector3(-3, -2, -8), boxes = new[] { Bx(-6, 0, 0, 6, 2, 9, 23, 12), Bx(-19, 0, 0, 13, 1, 9, 16, 24) } },
                                   } },
                    };
                case "chicken":
                    return new[]
                    {
                        new Part { name = "head", head = true, pivot = new Vector3(0, 15, -4), boxes = new[] { Bx(-2, -6, -2, 4, 6, 3, 0, 0), Bx(-2, -4, -4, 4, 2, 2, 14, 0), Bx(-1, -2, -3, 2, 2, 2, 14, 4) } },
                        new Part { name = "body", pivot = new Vector3(0, 16, 0), rotX = 90, boxes = new[] { Bx(-3, -4, -3, 6, 8, 6, 0, 9) } },
                        new Part { name = "legR", leg = 0, pivot = new Vector3(-2, 19, 1), boxes = new[] { Bx(-1, 0, -3, 3, 5, 3, 26, 0) } },
                        new Part { name = "legL", leg = 1, pivot = new Vector3(1, 19, 1), boxes = new[] { Bx(-1, 0, -3, 3, 5, 3, 26, 0) } },
                        new Part { name = "wingR", pivot = new Vector3(-4, 13, 0), boxes = new[] { Bx(0, 0, -3, 1, 4, 6, 24, 13) } },
                        new Part { name = "wingL", pivot = new Vector3(4, 13, 0), boxes = new[] { Bx(-1, 0, -3, 1, 4, 6, 24, 13) } },
                    };
            }
            return null;
        }

        // every other mob: the model read from Minecraft's own code (tools/gen_mc_models.py -> Generated/McModelsTable)
        static Part[] FromTable(string id)
        {
            if (!Generated.McModelsTable.Models.TryGetValue(id, out var m)) return null;
            var all = new List<Part>(); var parent = new List<int>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            float F(string x) => float.Parse(x, inv);
            foreach (var line in m.parts.Split('\n'))
            {
                var f = line.Split(';');
                var pv = f[2].Split(','); var rt = f[3].Split(',');
                var boxes = new List<B>();
                if (f.Length > 4 && f[4].Length > 0)
                    foreach (var bs in f[4].Split('|'))
                    {
                        var c = bs.Split(',');
                        boxes.Add(new B { u = int.Parse(c[0]), v = int.Parse(c[1]), x = F(c[2]), y = F(c[3]), z = F(c[4]), w = F(c[5]), h = F(c[6]), d = F(c[7]), grow = F(c[8]), mirror = c[9] == "1" });
                    }
                string n = f[0];
                var part = new Part { name = n, pivot = new Vector3(F(pv[0]), F(pv[1]), F(pv[2])), rotX = F(rt[0]), rotY = F(rt[1]), rotZ = F(rt[2]), boxes = boxes.ToArray(), head = n == "head" };
                // Minecraft's walk: right hind + left front together, the other two half a turn later; arms against legs
                switch (n)
                {
                    case "right_hind_leg": case "right_leg": case "left_arm": part.leg = 0; break;
                    case "left_hind_leg": case "left_leg": case "right_arm": part.leg = 1; break;
                    case "right_front_leg": part.leg = id == "spider" ? -1 : 2; break;
                    case "left_front_leg": part.leg = id == "spider" ? -1 : 3; break;
                    case "right_wing": case "right_wing_base": part.wing = -1; break;
                    case "left_wing": case "left_wing_base": part.wing = 1; break;
                }
                if (id == "spider" && n.EndsWith("_leg"))
                {
                    int k = n.Contains("middle_front") ? 2 : n.Contains("middle_hind") ? 3 : n.Contains("front") ? 1 : 4;
                    part.leg = -1; part.spiderLeg = n.StartsWith("right") ? k : -k;
                }
                if (id == "spider" && (n == "right_hind_leg" || n == "left_hind_leg")) part.leg = -1;
                all.Add(part); parent.Add(int.Parse(f[1]));
            }
            var top = new List<Part>(); var kids = new Dictionary<int, List<Part>>();
            for (int i = 0; i < all.Count; i++)
            {
                if (parent[i] < 0) top.Add(all[i]);
                else { if (!kids.TryGetValue(parent[i], out var l)) kids[parent[i]] = l = new List<Part>(); l.Add(all[i]); }
            }
            foreach (var kv in kids) all[kv.Key].kids = kv.Value.ToArray();
            return top.ToArray();
        }

        // Minecraft's rotation order (ModelPart: Z, then Y, then X); our axes are all turned round, which keeps the numbers
        static Quaternion Rot(float x, float y, float z) => Quaternion.AngleAxis(z, Vector3.forward) * Quaternion.AngleAxis(y, Vector3.up) * Quaternion.AngleAxis(x, Vector3.right);

        static Part Leg4(int i, Vector3 pivot) => new Part { name = "leg" + i, leg = i, pivot = pivot, boxes = new[] { Bx(-2, 0, -2, 4, 6, 4, 0, 16) } };
        static Part Leg(int i, Vector3 pivot, int h) => new Part { name = "leg" + i, leg = i, pivot = pivot, boxes = new[] { Bx(-2, 0, -2, 4, h, 4, 0, 16) } };
        static Part SheepLeg(int i, Vector3 pivot) => new Part { name = "leg" + i, leg = i, pivot = pivot, boxes = new[] { Bx(-2, 0, -2, 4, 12, 4, 0, 16), Bx(-2, 0, -2, 4, 6, 4, 0, 16, 0.5f, true) } };

        const float Px = 1f / 16f;
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Texture2D, Material> _mat = new Dictionary<Texture2D, Material>();

        public string AnimalId, TexturePath;
        /// <summary>Creeper: how swollen (0..1) and whether it flashes white this frame (McCreeper sets them).</summary>
        public float Swell; public bool Flash;
        /// <summary>On fire (in the sun): orange flicker. Scale: the whole model (slime sizes, cave spider).</summary>
        public bool Burning; public float Scale = 1f;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock _mpb;
        EntityAlive _e;
        Transform _root;
        readonly List<(Transform t, int leg, Vector3 r)> _legs = new List<(Transform, int, Vector3)>();
        readonly List<(Transform t, int side, Vector3 r)> _wings = new List<(Transform, int, Vector3)>();
        readonly List<(Transform t, int leg, float ry, float rz)> _spiderLegs = new List<(Transform, int, float, float)>();
        float _hop;
        readonly List<(Transform t, Vector3 p, Quaternion r, int i)> _spin = new List<(Transform, Vector3, Quaternion, int)>();
        readonly List<Renderer> _ours = new List<Renderer>();
        readonly List<Renderer> _scan = new List<Renderer>();
        readonly List<Renderer> _hide = new List<Renderer>();
        float _nextScan;
        /// <summary>All Minecraft mobs alive in this game (for "no blood on them").</summary>
        public static readonly List<McAnimal> All = new List<McAnimal>();
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        float _phase, _amount;
        Vector3 _lastPos;
        bool _built;

        void Start()
        {
            _e = GetComponent<EntityAlive>();
            _lastPos = transform.position;
        }

        static Texture2D Tex(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_tex.TryGetValue(path, out var t)) return t;
            t = MinecraftAssets.LoadTexture(path);
            if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
            _tex[path] = t;
            return t;
        }

        static Material Mat(Texture2D t)
        {
            if (t == null) return null;
            if (_mat.TryGetValue(t, out var m)) return m;
            m = Ryan.Mats.Cutout(t);
            _mat[t] = m;
            return m;
        }

        // the slime's outer jelly: see-through (Minecraft draws it translucent)
        static Material _glass;
        static Material Glass(Texture2D t)
        {
            if (_glass != null) return _glass;
            var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            _glass = sh != null ? new Material(sh) { mainTexture = t, renderQueue = 3000 } : Ryan.Mats.Blend(t, Color.white);
            return _glass;
        }

        void Build()
        {
            _built = true;
            var parts = Model(AnimalId) ?? FromTable(AnimalId);
            var tex = Tex(TexturePath);
            if (parts == null || tex == null) { Debug.LogWarning("[SVD] Minecraft animal " + AnimalId + ": no model or texture"); return; }
            var wool = AnimalId == "sheep" ? Tex("assets/minecraft/textures/entity/sheep/sheep_wool.png") : null;
            _root = new GameObject("SVD_McAnimal_" + AnimalId).transform;
            _root.SetParent(transform, false);
            void Add(Part part, Transform parent, bool child)
            {
                // Minecraft model space -> ours: x and z turned round, y up from the ground (24 px = the feet)
                var go = new GameObject("SVD_" + part.name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = child ? new Vector3(-part.pivot.x, -part.pivot.y, -part.pivot.z) * Px
                                                   : new Vector3(-part.pivot.x, 24f - part.pivot.y, -part.pivot.z) * Px;
                go.transform.localRotation = Rot(part.rotX, part.rotY, part.rotZ);
                foreach (var b in part.boxes)
                {
                    var t = b.wool ? wool : tex;
                    if (t == null) continue;
                    var c = new GameObject("box");
                    c.transform.SetParent(go.transform, false);
                    c.AddComponent<MeshFilter>().sharedMesh = Box(b, t);
                    var mr = c.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = part.glass ? Glass(t) : Mat(t);
                    _ours.Add(mr);
                }
                var baseRot = new Vector3(part.rotX, part.rotY, part.rotZ);
                if (part.wing != 0) _wings.Add((go.transform, part.wing, baseRot));
                if (part.leg >= 0) _legs.Add((go.transform, part.leg, baseRot));
                if (part.spiderLeg != 0) _spiderLegs.Add((go.transform, part.spiderLeg, part.rotY, part.rotZ));
                if (part.kids != null) foreach (var k in part.kids) Add(k, go.transform, true);
            }
            foreach (var part in parts) Add(part, _root, false);
            if (AnimalId == "blaze" || AnimalId == "ghast")
                foreach (Transform t in _root)
                    if (t.name.StartsWith("SVD_part")) _spin.Add((t, t.localPosition, t.localRotation, int.Parse(t.name.Substring(8))));
            Debug.Log($"[SVD] Minecraft {AnimalId} built ({_ours.Count} boxes)");
        }

        // a box with Minecraft's cube texture layout (the same one Steve's skin uses), centre relative to the pivot
        static Mesh Box(B b, Texture2D tex)
        {
            float tw = tex.width, th = tex.height;
            float u = b.u, v = b.v, w = b.w, h = b.h, d = b.d;
            Vector3 centre = new Vector3(-(b.x + w * 0.5f), -(b.y + h * 0.5f), -(b.z + d * 0.5f));
            var half = new Vector3(w, h, d) * 0.5f + Vector3.one * b.grow;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            void Face(Vector3 n, Vector3 up, float ru, float rv, float rw, float rh)
            {
                Vector3 right = Vector3.Cross(up, -n);
                float hw = Mathf.Abs(Vector3.Dot(right, half)), hh = Mathf.Abs(Vector3.Dot(up, half));
                Vector3 cc = centre + Vector3.Scale(n, half);
                float uL = ru / tw, uR = (ru + rw) / tw, vT = 1f - rv / th, vB = 1f - (rv + rh) / th;
                if (b.mirror) { float tmp = uL; uL = uR; uR = tmp; }
                int i = verts.Count;
                verts.Add((cc - right * hw - up * hh) * Px); uvs.Add(new Vector2(uL, vB));
                verts.Add((cc - right * hw + up * hh) * Px); uvs.Add(new Vector2(uL, vT));
                verts.Add((cc + right * hw + up * hh) * Px); uvs.Add(new Vector2(uR, vT));
                verts.Add((cc + right * hw - up * hh) * Px); uvs.Add(new Vector2(uR, vB));
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Face(Vector3.up, Vector3.back, u + d, v, w, d);
            Face(Vector3.down, Vector3.forward, u + d + w, v, w, d);
            Face(b.mirror ? Vector3.left : Vector3.right, Vector3.up, u, v + d, d, h);
            Face(Vector3.forward, Vector3.up, u + d, v + d, w, h);
            Face(b.mirror ? Vector3.right : Vector3.left, Vector3.up, u + d + w, v + d, d, h);
            Face(Vector3.back, Vector3.up, u + d + w + d, v + d, w, h);
            var m = new Mesh { name = "SVD_mob_box" };
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        void LateUpdate()
        {
            if (_e == null) return;
            if (!_built) Build();
            if (_root == null) return;
            // hide 7 Days' own animal (its meshes come and go: ragdoll, LOD)
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.5f;          // was every frame for every animal: slow with many of them
                GetComponentsInChildren(true, _scan);
                _hide.Clear();
                foreach (var r in _scan) if (r != null && !_ours.Contains(r)) _hide.Add(r);
                _scan.Clear();
            }
            foreach (var r in _hide) if (r != null && r.enabled) r.enabled = false;

            // keep upright on the animal's spot, turned with it
            _root.position = transform.position;
            _root.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            bool dead = _e.IsDead();
            if (dead) { _root.rotation *= Quaternion.Euler(0f, 0f, 90f); _root.position += Vector3.up * 0.2f; }

            // Minecraft voices: now and then, when hurt, when it dies
            Voices(dead);

            // creeper about to blow: swells and flashes white (Minecraft's CreeperRenderer)
            if (Swell > 0f)
            {
                float f = 1f + Mathf.Sin(Swell * 100f) * Swell * 0.01f;
                float g = Mathf.Clamp01(Swell); g *= g; g *= g;
                _root.localScale = new Vector3((1f + g * 0.4f) * f, (1f + g * 0.1f) / f, (1f + g * 0.4f) * f);
            }
            else _root.localScale = Vector3.one * Scale;
            // the slime hops: up and down while it moves, squashed when it lands (Minecraft's SlimeRenderer squish)
            if ((AnimalId == "slime" || AnimalId == "magma_cube" || AnimalId == "rabbit" || AnimalId == "frog") && !dead)
            {
                if (_amount > 0.05f || _hop > 0f) _hop += Time.deltaTime * 2.2f;
                if (_hop >= 1f) _hop = _amount > 0.05f ? 0f : 0f;
                float up = Mathf.Sin(_hop * Mathf.PI);
                float sq = 1f - up * 0.25f + (_hop < 0.1f && _amount > 0.05f ? 0.15f : 0f);
                _root.position += Vector3.up * up * 0.45f * Scale;
                _root.localScale = new Vector3(Scale / Mathf.Sqrt(sq), Scale * sq, Scale / Mathf.Sqrt(sq));
            }
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            // Minecraft: red for half a second when hurt (and red while dead); the creeper's white flash
            bool red = Time.time < _hurtUntil || dead;
            Color burn = new Color(1.6f, 0.9f + Mathf.Sin(Time.time * 23f) * 0.15f, 0.4f, 1f);
            _mpb.SetColor(ColorId, Flash ? new Color(3f, 3f, 3f, 1f) : red ? new Color(1f, 0.45f, 0.45f, 1f) : Burning ? burn : Color.white);
            foreach (var r in _ours) if (r != null) r.SetPropertyBlock(_mpb);

            // Minecraft's leg swing: cos(phase * 0.6662) * 1.4 * amount, opposite legs half a turn apart
            Vector3 p = transform.position;
            float moved = new Vector2(p.x - _lastPos.x, p.z - _lastPos.z).magnitude;
            _lastPos = p;
            float speed = Time.deltaTime > 0f ? moved / Time.deltaTime : 0f;
            _amount = Mathf.MoveTowards(_amount, dead ? 0f : Mathf.Clamp01(speed / 3f), Time.deltaTime * 4f);
            _phase += moved * 4f;          // Minecraft: limb swing grows 4 per block walked
            // phantom wings: Minecraft flaps them all the time (about 16 degrees up and down)
            float flapSpeed = AnimalId == "bee" ? 40f : AnimalId == "bat" || AnimalId == "allay" ? 14f : 7.5f;
            float flapSize = AnimalId == "phantom" ? 16f : 30f;
            foreach (var (t, side, r) in _wings)
                t.localRotation = Rot(r.x, r.y, r.z + side * Mathf.Cos(Time.time * flapSpeed) * flapSize);
            // blaze: three rings of 4 rods turn round (the middle one the other way) and bob; ghast: tentacles sway
            foreach (var (t, p0, r0, i) in _spin)
            {
                if (AnimalId == "blaze")
                {
                    int ring = i / 4; float dir = ring == 1 ? -1f : 1f;
                    float ang = Time.time * 60f * dir * (ring == 2 ? 0.75f : 1f);
                    t.localPosition = Quaternion.Euler(0f, ang, 0f) * new Vector3(p0.x, 0f, p0.z) + Vector3.up * (p0.y + Mathf.Cos(Time.time * 3f + i) * 0.06f);
                }
                else t.localRotation = r0 * Quaternion.Euler((0.2f * Mathf.Sin(Time.time * 6f + i) + 0.4f) * Mathf.Rad2Deg, 0f, 0f);
            }
            // spider legs: Minecraft's SpiderModel - pairs swing forward/back and lift up/down, alternating
            foreach (var (t, leg, ry, rz) in _spiderLegs)
            {
                int i = Mathf.Abs(leg) - 1; float s = leg > 0 ? 1f : -1f;
                float off = (i % 2 == 0) ? 0f : Mathf.PI;
                float swing = -Mathf.Cos(_phase * 0.6662f * 2f + off + i * Mathf.PI * 0.5f) * 0.4f * _amount;
                float lift = Mathf.Abs(Mathf.Sin(_phase * 0.6662f + off + i * Mathf.PI * 0.5f)) * 0.4f * _amount;
                t.localRotation = Rot(0f, ry + s * swing * Mathf.Rad2Deg, rz + s * lift * Mathf.Rad2Deg);
            }
            foreach (var (t, leg, r) in _legs)
            {
                float a = Mathf.Cos(_phase * 0.6662f + ((leg == 0 || leg == 3) ? 0f : Mathf.PI)) * 1.4f * _amount;
                t.localRotation = Rot(r.x + a * Mathf.Rad2Deg, r.y, r.z);
            }
        }

        float _nextSay = -1f, _lastHp = -1f, _hurtUntil; bool _saidDeath;
        void Voices(bool dead)
        {
            string say, hurt, death;
            switch (AnimalId)
            {
                case "cow": say = "mob/cow/say"; hurt = "mob/cow/hurt"; death = hurt; break;
                case "pig": say = "mob/pig/say"; hurt = say; death = "mob/pig/death"; break;
                case "sheep": say = "mob/sheep/say"; hurt = say; death = say; break;
                case "chicken": say = "mob/chicken/say"; hurt = "mob/chicken/hurt"; death = hurt; break;
                case "skeleton": say = "mob/skeleton/say"; hurt = "mob/skeleton/hurt"; death = "mob/skeleton/death"; break;
                case "spider": case "cave_spider": say = "mob/spider/say"; hurt = say; death = "mob/spider/death"; break;
                case "slime": say = null; hurt = Scale > 1.5f ? "mob/slime/big" : "mob/slime/small"; death = hurt; break;
                case "wolf": say = "mob/wolf/bark"; hurt = "mob/wolf/hurt"; death = "mob/wolf/death"; break;
                case "magma_cube": say = null; hurt = Scale > 1.5f ? "mob/magmacube/big" : "mob/magmacube/small"; death = hurt; break;
                case "rabbit": say = "mob/rabbit/idle"; hurt = "mob/rabbit/hurt"; death = "mob/rabbit/bunnymurder"; break;
                case "fox": say = "mob/fox/idle"; hurt = "mob/fox/hurt"; death = "mob/fox/death"; break;
                case "goat": say = "mob/goat/idle"; hurt = "mob/goat/hurt"; death = "mob/goat/death"; break;
                case "horse": say = "mob/horse/idle"; hurt = "mob/horse/hit"; death = "mob/horse/death"; break;
                case "llama": say = "mob/llama/idle"; hurt = "mob/llama/hurt"; death = "mob/llama/death"; break;
                case "camel": say = "mob/camel/ambient"; hurt = "mob/camel/hurt"; death = "mob/camel/death"; break;
                case "cat": say = "mob/cat/meow"; hurt = "mob/cat/hitt"; death = hurt; break;
                case "villager": say = "mob/villager/idle"; hurt = "mob/villager/hit"; death = "mob/villager/death"; break;
                case "bat": say = "mob/bat/idle"; hurt = "mob/bat/hurt"; death = "mob/bat/death"; break;
                case "parrot": say = "mob/parrot/idle"; hurt = "mob/parrot/hurt"; death = "mob/parrot/death"; break;
                case "bee": say = "mob/bee/buzz"; hurt = "mob/bee/hurt"; death = "mob/bee/death"; break;
                case "polar_bear": say = "mob/polarbear/idle"; hurt = "mob/polarbear/hurt"; death = "mob/polarbear/death"; break;
                case "panda": say = "mob/panda/idle"; hurt = "mob/panda/hurt"; death = "mob/panda/death"; break;
                case "iron_golem": say = null; hurt = "mob/irongolem/hit"; death = "mob/irongolem/death"; break;
                case "snow_golem": say = null; hurt = "mob/snowgolem/hurt"; death = "mob/snowgolem/death"; break;
                case "witch": say = "mob/witch/ambient"; hurt = "mob/witch/hurt"; death = "mob/witch/death"; break;
                case "illager": say = "mob/pillager/idle"; hurt = "mob/pillager/hurt"; death = "mob/pillager/death"; break;
                case "blaze": say = "mob/blaze/breathe"; hurt = "mob/blaze/hit"; death = "mob/blaze/death"; break;
                case "ghast": say = "mob/ghast/moan"; hurt = "mob/ghast/scream"; death = "mob/ghast/death"; break;
                case "hoglin": say = "mob/hoglin/idle"; hurt = "mob/hoglin/hurt"; death = "mob/hoglin/death"; break;
                case "piglin": say = "mob/piglin/idle"; hurt = "mob/piglin/hurt"; death = "mob/piglin/death"; break;
                case "frog": say = "mob/frog/idle"; hurt = "mob/frog/hurt"; death = "mob/frog/death"; break;
                case "armadillo": say = "mob/armadillo/ambient"; hurt = "mob/armadillo/hurt"; death = "mob/armadillo/death"; break;
                case "sniffer": say = "mob/sniffer/idle"; hurt = "mob/sniffer/hurt"; death = "mob/sniffer/death"; break;
                case "turtle": say = "mob/turtle/idle"; hurt = "mob/turtle/hurt"; death = "mob/turtle/death"; break;
                case "allay": say = "mob/allay/idle"; hurt = "mob/allay/hurt"; death = "mob/allay/death"; break;
                default: say = null; hurt = null; death = null; break;
            }
            Vector3 at = transform.position + Vector3.up * 0.8f;
            if (dead)
            {
                if (!_saidDeath && death != null) { _saidDeath = true; Sound.MinecraftSounds.PlayGroup(death, at, 16f, 1f, AnimalId == "pig" || AnimalId == "cow" ? 0.8f : 1f); }
                return;
            }
            float hp = _e.Health;
            if (_lastHp >= 0f && hp < _lastHp) { if (hurt != null) Sound.MinecraftSounds.PlayGroup(hurt, at, 16f, 1f, 1f + (Random.value - Random.value) * 0.2f); _hurtUntil = Time.time + 0.5f; }
            // the slime squelches every time it lands
            if ((AnimalId == "slime" || AnimalId == "magma_cube") && _hop > 0f && _hop < 0.05f && hurt != null) Sound.MinecraftSounds.PlayGroup(hurt, at, 16f, 0.4f * Scale, 1f);
            if (say == null) { _lastHp = hp; return; }
            _lastHp = hp;
            if (_nextSay < 0f) _nextSay = Time.time + Random.Range(4f, 16f);
            if (Time.time >= _nextSay) { _nextSay = Time.time + Random.Range(8f, 20f); Sound.MinecraftSounds.PlayGroup(say, at, 16f, 0.8f, 1f + (Random.value - Random.value) * 0.2f); }
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
