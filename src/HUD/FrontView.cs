using UnityEngine;
using SteveDaysToDie.Steve;

namespace SteveDaysToDie.HUD
{
    /// <summary>
    /// A mirror: a small live picture of Steve seen from the front, in a corner of the screen, so you
    /// can see your own character (the game's camera always turns the body away from itself).
    /// Press the key in the settings file (Insert by default) to show or hide it. It redraws about
    /// 20 times a second at a small size, so it costs little.
    /// </summary>
    public class FrontView : MonoBehaviour
    {
        const int Width = 360, Height = 480, EveryNthFrame = 3;

        public static FrontView Instance { get; private set; }
        /// <summary>For the automatic test: how many pictures the mirror has drawn.</summary>
        public static int RenderedFrames { get; private set; }

        EntityPlayerLocal _player;
        RenderTexture _rt;
        Camera _cam;
        GameObject _camGo;
        bool _on;
        int _frame;

        public bool On => _on;

        void Start()
        {
            Instance = this;
            _player = GetComponent<EntityPlayerLocal>();
        }

        public void Toggle(bool on)
        {
            _on = on;
            if (!on) Release();
        }

        // Minecraft's F5, with our own camera (never 7 Days' third person):
        // 0 = first person, 1 = third person from behind, 2 = from the front. The game's own camera key does the same.
        public static int Mode { get; private set; }
        public bool FullFront => Mode == 2;

        void Update()
        {
            if (_player == null) return;
            if (Mode != 0 && !_player.IsAlive()) SetMode(0);
            if (Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(Settings.GetKey("tecla_vista_frontal", KeyCode.F5)))
                CycleCamera();
        }

        public void CycleCamera() => SetMode((Mode + 1) % 3);

        public void SetMode(int mode)
        {
            try
            {
                int prev = Mode;
                Mode = mode;
                if (mode != 0) Toggle(false);
                if (_cam != null) { _cam.enabled = false; Release(); }
                ApplyCameraDistance(mode);
                // the game's own third person underneath (the held item goes to Steve's hand), our camera spot on top
                if (_player != null && _player.bFirstPersonView != (mode == 0)) _player.SetFirstPersonView(mode == 0, false);
                if (prev == 0 && mode != 0) SetHandCameras(false);       // only on the way into / out of F5
                else if (prev != 0 && mode == 0) SetHandCameras(true);
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] Camera F5: " + ex.Message); Mode = 0; Release(); }
        }

        void LateUpdate()
        {
            // Minecraft's first-person hand and the look of mining (from Ryan Ussery's mod, MIT)
            try { Ryan.FirstPerson.LateUpdate(_player); } catch (System.Exception ex) { Debug.LogWarning("[SVD] Hand: " + ex.Message); }
            Ryan.MiningLook.LateUpdate(_player);
            Crafting.McBlockLook.Tick();
            Crafting.McFurnaces.Tick();
            if (_player == null || !_player.IsAlive()) return;
            if (Mode != 0) return;      // the third person is the game's own camera (ApplyCameraDistance / FrontCamera)
            if (!_on) return;
            if (++_frame % EveryNthFrame != 0 && _rt != null) return;
            RenderOnce();
        }

        // Third person = the game's OWN third-person camera (vp_FPCamera), told Minecraft's distance: 4 blocks,
        // centred (7 Days puts it over the shoulder). The game places it, avoids walls with its own collision and
        // decides what of the world to draw from where it really is. (Moving the camera only while drawing broke
        // the map while walking: the game culls from the camera's position during its own update.)
        static Vector3? _gameOffset;
        readonly System.Collections.Generic.List<Camera> _handCams = new System.Collections.Generic.List<Camera>();

        void ApplyCameraDistance(int mode)
        {
            try
            {
                var cam = _player != null ? _player.vp_FPCamera : null;
                if (cam == null) return;
                if (_gameOffset == null) _gameOffset = cam.Position3rdPersonOffset;
                cam.Position3rdPersonOffset = mode == 0 ? _gameOffset.Value : new Vector3(0f, 0.1f, 4f);
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] F5 distance: " + ex.Message); }
        }

        /// <summary>
        /// F5 (both views): runs AFTER the game has placed its third-person camera (execution order) and sets it at
        /// Minecraft's spot, 4 blocks behind or in front looking back (7 Days keeps resetting its own distance). It stays there for the whole frame (no putting back),
        /// so the game culls and draws from the right place.
        /// </summary>
        [DefaultExecutionOrder(32000)]
        public class FrontCamera : MonoBehaviour
        {
            EntityPlayerLocal _p;
            void Start() { _p = GetComponent<EntityPlayerLocal>(); }

