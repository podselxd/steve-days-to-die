using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteveDaysToDie.Steve
{
    /// <summary>
    /// Shows a player (local or remote) as Steve: hides the 7 Days body meshes and stands six skin
    /// boxes on the player's spot. Steve moves the Minecraft way, not with the 7 Days skeleton:
    /// arms and legs swing alternately with the walking speed, the head stays steady and follows
    /// where the player looks, the body leans when sneaking and the right arm swings on every attack.
    /// In first person the boxes only cast Steve's shadow.
    /// </summary>
    public class SteveModel : MonoBehaviour
    {
        const float SwingPerMetre = 2.67f;      // Minecraft: 0.6662 rad per tick of walk
        const float MaxSwingSpeed = 5f;         // the swing does not get faster than this (m/s)
        const float FullSwingSpeed = 4.3f;      // walking speed at which limbs reach their full swing
        const float AttackSeconds = 0.3f;
        const float SneakLean = 28.6f;          // Minecraft leans the body 0.5 rad

        /// <summary>The local player's Steve (for the automatic test's pictures).</summary>
        public static Transform LocalRoot;
        /// <summary>Test only: pretend to walk at this speed (m/s) without moving; negative = off.</summary>
        public static float TestSpeed = -1f;
        /// <summary>Test only: pretend to sneak.</summary>
        public static bool TestSneak;

        EntityPlayer _player;
        EModelSDCS _model;
        GameObject _rig;
        Transform _root, _upper, _head, _armR, _armL, _legR, _legL;
        readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
        readonly List<SkinnedMeshRenderer> _hidden = new List<SkinnedMeshRenderer>();
        float _phase, _amount, _attackStart = -10f, _px = 0.05625f;

        /// <summary>The local player's Steve (for the front-view mirror).</summary>
        public static SteveModel LocalModel;
        /// <summary>Right arm angles last frame (total, walk swing, punch): read by the test log.</summary>
        public static Vector3 LastArmR;
        public bool ShadowOnly => _shadowOnly;
        Vector3 _lastPos;
        bool _hasPos, _shadowOnly;

        // the item in hand: the game hangs it from the 7 Days right hand; we move it to Steve's fist
        const float HeldTilt = 40f;             // Minecraft holds tools pointing forward and up
        const float HoldArmForward = -18f;      // Minecraft raises the arm a little when it holds something
        Transform _bHand, _bForeArm, _bMiddle, _bThumb;
        Transform _heldItem, _heldParent;
        Vector3 _heldLocalPos;
        Quaternion _heldLocalRot;
        bool _heldMoved, _bonesLogged;
        /// <summary>For the automatic test: distance from the held item to Steve's fist last frame (m), -1 when nothing is placed.</summary>
        public float HeldDistance { get; private set; } = -1f;

        public static bool Enabled => Settings.GetBool("ver_steve", true) && SteveCharacter.SkinTexture != null;

        void Start()
        {
            _player = GetComponent<EntityPlayer>();
            if (_player is EntityPlayerLocal) Combat.AttackCharge.OnSwing += OnSwing;
        }

        void OnSwing() { _attackStart = Time.time; }

        void LateUpdate()
        {
            if (!Enabled || _player == null) return;
            if (_model == null) _model = _player.emodel as EModelSDCS;
            if (_model == null) return;

            var rig = _model.baseRig;
            if (rig == null) return;
            if (rig != _rig || _root == null)
            {
                _rig = rig;
                Build();
            }

            HideNow();

            if (_root == null) return;
            bool show = _player.IsAlive();
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            // first person: keep only Steve's shadow on the ground
            var local = _player as EntityPlayerLocal;
            bool shadowOnly = local != null && local.bFirstPersonView;
            SetShadowOnly(shadowOnly);
            Animate();
        }

        public void SetShadowOnly(bool shadowOnly)
        {
            if (shadowOnly == _shadowOnly) return;
            _shadowOnly = shadowOnly;
            foreach (var mr in _renderers)
                if (mr != null) mr.shadowCastingMode = shadowOnly ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        // The game rebuilds its own meshes now and then (gear, view changes) and they would show for a
        // moment. Hiding them right before every frame is drawn leaves no gap, whatever order the
        // game's own updates ran in.
        void OnEnable() { Application.onBeforeRender += HideNow; SteveCharacter.SkinChanged += OnSkinChanged; }
        void OnDisable() { Application.onBeforeRender -= HideNow; SteveCharacter.SkinChanged -= OnSkinChanged; RestoreHeld(false); }

        // a new skin: throw the boxes away, the next frame builds them again with it
        void OnSkinChanged()
        {
            if (_root != null) { Destroy(_root.gameObject); _root = null; }
        }

        void HideNow()
        {
            if (!Enabled || _model == null) return;
            var rig = _model.baseRig;
            if (rig == null) return;
            SteveParts.BodyMeshes(rig, _hidden);
            for (int i = 0; i < _hidden.Count; i++)
                if (_hidden[i] != null) _hidden[i].forceRenderingOff = true;
            PlaceHeldItem();
        }

        bool HoldingSomething()
        {
            try
            {
                var ic = _player.inventory != null ? _player.inventory.holdingItem : null;
                return ic != null && !ic.Name.StartsWith("meleeHand");
            }
            catch { return false; }
        }

        // Moves the held item from the 7 Days hand to Steve's fist, keeping how the item sits in the hand.
        // The 7 Days hand's own frame is read from its bones (wrist -> middle finger, wrist -> thumb), so
        // any item, whatever its model, keeps its grip: the handle goes through Steve's fist and the tool
        // points forward and up, the way Minecraft holds things.
        void PlaceHeldItem()
        {
            Transform item = null;
            try { item = _player != null && _player.inventory != null ? _player.inventory.GetHoldingItemTransform() : null; }
            catch { }
            if (item != null && (_rig == null || !item.IsChildOf(_rig.transform))) item = null;     // first-person model, or not in the hand yet

            if (item != _heldItem || (item != null && item.parent != _heldParent))
            {
                RestoreHeld(false);
                if (item != null)
                {
                    _heldItem = item;
                    _heldParent = item.parent;
                    _heldLocalPos = item.localPosition;
                    _heldLocalRot = item.localRotation;
                }
            }
            if (_heldItem == null || _heldParent == null) { HeldDistance = -1f; return; }

            var local = _player as EntityPlayerLocal;
            bool active = _root != null && _root.gameObject.activeInHierarchy && _armR != null && _bHand != null
                          && !(local != null && local.bFirstPersonView);
            if (!active) { RestoreHeld(true); HeldDistance = -1f; return; }

            // where the game wants the item this frame (from its own pose, never from our last move)
            Vector3 pi = _heldParent.TransformPoint(_heldLocalPos);
            Quaternion ri = _heldParent.rotation * _heldLocalRot;
            Vector3 ph = _bHand.position;
            Quaternion rh = _bHand.rotation;

            // the 7 Days hand: fingers direction and thumb side
            Vector3 fingers = _bMiddle != null ? _bMiddle.position - ph
                            : (_bForeArm != null ? ph - _bForeArm.position : -_root.up);
            Vector3 thumb = _bThumb != null ? _bThumb.position - ph : _root.forward;
            if (fingers.sqrMagnitude < 1e-8f || thumb.sqrMagnitude < 1e-8f) return;
            Quaternion handNow = Quaternion.LookRotation(fingers.normalized, thumb.normalized);

            // Steve's fist: fingers along the arm, thumb to the front of the arm, tilted up like Minecraft
            Vector3 down = _armR.TransformDirection(Vector3.down);
            Vector3 front = _armR.TransformDirection(Vector3.forward);
            Quaternion fist = Quaternion.AngleAxis(HeldTilt, Vector3.Cross(front, -down).normalized) * Quaternion.LookRotation(down, front);

            Quaternion turn = fist * Quaternion.Inverse(handNow);
            Vector3 wrist = _armR.TransformPoint(new Vector3(0f, -8.5f * _px, 0f));
            _heldItem.SetPositionAndRotation(wrist + turn * (pi - ph), turn * ri);
            _heldMoved = true;

            Vector3 fistCentre = _armR.TransformPoint(new Vector3(0f, -10f * _px, 0f));
            HeldDistance = Vector3.Distance(_heldItem.position, fistCentre);
        }

        // gives the item back its own place in the 7 Days hand (first person, Steve off, item changed)
        void RestoreHeld(bool keepTracking)
        {
            if (_heldItem != null && _heldMoved && _heldItem.parent == _heldParent)
            {
                _heldItem.localPosition = _heldLocalPos;
                _heldItem.localRotation = _heldLocalRot;
            }
            _heldMoved = false;
            if (!keepTracking) { _heldItem = null; _heldParent = null; }
        }

        static Transform Bone(SDCSUtils.TransformCatalog cat, params string[] names)
        {
            if (cat == null) return null;
            foreach (var n in names)
                if (cat.TryGetValue(n, out var t) && t != null) return t;
            return null;
        }

        void Animate()
        {
            // how fast the player is moving over the ground
            Vector3 pos = _player.position;
            float speed = 0f;
            if (_hasPos && Time.deltaTime > 0f)
            {
                Vector3 d = pos - _lastPos; d.y = 0f;
                float m = d.magnitude;
                if (m < 3f) speed = m / Time.deltaTime;
            }
            _lastPos = pos; _hasPos = true;
            if (TestSpeed >= 0f && _player is EntityPlayerLocal) speed = TestSpeed;

            _phase += Mathf.Min(speed, MaxSwingSpeed) * Time.deltaTime * SwingPerMetre;
            _amount = Mathf.Lerp(_amount, Mathf.Clamp01(speed / FullSwingSpeed), Mathf.Clamp01(Time.deltaTime * 10f));

            // Minecraft's sneak: the body tips forward from the neck, so the hips go back over the legs,
            // which step back 4 px; the whole upper body drops 3.2 px and the head one more
            bool sneak = _player.IsCrouching || (TestSneak && _player is EntityPlayerLocal);
            _upper.localPosition = new Vector3(0f, (24f - (sneak ? 3.2f : 0f)) * _px, 0f);
            _upper.localRotation = Quaternion.Euler(sneak ? SneakLean : 0f, 0f, 0f);
            _head.localPosition = new Vector3(0f, sneak ? -1f * _px : 0f, 0f);
            float legBack = sneak ? -4f * _px : 0f;
            float legY = (12f - (sneak ? 0.2f : 0f)) * _px;
            _legR.localPosition = new Vector3(+2f * _px, legY, legBack);
            _legL.localPosition = new Vector3(-2f * _px, legY, legBack);

            // Minecraft's walk: right arm with left leg, left arm with right leg
            float swing = Mathf.Cos(_phase) * _amount * Mathf.Rad2Deg;
            _legR.localRotation = Quaternion.Euler(swing * 1.4f, 0f, 0f);
            _legL.localRotation = Quaternion.Euler(-swing * 1.4f, 0f, 0f);

            // arms also sway a little while standing, as in Minecraft
            float age = Time.time * 20f;
            float swayZ = (Mathf.Cos(age * 0.09f) * 0.05f + 0.05f) * Mathf.Rad2Deg;
            float swayX = Mathf.Sin(age * 0.067f) * 0.05f * Mathf.Rad2Deg;
            float attack = 0f;
            float t = (Time.time - _attackStart) / AttackSeconds;
            if (t >= 0f && t < 1f) attack = Mathf.Sin(t * Mathf.PI) * 80f;      // punch: right arm forward
            // holding something: Minecraft raises the arm a little and halves its walking swing
            bool holding = HoldingSomething();
            float armSwing = holding ? swing * 0.5f : swing;
            float armX = -armSwing - attack + swayX + (holding ? HoldArmForward : 0f);
            _armR.localRotation = Quaternion.Euler(armX, 0f, swayZ);
            LastArmR = new Vector3(armX, swing, attack);     // x total, walk swing, punch (for the test log)
            _armL.localRotation = Quaternion.Euler(swing - swayX, 0f, -swayZ);

            // head: steady, turned to where the player looks
            Vector3 look = _root.InverseTransformDirection(_player.GetLookVector());
            float pitch = Mathf.Atan2(look.y, Mathf.Sqrt(look.x * look.x + look.z * look.z)) * Mathf.Rad2Deg;
            float yaw = Mathf.Clamp(Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, -75f, 75f);
            _head.localRotation = Quaternion.Inverse(_upper.localRotation) * Quaternion.Euler(-Mathf.Clamp(pitch, -80f, 80f), yaw, 0f);
        }

        void Build()
        {
            if (_root != null) Destroy(_root.gameObject);
            _renderers.Clear();
            try
            {
                // one skin pixel, from the player's real height: the 7 Days neck sits at about 86% of
                // the body's height, and Steve is 32 pixels tall
                float px = 0.05625f;
                var cat = _model.boneCatalog;
                if (cat != null && cat.TryGetValue("Neck", out var neck) && neck != null)
                {
                    float h = (neck.position.y - _rig.transform.position.y) / 0.86f / 32f;
                    if (h > 0.045f && h < 0.065f) px = h;
                }
                _px = px;
                int layer = _rig.layer;

                var rootGo = new GameObject("SVD_Steve") { layer = layer };
                _root = rootGo.transform;
                _root.SetParent(_rig.transform, false);
                var upperGo = new GameObject("SVD_upper") { layer = layer };
                _upper = upperGo.transform;
                _upper.SetParent(_root, false);
                _upper.localPosition = new Vector3(0f, 24f * px, 0f);          // neck: the body leans from here, as in Minecraft

                _legR = Add("legR", SteveParts.RightLeg(px), SteveParts.RightLegLayer(px), _root, new Vector3(+2f, 12f, 0f) * px, layer);
                _legL = Add("legL", SteveParts.LeftLeg(px), SteveParts.LeftLegLayer(px), _root, new Vector3(-2f, 12f, 0f) * px, layer);
                Add("body", SteveParts.Body(px), SteveParts.BodyLayer(px), _upper, Vector3.zero, layer);
                _armR = Add("armR", SteveParts.RightArm(px), SteveParts.RightArmLayer(px), _upper, new Vector3(+6f, -1f, 0f) * px, layer);
                _armL = Add("armL", SteveParts.LeftArm(px), SteveParts.LeftArmLayer(px), _upper, new Vector3(-6f, -1f, 0f) * px, layer);
                _head = Add("head", SteveParts.Head(px), SteveParts.HeadLayer(px), _upper, Vector3.zero, layer);
                _shadowOnly = false;

                // bones of the 7 Days right hand: their frame tells how the item sits in the hand
                _bHand = Bone(cat, "RightHand");
                _bForeArm = Bone(cat, "RightForeArm", "RightLowerArm");
                _bMiddle = Bone(cat, "RightHandMiddle1", "RightHandMiddle", "RightHandIndex1");
                _bThumb = Bone(cat, "RightHandThumb1", "RightHandThumb");
                if (!_bonesLogged)
                {
                    _bonesLogged = true;
                    Debug.Log($"[SVD] Hand bones: hand {(_bHand != null ? _bHand.name : "MISSING")}, forearm {(_bForeArm != null ? _bForeArm.name : "missing")}, " +
                              $"fingers {(_bMiddle != null ? _bMiddle.name : "missing")}, thumb {(_bThumb != null ? _bThumb.name : "missing")}.");
                }

                bool isLocal = _player is EntityPlayerLocal;
                if (isLocal) { LocalRoot = _root; LocalModel = this; }
                Debug.Log($"[SVD] Steve built for {(isLocal ? "local player" : "player " + _player.entityId)} (pixel = {px:0.0000} m, height {px * 32f:0.00} m, skin {(SteveParts.Legacy ? "64x32" : "64x64")}).");
                MeltyTest.Pass("steve-model");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SVD] Steve build failed: " + ex);
                MeltyTest.Fail("steve-model", ex.Message);
            }
        }

        Transform Add(string name, Mesh mesh, Mesh outerLayer, Transform parent, Vector3 pivot, int layer)
        {
            var go = SteveParts.Create(name, mesh, layer);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pivot;
            _renderers.Add(go.GetComponent<MeshRenderer>());
            if (outerLayer != null)
            {
                // the skin's second layer moves with its part
                var ol = SteveParts.Create(name + "Layer", outerLayer, layer);
                ol.transform.SetParent(go.transform, false);
                _renderers.Add(ol.GetComponent<MeshRenderer>());
            }
            return go.transform;
        }

        void OnDestroy()
        {
            if (_player is EntityPlayerLocal) Combat.AttackCharge.OnSwing -= OnSwing;
            if (_root != null) Destroy(_root.gameObject);
        }
    }
}
