using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StarFall.Editor
{
    public static class RemainingFusionChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture, Action<Button> click)
        {
            var battle = app.Battle; var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = config.enableBossEncounters = false;
            SetConfig(battle, config);
            long wallet = app.Progress.Snapshot.souls;
            int proofs = app.Progress.Snapshot.proofs;
            try
            {
                app.UI.OpenWorkshop(); capture(app, "40-all-combinations");
                var buttons = app.UI.GetComponentsInChildren<Button>(true).Where(b => b.name.StartsWith("FusionDemo")).ToArray();
                check(buttons.Length == 10, "Workshop exposes ten one-click combinations");
                for (int a = 1; a <= 5; a++)
                    for (int b = 1; b <= 5; b++)
                        check(ElementCatalog.Compatible((DamageElement)a, (DamageElement)b) == (a != b), "All distinct elements are compatible; no self-pairing");
                for (int pair = 3; pair < 10; pair++)
                {
                    app.ReturnToBase(); app.UI.OpenWorkshop();
                    click(buttons.Single(b => b.name == "FusionDemo" + pair));
                    check(battle.CurrentFusion == (FusionKind)(pair + 1) && battle.EnemyCount == 12, "Actual showcase button selects pairing " + pair);
                    for (int reverse = 0; reverse < 2; reverse++)
                    {
                        if (reverse == 1)
                        {
                            app.TestResonance = FusionCatalog.Primary[pair];
                            app.RestartTraining((int)FusionCatalog.Secondary[pair] - 1);
                            battle.TrainingTargets(); battle.TrainingChargeFusion();
                        }
                        var victims = battle.Enemies.ToArray();
                        var counts = new Dictionary<DamageElement, int>();
                        foreach (var enemy in victims)
                        {
                            enemy.PrepareTrainingTarget(10000, 0);
                            var originalEvent = enemy.Damaged;
                            enemy.Damaged = (p, amount, element) => { originalEvent?.Invoke(p, amount, element); counts.TryGetValue(element, out int n); counts[element] = n + 1; };
                        }
                        var aim = pair == 7 ? victims[1].Position : new Vector3(0, 0.6f, 4);
                        float chargePerCast = 100f / (battle.AttackFrequency * 6);
                        battle.Step(0.02f, Vector2.zero, aim);
                        check(battle.FusionCastCount == 1 && battle.ActiveFusionCount == 1, "Charged combination casts " + pair + "/" + reverse);
                        float elapsed = pair == 3 ? 0.7f : pair == 7 ? 1 : 1.5f;
                        battle.Step(elapsed, Vector2.zero, aim, true);
                        float health = victims.Sum(e => e.Health);
                        check(health < 120000 && app.UI.DamageNumberCount > 0, "Combination damages enemies with numbers " + pair + "/" + reverse);
                        if (reverse == 0) { app.UI.Refresh(app.Session, 0); capture(app, "41-combination-" + pair); }
                        app.Session.Pause(); battle.Step(5, Vector2.zero);
                        check(battle.ActiveFusionCount == 1 && victims.Sum(e => e.Health) == health, "Pause freezes new combination " + pair);
                        app.Resume(); app.TrainingUpgrade(); battle.Step(5, Vector2.zero);
                        check(app.Session.State == MatchState.ChoosingBuild && victims.Sum(e => e.Health) == health, "Choice freezes new combination " + pair);
                        BuildChecks.DrainChoices(app.Session);
                        if (pair == 9)
                        {
                            check(victims.Any(e => e.RootRemaining > 0), "Stone tomb roots enemies before collapse");
                            battle.Step(1.1f, Vector2.zero, aim, true);
                            check(victims.Sum(e => e.Health) < health, "Stone tomb collapse deals delayed damage");
                            if (reverse == 0) { app.UI.Refresh(app.Session, 0); capture(app, "42-tomb-collapse"); }
                        }
                        battle.Step(5, Vector2.zero, aim, true);
                        check(counts.ContainsKey(FusionCatalog.Primary[pair]) && counts.ContainsKey(FusionCatalog.Secondary[pair]), "Each combination emits both constituent damage elements " + pair);
                        check(battle.FusionEnergy <= chargePerCast + 0.01f, "Combination and status ticks do not recursively charge " + pair);
                        check(battle.ActiveFusionCount == 0, "Combination expires and returns visual to pool " + pair);
                    }
                }

                // Distinguish persistent conduction from repeated one-shot steam damage.
                app.StartShowcase(6);
                var subject = battle.Enemies[5]; subject.PrepareTrainingTarget(10000, 0);
                subject.transform.position = new Vector3(0, 0.6f, 4);
                var lightning = new List<float>(); var previous = subject.Damaged;
                subject.Damaged = (p, n, e) => { previous?.Invoke(p, n, e); if (e == DamageElement.Lightning) lightning.Add(n); };
                battle.Step(0.02f, Vector2.zero, new Vector3(0, 0.6f, 4));
                battle.Step(2.2f, Vector2.zero, null, true);
                check(lightning.Contains(4) && lightning.Contains(6) && lightning.Contains(8) && lightning.Contains(10), "Conductive pool ramps over four hits then caps");

                app.StartShowcase(7);
                foreach (var e in battle.Enemies) { e.transform.position = new Vector3(12, 0.6f, -10); e.PrepareTrainingTarget(10000, 0); }
                var first = battle.Enemies[0]; var behind = battle.Enemies[1];
                first.transform.position = new Vector3(0, 0.6f, 2); behind.transform.position = new Vector3(0, 0.6f, 6);
                battle.Step(0.02f, Vector2.zero, first.Position); battle.Step(1.1f, Vector2.zero, null, true);
                check(first.Health < behind.Health && behind.Health < 10000, "Rail prioritizes one target and pierces enemies behind it");
                app.RestartTraining(); battle.TrainingTargets(); battle.TrainingChargeFusion();
                first = battle.Enemies[1]; Vector3 locked = first.Position;
                battle.Step(0.02f, Vector2.zero, locked);
                int generation = first.Generation;
                first.Spawn(new Vector3(12, 0.6f, -10), config); first.PrepareTrainingTarget(10000, 0);
                battle.Step(1.2f, Vector2.zero, null, true);
                check(first.Generation != generation && first.Health == 10000, "Delayed rail does not follow a recycled target instance");

                // An arena-spanning wall separates the cast from every victim.
                config.enableObstacles = true; config.obstacles = new[] { new Vector4(0, 0, 0.3f, 30) };
                for (int pair = 3; pair < 10; pair++)
                {
                    app.StartShowcase(pair);
                    battle.Player.transform.position = new Vector3(-5, 0.6f, 0);
                    foreach (var e in battle.Enemies) { e.transform.position = new Vector3(2, 0.6f, 0); e.PrepareTrainingTarget(10000, 0); }
                    battle.Step(0.02f, Vector2.zero, new Vector3(4, 0.6f, 0));
                    battle.Step(5, Vector2.zero, null, true);
                    check(battle.Enemies.All(e => e.Health == 10000), "Combination cannot damage across wall " + pair);
                    if (pair == 7) check(battle.FusionEnergy == 100 && battle.FusionCastCount == 0, "Blocked rail target preserves charge");
                }
                app.ReturnToBase();
                check(app.Progress.Snapshot.souls == wallet && app.Progress.Snapshot.proofs == proofs, "All ten presets preserve permanent rewards");
            }
            finally { app.ReturnToBase(); SetConfig(battle, original); UnityEngine.Object.Destroy(config); }
        }
        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
