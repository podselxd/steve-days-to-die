using UnityEngine;

namespace SteveDaysToDie.HUD
{
    /// <summary>
    /// The 7 Days toolbelt keeps working (it handles the slot keys and the scroll wheel) but sits out
    /// of sight while the Minecraft bar is on screen. It comes back whenever a menu with a cursor is
    /// open, so items can still be dragged onto it from the game's own inventory.
    /// </summary>
    public class NativeToolbelt : MonoBehaviour
    {
        const string WindowName = "windowToolbelt";
        const int Away = 4000;

        EntityPlayerLocal _player;
        XUiV_Window _win;
        Vector2i _home;
        bool _shown = true;
        float _nextLook;

        /// <summary>For the automatic test: is the game's own toolbelt on screen right now?</summary>
        public static bool NativeShown { get; private set; } = true;

        void Start()
        {
            _player = GetComponent<EntityPlayerLocal>();
        }

        void Update()
        {
            if (_player == null) return;
            if (_win == null)
            {
                if (Time.time < _nextLook) return;
                _nextLook = Time.time + 0.5f;
                var xui = LocalPlayerUI.GetUIForPlayer(_player)?.xui;
                _win = xui != null ? xui.GetWindow(WindowName) : null;
                if (_win == null) return;
                _home = _win.Position;
                _shown = true;
                Debug.Log($"[SVD] Native toolbelt found at {_home.x},{_home.y}.");
            }

            // the game's own toolbelt is on screen exactly when the Minecraft bar is not (a menu, dead, HUD off)
            bool show = !HUDTextures.Loaded || !MinecraftHUD.Visible(_player);
            if (show == _shown) return;
            _shown = show;
            NativeShown = show;
            _win.Position = show ? _home : new Vector2i(_home.x, _home.y - Away);
        }

        void OnDestroy()
        {
            if (_win != null && !_shown) _win.Position = _home;
        }
    }
}
