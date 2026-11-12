using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SteveDaysToDie.Steve
{
    /// <summary>
    /// First person: hides the 7 Days arms (what they hold stays visible) and shows Steve's right arm
    /// when nothing is held. Every attack swings that right arm, whichever hand the game's own
    /// animation uses.
    /// </summary>
    public class SteveArm : MonoBehaviour
    {
        // Position and size approved by the creator (2026-11-11): do not move or shrink.
        static readonly Vector3 RestPos = new Vector3(0.40f, -0.50f, 0.34f);
        static readonly Vector3 RestDir = new Vector3(-0.26f, 0.44f, 0.62f);
        static readonly Vector3 SwingPos = new Vector3(-0.16f, -0.06f, 0.14f);
        static readonly Vector3 SwingDir = new Vector3(-0.30f, -0.55f, 0.10f);
        const float SwingSeconds = 0.25f;
        const float Pixel = 0.042f;

        static SteveArm _instance;

        EntityPlayerLocal _player;
        EModelSDCS _model;
        GameObject _arm;
        GameObject _rigFP;
        readonly System.Collections.Generic.List<SkinnedMeshRenderer> _hidden = new System.Collections.Generic.List<SkinnedMeshRenderer>();
        float _swingStart = -10f;

        void Start()
        {
            _instance = this;
            _player = GetComponent<EntityPlayerLocal>();
            Combat.AttackCharge.OnSwing += OnSwing;
        }

        void OnDestroy()
        {
            Combat.AttackCharge.OnSwing -= OnSwing;
            if (_arm != null) Destroy(_arm);
            if (_instance == this) _instance = null;
        }

        void OnSwing() { _swingStart = Time.time; }

        // Hide the game's own first-person arms right before every frame is drawn (see SteveModel.HideNow).
        void OnEnable() { Application.onBeforeRender += HideNow; SteveCharacter.SkinChanged += OnSkinChanged; }
        void OnDisable() { Application.onBeforeRender -= HideNow; SteveCharacter.SkinChanged -= OnSkinChanged; }

        void OnSkinChanged()
        {
            if (_arm != null) { Destroy(_arm); _arm = null; }
            _rigFP = null;
        }

        void HideNow()
        {
            if (!SteveModel.Enabled || _model == null) return;
            var rig = _model.baseRigFP;
            if (rig == null) return;
            SteveParts.BodyMeshes(rig, _hidden);
            for (int i = 0; i < _hidden.Count; i++)
                if (_hidden[i] != null) _hidden[i].forceRenderingOff = true;
        }

        public static bool BareHanded(EntityPlayerLocal p)
        {
            var ic = p.inventory?.holdingItem;
            return ic == null || ic.Name.StartsWith("meleeHand");
        }

        /// <summary>Test only: what is drawn in first person right now (to check that held items show).</summary>
        public static string Describe()
        {
            var me = _instance;
            if (me == null || me._player == null || me._player.cameraTransform == null) return "no first-person rig";
            var sb = new StringBuilder();
            foreach (var r in me._player.cameraTransform.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                sb.Append(r is SkinnedMeshRenderer ? "S:" : "M:").Append(r.name);
                if (r.forceRenderingOff) sb.Append("(hidden)");
                sb.Append(' ');
            }
            return sb.ToString();
        }

        void LateUpdate()
        {
            if (!SteveModel.Enabled || _player == null) return;
            if (_model == null) _model = _player.emodel as EModelSDCS;
            if (_model == null) return;
            var rig = _model.baseRigFP;
            if (rig == null) return;

            try
            {
                if (rig != _rigFP || _arm == null)
                {
                    _rigFP = rig;
                    if (_arm != null) Destroy(_arm);
                    int layer = LayerMask.NameToLayer("HoldingItem");
                    _arm = SteveParts.Create("armFP", SteveParts.RightArm(Pixel), layer < 0 ? rig.layer : layer);
                    var sleeve = SteveParts.RightArmLayer(Pixel);         // the skin's second layer (sleeve)
                    if (sleeve != null)
                    {
                        var sl = SteveParts.Create("armFPLayer", sleeve, _arm.layer);
                        sl.transform.SetParent(_arm.transform, false);
                    }
                    foreach (var mr in _arm.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        mr.shadowCastingMode = ShadowCastingMode.Off;      // a floating arm must not drop a shadow
                        mr.receiveShadows = false;
                    }
                    // Hang it on the camera that actually draws held items, so it is always in view.
                    Camera cam = null;
                    int bit = 1 << _arm.layer;
                    foreach (var c in _player.cameraTransform.GetComponentsInChildren<Camera>(true))
                        if ((c.cullingMask & bit) != 0 && (cam == null || c != _player.playerCamera)) cam = c;
                    if (cam == null) foreach (var c in Camera.allCameras) if ((c.cullingMask & bit) != 0) cam = c;
                    _arm.transform.SetParent(cam != null ? cam.transform : _player.cameraTransform, false);
                    Debug.Log($"[SVD] Steve first-person arm ready (camera: {(cam != null ? cam.name + " fov " + cam.fieldOfView.ToString("0") : "none")}).");
                }

                HideNow();

                bool show = _player.bFirstPersonView && _player.IsAlive() && BareHanded(_player);
                if (_arm.activeSelf != show) _arm.SetActive(show);
                if (!show) return;

                float t = Mathf.Clamp01((Time.time - _swingStart) / SwingSeconds);
                float s = t >= 1f ? 0f : Mathf.Sin(t * Mathf.PI);
                Vector3 dir = (RestDir + SwingDir * s).normalized;
                _arm.transform.localPosition = RestPos + SwingPos * s;
                _arm.transform.localRotation = Quaternion.FromToRotation(Vector3.down, dir) * Quaternion.Euler(0f, -25f, 0f);
                MeltyTest.Pass("steve-arm");
            }
            catch (Exception ex)
            {
                Debug.LogError("[SVD] Steve arm: " + ex);
                MeltyTest.Fail("steve-arm", ex.Message);
                enabled = false;
            }
        }
    }
}
