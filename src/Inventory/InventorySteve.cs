using UnityEngine;

namespace SteveDaysToDie.McInv
{
    /// <summary>
    /// Steve in Minecraft's inventory, in 3D and following the mouse like Minecraft's (InventoryScreen.renderEntityInInventory):
    /// body yaw = atan(dx / 40) x 20 deg, head yaw x 40 deg, head pitch = atan(dy / 40) x 20 deg, dx/dy = mouse offset from
    /// the player's eyes in GUI pixels. A small Steve made of Steve's own parts (current skin) sits far below the world on
    /// its own layer and a camera of its own draws it into a picture the inventory shows.
    /// </summary>
    public static class InventorySteve
    {
        const int Layer = 26;
        static RenderTexture _rt;
        static Camera _cam;
        static Transform _root, _body, _head;
        static Texture2D _skin;
        static Transform _upper, _armR, _armL, _legR, _legL;
        static string _armorKey;
        static readonly System.Collections.Generic.List<GameObject> _armor = new System.Collections.Generic.List<GameObject>();

        // the armour you wear, on the inventory Steve (rebuilt when the 4 armour slots change)
        static void Armor()
        {
            var p = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (p == null || _head == null) return;
            string Slot(int i)
            {
                try { var v = p.equipment.GetSlotItem(i); string n = v?.ItemClass?.Name; return n != null && n.StartsWith("mc_") ? n.Substring(3) : null; }
                catch { return null; }
            }
            string h = Slot(0), c = Slot(1), l = Slot(2), f = Slot(3);
            string key = h + "|" + c + "|" + l + "|" + f + "|" + _root.GetInstanceID();
            if (key == _armorKey) return;
            _armorKey = key;
            foreach (var g in _armor) if (g != null) Object.Destroy(g);
            _armor.Clear();
            try { _armor.AddRange(Steve.SteveArmor.Build(h, c, l != null && l.EndsWith("_leggings") ? l : null, f, 1f / 16f, Layer, _head, _upper, _armR, _armL, _legR, _legL)); }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] inventory armour: " + ex.Message); }
        }

        static Transform Part(string name, Mesh m, Mesh layer, Transform parent, Vector3 pos, Material mat)
        {
            var go = new GameObject("SVD_inv_" + name) { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (layer != null)
            {
                var l = new GameObject("layer") { layer = Layer };
                l.transform.SetParent(go.transform, false);
                l.AddComponent<MeshFilter>().sharedMesh = layer;
                l.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go.transform;
        }

        static void Build()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _skin = SteveCharacter.SkinTexture;
            if (_skin == null) return;
            float px = 1f / 16f;
            Shader sh = Shader.Find("Unlit/Transparent Cutout") ?? Shader.Find("Unlit/Texture");
            var mat = sh != null ? new Material(sh) { mainTexture = _skin } : Steve.SteveParts.Material;
            _root = new GameObject("SVD_InventorySteve") { layer = Layer }.transform;
            Object.DontDestroyOnLoad(_root.gameObject);
            _root.position = new Vector3(0f, -2000f, 0f);
            _body = new GameObject("body") { layer = Layer }.transform;
            _body.SetParent(_root, false);
            var upper = new GameObject("upper") { layer = Layer }.transform;
            _upper = upper; _armorKey = null; _armor.Clear();
            upper.SetParent(_body, false);
            upper.localPosition = new Vector3(0f, 24f * px, 0f);
            _legR = Part("legR", Steve.SteveParts.RightLeg(px), Steve.SteveParts.RightLegLayer(px), _body, new Vector3(2f, 12f, 0f) * px, mat);
            _legL = Part("legL", Steve.SteveParts.LeftLeg(px), Steve.SteveParts.LeftLegLayer(px), _body, new Vector3(-2f, 12f, 0f) * px, mat);
            Part("body", Steve.SteveParts.Body(px), Steve.SteveParts.BodyLayer(px), upper, Vector3.zero, mat);
            _armR = Part("armR", Steve.SteveParts.RightArm(px), Steve.SteveParts.RightArmLayer(px), upper, new Vector3(6f, -1f, 0f) * px, mat);
            _armL = Part("armL", Steve.SteveParts.LeftArm(px), Steve.SteveParts.LeftArmLayer(px), upper, new Vector3(-6f, -1f, 0f) * px, mat);
            _head = Part("head", Steve.SteveParts.Head(px), Steve.SteveParts.HeadLayer(px), upper, Vector3.zero, mat);
            if (_cam == null)
            {
                _cam = new GameObject("SVD_InventorySteveCamera").AddComponent<Camera>();
                Object.DontDestroyOnLoad(_cam.gameObject);
                _cam.enabled = false;
                _cam.orthographic = true;
                _cam.cullingMask = 1 << Layer;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                _cam.nearClipPlane = 0.1f;
                _cam.farClipPlane = 10f;
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
            }
        }

        /// <summary>Draws Steve in rect r (GUI), looking at the mouse. eyes = where his eyes are on screen (GUI).</summary>
        public static void Draw(Rect r, Vector2 mouse, int scale)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (_root == null || _skin != SteveCharacter.SkinTexture) Build();
            if (_root == null) return;
            Armor();
            int w = Mathf.Max(16, Mathf.RoundToInt(r.width)), h = Mathf.Max(16, Mathf.RoundToInt(r.height));
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
                _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            }
            // Minecraft's numbers, with its GUI pixels (our scale)
            Vector2 eyes = new Vector2(r.center.x, r.y + r.height * 0.27f);
            float dx = (mouse.x - eyes.x) / scale, dy = (mouse.y - eyes.y) / scale;
            float yaw = Mathf.Atan(dx / 40f), pitch = Mathf.Atan(dy / 40f);
            _body.localRotation = Quaternion.Euler(0f, 180f - yaw * 20f, 0f);
            _head.localRotation = Quaternion.Euler(pitch * 20f, -yaw * 20f, 0f);
            // camera in front of him, framing his 32 px height
            _cam.orthographicSize = 1.15f;
            _cam.aspect = (float)w / h;
            _cam.transform.position = _root.position + new Vector3(0f, 1.0f, -5f);
            _cam.transform.rotation = Quaternion.identity;
            _cam.targetTexture = _rt;
            _cam.Render();
            _cam.targetTexture = null;
            GUI.DrawTexture(r, _rt, ScaleMode.StretchToFill, true);
        }
    }
}
