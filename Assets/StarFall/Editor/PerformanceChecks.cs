using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarFall.Editor
{
    [InitializeOnLoad]
    public static class PerformanceChecks
    {
        private const string Pending = "StarFall.Performance.Pending";
        private static GameApplication app;
        private static RenderTexture target;
        private static Texture2D readback;
        private static Canvas canvas;
        private static Camera camera;
        private static int pair, frame;
        private static readonly List<double> work = new List<double>(), simulation = new List<double>(), intervals = new List<double>();
        private static readonly List<string> report = new List<string>();
        private static long previous;
        private static bool passed = true;
        static PerformanceChecks() { EditorApplication.update += Update; }
        [MenuItem("StarFall/Run 1080p Performance Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before benchmarking.");
            pair = frame = 0; passed = true; app = null; report.Clear();
            SessionState.SetBool(Pending, true); SessionState.SetBool("StarFall.Checks.Pending", true);
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static void Setup()
        {
            app.enabled = false; Time.timeScale = 0; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            target = new RenderTexture(1920, 1080, 24); target.Create();
            readback = new Texture2D(1, 1, TextureFormat.RGB24, false);
            camera = Camera.main; canvas = app.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            camera.targetTexture = target;
            report.Add($"Unity {Application.unityVersion}; {SystemInfo.processorType}; {SystemInfo.graphicsDeviceName}; 1920x1080 D3D11 rendered Play Mode, GPU-synchronized readback.");
            report.Add("Each pair: 180 warmup + 900 measured frames, 30 mobile durable enemies, level15, rapid automatic fire, forced ultimate every3s. Work includes simulation/UI/render/GPU wait; interval includes editor scheduling.");
            Prepare();
        }
        private static void Prepare()
        {
            UnityEngine.Random.InitState(6081 + pair);
            app.StartShowcase(pair < 10 ? pair : 6);
            app.Session.Builds.Reset(null, 6081 + pair, app.Battle.ActiveGem.element);
            app.Session.GrantTrainingBuild((BuildId)((int)BuildId.ResFire + (int)app.TestResonance - 1));
            while (!app.Session.Builds.IsMaxed)
            {
                app.TrainingUpgrade(); var b = app.Session.Builds;
                var pick = b.Offers[0];
                foreach (var preferred in new[] { BuildId.FormFan, BuildId.EvolveSpread, BuildId.UltimateField, BuildId.MasterHarmony, BuildId.SwiftCasting, BuildId.FireAffinity })
                    if (b.Offers.Contains(preferred)) { pick = preferred; break; }
                app.Session.ChooseBuild(pick, b.Revision);
            }
            var spawn = typeof(BattleController).GetMethod("SpawnEnemy", BindingFlags.NonPublic | BindingFlags.Instance);
            while (app.Battle.EnemyCount < 30) spawn.Invoke(app.Battle, new object[] { EnemyKind.Normal });
            if (pair == 10) spawn.Invoke(app.Battle, new object[] { EnemyKind.FinalBoss });
            for (int i = 0; i < 30; i++)
            {
                var e = app.Battle.Enemies[i]; e.PrepareTrainingTarget(10000000, 1.5f);
                if (pair == 10 && e.Kind == EnemyKind.Normal) e.ConfigureWave((EnemyRole)(i % 4), 1, 0);
                e.transform.position = new Vector3((i % 6 - 2.5f) * 1.2f, 0.6f, 2 + i / 6 * 1.1f);
            }
            frame = 0; work.Clear(); simulation.Clear(); intervals.Clear(); previous = Stopwatch.GetTimestamp();
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (app == null)
                {
                    app = UnityEngine.Object.FindAnyObjectByType<GameApplication>();
                    if (app == null || app.Battle == null) { app = null; return; }
                    Setup(); return;
                }
                long now = Stopwatch.GetTimestamp(); double interval = (now - previous) * 1000.0 / Stopwatch.Frequency; previous = now;
                var timer = Stopwatch.StartNew();
                var b = app.Battle;
                if (frame % 180 == 0) b.TrainingChargeFusion();
                var aim = b.Enemies[1].Position;
                b.Step(1f / 60, frame % 180 < 90 ? Vector2.right : Vector2.left, aim, ultimate: frame % 180 == 0);
                app.UI.Refresh(app.Session, 0);
                double cpu = timer.Elapsed.TotalMilliseconds;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); RenderTexture.active = null;
                if (frame >= 180) { work.Add(timer.Elapsed.TotalMilliseconds); simulation.Add(cpu); intervals.Add(interval); }
                if (++frame < 1080) return;
                var sorted = work.OrderBy(x => x).ToArray(); var cadence = intervals.OrderBy(x => x).ToArray();
                double p99 = sorted[(int)(sorted.Length * 0.99) - 1];
                bool ok = work.Max() <= 1000.0 / 45 && cadence[(int)(cadence.Length * 0.99) - 1] <= 1000.0 / 45;
                passed &= ok;
                report.Add($"{pair}: {FusionCatalog.Name(b.CurrentFusion)} enemies={b.EnemyCount} CPUmean={simulation.Average():F2}ms workMean={work.Average():F2}ms p99={p99:F2}ms max={work.Max():F2}ms cadenceMeanFPS={1000 / intervals.Average():F1} cadenceMax={intervals.Max():F2}ms below45={intervals.Count(x => x > 1000.0 / 45)} cadenceP99={cadence[(int)(cadence.Length * 0.99) - 1]:F2}ms {(ok ? "PASS" : "FAIL")}");
                Directory.CreateDirectory("Logs/Performance"); File.WriteAllLines("Logs/Performance/result.txt", report);
                if (++pair < 11) Prepare(); else Finish();
            }
            catch (Exception e) { report.Add(e.ToString()); passed = false; Finish(); }
        }
        private static void Finish()
        {
            report.Add(passed ? "PERFORMANCE_PASS" : "PERFORMANCE_FAIL"); Directory.CreateDirectory("Logs/Performance"); File.WriteAllLines("Logs/Performance/result.txt", report);
            SessionState.SetBool(Pending, false); SessionState.SetBool("StarFall.Checks.Pending", false);
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            Time.timeScale = 1;
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1); else EditorApplication.isPlaying = false;
        }
    }
}
