using System;
using UnityEditor;
using UnityEngine;

namespace StarFall.Editor
{
    public static class StageThreeChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture)
        {
            var battle = app.Battle;
            var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = config.enableBossEncounters = true;
            config.miniBossTime = 1; config.duration = 3;
            config.enemySpeed = config.miniBossSpeed = config.finalBossSpeed = 0;
            config.initialSpawnInterval = config.finalSpawnInterval = 0.1f;
            config.bossAttackInterval = 0.5f; config.bossWarningTime = 1;
            config.finalBossHealth = 6;
            config.levelExperience = new[] { 100000 };
            SetConfig(battle, config);
            try
            {
                var nav = new ArenaNavigation(config);
                var start = new Vector3(-13, 0.6f, -3);
                var target = new Vector3(-3, 0.6f, -3);
                var stopped = nav.Move(start, Vector3.right * 10, config.playerRadius);
                check(stopped.x < -10 && nav.IsFree(stopped, config.playerRadius), "Fast movement cannot tunnel through stone barrier");
                check(nav.FirstWallHit(start, target, 0.23f, out float hit) && hit > 0 && hit < 1, "Projectile wall sweep truncates flight");
                foreach (float radius in new[] { config.enemyRadius, config.miniBossRadius, config.finalBossRadius })
                {
                    var position = start;
                    for (int i = 0; i < 1500 && Vector3.Distance(position, target) > 0.4f; i++)
                    {
                        position = nav.Move(position, nav.Direction(position, target, radius) * 0.05f, radius);
                        check(nav.IsFree(position, radius), "Navigation respects individual actor clearance");
                    }
                    check(Vector3.Distance(position, target) < 0.4f, "Normal and both boss sizes navigate around obstacles");
                }
                app.ReturnToMenu(); app.NewGame(); app.Deploy();
                battle.Player.transform.position = start;
                battle.Step(0.8f, Vector2.zero, target);
                check(battle.ProjectileCount <= 1, "Actual fireballs expire on obstacle");
                battle.Player.transform.position = new Vector3(0, 0.6f, 0);
                battle.Step(0.3f, Vector2.zero, null, true);
                check(battle.Boss != null && battle.Boss.Kind == EnemyKind.MiniBoss, "Mini boss arrives on schedule");
                var mini = battle.Boss;
                app.UI.Refresh(app.Session, 0); capture(app, "13-mini-boss");
                float health = app.Session.Health;
                battle.Step(0.5f, Vector2.zero, null, true);
                check(battle.WarningCount > 0 && app.Session.Health == health, "Boss attack warns before dealing damage");
                int warnings = battle.WarningCount;
                app.Session.Pause(); battle.Step(5, Vector2.one);
                check(battle.WarningCount == warnings && app.Session.Health == health, "Pause freezes warning timers");
                app.Session.Resume();
                app.Session.CollectSoul(100000);
                float choiceTime = app.Session.Elapsed;
                battle.Step(5, Vector2.one);
                check(app.Session.State == MatchState.ChoosingBuild && battle.WarningCount == warnings && app.Session.Elapsed == choiceTime, "Build selection freezes boss warning and encounter clock");
                app.Session.ChooseBuild(app.Session.Builds.Offers[0], app.Session.Builds.Revision);
                health = app.Session.Health;
                battle.Step(1, Vector2.zero, null, true);
                check(app.Session.Health < health, "Remaining inside telegraph causes damage");
                battle.Player.transform.position = new Vector3(15, 0.6f, 10);
                battle.Step(0.7f, Vector2.zero, null, true);
                check(battle.Boss == mini && battle.Boss.Kind == EnemyKind.MiniBoss, "Final boss waits for undefeated mini boss even after deadline");
                check(battle.EnemyCount <= battle.NormalEnemyLimit + 1, "Normal spawn cap permits separate boss");
                mini.ApplyBurn(1000);
                battle.Step(1.01f, Vector2.zero, null, true);
                check(battle.Boss != null && battle.Boss.Kind == EnemyKind.FinalBoss, "Burn kill advances to final boss once");
                var boss = battle.Boss;
                int count = battle.EnemyCount;
                battle.Step(0.6f, Vector2.zero, null, true);
                check(battle.EnemyCount == count, "Final encounter stops adding normal enemies");
                app.UI.Refresh(app.Session, 0); capture(app, "14-final-boss");
                config.bossAttackInterval = 100;
                battle.Step(0.5f, Vector2.zero, null, true);
                health = app.Session.Health;
                battle.Player.transform.position = new Vector3(-15, 0.6f, 0);
                battle.Step(2, Vector2.zero, null, true);
                check(app.Session.Health == health, "Moving away from locked warning positions avoids damage");
                battle.Player.transform.position = new Vector3(0, 0.6f, 0);
                boss.transform.position = new Vector3(0, 0.6f, 4);
                battle.Step(0.5f, Vector2.zero, Vector3.forward * 10);
                check(app.Session.State == MatchState.ClaimingReward && !app.Session.HasClearanceProof, "Fireball kills final boss but does not immediately win");
                check(battle.EnemyCount == 0 && battle.ProjectileCount == 0 && battle.WarningCount == 0 && battle.Souls.Count == 0, "Final death clears survivors and hazards");
                check(battle.ChestAvailable && !battle.TryOpenChest(), "Chest requires nearby interaction");
                float elapsed = app.Session.Elapsed;
                app.Session.Pause(); check(!battle.TryOpenChest(), "Paused chest cannot be claimed"); app.Session.Resume();
                check(app.Session.State == MatchState.ClaimingReward, "Pause restores reward phase");
                battle.Step(1, Vector2.up);
                check(app.Session.Elapsed == elapsed && battle.EnemyCount == 0 && battle.ProjectileCount == 0, "Reward phase allows movement without combat or timer");
                battle.Player.transform.position = battle.ChestPosition;
                app.UI.Refresh(app.Session, 0); capture(app, "15-clearance-chest");
                // Caller injects a real E key event, then verifies victory and automatic menu return.
            }
            finally { SetConfig(battle, original); UnityEngine.Object.Destroy(config); }
        }

        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