            void LateUpdate()
            {
                if (Mode == 0 || _p == null || _p.playerCamera == null || _p.bFirstPersonView) return;
                var t = _p.playerCamera.transform;
                Vector3 eye = _p.transform.position + Vector3.up * 1.62f;
                Vector3 look = t.forward;                              // the game's camera follows the mouse: where Steve looks
                // Minecraft: 4 blocks behind (F5 1) or in front looking back (F5 2); closer when a wall is in the way
                Vector3 dir = Mode == 1 ? -look : look;
                float dist = 4f;
                foreach (var hit in Physics.RaycastAll(eye, dir, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.isTrigger && !hit.collider.transform.IsChildOf(_p.transform) && hit.distance - 0.3f < dist)
                        dist = Mathf.Max(0.5f, hit.distance - 0.3f);
                t.position = eye + dir * dist;
                t.rotation = Mode == 1 ? Quaternion.LookRotation(look, Vector3.up) : Quaternion.LookRotation(-look, Vector3.up);
            }
        }

        // 7 Days draws the first-person arm and weapon with cameras hanging from the player's camera: off in F5
        void SetHandCameras(bool on)
        {
            try
            {
                var main = _player != null ? _player.playerCamera : null;
                if (main == null) return;
                if (!on)
                {
                    _handCams.Clear();
                    foreach (var c in main.GetComponentsInChildren<Camera>(true))
                        if (c != main && c.enabled) { c.enabled = false; _handCams.Add(c); }
                }
                else
                {
                    foreach (var c in _handCams) if (c != null) c.enabled = true;
                    _handCams.Clear();
                }
            }
            catch { }
        }

        /// <summary>The game's own camera key (7 Days' third person) runs our F5 cycle instead.</summary>
        [HarmonyLib.HarmonyPatch(typeof(EntityPlayerLocal), "SwitchFirstPersonViewFromInput")]
        static class GameCameraKeyPatch
        {
            static bool Prefix(EntityPlayerLocal __instance)
            {
                // F5 is the camera; the game's own camera key (the Ñ) now switches items between the games
                return !SteveModel.Enabled;
            }
        }

        void RenderOnce()
        {
            var model = SteveModel.LocalModel;
            var root = SteveModel.LocalRoot;
            if (model == null || root == null || _player.playerCamera == null) return;
            try
            {
                if (_rt == null) _rt = new RenderTexture(Width, Height, 24) { name = "SVD_FrontView" };
                if (_cam == null)
                {
                    _camGo = new GameObject("SVD_FrontView");
                    _cam = _camGo.AddComponent<Camera>();
                    _cam.enabled = false;
                }
                _cam.CopyFrom(_player.playerCamera);        // same layers, sky and fog as the player's view
                _cam.enabled = false;
                _cam.allowHDR = false;
                _cam.allowMSAA = false;
                _cam.rect = new Rect(0f, 0f, 1f, 1f);
                _cam.fieldOfView = 38f;
                _cam.nearClipPlane = 0.05f;
                _cam.targetTexture = _rt;
                // the first-person arm and the item in hand live on their own layer, glued to the player's
                // camera: seen from the front they would float by Steve's face, so the mirror leaves them out
                int heldLayer = LayerMask.NameToLayer("HoldingItem");
                if (heldLayer >= 0) _cam.cullingMask &= ~(1 << heldLayer);

                // in front of Steve; closer when something is in the way
                Vector3 target = root.position + Vector3.up * 0.9f;
                Vector3 pos = target + root.forward * 1.2f;
                foreach (float dist in new[] { 3.0f, 2.4f, 1.8f })
                {
                    Vector3 p = target + root.forward * dist + Vector3.up * 0.15f;
                    Vector3 d = p - target;
                    if (!Physics.Raycast(target, d.normalized, d.magnitude + 0.2f)) { pos = p; break; }
                }
                _camGo.transform.position = pos;
                _camGo.transform.LookAt(target);

                // in first person Steve only casts his shadow: show him whole for this one picture
                bool was = model.ShadowOnly;
                model.SetShadowOnly(false);
                _cam.Render();
                model.SetShadowOnly(was);
                RenderedFrames++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SVD] FrontView: " + ex.Message);
                _on = false;
                Release();
            }
        }

        void OnGUI()
        {
            if (!_on || _rt == null || !MinecraftHUD.Visible(_player)) return;
            if (Event.current.type != EventType.Repaint) return;
            int h = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.38f), 220, 480);
            int w = h * 3 / 4;
            int x = 12, y = Mathf.RoundToInt(Screen.height * 0.18f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(x - 3, y - 3, w + 6, h + 6), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, w, h), _rt, ScaleMode.StretchToFill, false);
            GUI.color = old;
        }

        void Release()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_camGo != null) { Destroy(_camGo); _camGo = null; _cam = null; }
        }

        void OnDestroy()
        {
            Release();
            if (Instance == this) Instance = null;
        }
    }
}
