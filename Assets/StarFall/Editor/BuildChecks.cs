using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarFall.Editor
{
    public static class BuildChecks
    {
        private static readonly int[] TestCosts = { 1, 10000 };

        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture)
        {
            CheckProgression(check);
            var battle = app.Battle;
            var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = config.enableBossEncounters = false;
            config.enemySpeed = 0;
            config.initialSpawnInterval = config.finalSpawnInterval = 100;
            config.maxEnemies = 1;
            SetConfig(battle, config);
            try
            {
                SetBuilds(app, BuildId.SwiftCasting, BuildId.SwiftCasting, BuildId.SwiftCasting);
                check(Mathf.Abs(battle.AttackFrequency - 3.2f) < 0.0001f, "Casting ranks add base frequency, not compound it");
                battle.Step(0.01f, Vector2.zero, Vector3.right * 15);
                int shots = battle.ShotsFired;
                battle.Step(5, Vector2.zero, Vector3.right * 15);
                check(battle.ShotsFired - shots == 16, "Rank three casting emits 16 fireballs in five seconds");

                SetBuilds(app, BuildId.FireAffinity, BuildId.FireAffinity, BuildId.FireAffinity);
                check(Mathf.Abs(battle.FireEfficiency - 1.6f) < 0.0001f, "Fire efficiency reaches 160 percent");
                battle.Step(0.36f, Vector2.zero, Vector3.forward * 10, true);
                var enemy = battle.Enemies[0];
                enemy.transform.position = new Vector3(0, 0.6f, 4);
                battle.Step(0.3f, Vector2.zero, Vector3.forward * 10);
                check(Mathf.Abs(enemy.Health - 11.2f) < 0.001f, "Fire affinity changes real projectile damage to 12.8");

                StartBuildSequence(app, BuildId.Vitality, BuildId.Vitality, BuildId.Vitality);
                Grant(app.Session, BuildId.Vitality); Grant(app.Session, BuildId.Vitality);
                app.Session.Damage(35);
                Grant(app.Session, BuildId.Vitality);
                check(app.Session.MaxHealth == 160 && app.Session.Health == 125, "Vitality grants exactly 20 max health and 20 healing each rank");

                SetBuilds(app, BuildId.LightSteps, BuildId.LightSteps, BuildId.LightSteps);
                battle.Step(0.1f, Vector2.right, Vector3.forward * 10, true);
                check(Mathf.Abs(battle.Player.Position.x - 0.744f) < 0.001f, "Light Steps adds base movement speed per rank");

                SetBuilds(app, BuildId.SoulAttraction, BuildId.SoulAttraction);
                check(Mathf.Abs(battle.ActiveSoulRadius - 9.75f) < 0.001f && battle.CollectionRadius == 3, "Attraction increases active radius only");
                check(Mathf.Abs(battle.SoulTravelSpeed - 12.6f) < 0.001f, "Attraction increases soul travel speed");

                SetBuilds(app, BuildId.SoulFire);
                app.Session.CollectSoul(4, false);
                check(app.Session.SoulFireStacks == 0, "Passive collection grants no Soul Fire");
                app.Session.CollectSoul(7, true);
                check(app.Session.SoulFireStacks == 5, "Soul Fire counts experience and caps at five");
                battle.Step(0.1f, Vector2.zero, Vector3.forward * 10, true);
                check(app.Session.SoulFireStacks == 5 && battle.ShotsFired == 0, "Absorption never consumes Soul Fire");
                battle.Step(0.01f, Vector2.zero, Vector3.forward * 10);
                check(app.Session.SoulFireStacks == 4 && Math.Abs(battle.Projectiles[0].Multiplier - 1.04f) < 0.001f, "One cast consumes one charge for 30 percent more direct damage");
                battle.Step(3, Vector2.zero, Vector3.forward * 10);
                check(app.Session.SoulFireStacks == 0 && battle.Projectiles[battle.ProjectileCount - 1].Multiplier == 0.8f, "Direct damage returns to normal when charges run out");

                SetBuilds(app, BuildId.SoulRecovery);
                app.Session.Damage(10);
                app.Session.CollectSoul(9);
                check(app.Session.Health == 90 && app.Session.RecoveryProgress == 9, "Recovery ignores experience acquired before the card");
                app.Session.CollectSoul(1, true);
                check(app.Session.Health == 92 && app.Session.RecoveryProgress == 0, "Recovery heals two per ten experience");
                app.Session.CollectSoul(30);
                check(app.Session.Health == 98, "Large pickups account for all recovery thresholds");
                app.Session.CollectSoul(20);
                check(app.Session.Health == 100 && app.Session.RecoveryProgress == 0, "Full-health healing is consumed rather than banked");
                app.Session.Damage(10); app.Session.CollectSoul(1);
                check(app.Session.Health == 90, "Spent healing cannot trigger retroactively");

                CheckBurn(app, config, check);
                SetBuilds(app, BuildId.FireAffinity, BuildId.Embers, BuildId.SoulFire, BuildId.SoulAttraction, BuildId.SoulRecovery);
                app.Session.CollectSoul(5, true);
                app.Session.CollectSoul(app.Session.Builds.ExperienceRequired);
                app.UI.Refresh(app.Session, 0);
                check(app.UI.BuildSelectionVisible && app.Session.Builds.Offers.Count == 3, "Live draft UI displays three choices");
                capture(app, "09-build-choices");
                DrainChoices(app.Session);
                battle.Step(0.36f, Vector2.zero, Vector3.forward * 10, true);
                battle.Enemies[0].transform.position = new Vector3(0, 0.6f, 4);
                battle.Step(0.35f, Vector2.zero, Vector3.forward * 10);
                app.UI.Refresh(app.Session, 0);
                capture(app, "10-build-combat");
                app.Session.Pause(); app.UI.Refresh(app.Session, 0);
                capture(app, "11-build-summary");
                app.ReturnToMenu(); app.NewGame(); app.Deploy();
                check(app.Session.Builds.TotalRanks == 0 && app.Session.Builds.Level == 1 && app.Session.SoulFireStacks == 0
                    && app.Session.RecoveryProgress == 0 && app.Session.MaxHealth == 100, "New game clears every build and runtime effect");
                battle.Step(0.4f, Vector2.zero, Vector3.back * 10, true);
                check(!battle.Enemies[0].IsBurning, "Pooled enemy has no lingering burn");
                Debug.Log("STARFALL_BUILD_RULES_OK: eight cards, drafts, ranks, overflow, damage, burn, charges, healing and reset.");
            }
            finally
            {
                app.ReturnToMenu(); SetConfig(battle, original); UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void CheckProgression(Action<bool, string> check)
        {
            var session = new MatchSession();
            session.Start(180, 100, null, 7);
            session.CollectSoul(2);
            check(session.Builds.Level == 1 && session.State == MatchState.Playing, "Below threshold does not level");
            session.CollectSoul(1);
            check(session.Builds.Level == 2 && session.Builds.Experience == 0 && session.State == MatchState.ChoosingBuild, "Third soul opens first level choice");
            var unique = new HashSet<BuildId>(session.Builds.Offers);
            check(unique.Count == 3, "Offers contain distinct cards");
            float time = session.Elapsed;
            session.Advance(3); session.Damage(50); session.Pause(); session.Resume();
            check(session.State == MatchState.ChoosingBuild && session.Elapsed == time && session.Health == 100, "Draft blocks time, damage and pause/resume bypass");
            int revision = session.Builds.Revision;
            var first = session.Builds.Offers[0];
            check(!session.ChooseBuild(first, revision - 1), "Invalid ticket rejected");
            check(session.ChooseBuild(first, revision) && session.State == MatchState.Playing, "Valid choice resumes play");
            check(!session.ChooseBuild(first, revision), "Double selection rejected");
            session.CollectSoul(10);
            check(session.Builds.PendingChoices == 2 && session.Builds.Level == 4 && session.Builds.Experience == 1, "Large pickup preserves excess experience and queues upgrades");
            revision = session.Builds.Revision;
            first = session.Builds.Offers[0];
            session.ChooseBuild(first, revision);
            check(session.State == MatchState.ChoosingBuild && !session.ChooseBuild(first, revision), "Queued choices stay paused and reject previous ticket");
            DrainChoices(session);
            check(session.State == MatchState.Playing, "Final pending choice resumes play");

            var full = new MatchSession();
            full.Start(180, 100, null, 42); full.CollectSoul(100000);
            int selected = 0;
            while (full.State == MatchState.ChoosingBuild)
            {
                unique = new HashSet<BuildId>(full.Builds.Offers);
                check(unique.Count == full.Builds.Offers.Count && unique.Count > 0, "Late draft never duplicates or empties");
                foreach (var id in unique) check(full.Builds.Rank(id) < BuildCatalog.Get(id).MaxRank, "Maxed cards removed from pool");
                first = full.Builds.Offers[0];
                check(full.ChooseBuild(first, full.Builds.Revision), "All pending ranks selectable");
                if (++selected > 30) throw new Exception("Draft loop did not terminate");
            }
            check(full.Builds.IsMaxed && selected == full.Builds.MaximumRanks && full.Builds.Level == selected + 1, "Pool exhaustion finishes the active elemental pool without soft lock");
            full.CollectSoul(100);
            check(full.State == MatchState.Playing, "Experience after maximum does not reopen empty draft");
        }

        private static void CheckBurn(GameApplication app, BattleConfig config, Action<bool, string> check)
        {
            var probe = new GameObject("BurnProbe").AddComponent<EnemyController>();
            try
            {
                probe.Spawn(Vector3.zero, config);
                probe.ApplyBurn(2); probe.StepBurn(1);
                check(Mathf.Abs(probe.Health - 22) < 0.001f, "Burn ticks once per second");
                probe.StepBurn(0.5f); probe.ApplyBurn(2); probe.StepBurn(0.5f);
                check(Mathf.Abs(probe.Health - 20) < 0.001f, "Refresh preserves next tick rather than stacking or delaying it");
                probe.StepBurn(2.5f);
                check(Mathf.Abs(probe.Health - 16) < 0.001f && !probe.IsBurning, "Refreshed burn expires after three seconds");
                probe.StepBurn(10); check(probe.Health == 16, "Expired burn deals no more damage");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe.gameObject); }

            config.enemyHealth = 9;
            SetBuilds(app, BuildId.Embers);
            var battle = app.Battle;
            battle.Step(0.36f, Vector2.zero, Vector3.forward * 10, true);
            var enemy = battle.Enemies[0]; enemy.transform.position = new Vector3(0, 0.6f, 4);
            battle.Step(0.3f, Vector2.zero, Vector3.forward * 10);
            check(enemy.Health == 1 && enemy.IsBurning, "Fireball attaches burn to surviving enemy");
            app.Session.Pause(); float remaining = enemy.BurnRemaining;
            battle.Step(3, Vector2.zero);
            check(enemy.Health == 1 && enemy.BurnRemaining == remaining, "Pause freezes burn duration and ticks");
            app.Resume(); battle.Step(1.1f, Vector2.zero, Vector3.back * 10, true);
            check(app.Session.Kills == 1 && battle.EnemyCount == 0 && battle.Souls.Count == 1, "Burn kill awards exactly one kill and one soul");
            battle.Step(1, Vector2.zero, Vector3.back * 10, true);
            check(app.Session.Kills == 1, "Burn death cannot award twice");
            config.enemyHealth = 24;

            SetBuilds(app, BuildId.FireAffinity, BuildId.Embers);
            battle.Step(0.36f, Vector2.zero, Vector3.forward * 10, true);
            enemy = battle.Enemies[0]; enemy.transform.position = new Vector3(0, 0.6f, 4);
            battle.Step(0.3f, Vector2.zero, Vector3.forward * 10);
            battle.Step(1, Vector2.zero, Vector3.back * 10, true);
            check(Mathf.Abs(enemy.Health - 10.8f) < 0.001f, "Affinity increases both direct fire and burn damage");
        }

        private static int FindSeed(BuildId[] sequence)
        {
            for (int seed = 0; seed < 20000; seed++)
            {
                var builds = new BuildProgression(); builds.Reset(TestCosts, seed);
                bool matches = true;
                foreach (var id in sequence)
                {
                    builds.AddExperience(builds.ExperienceRequired);
                    if (!builds.Choose(id, builds.Revision)) { matches = false; break; }
                }
                if (matches) return seed;
            }
            throw new Exception("No deterministic test draft found");
        }

        public static void StartBuildSequence(GameApplication app, params BuildId[] sequence)
        {
            app.ReturnToMenu(); app.NewGame(); app.Deploy();
            app.Session.Start(180, 100, TestCosts, FindSeed(sequence));
        }
        private static void SetBuilds(GameApplication app, params BuildId[] sequence)
        {
            StartBuildSequence(app, sequence);
            foreach (var id in sequence) Grant(app.Session, id);
        }
        public static void Grant(MatchSession session, BuildId id)
        {
            session.CollectSoul(session.Builds.ExperienceRequired);
            if (!session.ChooseBuild(id, session.Builds.Revision)) throw new Exception("Test grant failed: " + id);
        }
        public static void DrainChoices(MatchSession session)
        {
            while (session.State == MatchState.ChoosingBuild)
                if (!session.ChooseBuild(session.Builds.Offers[0], session.Builds.Revision)) throw new Exception("Cannot resolve queued draft");
        }
        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}



