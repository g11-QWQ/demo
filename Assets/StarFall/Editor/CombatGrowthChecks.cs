using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarFall.Editor
{
    public static class CombatGrowthChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture)
        {
            var battle = app.Battle; var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = config.enableBossEncounters = false;
            config.enemySpeed = 0;
            SetConfig(battle, config);
            long wallet = app.Progress.Snapshot.souls;
            try
            {
                for (int gem = 0; gem < 5; gem++)
                {
                    for (int form = 1; form <= 3; form++)
                    {
                        app.ReturnToBase(); app.TestResonance = DamageElement.Physical; app.StartTraining(); app.RestartTraining(gem);
                        float baseRate = battle.AttackFrequency;
                        check(app.Session.GrantTrainingBuild((BuildId)((int)BuildId.FormPierce + form - 1)), "Form can be selected once");
                        check(!app.Session.GrantTrainingBuild((BuildId)((int)BuildId.FormPierce + form % 3)), "Conflicting form rejected");
                        battle.Step(0.01f, Vector2.zero, Vector3.forward * 10);
                        if (form == 1) check(battle.Projectiles.All(p => p.PiercesLeft >= 2), "Piercing form changes actual projectiles for gem " + gem);
                        if (form == 2) check(battle.ProjectileCount >= 3 && battle.Projectiles.All(p => p.Multiplier < 1), "Spread trades individual power for multiple projectiles " + gem);
                        if (form == 3) check(Mathf.Abs(battle.AttackFrequency - baseRate * 0.7f) < 0.001f && battle.Projectiles.All(p => p.Heavy), "Heavy form trades cadence for stronger attacks " + gem);
                    }
                }
                for (int pair = 0; pair < 10; pair++)
                {
                    for (int evolution = 1; evolution <= 3; evolution++)
                    {
                        app.StartShowcase(pair);
                        foreach (var enemy in battle.Enemies) enemy.PrepareTrainingTarget(10000, 0);
                        var aim = battle.Enemies[1].Position;
                        app.Session.GrantTrainingBuild((BuildId)((int)BuildId.EvolveSpread + evolution - 1));
                        battle.Step(5.5f, Vector2.zero, aim);
                        check(battle.FusionCastCount == 0 && battle.FusionEnergy == 100, "Full energy never auto-releases ultimate " + pair + "/" + evolution);
                        check(battle.AttackLinkTriggers > 0 && battle.Enemies.Any(e => e.Health < 10000), "Normal linkage and evolution work without ultimate " + pair + "/" + evolution);
                        if (pair < 3 && evolution == 1) { app.UI.Refresh(app.Session, 0); capture(app, "50-normal-link-" + pair); }
                        int shots = battle.ShotsFired;
                        battle.Step(0.02f, Vector2.zero, aim, ultimate: true);
                        check(battle.FusionCastCount == 1 && battle.ActiveFusionCount > 0, "Q-style input releases ultimate independently " + pair);
                        battle.Step(0.7f, Vector2.zero, aim);
                        check(battle.ShotsFired > shots && battle.AttackLinkTriggers > 0, "Normal attacks and ultimate coexist " + pair);
                        if (pair < 3 && evolution == 1) { app.UI.Refresh(app.Session, 0); capture(app, "51-normal-and-ultimate-" + pair); }
                        app.Session.Pause(); int active = battle.ActiveFusionCount; float elapsed = app.Session.Elapsed;
                        battle.Step(10, Vector2.one, aim, ultimate: true);
                        check(app.Session.Elapsed == elapsed && battle.ActiveFusionCount == active, "Pause blocks all simulations and Q");
                        app.Resume(); app.RestartTraining();
                        check(battle.ActiveAttackLinkCount == 0 && battle.ActiveFusionCount == 0 && battle.FusionEnergy == 0, "Restart cleans ordinary and ultimate effects");
                    }
                }
                app.ReturnToBase(); app.TestResonance = DamageElement.Physical; app.StartGrowthTraining();
                check(battle.LongTraining && app.Session.Duration == 480 && app.Session.Builds.Level == 1 && !battle.UltimateUnlocked, "Full growth trial starts from level one and eight-minute boss clock");
                for (int level = 2; level <= 15; level++)
                {
                    app.TrainingUpgrade();
                    check(app.Session.State == MatchState.ChoosingBuild && app.Session.Builds.ChoiceLevel == level, "Progression presents next level in order " + level);
                    float time = app.Session.Elapsed;
                    battle.Step(30, Vector2.one, Vector3.forward * 8, ultimate: true);
                    check(app.Session.Elapsed == time && app.Session.State == MatchState.ChoosingBuild, "Selection has unlimited time and automatically freezes combat");
                    if (level % 3 == 0) { app.UI.Refresh(app.Session, 0); capture(app, "52-breakthrough-" + level); }
                    var offers = app.Session.Builds.Offers;
                    var pick = level == 6 && offers.Contains(BuildId.ResLightning) ? BuildId.ResLightning : level == 12 ? BuildId.UltimateCycle : offers[0];
                    app.Session.ChooseBuild(pick, app.Session.Builds.Revision);
                    battle.Step(0.01f, Vector2.zero, null, true);
                    if (level < 12) check(!battle.UltimateUnlocked, "Ordinary linkage does not unlock ultimate early");
                    if (level == 12) check(battle.UltimateUnlocked && battle.FusionEnergy == 100, "Level twelve unlock grants exactly one full charge");
                }
                check(app.Session.Builds.Level == 15 && app.Session.Builds.TotalRanks == 14, "Fifteen levels finish fourteen meaningful selections");
                battle.TrainingTargets(); battle.TrainingChargeFusion();
                var targetPoint = battle.Enemies[1].Position;
                battle.Step(0.1f, Vector2.zero, targetPoint, true, true);
                check(battle.FusionCastCount == 0 && battle.FusionEnergy == 100, "Absorption blocks Q without consuming energy");
                battle.Step(0.02f, Vector2.zero, targetPoint, ultimate: true);
                check(battle.FusionCastCount == 1 && battle.FusionEnergy < 100, "Release after absorption succeeds");
                battle.Step(0.1f, Vector2.zero, targetPoint, true);
                check(battle.FusionEnergy < 100, "Unlock gift does not refill each simulation tick");

                // Verify the actual arrival times without requiring eight minutes of editor wall time.
                app.ReturnToBase(); config.enableBossEncounters = true; config.enableEnemyRoles = true;
                config.enemySpeed = config.miniBossSpeed = config.finalBossSpeed = 0;
                app.StartGrowthTraining();
                app.Session.Advance(239.9f); battle.Step(0.01f, Vector2.zero, null, true);
                check(battle.Boss == null, "Mini boss does not arrive before four minutes");
                app.Session.Advance(0.2f); battle.Step(0.01f, Vector2.zero, null, true);
                check(battle.Boss != null && battle.Boss.Kind == EnemyKind.MiniBoss, "Mini boss arrives at four minutes");
                battle.Boss.ApplyBurn(100000); battle.Step(1.1f, Vector2.zero, null, true);
                app.Session.Advance(480 - app.Session.Elapsed); battle.Step(0.01f, Vector2.zero, null, true);
                check(battle.Boss != null && battle.Boss.Kind == EnemyKind.FinalBoss, "Final boss arrives at eight minutes after mini boss defeat");
                battle.Boss.ApplyBurn(100000); battle.Step(1.1f, Vector2.zero, null, true);
                check(app.Session.State == MatchState.ClaimingReward && battle.ActiveAttackLinkCount == 0, "Final kill clears all effects before chest phase");
                battle.Player.transform.position = battle.ChestPosition;
                check(battle.TryOpenChest() && app.Session.State == MatchState.Won && app.Session.HasClearanceProof, "Opening chest grants proof and settles the new run");
                app.ReturnToBase();
                check(app.Progress.Snapshot.souls == wallet, "Full growth trial does not change persistent currency");
            }
            finally { app.ReturnToBase(); SetConfig(battle, original); UnityEngine.Object.Destroy(config); }
        }
        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
