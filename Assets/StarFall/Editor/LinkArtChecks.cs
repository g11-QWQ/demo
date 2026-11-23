using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace StarFall.Editor
{
    public static class LinkArtChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture)
        {
            var battle = app.Battle; var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original); config.enableObstacles = config.enableBossEncounters = false;
            var serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                for (int pair = 0; pair < 10; pair++)
                {
                    app.StartShowcase(pair);
                    foreach (var enemy in battle.Enemies) enemy.PrepareTrainingTarget(100000, 0);
                    if (battle.CurrentFusion == FusionKind.SandDrill)
                        for (int n = 0; n < battle.Enemies.Count; n++) battle.Enemies[n].transform.position = battle.Player.Position + new Vector3((n % 4) * 1.5f, 0, 1.5f + n / 4);
                    var kind = battle.CurrentFusion;
                    var aim = battle.Enemies[1].Position;
                    // Real movement and hits, not an injected trigger counter.
                    for (int frame = 0; frame < 1800 && battle.LinkArtCasts == 0; frame++)
                    {
                        var move = LinkArtCatalog.UsesMovement(kind) ? (frame % 120 < 60 ? Vector2.right : Vector2.left) : Vector2.zero;
                        battle.Step(1f / 60, move, aim);
                    }
                    check(battle.LinkArtCasts == 1 && battle.ActiveLinkArts == 1, "Automatic signature triggers for pair " + pair);
                    float health = battle.Enemies.Sum(e => e.Health);
                    battle.Step(0.85f, Vector2.zero, aim, true);
                    check(battle.Enemies.Sum(e => e.Health) < health, "Signature deals real damage " + pair);
                    app.UI.Refresh(app.Session, 0); capture(app, "60-link-art-" + pair);
                    health = battle.Enemies.Sum(e => e.Health);
                    float progress = battle.LinkArtProgress;
                    app.Session.Pause(); battle.Step(30, Vector2.right, aim);
                    check(battle.Enemies.Sum(e => e.Health) == health && battle.LinkArtProgress == progress && battle.ActiveLinkArts == 1, "Pause freezes auto skill and trigger progress " + pair);
                    app.Resume();
                    battle.Step(0.02f, Vector2.zero, aim, ultimate: true);
                    check(battle.ActiveFusionCount > 0 && battle.ActiveLinkArts > 0, "Automatic skill coexists with ultimate " + pair);
                    battle.TrainingTargets();
                    check(battle.ActiveLinkArts == 0 && battle.LinkArtCasts == 0 && battle.LinkArtProgress == 0, "Reset clears automatic skill and counters " + pair);
                    if (LinkArtCatalog.UsesMovement(kind))
                    {
                        battle.Step(8, Vector2.zero, aim, true);
                        check(battle.LinkArtProgress == 0, "Standing still never charges movement skill");
                    }
                }
                app.ReturnToBase(); app.StartGrowthTraining();
                float initialRate = battle.AttackFrequency;
                for (int level = 2; level <= 15; level++)
                {
                    app.TrainingUpgrade(); var builds = app.Session.Builds;
                    app.Session.ChooseBuild(builds.Offers.First(), builds.Revision);
                    if (level == 6) check(battle.GrowthPower >= 1.75f, "Level six grants reliable damage growth regardless of cards");
                }
                check(battle.GrowthPower >= 2.7f, "Level fifteen has strong baseline damage growth");
                check(battle.UltimateChargeSeconds <= 12, "All ultimate styles recharge within twelve effective-hit seconds");
                app.ReturnToBase();
                // A real kill/collection pilot checks the XP curve. Invulnerability isolates
                // progression from steering quality; this is not a human difficulty rating.
                config.enableBossEncounters = config.enableEnemyRoles = true;
                UnityEngine.Random.InitState(481);
                app.SelectGem(0);
                app.StartGrowthTraining();
                app.Session.Builds.Reset(config.levelExperience, 481, DamageElement.Fire);
                var report = new System.Text.StringBuilder("Invulnerable fire pilot; actual kills and soul collection, no XP grants.\n");
                int nextReport = 60, peak = 0;
                for (int frame = 0; frame < 6000 && app.Session.State != MatchState.ClaimingReward; frame++)
                {
                    if (app.Session.State == MatchState.ChoosingBuild)
                    {
                        var b = app.Session.Builds;
                        var priorities = new[] { BuildId.ResLightning, BuildId.FormPierce, BuildId.EvolveSpread, BuildId.UltimateCycle, BuildId.MasterAttack, BuildId.FireAffinity, BuildId.SwiftCasting, BuildId.FlameBurst };
                        var pick = b.Offers[0]; foreach (var id in priorities) if (b.Offers.Contains(id)) { pick = id; break; }
                        app.Session.ChooseBuild(pick, b.Revision); continue;
                    }
                    var position = battle.Player.Position;
                    var target = battle.Enemies.OrderBy(e => (e.Position - position).sqrMagnitude).FirstOrDefault();
                    var soul = battle.Souls.OrderBy(s => (s.Position - position).sqrMagnitude).FirstOrDefault();
                    Vector3 travel = soul != null ? soul.Position - position : target != null ? target.Position - position : Vector3.zero;
                    if (soul == null && target != null && travel.magnitude < 4) travel = -travel;
                    bool absorb = soul != null && travel.magnitude < battle.ActiveSoulRadius && frame % 40 < 6;
                    battle.Step(0.1f, new Vector2(travel.x, travel.z).normalized, target != null ? target.Position : position + Vector3.forward * 8, absorb, !absorb);
                    peak = Mathf.Max(peak, battle.EnemyCount);
                    check(battle.EnemyCount <= config.maxEnemies, "Dense waves respect total cap including boss");
                    if (app.Session.Elapsed >= nextReport)
                    {
                        report.AppendLine($"{nextReport}s: level={app.Session.Builds.Level}, kills={app.Session.Kills}, souls={app.Session.SoulExperience}, peakEnemies={peak}");
                        nextReport += 60;
                    }
                }
                report.AppendLine($"End: {app.Session.Elapsed:0.0}s, {app.Session.State}, level={app.Session.Builds.Level}");
                System.IO.File.WriteAllText("Logs/Checks/power-curve-pilot.txt", report.ToString());
                check(app.Session.Builds.Level >= 12, "Natural kill-and-collect pilot reaches ultimate without injected experience");
                app.ReturnToBase();
            }
            finally
            {
                app.ReturnToBase(); serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = original; serialized.ApplyModifiedPropertiesWithoutUndo(); UnityEngine.Object.Destroy(config);
            }
        }
    }
}
