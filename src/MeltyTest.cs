using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SteveDaysToDie
{
    /// <summary>
    /// Own log file (%LOCALAPPDATA%\SteveDaysToDie\melty.log) and Melty's automatic test mode.
    /// Test mode is active only while %LOCALAPPDATA%\Melty\test\steve-vs-zombies.json exists:
    /// the mod then starts a throwaway world by itself, runs its demo (MeltyDemo) and reports its
    /// checks. It never quits the game.
    /// </summary>
    public static class MeltyTest
    {
        public const string Slug = "steve-vs-zombies";
        const string TestWorld = "Navezgane";
        const string TestGameName = "SVDMeltyTest";

        public static bool Active { get; private set; }
        static string _checksPath, _statePath, _logPath;
        static readonly object _lock = new object();
        static readonly HashSet<string> _reported = new HashSet<string>();
        static bool _menuSeen;

        public static void Init()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            try
            {
                string dir = Path.Combine(local, "SteveDaysToDie");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "melty.log");
                File.WriteAllText(_logPath, "Steve vs Zombies log " + DateTime.Now.ToString("s") + Environment.NewLine);
                Application.logMessageReceivedThreaded += OnLog;
            }
            catch { }

            try
            {
                string testFile = Path.Combine(local, "Melty", "test", Slug + ".json");
                if (File.Exists(testFile))
                {
                    string json = File.ReadAllText(testFile);
                    var m = Regex.Match(json, "\"checks\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (m.Success)
                    {
                        _checksPath = Regex.Unescape(m.Groups[1].Value);
                        Active = true;
                        var st = Regex.Match(json, "\"state\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                        if (st.Success) _statePath = Regex.Unescape(st.Groups[1].Value);
                        Debug.Log("[SVD] Melty test mode ON. Test file: " + Regex.Replace(json, "\\s+", " "));
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[SVD] Melty test file unreadable: " + ex.Message); }

            try
            {
                ModEvents.MainMenuOpened.RegisterHandler(OnMainMenuOpened);
                ModEvents.PlayerSpawnedInWorld.RegisterHandler(OnPlayerSpawned);
            }
            catch (Exception ex) { Debug.LogError("[SVD] Could not register game events: " + ex); }

            if (Active)
            {
                var go = new GameObject("SVD_MeltyTestDriver");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<MeltyTestDriver>();
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (_logPath == null || msg == null) return;
            bool ours = msg.IndexOf("[SVD]", StringComparison.Ordinal) >= 0
                     || msg.IndexOf("SteveDaysToDie", StringComparison.Ordinal) >= 0
                     || (stack != null && stack.IndexOf("SteveDaysToDie", StringComparison.Ordinal) >= 0);
            if (!ours && type != LogType.Exception) return;
            try
            {
                lock (_lock)
                    File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss") + " " + type + " " + msg
                        + (type == LogType.Exception || type == LogType.Error ? Environment.NewLine + stack : "")
                        + Environment.NewLine);
            }
            catch { }
        }

        public static void Pass(string check) => Report(check, null);
        public static void Fail(string check, string why) => Report(check, why ?? "unknown");

        static void Report(string check, string why)
        {
            if (!Active || _checksPath == null) return;
            lock (_lock)
            {
                if (!_reported.Add(check)) return;
                try
                {
                    File.AppendAllText(_checksPath,
                        (why == null ? "pass " + check : "fail " + check + ": " + why) + "\n");
                }
                catch { }
            }
            Debug.Log("[SVD] Melty check " + (why == null ? "pass " : "FAIL ") + check + (why == null ? "" : ": " + why));
        }

        /// <summary>Says what the demo is showing right now (log, and Melty's state file when it names one).</summary>
        public static void SetPhase(string phase)
        {
            Debug.Log("[SVD] Melty test phase: " + phase);
            if (_statePath == null) return;
            try { File.WriteAllText(_statePath, phase); } catch { }
        }

        static void OnMainMenuOpened(ref ModEvents.SMainMenuOpenedData _data)
        {
            _menuSeen = true;
        }

        static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData _data)
        {
            if (!_data.IsLocalPlayer) return;
            try
            {
                var player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player != null)
                {
                    Patches.PlayerSpawnPatch.Attach(player);
                    Pass("in-world");
                    if (Active)
                    {
                        GiveTestItems(player);
                        MeltyDemo.Start(player);
                    }
                }
                else Fail("in-world", "no local player after spawn");
            }
            catch (Exception ex) { Debug.LogError("[SVD] OnPlayerSpawned: " + ex); }
        }

        // Test runs only: put a few of the game's own items on the bar so the run shows it working.
        static void GiveTestItems(EntityPlayerLocal player)
        {
            string[] names = { "meleeToolRepairT0StoneAxe", "resourceWood", "resourceRockSmall", "drinkJarBoiledWater", "foodCanChili" };
            int[] counts = { 1, 64, 32, 3, 2 };
            int[] slots = { 0, 1, 2, 3, 9 };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    var existing = player.inventory.GetItem(slots[i]);
                    if (existing != null && !existing.IsEmpty()) continue;
                    var iv = ItemClass.GetItem(names[i], false);
                    if (iv == null || iv.IsEmpty()) { Debug.LogWarning("[SVD] test item missing: " + names[i]); continue; }
                    player.inventory.SetItem(slots[i], new ItemStack(iv, counts[i]));
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] test item " + names[i] + ": " + ex.Message); }
            }
        }

        class MeltyTestDriver : MonoBehaviour
        {
            bool _started;
            float _readySince = -1f, _nextSpawnTry, _nextStatus;

            // The "ready to spawn?" screen waits for a click: press its button ourselves.
            void TrySpawn()
            {
                if (Time.realtimeSinceStartup < _nextSpawnTry) return;
                _nextSpawnTry = Time.realtimeSinceStartup + 3f;
                try
                {
                    var gm = GameManager.Instance;
                    if (gm == null || gm.World == null) return;
                    var ui = LocalPlayerUI.primaryUI;
                    if (ui == null || string.IsNullOrEmpty(XUiC_SpawnSelectionWindow.ID)) return;
                    if (!ui.windowManager.IsWindowOpen(XUiC_SpawnSelectionWindow.ID)) return;
                    var win = ui.xui.GetChildByType<XUiC_SpawnSelectionWindow>();
                    if (win == null) return;
                    Debug.Log("[SVD] Melty test: pressing spawn.");
                    win.SpawnButtonPressed(SpawnMethod.Invalid);
                }
                catch (Exception ex) { Debug.LogWarning("[SVD] Melty test spawn: " + ex.Message); }
            }

            // A line every few seconds with the numbers the features run on, to read after the run.
            void Status()
            {
                if (Time.realtimeSinceStartup < _nextStatus) return;
                _nextStatus = Time.realtimeSinceStartup + 5f;
                try
                {
                    var p = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (p == null || p.Stats == null) return;
                    var f = p.Stats.Food; var st = p.Stats.Stamina;
                    Debug.Log($"[SVD] status: hp {p.Health}/{p.GetMaxHealth()} food {f.Value:0.0}/{f.Max:0} sat {Survival.Hunger.Saturation:0.0} " +
                              $"tired {Survival.Hunger.Exhaustion:0.00} stamina {st.Value:0}/{st.Max:0} run {p.MovementRunning} ground {p.onGround} " +
                              $"noSprint {Survival.Hunger.SprintBlocked} hud {MinecraftHUD.Visible(p)} nativeBar {HUD.NativeToolbelt.NativeShown} " +
                              $"bar {Combat.AttackCharge.Charge:0.00}/{Combat.AttackCharge.CurrentCooldown:0.00}s firstPerson {p.bFirstPersonView} armR {Steve.SteveModel.LastArmR.x:0}/{Steve.SteveModel.LastArmR.y:0}/{Steve.SteveModel.LastArmR.z:0} armor {MinecraftHUD.GetArmorPoints(p)}");
                }
                catch { }
            }

            void Update()
            {
                if (_started) { TrySpawn(); Status(); return; }
                var gm = GameManager.Instance;
                if (gm == null || LocalPlayerUI.primaryUI == null) return;
                if (gm.World != null) { _started = true; return; }
                if (_readySince < 0f) _readySince = Time.realtimeSinceStartup;
                float waited = Time.realtimeSinceStartup - _readySince;
                // Start once the main menu is up (plus a moment), or after 60 s whatever is on screen (news page).
                if ((_menuSeen && waited > 4f) || waited > 60f)
                {
                    _started = true;
                    StartCoroutine(StartWorld());
                }
            }

            IEnumerator StartWorld()
            {
                yield return new WaitForSeconds(2f);
                try
                {
                    Debug.Log("[SVD] Melty test: starting world " + TestWorld + " / " + TestGameName);
                    GamePrefs.Set(EnumGamePrefs.GameWorld, TestWorld);
                    GamePrefs.Set(EnumGamePrefs.GameName, TestGameName);
                    GamePrefs.Set(EnumGamePrefs.GameMode, "GameModeSurvival");
                    GamePrefs.Set(EnumGamePrefs.ServerEnabled, false);
                    try
                    {
                        var loc = PathAbstractions.WorldsSearchPaths.GetLocation(TestWorld);
                        GamePrefs.Set(EnumGamePrefs.GameWorldLocationType, (int)loc.Type);
                    }
                    catch (Exception ex) { Debug.LogWarning("[SVD] world location: " + ex.Message); }

                    var ui = LocalPlayerUI.primaryUI;
                    try { ui.windowManager.Close(XUiC_NewsScreen.ID); } catch { }
                    try { ui.windowManager.Close(XUiC_MainMenu.ID); } catch { }
                    try { XUiC_MainMenu.CloseGlobalMenuWindows(ui.xui); } catch { }

                    var err = SingletonMonoBehaviour<ConnectionManager>.Instance.StartServers("", true);
                    Debug.Log("[SVD] Melty test: StartServers -> " + err);
                    if (err != NetworkConnectionError.NoError)
                        Fail("in-world", "could not start world: " + err);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[SVD] Melty test start failed: " + ex);
                    Fail("in-world", ex.Message);
                }
            }
        }
    }
}
