using System;
using System.Collections;
using System.IO;
using SteveDaysToDie.Combat;
using SteveDaysToDie.HUD;
using SteveDaysToDie.Steve;
using SteveDaysToDie.Survival;
using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>
    /// Melty test runs only (never in normal play): with nobody playing, shows each feature to the
    /// recording, saves its own pictures to %LOCALAPPDATA%\SteveDaysToDie\test and runs the checks
    /// that need the real game (a critical hit on an animal, hunger, the menu).
    /// </summary>
    static class MeltyDemo
    {
        static string _dir;
        static bool _started;

        public static void Start(EntityPlayerLocal player)
        {
            if (_started) return;
            _started = true;
            try
            {
                _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteveDaysToDie", "test");
                Directory.CreateDirectory(_dir);
                foreach (var f in Directory.GetFiles(_dir, "*.png")) File.Delete(f);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo folder: " + ex.Message); }
            GameManager.Instance.StartCoroutine(Run(player));
        }

        static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

        static IEnumerator Run(EntityPlayerLocal p)
        {
            RuleChecks();
            // the game keeps its loading screen up for a few seconds after we are in the world
            for (int i = 0; i < 80 && p != null; i++)
            {
                bool loading = false;
                try { loading = p.PlayerUI.windowManager.IsWindowOpen(XUiC_LoadingScreen.ID); } catch { }
                if (!loading) break;
                yield return Wait(0.5f);
            }
            yield return Wait(3f);
            if (p == null) yield break;

            // ---- first person ----
            MeltyTest.SetPhase("mano");
            Hold(p, 5);                                    // empty slot: Steve's arm
            yield return Wait(1.5f);
            yield return Shot("fp-mano-reposo");
            for (int i = 0; i < 6; i++)
            {
                AttackCharge.Swing();
                yield return Wait(0.11f);
                if (i == 2) yield return Shot("fp-mano-golpe");
                yield return Wait(0.3f);
            }

            MeltyTest.SetPhase("hacha");
            Hold(p, 0);                                    // stone axe: bar refills in 1.25 s
            yield return Wait(2f);
            AttackCharge.Swing();
            yield return Wait(0.55f);
            yield return Shot("fp-hacha");
            Debug.Log("[SVD] first person shows: " + SteveArm.Describe());

            MeltyTest.SetPhase("comida");
            Hold(p, 9);
            yield return Wait(2.2f);
            yield return Shot("fp-comida");
            Debug.Log("[SVD] first person shows: " + SteveArm.Describe());

            MeltyTest.SetPhase("agua");
            Hold(p, 3);
            yield return Wait(2.2f);
            yield return Shot("fp-agua");

            MeltyTest.SetPhase("madera");
            Hold(p, 1);
            yield return Wait(2.2f);
            yield return Shot("fp-madera");

            // ---- a game menu: our bar must leave and the game's own toolbelt come back ----
            MeltyTest.SetPhase("menu");
            Hold(p, 5);
            bool opened = Menu(p, true);
            yield return Wait(1.5f);
            bool hidOurs = !MinecraftHUD.Visible(p);
            bool inMenu = NativeToolbelt.NativeShown && hidOurs;
            yield return Shot("menu");
            Menu(p, false);
            yield return Wait(1.2f);
            bool afterMenu = !NativeToolbelt.NativeShown && MinecraftHUD.Visible(p);
            if (!opened || !hidOurs) MeltyTest.Fail("menu-hud", $"the test could not open a menu (opened={opened}, our bar hidden={hidOurs})");
            else if (inMenu && afterMenu) MeltyTest.Pass("menu-hud");
            else MeltyTest.Fail("menu-hud", $"in menu ok={inMenu}, after menu ok={afterMenu}");

            // ---- the front-view mirror, in first and in third person ----
            MeltyTest.SetPhase("espejo");
            var fv = FrontView.Instance;
            if (fv != null)
            {
                fv.Toggle(true);
                yield return Wait(2.5f);                    // nothing moving: both arms must hang straight
                yield return Shot("espejo-reposo");
                AttackCharge.Swing();
                yield return Wait(0.08f);
                yield return Shot("espejo-golpe-1");
                yield return Wait(0.1f);
                yield return Shot("espejo-golpe-2");
                yield return Wait(1.2f);
                yield return Shot("espejo-despues");        // and straight again after the punch
            }

            // ---- third person: Steve ----
            MeltyTest.SetPhase("steve");
            ThirdPerson(p, true);
            yield return Wait(2.5f);
            yield return Shot("tp-reposo");
            yield return Selfie(p, "selfie-frente", new Vector3(0f, 1.0f, 3.0f));
            SteveModel.TestSpeed = 4.3f;                   // walk on the spot
            yield return Wait(0.45f);
            yield return Selfie(p, "selfie-lado-camina1", new Vector3(3.0f, 1.0f, 0.2f));
            yield return Wait(0.3f);
            yield return Selfie(p, "selfie-lado-camina2", new Vector3(3.0f, 1.0f, 0.2f));
            yield return Selfie(p, "selfie-frente-camina", new Vector3(1.6f, 1.1f, 2.6f));
            SteveModel.TestSpeed = -1f;
            yield return Wait(0.6f);
            AttackCharge.Swing();
            yield return Wait(0.13f);
            yield return Selfie(p, "selfie-golpe", new Vector3(2.2f, 1.1f, 2.0f));
            SteveModel.TestSneak = true;
            yield return Wait(0.3f);
            yield return Selfie(p, "selfie-agachado", new Vector3(3.0f, 1.0f, 0.2f));
            SteveModel.TestSneak = false;

            // the axe must sit in Steve's fist in third person (not where the 7 Days hand was)
            Hold(p, 0);
            yield return Wait(2f);
            var sm = SteveModel.LocalModel;
            float held = sm != null ? sm.HeldDistance : -1f;
            yield return Selfie(p, "selfie-hacha", new Vector3(2.2f, 1.1f, 2.0f));
            yield return Selfie(p, "selfie-hacha-lado", new Vector3(3.0f, 1.0f, 0.2f));
            if (held >= 0f && held < 0.6f) MeltyTest.Pass("held-item");
            else MeltyTest.Fail("held-item", held < 0f ? "no item was placed in the hand" : $"the item is {held:0.00} m from the fist");
            Hold(p, 5);
            yield return Wait(0.5f);

            yield return Shot("espejo-tercera-persona");
            if (fv != null) fv.Toggle(false);
            if (FrontView.RenderedFrames >= 5) MeltyTest.Pass("front-view");
            else MeltyTest.Fail("front-view", "the mirror drew " + FrontView.RenderedFrames + " pictures");

            // skins are chosen in the launcher only now (no Skins button in the game), so no skins check here

            // ---- checks inside the real game ----
            MeltyTest.SetPhase("comprobaciones");
            yield return HungerChecks(p);
            yield return CritCheck(p);

            ThirdPerson(p, false);
            Hold(p, 5);
            MeltyTest.SetPhase("fin");
            Debug.Log("[SVD] Melty test: demo done.");
        }

        // ---- the rules as pure numbers ----

        static void RuleChecks()
        {
            bool crit = AttackCharge.Damage(10, 1f, false, 0f) == 10 && AttackCharge.Damage(10, 1f, true, 0f) == 15
                     && AttackCharge.Damage(10, 0f, false, 0f) == 2 && AttackCharge.Damage(10, 1f, true, 3f) == 18;
            if (crit) MeltyTest.Pass("crit-rule"); else MeltyTest.Fail("crit-rule", "damage formula gave wrong numbers");

            float ex = 8.1f, sat = 1f, food = 20f;
            Hunger.Spend(ref ex, ref sat, ref food);       // 8.1 tiredness: 1 saturation, then 1 food
            bool hunger = sat == 0f && food == 19f && ex > 0.05f && ex < 0.15f;
            if (hunger) MeltyTest.Pass("hunger-rule"); else MeltyTest.Fail("hunger-rule", $"sat {sat} food {food} left {ex}");
        }

        // ---- the skins screen: open it, put a different skin on in the middle of the game, restore the player's own ----

        static IEnumerator SkinsCheck(EntityPlayerLocal p)
        {
            MeltyTest.SetPhase("skins");
            var menu = Skins.SkinMenu.Instance;
            var before = SteveCharacter.SkinTexture;
            string nameBefore = SteveCharacter.SkinName;
            string settingBefore = Settings.Get("skin", "auto");
            string testFile = null;
            bool ok = false;
            string why = "";
            if (menu == null || before == null) { MeltyTest.Fail("skin-menu", "no skin screen or no skin"); yield break; }

            // a green copy of the current skin, saved in the skins folder like a player would add one
            try
            {
                var px = before.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a == 0) continue;
                    px[i].r = (byte)(px[i].r * 0.35f);
                    px[i].g = (byte)Mathf.Min(255, px[i].g * 1.4f + 50);
                    px[i].b = (byte)(px[i].b * 0.35f);
                }
                var tint = new Texture2D(before.width, before.height, TextureFormat.RGBA32, false);
                tint.SetPixels32(px);
                tint.Apply();
                testFile = Skins.SkinLibrary.SaveTexture(tint, "prueba-verde");
                UnityEngine.Object.Destroy(tint);
            }
            catch (Exception ex) { why = "could not make the test skin: " + ex.Message; }

            if (testFile != null)
            {
                menu.OpenForTest();
                yield return Wait(1f);
                yield return Shot("skins-pantalla");
                var entry = Skins.SkinLibrary.Entries.Find(e => e.Key == testFile);
                if (entry == null) why = "the new skin is not in the library";
                else if (Skins.SkinLibrary.Entries.Count < 2) why = "the library has only " + Skins.SkinLibrary.Entries.Count + " skin";
                else
                {
                    Skins.SkinLibrary.Apply(entry);
                    yield return Wait(0.8f);
                    yield return Shot("skins-pantalla-usada");
                    menu.Close();
                    yield return Wait(0.5f);
                    yield return Selfie(p, "selfie-skin-verde", new Vector3(0f, 1.0f, 3.0f));
                    ok = !ReferenceEquals(SteveCharacter.SkinTexture, before) && SteveCharacter.SkinName == "prueba-verde"
                         && Settings.Get("skin", "auto") == testFile;
                    if (!ok) why = $"skin in use is '{SteveCharacter.SkinName}', setting '{Settings.Get("skin", "auto")}'";
                }
                menu.Close();
            }

            // put the player's own skin and setting back, remove the test file
            SteveCharacter.SetSkin(before, nameBefore);
            Settings.Set("skin", settingBefore);
            if (testFile != null) Skins.SkinLibrary.Delete(testFile);
            Skins.SkinLibrary.Refresh();

            if (ok) MeltyTest.Pass("skin-menu"); else MeltyTest.Fail("skin-menu", why);

            // the Skins button must show in the pause menu (that is how a skin is changed during a game)
            bool opened = false;
            try
            {
                p.PlayerUI.windowManager.Open(XUiC_InGameMenuWindow.ID, true);
                opened = true;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo pause menu: " + ex.Message); }
            yield return Wait(1.2f);
            bool shown = Skins.SkinMenu.ButtonVisible;
            yield return Shot("skins-boton-menu-pausa");
            try { p.PlayerUI.windowManager.Close(XUiC_InGameMenuWindow.ID); } catch { }
            yield return Wait(0.6f);
            if (!opened) MeltyTest.Fail("skin-button", "the test could not open the pause menu");
            else if (shown) MeltyTest.Pass("skin-button");
            else MeltyTest.Fail("skin-button", "the Skins button did not show with the pause menu open");
        }

        // ---- hunger, sprint and regeneration in the running game ----

        static IEnumerator HungerChecks(EntityPlayerLocal p)
        {
            var food = p.Stats.Food;
            var health = p.Stats.Health;
            float point = food.Max / 20f;

            // 1) tiredness is paid with saturation first, then with food
            health.Value = health.Max;
            food.Value = food.Max;                          // "eat" until full: saturation fills too
            yield return Wait(0.4f);
            float sat0 = Hunger.Saturation, food0 = food.Value;
            Hunger.AddExhaustion(4f * (Mathf.Ceil(sat0) + 1f) + 0.2f);
            yield return Wait(0.4f);
            float lost = (food0 - food.Value) / point;
            if (sat0 > 0f && Hunger.Saturation <= 0.01f && lost > 0.7f && lost < 2.3f) MeltyTest.Pass("hunger-game");
            else MeltyTest.Fail("hunger-game", $"saturation {sat0:0.0}->{Hunger.Saturation:0.0}, food points lost {lost:0.00}");

            // 2) 6 food points or fewer: no sprinting
            food.Value = point * 5f;
            int blocks0 = Hunger.SprintBlocksApplied;
            yield return Wait(0.6f);
            if (Hunger.SprintBlocked && Hunger.SprintBlocksApplied > blocks0) MeltyTest.Pass("sprint-block");
            else MeltyTest.Fail("sprint-block", $"blocked={Hunger.SprintBlocked}, switch applied {Hunger.SprintBlocksApplied - blocks0} times");

            // 3) well fed: health comes back
            food.Value = food.Max;
            yield return Wait(0.4f);
            health.Value = health.Max * 0.6f;
            float h0 = health.Value;
            yield return Wait(3f);
            float healed = (health.Value - h0) / (health.Max / 20f);
            if (healed >= 1f) MeltyTest.Pass("regen");      // zombies near the spawn may take some back during the 3 s
            else MeltyTest.Fail("regen", $"healed {healed:0.0} health points in 3 s (food {food.Value / point:0.0}, saturation {Hunger.Saturation:0.0})");

            health.Value = health.Max;
            food.Value = food.Max;
            yield return Wait(0.3f);
        }

        // ---- a real hit on a real animal: full charge, empty bar, and a critical while falling ----

        static IEnumerator CritCheck(EntityPlayerLocal p)
        {
            Hold(p, 5);
            yield return Wait(0.6f);
            EntityAlive target = SpawnTarget(p);
            if (target == null) { MeltyTest.Fail("crit-hit", "could not spawn a test animal"); yield break; }
            yield return Wait(1.5f);

            string r1 = Hit(p, target);                     // bar full, standing
            string r2 = Hit(p, target);                     // at once: bar empty
            yield return Wait(1f);
            float y0 = p.position.y;
            Lift(p, 3.0f);
            bool falling = false;
            for (int i = 0; i < 180 && !falling; i++)
            {
                yield return null;
                if (p == null) yield break;
                falling = AttackCharge.IsFalling(p);
                if (i % 15 == 0)
                    Debug.Log($"[SVD] lift: height {p.position.y - y0:0.00} m, ground {p.onGround}, speed {AttackCharge.VerticalSpeed:0.0} m/s, falling {falling}");
            }
            string r3 = Hit(p, target);                     // bar full, falling
            Remove(target);

            string got = $"standing {r1}, empty bar {r2}, falling {r3}";
            if (r1 == "10>10" && r2 == "10>2" && r3 == "10>15 crit") MeltyTest.Pass("crit-hit");
            else MeltyTest.Fail("crit-hit", got + (falling ? "" : " (the player never fell)"));
            Debug.Log("[SVD] Melty test: hits on the animal: " + got);
        }

        static string Hit(EntityPlayerLocal p, EntityAlive target)
        {
            try
            {
                if (target == null || target.IsDead() || target.transform == null) return "no target";
                AttackCharge.Swing();
                int n = AttackCharge.HitCount;
                target.DamageEntity(new DamageSourceEntity(EnumDamageSource.External, EnumDamageTypes.Bashing, p.entityId), 10, false, 1f);
                if (AttackCharge.HitCount == n) return "not counted";
                return AttackCharge.LastHitBefore + ">" + AttackCharge.LastHitAfter + (AttackCharge.LastHitCrit ? " crit" : "");
            }
            catch (Exception ex) { return "error " + ex.GetType().Name + " " + ex.Message + " @ " + (ex.StackTrace ?? "").Split('\n')[0].Trim(); }
        }

        static EntityAlive SpawnTarget(EntityPlayerLocal p)
        {
            try
            {
                Vector3 fwd = p.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                var e = EntityFactory.CreateEntity(EntityClass.FromString("animalStag"), p.position + fwd.normalized * 3f + Vector3.up * 0.5f) as EntityAlive;
                if (e == null) return null;
                GameManager.Instance.World.SpawnEntityInWorld(e);
                return e;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo spawn: " + ex.Message); return null; }
        }

        static void Remove(EntityAlive e)
        {
            try { if (e != null) GameManager.Instance.World.RemoveEntity(e.entityId, EnumRemoveEntityReason.Despawned); }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo remove: " + ex.Message); }
        }

        // ---- small actions ----

        static void Hold(EntityPlayerLocal p, int slot)
        {
            try { p.inventory.SetHoldingItemIdx(slot); } catch (Exception ex) { Debug.LogWarning("[SVD] demo hold: " + ex.Message); }
        }

        static void ThirdPerson(EntityPlayerLocal p, bool on)
        {
            try { p.SetFirstPersonView(!on, false); } catch (Exception ex) { Debug.LogWarning("[SVD] demo view: " + ex.Message); }
        }

        static void Lift(EntityPlayerLocal p, float metres)
        {
            // the controller works in the scene's own coordinates (transform.position); the entity's
            // position is world coordinates = scene + the game's floating origin (tens of metres off)
            Vector3 target = p.transform.position + Vector3.up * metres;
            try { var c = p.vp_FPController; if (c != null) c.SetPosition(target); else p.SetPosition(p.position + Vector3.up * metres, true); }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo lift: " + ex.Message); }
        }

        static bool Menu(EntityPlayerLocal p, bool open)
        {
            try
            {
                if (open) XUiC_WindowSelector.OpenSelectorAndWindow(p, "character");
                else
                {
                    var wm = p.PlayerUI.windowManager;
                    wm.CloseAllOpenModalWindows();
                    wm.Close("windowpaging");
                }
                return true;
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] demo menu: " + ex.Message); return false; }
        }

        // ---- pictures ----

        // The game's own frame, exactly as the player sees it (HUD included).
        static IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
                Debug.Log("[SVD] Melty test: picture " + name);
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] picture " + name + ": " + ex.Message); }
            yield return null;
            yield return null;
            yield return null;
        }

        // A second camera looking back at Steve (the game's third-person camera only shows his back).
        static IEnumerator Selfie(EntityPlayerLocal p, string name, Vector3 offset)
        {
            yield return new WaitForEndOfFrame();
            try { TakeSelfie(p, name, offset); }
            catch (Exception ex) { Debug.LogWarning("[SVD] selfie " + name + ": " + ex); }
        }

        static void TakeSelfie(EntityPlayerLocal p, string name, Vector3 offset)
        {
            var root = SteveModel.LocalRoot;
            if (root == null || p == null || p.playerCamera == null) { Debug.LogWarning("[SVD] selfie " + name + ": no Steve to look at"); return; }
            Vector3 target = root.position + Vector3.up * 0.9f;

            // turn the viewpoint around Steve until nothing (a tree, a wall, a slope) is in the way
            Vector3 camPos = root.position + root.TransformDirection(offset);
            float usedTurn = 0f;
            foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f })
            {
                Vector3 pos = root.position + root.TransformDirection(Quaternion.Euler(0f, turn, 0f) * offset);
                Vector3 dir = target - pos;
                if (!Physics.Raycast(pos, dir.normalized, dir.magnitude - 0.9f))
                {
                    camPos = pos; usedTurn = turn;
                    break;
                }
            }

            var go = new GameObject("SVD_Selfie");
            Texture2D tex = null;
            RenderTexture rt = null;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(p.playerCamera);               // same layers, sky and fog as the player's view
                cam.enabled = false;
                cam.allowHDR = false;
                cam.allowMSAA = false;
                cam.rect = new Rect(0f, 0f, 1f, 1f);
                cam.fieldOfView = 38f;
                cam.nearClipPlane = 0.05f;
                go.transform.position = camPos;
                go.transform.LookAt(target);

                rt = RenderTexture.GetTemporary(540, 720, 24);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
                Debug.Log($"[SVD] Melty test: picture {name} (viewpoint turned {usedTurn:0} degrees)");
            }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (tex != null) UnityEngine.Object.Destroy(tex);
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
