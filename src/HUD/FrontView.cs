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

        void Update()
        {
            if (_player == null) return;
            if (Cursor.lockState == CursorLockMode.Locked && Input.GetKeyDown(Settings.GetKey("tecla_vista_frontal", KeyCode.Insert)))
                Toggle(!_on);
        }

        void LateUpdate()
        {
            if (!_on || _player == null || !_player.IsAlive()) return;
            if (++_frame % EveryNthFrame != 0 && _rt != null) return;
            RenderOnce();
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
