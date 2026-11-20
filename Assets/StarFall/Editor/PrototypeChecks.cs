using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace StarFall.Editor
{
    [InitializeOnLoad]
    public static class PrototypeChecks
    {
        private const string Pending = "StarFall.Checks.Pending";
        private static int phase, assertions;
        private static double deadline, nextStep;
        private static Vector3 beforeInput;
        private static Keyboard keyboard;
        private static Mouse mouse;
        private static int shotsBeforeInput;
        private static int inputStartFrame;
        private static InputSettings originalInputSettings;
        private static InputSettings testInputSettings;
        private static readonly List<string> errors = new List<string>();

        static PrototypeChecks()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += (message, trace, type) =>
            {
                if (SessionState.GetBool(Pending, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    errors.Add(message);
            };
        }

        [MenuItem("StarFall/Run Prototype Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before running checks.");
            phase = assertions = 0;
            deadline = nextStep = 0;
            errors.Clear();
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Update()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 120;
            try
            {
                if (errors.Count > 0) throw new Exception(string.Join("\n", errors));
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Play Mode checks timed out.");
                var app = UnityEngine.Object.FindAnyObjectByType<GameApplication>();
                if (app == null || app.UI == null || app.Battle.Player == null) return;
                if (EditorApplication.timeSinceStartup < nextStep) return;
                switch (phase)
                {
                    case 0:
                        CheckRules();
                        Check(app.UI.MenuVisible, "Main menu starts visible");
                        Capture(app, "01-main-menu");
                        app.OpenSettings();
                        var slider = app.GetComponentInChildren<Slider>(true);
                        float volume = slider.value;
                        slider.value = 0.23f;
                        Check(Mathf.Abs(app.Audio.Volume - 0.23f) < 0.001f, "Settings volume reaches audio service");
                        slider.value = volume;
                        Capture(app, "02-settings");
                        app.CloseSettings();
                        Click(app.UI.NewGameButton); Capture(app, "base-entry"); Click(app.UI.DeployButton);
                        Check(app.Session.State == MatchState.Playing, "New game button starts match");
                        beforeInput = app.Battle.Player.Position;
                        originalInputSettings = InputSystem.settings;
                        testInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
                        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        InputSystem.settings = testInputSettings;
                        keyboard = InputSystem.AddDevice<Keyboard>("StarFallTestKeyboard");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                        inputStartFrame = Time.frameCount;
                        nextStep = EditorApplication.timeSinceStartup + 0.3;
                        phase = 1;
                        break;
                    case 1:
                        if (Time.frameCount < inputStartFrame + 10)
                        {
                            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                            return;
                        }
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Check(app.Battle.Player.Position.z > beforeInput.z, "W input moves player in Play Mode");
                        mouse = InputSystem.AddDevice<Mouse>("StarFallTestMouse");
                        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * 0.8f, Screen.height * 0.5f) });
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.W));
                        inputStartFrame = Time.frameCount;
                        phase = 10;
                        break;
                    case 10:
                        if (Time.frameCount < inputStartFrame + 10) return;
                        Check(app.Battle.Player.IsAbsorbing, "Space key starts absorption in Play Mode");
                        Check(app.Battle.Player.AimDirection.x > 0.5f, "Mouse right side aims staff to right in Play Mode");
                        shotsBeforeInput = app.Battle.ShotsFired;
                        beforeInput = app.Battle.Player.Position;
                        inputStartFrame = Time.frameCount;
                        phase = 11;
                        break;
                    case 11:
                        if (Time.frameCount < inputStartFrame + 10) return;
                        Check(app.Battle.ShotsFired == shotsBeforeInput, "Space input suppresses automatic attacks");
                        Check(app.Battle.Player.Position.z > beforeInput.z, "Movement continues during absorption");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        inputStartFrame = Time.frameCount;
                        phase = 12;
                        break;
                    case 12:
                        if (Time.frameCount < inputStartFrame + 10) return;
                        Check(!app.Battle.Player.IsAbsorbing, "Releasing Space ends absorption in Play Mode");
                        Time.timeScale = 0;
                        PhaseOneChecks.Run(app, Check, Capture);
                        BuildChecks.Run(app, Check, Capture);
                        BuildChecks.StartBuildSequence(app, BuildId.Vitality);
                        app.Battle.Step(0.4f, Vector2.zero, Vector3.forward * 10);
                        app.Session.CollectSoul(1);
                        float draftTime = app.Session.Elapsed;
                        var draftPosition = app.Battle.Enemies[0].Position;
                        app.Battle.Step(1, Vector2.one, Vector3.forward * 10);
                        Check(app.Session.Elapsed == draftTime && app.Battle.Enemies[0].Position == draftPosition, "Live choice freezes battle simulation");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                        nextStep = EditorApplication.timeSinceStartup + 0.4;
                        phase = 20;
                        break;
                    case 20:
                        Check(app.Session.State == MatchState.ChoosingBuild && !app.UI.ChoicesArmed, "Held absorption key cannot auto-select an upgrade");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        nextStep = EditorApplication.timeSinceStartup + 0.4;
                        phase = 21;
                        break;
                    case 21:
                        Check(app.UI.ChoicesArmed, "Choice input arms after held key is released");
                        Capture(app, "12-build-interactive");
                        int vitalityIndex = 0;
                        while (app.Session.Builds.Offers[vitalityIndex] != BuildId.Vitality) vitalityIndex++;
                        Click(app.UI.GetBuildButton(vitalityIndex));
                        Check(app.Session.State == MatchState.Playing && app.Session.MaxHealth == 120 && !app.UI.BuildSelectionVisible, "Real card click applies effect and resumes game");
                        app.ReturnToMenu(); app.NewGame(); app.Deploy();
                        app.Session.CollectSoul(7);
                        Check(app.Session.Builds.PendingChoices == 2, "Live multi-level pickup queues two choices");
                        nextStep = EditorApplication.timeSinceStartup + 0.4;
                        phase = 22;
                        break;
                    case 22:
                        Click(app.UI.GetBuildButton(0));
                        app.UI.GetBuildButton(0).onClick.Invoke();
                        Check(app.Session.State == MatchState.ChoosingBuild && app.Session.Builds.TotalRanks == 1, "Double click cannot accept next queued choice");
                        nextStep = EditorApplication.timeSinceStartup + 0.4;
                        phase = 23;
                        break;
                    case 23:
                        Click(app.UI.GetBuildButton(0));
                        Check(app.Session.State == MatchState.Playing && app.Session.Builds.TotalRanks == 2, "Second queued UI choice resumes combat");
                        CheckBattle(app);
                        phase = 2;
                        nextStep = EditorApplication.timeSinceStartup + 0.25;
                        break;
                    case 2:
                        Capture(app, "03-battle");
                        app.Session.Pause();
                        float elapsed = app.Session.Elapsed;
                        Vector3 position = app.Battle.Player.Position;
                        app.Battle.Step(1, Vector2.one);
                        Check(app.Session.Elapsed == elapsed && app.Battle.Player.Position == position, "Pause freezes combat and movement");
                        Capture(app, "04-pause");
                        app.Resume();
                        StageThreeChecks.Run(app, Check, Capture);
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                        phase = 30; nextStep = EditorApplication.timeSinceStartup + 0.2; break;
                    case 30:
                        Check(app.Session.State == MatchState.Won && app.Session.HasClearanceProof, "Real E input claims proof and wins"); Check(!app.Battle.TryOpenChest() && !app.Session.ClaimClearanceProof(), "Proof cannot be claimed twice");
                        app.UI.Refresh(app.Session, app.Battle.Config.resultDisplaySeconds);
                        Capture(app, "05-victory");
                        phase = 3;
                        nextStep = EditorApplication.timeSinceStartup + app.Battle.Config.resultDisplaySeconds + 0.8;
                        break;
                    case 3:
                        Check(app.Session.State == MatchState.HomeBase && app.UI.BaseVisible, "Victory automatically returns to base");
                        Check(app.Battle.EnemyCount == 0 && app.Battle.ProjectileCount == 0, "Return cleans active entities");
                        app.ReturnToMenu();
                        for (int i = 0; i < 3; i++)
                        {
                            Click(app.UI.NewGameButton); Capture(app, "base-entry"); Click(app.UI.DeployButton);
                            Check(app.Session.Health == 100 && app.Session.Kills == 0 && app.Session.Elapsed == 0 && !app.Session.HasClearanceProof && !app.Session.FinalBossDefeated && !app.Battle.ChestAvailable && app.Battle.Boss == null && app.Battle.WarningCount == 0, "Restart resets state " + i);
                            app.Battle.Step(0.4f, Vector2.zero);
                            Check(app.Battle.EnemyCount == 1, "Restart has a single spawn schedule " + i);
                            app.ReturnToMenu();
                        }
                        Click(app.UI.NewGameButton); Capture(app, "base-entry"); Click(app.UI.DeployButton);
                        app.Battle.Step(0.4f, Vector2.zero);
                        var enemy = app.Battle.Enemies[0];
                        float health = app.Session.Health;
                        enemy.transform.position = app.Battle.Player.Position;
                        app.Battle.Step(0.01f, Vector2.zero);
                        Check(app.Session.Health < health, "Enemy contact causes damage");
                        float afterHit = app.Session.Health;
                        app.Battle.Step(0.01f, Vector2.zero);
                        Check(app.Session.Health == afterHit, "Contact cooldown prevents per-frame damage");
                        app.Session.Damage(10000);
                        Check(app.Session.State == MatchState.Lost, "Zero health loses match");
                        app.UI.Refresh(app.Session, 4);
                        Capture(app, "06-defeat");
                        phase = 4;
                        nextStep = EditorApplication.timeSinceStartup + app.Battle.Config.resultDisplaySeconds + 0.8;
                        break;
                    case 4:
                        Check(app.UI.BaseVisible, "Defeat also returns to base");
                        StageFourChecks.Run(app, Check, Capture, Click);
                        ElementsChecks.Run(app, Check, Capture, Click);
                        FusionShowcaseChecks.Run(app, Check, Capture);
                        app.StartTraining();
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit3));
                        inputStartFrame = Time.frameCount; phase = 40; break;
                    case 40:
                        if (Time.frameCount < inputStartFrame + 10) return;
                        Check(app.TestMode && app.Battle.ActiveGem.element == DamageElement.Wind, "Real number key switches training gem");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.U));
                        inputStartFrame = Time.frameCount; phase = 41; break;
                    case 41:
                        if (Time.frameCount < inputStartFrame + 10) return;
                        Check(app.Session.State == MatchState.ChoosingBuild, "Real U key opens a free training upgrade");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                        inputStartFrame = Time.frameCount; phase = 42; break;
                    case 42:
                        if (Time.frameCount < inputStartFrame + 10) return;
                        Check(app.Session.State == MatchState.Playing && app.Session.Builds.TotalRanks == 0, "Real R key restarts training from a pending choice");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        app.ReturnToBase();
                        Time.timeScale = 1;
                        Finish(true, $"STARFALL_CHECKS_OK: {assertions} assertions; Play Mode, UI, input, combat, pause, victory, defeat and restart checked.");
                        break;
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void CheckRules()
        {
            var match = new MatchSession();
            int endings = 0;
            match.StateChanged += state => { if (state == MatchState.Won || state == MatchState.Lost) endings++; };
            match.Start(180, 100);
            match.Advance(179);
            Check(match.State == MatchState.Playing && match.Remaining == 1, "No early victory");
            match.Pause(); match.Advance(99); match.Damage(100);
            Check(match.Remaining == 1 && match.Health == 100, "Paused rules reject time and damage");
            match.Resume(); match.Advance(1); match.Advance(10); Check(match.State == MatchState.Playing, "Timer alone never wins");
            match.DefeatFinalBoss(); match.ClaimClearanceProof(); match.Damage(100);
            Check(match.State == MatchState.Won && endings == 1, "Victory terminal and emitted once");
            match.ReturnToMenu(); match.Start(180, 100); match.Damage(100); match.Advance(180);
            Check(match.State == MatchState.Lost && endings == 2, "Defeat is terminal");
            Check(Projectile.Sweep(new Vector3(-10, 0, 0), new Vector3(10, 0, 0), Vector3.zero, 0.6f, out float hit)
                && hit > 0 && hit < 0.5f, "Fast projectile detects swept hit");
            Check(!Projectile.Sweep(new Vector3(-10, 0, 2), new Vector3(10, 0, 2), Vector3.zero, 0.6f, out _), "Swept miss stays miss");
            Check(Projectile.Sweep(Vector3.zero, Vector3.right, Vector3.zero, 0.6f, out hit) && hit == 0, "Spawn-overlap hit is detected");
        }

        private static void CheckBattle(GameApplication app)
        {
            var battle = app.Battle;
            app.ReturnToMenu(); app.NewGame(); app.Deploy();
            battle.Step(0.1f, new Vector2(1, 1));
            float distance = new Vector2(battle.Player.Position.x, battle.Player.Position.z).magnitude;
            Check(Mathf.Abs(distance - battle.Config.playerSpeed * 0.1f) < 0.001f, "Diagonal movement normalized");
            battle.Player.Move(new Vector2(1, 1), 100, battle.Config);
            Check(battle.Player.Position.x <= battle.Config.arenaSize.x / 2 - battle.Config.playerRadius + 0.001f
                && battle.Player.Position.z <= battle.Config.arenaSize.y / 2 - battle.Config.playerRadius + 0.001f, "Arena boundaries clamp movement");
            app.ReturnToMenu(); app.NewGame(); app.Deploy();
            battle.Step(0.4f, Vector2.zero);
            Check(battle.EnemyCount == 1, "Enemy spawns");
            battle.Enemies[0].transform.position = battle.Player.Position + Vector3.forward * 7;
            for (int i = 0; i < 50; i++) battle.Step(0.02f, Vector2.zero);
            Check(app.Session.Kills >= 1, "Automatic fire kills enemy without attack input");
            // Exercise late-game spawn cap without changing the saved config asset.
            for (int i = 0; i < 500; i++)
            {
                float angle = i * 0.05f;
                var target = new Vector3(Mathf.Cos(angle) * 14, 0.6f, Mathf.Sin(angle) * 8);
                var direction = target - battle.Player.Position;
                battle.Step(0.02f, new Vector2(direction.x, direction.z).normalized);
                Check(battle.EnemyCount <= battle.Config.maxEnemies, "Enemy count capped");
            }
            Check(app.Session.State == MatchState.Playing, "Sample combat remains active");
            // Deterministic screenshot fixture: real enemies/projectiles, no source asset changes.
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                float angle = (i + 1) * 1.1f;
                battle.Enemies[i].transform.position = battle.Player.Position + new Vector3(Mathf.Cos(angle) * 6, 0, Mathf.Sin(angle) * 6);
            }
            app.UI.Refresh(app.Session, 0);
        }

        private static void Click(Button button)
        {
            // Tests can open and click several panels in one editor tick. Render the new
            // canvas before raycasting so Unity has refreshed graphic culling/layout.
            Capture(UnityEngine.Object.FindAnyObjectByType<GameApplication>(), "ui-interaction");
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == button.gameObject, "Button reachable by pointer: " + button.name + " at " + pointer.position + " screen " + Screen.width + "x" + Screen.height + " first hit " + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void Capture(GameApplication app, string name)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("Logs/Checks");
            var camera = Camera.main;
            var canvas = app.GetComponentInChildren<Canvas>();
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes("Logs/Checks/" + name + ".png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("CHECK FAILED: " + description);
            assertions++;
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Pending, false);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (testInputSettings != null) UnityEngine.Object.DestroyImmediate(testInputSettings);
            Time.timeScale = 1;
            Directory.CreateDirectory("Logs/Checks");
            File.WriteAllText("Logs/Checks/result.txt", message);
            if (success) Debug.Log(message); else Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}






