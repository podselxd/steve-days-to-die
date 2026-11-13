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
        struct B { public float x, y, z; public int w, h, d, u, v; public float grow; public bool wool; }
        class Part { public string name; public Vector3 pivot; public float rotX; public B[] boxes; public int leg = -1; public bool head; }

        static B Bx(float x, float y, float z, int w, int h, int d, int u, int v, float grow = 0f, bool wool = false)
            => new B { x = x, y = y, z = z, w = w, h = h, d = d, u = u, v = v, grow = grow, wool = wool };

        // Minecraft's models (QuadrupedModel / CowModel / PigModel / SheepModel / ChickenModel)
        static Part[] Model(string id)
        {
            switch (id)
            {
                case "cow":
                    return new[]
                    {
                        new Part { name = "head", head = true, pivot = new Vector3(0, 4, -8), boxes = new[] { Bx(-4, -4, -6, 8, 8, 6, 0, 0), Bx(-5, -5, -4, 1, 3, 1, 22, 0), Bx(4, -5, -4, 1, 3, 1, 22, 0) } },
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

        static Part Leg4(int i, Vector3 pivot) => new Part { name = "leg" + i, leg = i, pivot = pivot, boxes = new[] { Bx(-2, 0, -2, 4, 6, 4, 0, 16) } };
        static Part Leg(int i, Vector3 pivot, int h) => new Part { name = "leg" + i, leg = i, pivot = pivot, boxes = new[] { Bx(-2, 0, -2, 4, h, 4, 0, 16) } };
        static Part SheepLeg(int i, Vector3 pivot) => new Part { name = "leg" + i, leg = i, pivot = pivot, boxes = new[] { Bx(-2, 0, -2, 4, 12, 4, 0, 16), Bx(-2, 0, -2, 4, 6, 4, 0, 16, 0.5f, true) } };

        const float Px = 1f / 16f;
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Texture2D, Material> _mat = new Dictionary<Texture2D, Material>();

        public string AnimalId, TexturePath;
        /// <summary>Creeper: how swollen (0..1) and whether it flashes white this frame (McCreeper sets them).</summary>
        public float Swell; public bool Flash;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        MaterialPropertyBlock _mpb;
        EntityAlive _e;
        Transform _root;
        readonly List<(Transform t, int leg)> _legs = new List<(Transform, int)>();
        readonly List<Renderer> _ours = new List<Renderer>();
        readonly List<Renderer> _scan = new List<Renderer>();
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

        void Build()
        {
            _built = true;
            var parts = Model(AnimalId);
            var tex = Tex(TexturePath);
            if (parts == null || tex == null) { Debug.LogWarning("[SVD] Minecraft animal " + AnimalId + ": no model or texture"); return; }
            var wool = AnimalId == "sheep" ? Tex("assets/minecraft/textures/entity/sheep/sheep_wool.png") : null;
            _root = new GameObject("SVD_McAnimal_" + AnimalId).transform;
            _root.SetParent(transform, false);
            foreach (var part in parts)
            {
                // Minecraft model space -> ours: x and z turned round, y up from the ground (24 px = the feet)
                var go = new GameObject("SVD_" + part.name);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = new Vector3(-part.pivot.x, 24f - part.pivot.y, -part.pivot.z) * Px;
                go.transform.localRotation = Quaternion.Euler(part.rotX, 0f, 0f);
                foreach (var b in part.boxes)
                {
                    var t = b.wool ? wool : tex;
                    if (t == null) continue;
                    var c = new GameObject("box");
                    c.transform.SetParent(go.transform, false);
                    c.AddComponent<MeshFilter>().sharedMesh = Box(b, t);
                    var mr = c.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mat(t);
                    _ours.Add(mr);
                }
                if (part.leg >= 0) _legs.Add((go.transform, part.leg));
            }
            Debug.Log($"[SVD] Minecraft {AnimalId} built ({_ours.Count} boxes)");
        }

        // a box with Minecraft's cube texture layout (the same one Steve's skin uses), centre relative to the pivot
        static Mesh Box(B b, Texture2D tex)
        {
            float tw = tex.width, th = tex.height;
            int u = b.u, v = b.v, w = b.w, h = b.h, d = b.d;
            Vector3 centre = new Vector3(-(b.x + w * 0.5f), -(b.y + h * 0.5f), -(b.z + d * 0.5f));
            var half = new Vector3(w, h, d) * 0.5f + Vector3.one * b.grow;
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            void Face(Vector3 n, Vector3 up, int ru, int rv, int rw, int rh)
            {
                Vector3 right = Vector3.Cross(up, -n);
                float hw = Mathf.Abs(Vector3.Dot(right, half)), hh = Mathf.Abs(Vector3.Dot(up, half));
                Vector3 cc = centre + Vector3.Scale(n, half);
                float uL = ru / tw, uR = (ru + rw) / tw, vT = 1f - rv / th, vB = 1f - (rv + rh) / th;
                int i = verts.Count;
                verts.Add((cc - right * hw - up * hh) * Px); uvs.Add(new Vector2(uL, vB));
                verts.Add((cc - right * hw + up * hh) * Px); uvs.Add(new Vector2(uL, vT));
                verts.Add((cc + right * hw + up * hh) * Px); uvs.Add(new Vector2(uR, vT));
                verts.Add((cc + right * hw - up * hh) * Px); uvs.Add(new Vector2(uR, vB));
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            Face(Vector3.up, Vector3.back, u + d, v, w, d);
            Face(Vector3.down, Vector3.forward, u + d + w, v, w, d);
            Face(Vector3.right, Vector3.up, u, v + d, d, h);
            Face(Vector3.forward, Vector3.up, u + d, v + d, w, h);
            Face(Vector3.left, Vector3.up, u + d + w, v + d, d, h);
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
            GetComponentsInChildren(true, _scan);
            foreach (var r in _scan) if (r != null && r.enabled && !_ours.Contains(r)) r.enabled = false;
            _scan.Clear();

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
            else _root.localScale = Vector3.one;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor(ColorId, Flash ? new Color(3f, 3f, 3f, 1f) : Color.white);
            foreach (var r in _ours) if (r != null) r.SetPropertyBlock(_mpb);

            // Minecraft's leg swing: cos(phase * 0.6662) * 1.4 * amount, opposite legs half a turn apart
            Vector3 p = transform.position;
            float moved = new Vector2(p.x - _lastPos.x, p.z - _lastPos.z).magnitude;
            _lastPos = p;
            float speed = Time.deltaTime > 0f ? moved / Time.deltaTime : 0f;
            _amount = Mathf.MoveTowards(_amount, dead ? 0f : Mathf.Clamp01(speed / 3f), Time.deltaTime * 4f);
            _phase += moved * 6f;
            foreach (var (t, leg) in _legs)
            {
                float a = Mathf.Cos(_phase * 0.6662f + ((leg == 0 || leg == 3) ? 0f : Mathf.PI)) * 1.4f * _amount;
                t.localRotation = Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f);
            }
        }

        float _nextSay = -1f, _lastHp = -1f; bool _saidDeath;
        void Voices(bool dead)
        {
            string say = AnimalId == "cow" ? "mob/cow/say" : AnimalId == "pig" ? "mob/pig/say" : AnimalId == "sheep" ? "mob/sheep/say" : AnimalId == "chicken" ? "mob/chicken/say" : null;
            string hurt = AnimalId == "cow" ? "mob/cow/hurt" : AnimalId == "chicken" ? "mob/chicken/hurt" : say;
            if (say == null) return;
            Vector3 at = transform.position + Vector3.up * 0.8f;
            if (dead)
            {
                if (!_saidDeath) { _saidDeath = true; Sound.MinecraftSounds.PlayGroup(AnimalId == "pig" ? "mob/pig/death" : hurt, at, 16f, 1f, 0.8f); }
                return;
            }
            float hp = _e.Health;
            if (_lastHp >= 0f && hp < _lastHp) Sound.MinecraftSounds.PlayGroup(hurt, at, 16f, 1f, 1f + (Random.value - Random.value) * 0.2f);
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
