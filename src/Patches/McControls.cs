using HarmonyLib;
using UnityEngine;

namespace SteveDaysToDie.Patches
{
    /// <summary>
    /// Minecraft's controls (user 2026-11-13):
    ///  - the mouse wheel moves ONE slot per notch, wheel down = next slot, at once (no 7 Days holster wait), wrapping 1..9;
    ///    7 Days' own wheel handling is switched off (PlayerMoveController.SetInventoryIdxFromScroll);
    ///  - Q drops one of the held item, Ctrl+Q the whole stack (setting tecla_tirar);
    ///  - Shift = sneak (crouch), like Minecraft; sprinting is double-tap W (Survival.DoubleTapSprint).
    ///    7 Days' crouch action gets Left Shift added and its run action loses it (setting agachar_shift=no keeps 7 Days').
    /// </summary>
    public static class McControls
    {
        static bool _bound;

        public static void Tick(EntityPlayerLocal p)
        {
            if (p == null || Cursor.lockState != CursorLockMode.Locked) return;
            try
            {
                if (!_bound) Bind(p);
                EdgeStop(p);
                // the wheel
                float w = Input.mouseScrollDelta.y;
                if (w != 0f && !p.AttachedToEntity)
                {
                    int cur = p.inventory.GetFocusedItemIdx();
                    int next = ((cur + (w < 0f ? 1 : -1)) % 9 + 9) % 9;
                    p.inventory.SetFocusedItemIdx(next);
                    p.inventory.SetHoldingItemIdxNoHolsterTime(next);
                }
                // placing a block swings the arm, like Minecraft
                if (Input.GetMouseButtonDown(1) && p.inventory.holdingItem is ItemClassBlock) Ryan.FirstPerson.Swing();
                // right-click with armour in hand: put it on (Minecraft)
                if (Input.GetMouseButtonDown(1) && McInv.McInventory.EquipFromHand(p)) Ryan.FirstPerson.Swing();
                // Q: drop
                if (Input.GetKeyDown(Settings.GetKey("tecla_tirar", KeyCode.Q)))
                {
                    int slot = p.inventory.holdingItemIdx;
                    var st = p.inventory.GetItem(slot);
                    if (st != null && !st.IsEmpty())
                    {
                        bool all = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                        var drop = st.Clone(); drop.count = all ? st.count : 1;
                        var left = st.Clone(); left.count = st.count - drop.count;
                        p.inventory.SetItem(slot, left.count > 0 ? left : ItemStack.Empty.Clone());
                        Vector3 look = p.GetLookVector();
                        GameManager.Instance.ItemDropServer(drop, p.getHeadPosition() + look * 0.3f, Vector3.zero, look * 4f + Vector3.up, p.entityId, 60f, false, -1);
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] controls: " + ex.Message); }
        }

        // Minecraft's sneak keeps you from walking off an edge: while crouching on the ground, a step that would leave
        // nothing under your feet is taken back (the last spot with ground under it, in the scene's coordinates)
        static Vector3 _safe; static bool _hasSafe;
        static readonly RaycastHit[] _hits = new RaycastHit[8];
        static bool GroundUnder(EntityPlayerLocal p, Vector3 scenePos)
        {
            int n = Physics.RaycastNonAlloc(scenePos + Vector3.up * 0.2f, Vector3.down, _hits, 0.9f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) if (!_hits[i].collider.transform.IsChildOf(p.transform)) return true;
            return false;
        }

        static void EdgeStop(EntityPlayerLocal p)
        {
            var c = p.vp_FPController;
            if (c == null) return;
            Vector3 here = p.transform.position;
            if (!p.IsCrouching) { _hasSafe = false; return; }
            // the feet cover about 0.6 x 0.6: ground under any corner keeps you up, like Minecraft's box
            bool supported = false;
            foreach (var o in new[] { Vector3.zero, new Vector3(0.28f, 0, 0.28f), new Vector3(-0.28f, 0, 0.28f), new Vector3(0.28f, 0, -0.28f), new Vector3(-0.28f, 0, -0.28f) })
                if (GroundUnder(p, here + o)) { supported = true; break; }
            if (supported) { _safe = here; _hasSafe = true; return; }
            // just stepped off (still at the edge's height): back onto the edge
            if (_hasSafe && Mathf.Abs(here.y - _safe.y) < 0.4f) c.SetPosition(new Vector3(_safe.x, _safe.y, _safe.z));
            else _hasSafe = false;
        }

        static void Bind(EntityPlayerLocal p)
        {
            _bound = true;
            try
            {
                var ui = LocalPlayerUI.GetUIForPlayer(p);
                var a = ui != null ? ui.playerInput : null;
                if (a == null) return;
                var shift = new InControl.KeyBindingSource(InControl.Key.LeftShift);
                var ctrl = new InControl.KeyBindingSource(InControl.Key.LeftControl);
                var c = new InControl.KeyBindingSource(InControl.Key.C);
                // The keys are set EVERY time (not only when changed): 7 Days saves its key bindings, so the old
                // Minecraft swap (Shift sneak / Ctrl sprint) had stayed saved in the user's game.
                // Setting "shift_correr" (new name: the old "agachar_shift=si" saved in ajustes.cfg must not count):
                //   si (default, user 2026-11-13 "intercambia los botones shift y control"): Shift = run, Ctrl = crouch (hold), C = toggle crouch
                //   no: Minecraft's Shift = sneak, Ctrl = sprint, C = toggle crouch
                bool shiftRuns = Settings.GetBool("shift_correr", true);
                var run = shiftRuns ? shift : ctrl;
                var crouch = shiftRuns ? ctrl : shift;
                a.Run.RemoveBinding(shift); a.Run.RemoveBinding(ctrl);
                a.Crouch.RemoveBinding(shift); a.Crouch.RemoveBinding(ctrl); a.Crouch.RemoveBinding(c);
                a.ToggleCrouch.RemoveBinding(shift); a.ToggleCrouch.RemoveBinding(ctrl); a.ToggleCrouch.RemoveBinding(c);
                a.Run.AddBinding(run);
                a.Crouch.AddBinding(crouch);
                a.ToggleCrouch.AddBinding(c);
                Debug.Log(shiftRuns ? "[SVD] controls: Shift = run, Ctrl = crouch, C = toggle crouch" : "[SVD] controls: Shift = sneak, Ctrl = sprint, C = toggle crouch (Minecraft)");
            }
            catch (System.Exception ex) { Debug.LogWarning("[SVD] shift binding: " + ex.Message); }
        }

        [HarmonyPatch(typeof(PlayerMoveController), "SetInventoryIdxFromScroll")]
        static class NoGameWheel
        {
            static bool Prefix() => false;          // our wheel above does it, Minecraft's way
        }
    }
}
