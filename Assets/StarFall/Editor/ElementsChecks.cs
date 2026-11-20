using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StarFall.Editor
{
    public static class ElementsChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture, Action<Button> click)
        {
            CheckDrafts(check);
            CheckLegacySave(check);
            var battle = app.Battle; var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = config.enableBossEncounters = false;
            config.enemyHealth = 100; config.enemySpeed = config.finalBossSpeed = 0;
            config.initialSpawnInterval = config.finalSpawnInterval = 0.1f; config.maxEnemies = 4;
            SetConfig(battle, config);
            try
            {
                app.OpenSettings(); app.CloseSettings();
                app.UI.OpenWorkshop(); click(app.UI.GemButton(0));
                capture(app, "21-gem-workshop");
                long wallet = app.Progress.Snapshot.souls; int proofs = app.Progress.Snapshot.proofs;
                string receipt = app.Progress.Snapshot.lastRunId;
                app.StartTraining();
                check(app.TestMode && app.Session.Invulnerable && app.Session.Duration == 60 && app.Session.MaxHealth == 100, "Training uses base stats, sixty-second clock and default invulnerability");
                app.Session.Damage(10000); check(app.Session.Health == 100, "Training invulnerability suppresses damage");
                for (int index = 0; index < 5; index++)
                {
                    app.TestResonance = DamageElement.Physical; app.RestartTraining(index);
                    var victims = Prepare(app);
                    battle.Step(0.4f, Vector2.zero, Vector3.forward * 12);
                    check(battle.ActiveGem.element == (DamageElement)(index + 1), "Training switches element " + index);
                    if (index == 0) check(victims[0].Health == 92 && victims[0].IsBurning, "Base fireball deals eight and burns without a card");
                    if (index == 1) check(victims[0].Health == 90 && victims[1].Health == 90 && victims[2].Health == 100, "Lightning explosion hits each nearby enemy exactly once");
                    if (index == 2) check(battle.ShotsFired == 1 && victims.Sum(e => 100 - e.Health) >= 10, "Wind casts a spread of independent blades");
                    if (index == 3) check(victims[0].Health == 93 && victims[0].SlowRemaining > 0 && Math.Abs(victims[0].SlowStrength - 0.25f) < 0.001f, "Ocean applies direct damage and timed slow");
                    if (index == 4) check(victims[0].Health == 76 && victims[1].Health == 100, "Earth concentrates twenty-four damage on one target");
                    app.UI.Refresh(app.Session, 0); capture(app, "22-element-" + index);
                }
                app.TestResonance = DamageElement.Lightning; app.RestartTraining(0);
                var targets = Prepare(app); battle.TrainingChargeFusion(); battle.Step(0.9f, Vector2.zero, Vector3.forward * 12);
                check(battle.FusionCastCount == 1 && targets[1].Health < 100, "Charged fire-lightning releases a marked lightning strike");
                app.UI.Refresh(app.Session, 0); capture(app, "23-fire-lightning");
                app.TestResonance = DamageElement.Wind; app.RestartTraining(0);
                targets = Prepare(app); battle.TrainingChargeFusion(); battle.Step(1.6f, Vector2.zero, Vector3.forward * 12);
                check(targets[1].IsBurning && battle.FusionCastCount == 1, "Fire-wind tornado burns the nearby group");
                app.TestResonance = DamageElement.Fire; app.RestartTraining(1);
                targets = Prepare(app); battle.TrainingChargeFusion(); battle.Step(0.9f, Vector2.zero, Vector3.forward * 12);
                check(targets[0].IsBurning && targets[1].IsBurning, "Lightning-fire judgment leaves fire on surviving victims");
                app.TestResonance = DamageElement.Fire; app.RestartTraining(2);
                targets = Prepare(app); battle.TrainingChargeFusion(); battle.Step(1.3f, Vector2.zero, Vector3.forward * 12);
                check(targets[0].IsBurning && battle.ResonanceTriggers > 0, "Wind-fire tornado burns victims");
                app.TestResonance = DamageElement.Earth; app.RestartTraining(3);
                targets = Prepare(app); battle.Step(1.7f, Vector2.zero, Vector3.forward * 12);
                check(battle.MudCount > 0 && targets[1].SlowRemaining > 0, "Ocean-earth creates a slowing ground zone");
                float remaining = targets[1].SlowRemaining;
                app.Session.Pause(); battle.Step(5, Vector2.zero);
                check(targets[1].SlowRemaining == remaining && battle.MudCount > 0, "Pause freezes elemental control and zones");
                app.Resume(); battle.Step(0.6f, Vector2.zero, Vector3.forward * 12, true);
                check(targets[1].RootRemaining > 0, "Lingering inside swamp briefly roots a normal enemy");
                app.UI.Refresh(app.Session, 0); capture(app, "24-ocean-earth");
                app.TestResonance = DamageElement.Ocean; app.RestartTraining(4);
                targets = Prepare(app); battle.Step(3, Vector2.zero, Vector3.forward * 12);
                check(battle.MudCount > 0 && targets[1].SlowRemaining > 0, "Earth-ocean also creates swamp through heavy shots");

                app.TestResonance = DamageElement.Physical; app.RestartTraining(2);
                app.Session.Builds.GrantForTraining(BuildId.WindFan);
                app.Session.Builds.GrantForTraining(BuildId.WindPierce);
                app.Session.Builds.GrantForTraining(BuildId.SoulFire);
                app.Session.CollectSoul(1, true);
                battle.Step(0.01f, Vector2.zero, Vector3.forward * 12);
                check(battle.ProjectileCount == 4 && app.Session.SoulFireStacks == 0 && battle.Projectiles.All(p => p.Multiplier > 0.7f), "A wind volley consumes one soul charge shared by all blades");
                check(battle.Projectiles.All(p => p.PiercesLeft == 1), "Wind pierce upgrade snapshots into each blade");
                app.RestartTraining(2); app.Session.Builds.GrantForTraining(BuildId.WindPierce);
                targets = Prepare(app);
                for (int i = 0; i < targets.Length; i++) targets[i].transform.position = new Vector3(0, 0.6f, 4 + i * 3);
                battle.Step(0.6f, Vector2.zero, Vector3.forward * 18);
                check(targets[0].Health < 100 && targets[1].Health < 100 && targets[2].Health == 100, "Wind pierces one additional target and stops before the third");
                app.RestartTraining(1); app.Session.Builds.GrantForTraining(BuildId.ThunderArea); app.Session.Builds.GrantForTraining(BuildId.ThunderChain);
                targets = Prepare(app); targets[1].transform.position = new Vector3(2, 0.6f, 5); targets[2].transform.position = new Vector3(3.5f, 0.6f, 5);
                battle.Step(0.4f, Vector2.zero, Vector3.forward * 12);
                check(targets[1].Health == 90 && targets[2].Health == 95 && targets[3].Health == 100, "Lightning radius and chain upgrades add distinct non-duplicate hits");
                app.UI.Refresh(app.Session, 0); capture(app, "25-lightning-chain");
                app.RestartTraining(3); app.Session.Builds.GrantForTraining(BuildId.OceanChill); app.Session.Builds.GrantForTraining(BuildId.OceanPressure);
                targets = Prepare(app); battle.Step(1, Vector2.zero, Vector3.forward * 12);
                check(Math.Abs(targets[0].Health - 84.95f) < 0.01f && Math.Abs(targets[0].SlowStrength - 0.35f) < 0.001f, "Ocean upgrades strengthen slow and damage against controlled enemies");
                app.RestartTraining(0); app.Session.Builds.GrantForTraining(BuildId.FlameBurst); app.Session.Builds.GrantForTraining(BuildId.Embers);
                targets = Prepare(app); battle.Step(0.9f, Vector2.zero, Vector3.forward * 12); battle.Step(0.5f, Vector2.zero, null, true);
                check(Math.Abs(targets[0].Health - 79.8f) < 0.01f, "Fire upgrades increase direct damage against burning targets and burn ticks");
                app.RestartTraining(4); app.Session.Builds.GrantForTraining(BuildId.EarthCore); app.Session.Builds.GrantForTraining(BuildId.EarthMark);
                targets = Prepare(app); battle.Step(1.7f, Vector2.zero, Vector3.forward * 12);
                check(Math.Abs(targets[0].Health - 42.6f) < 0.01f && targets[0].EarthMarks == 2, "Earth core and marks strengthen subsequent single-target hits");
                var bossProbe = new GameObject("BossControlProbe").AddComponent<EnemyController>();
                bossProbe.Spawn(Vector3.zero, config, EnemyKind.FinalBoss); bossProbe.ApplySlow(0.5f, 2); bossProbe.ApplyRoot(1);
                check(bossProbe.SlowStrength == 0.25f && bossProbe.RootRemaining == 0.25f, "Boss control resistance reduces slow and root duration");
                bossProbe.Step(0.3f, Vector3.zero, config); bossProbe.ApplyRoot(1);
                check(bossProbe.RootRemaining == 0, "Root immunity blocks immediate reapplication");
                bossProbe.ApplySlow(0.8f, 1); bossProbe.Step(0.5f, Vector3.zero, config); bossProbe.ApplySlow(0.2f, 5);
                check(bossProbe.SlowRemaining <= 0.5f, "Weaker slow cannot extend the duration of a stronger slow");
                UnityEngine.Object.DestroyImmediate(bossProbe.gameObject);

                config.enableObstacles = true; config.obstacles = new[] { new Vector4(0, 5, 0.2f, 4) };
                app.TestResonance = DamageElement.Physical; app.RestartTraining(1);
                targets = Prepare(app);
                battle.Player.transform.position = new Vector3(-4, 0.6f, 5);
                targets[0].transform.position = new Vector3(-0.8f, 0.6f, 5);
                targets[1].transform.position = new Vector3(0.8f, 0.6f, 5);
                targets[2].transform.position = new Vector3(4, 0.6f, -5);
                targets[3].transform.position = new Vector3(-4, 0.6f, -5);
                battle.Step(0.3f, Vector2.zero, new Vector3(10, 0.6f, 5));
                check(targets[0].Health == 90 && targets[1].Health == 100, $"Lightning area cannot damage an enemy across a thin wall ({targets[0].Health}/{targets[1].Health})");
                config.enableObstacles = false;

                app.RestartTraining(0); config.finalBossHealth = 3; battle.TrainingBoss();
                check(battle.Boss != null && battle.Boss.Kind == EnemyKind.FinalBoss && app.Session.Elapsed >= 60, "Training jumps directly to final boss");
                battle.Boss.transform.position = new Vector3(0, 0.6f, 4);
                battle.Step(0.4f, Vector2.zero, Vector3.forward * 12);
                check(app.Session.State == MatchState.ClaimingReward && battle.MudCount == 0, "Training boss death still uses chest flow and clears effects");
                battle.Player.transform.position = battle.ChestPosition; battle.TryOpenChest();
                check(app.Session.State == MatchState.Won && app.Progress.Snapshot.souls == wallet && app.Progress.Snapshot.proofs == proofs, "Training victory grants no permanent rewards");
                app.RestartTraining(); app.Session.CollectSoul(100); BuildChecks.DrainChoices(app.Session);
                app.ReturnToBase();
                check(app.Progress.Snapshot.souls == wallet && app.Progress.Snapshot.lastRunId == receipt && app.Progress.Snapshot.activeRunId == "", "Training pickups, restarts and exits leave persistent ledger unchanged");
                check(app.Progress.Snapshot.gemIndex == 0, "Runtime test switching does not overwrite saved equipment");
                app.SelectGem(3); app.Deploy();
                check(!app.TestMode && !app.Session.Invulnerable && battle.ActiveGem.element == DamageElement.Ocean && app.Session.Duration == config.duration, "Normal deployment uses saved gem without training conveniences");
                app.ReturnToBase(); app.SelectGem(0);
            }
            finally { app.ReturnToBase(); SetConfig(battle, original); UnityEngine.Object.Destroy(config); }
        }
        private static EnemyController[] Prepare(GameApplication app)
        {
            app.Battle.Step(0.7f, Vector2.zero, null, true);
            var enemies = app.Battle.Enemies.ToArray();
            if (enemies.Length != 4) throw new Exception("Element fixture needs four enemies");
            enemies[0].transform.position = new Vector3(0, 0.6f, 5);
            enemies[1].transform.position = new Vector3(1, 0.6f, 5);
            enemies[2].transform.position = new Vector3(4, 0.6f, 5);
            enemies[3].transform.position = new Vector3(-4, 0.6f, 5);
            return enemies;
        }
        private static void CheckDrafts(Action<bool, string> check)
        {
            for (int e = 1; e <= 5; e++)
            {
                var builds = new BuildProgression(); builds.Reset(null, 23, (DamageElement)e);
                for (int choice = 0; choice < 6; choice++)
                {
                    builds.AddExperience(builds.ExperienceRequired);
                    if (choice == 2 || choice == 5) check(builds.Offers.Any(BuildCatalog.IsResonance), "Third and sixth choices guarantee compatible resonance opportunities");
                    foreach (var id in builds.Offers)
                        check(BuildCatalog.IsResonance(id) ? ElementCatalog.Compatible((DamageElement)e, BuildCatalog.ResonanceElement(id)) : BuildCatalog.ExclusiveElement(id) == DamageElement.Physical || BuildCatalog.ExclusiveElement(id) == (DamageElement)e, "Draft contains only usable elemental cards");
                    var selected = choice == 5 ? builds.Offers.First(BuildCatalog.IsResonance) : builds.Offers.First(id => !BuildCatalog.IsResonance(id));
                    builds.Choose(selected, builds.Revision);
                }
                check(builds.Resonance != DamageElement.Physical, "Resonance selection binds one secondary element");
                builds.AddExperience(100000); int guard = 0;
                while (builds.PendingChoices > 0 && guard++ < 40)
                {
                    check(builds.Offers.Count > 0 && !builds.Offers.Any(BuildCatalog.IsResonance), "Bound resonance removes all other resonance cards without starving pool");
                    builds.Choose(builds.Offers[0], builds.Revision);
                }
                check(builds.PendingChoices == 0 && builds.TotalRanks == builds.MaximumRanks, "Every elemental pool exhausts without a stuck draft");
            }
        }
        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        [Serializable] private sealed class LegacyEnvelope { public string payload, checksum; }
        private static void CheckLegacySave(Action<bool, string> check)
        {
            string payload = JsonUtility.ToJson(new PermanentProgress { souls = 37 }).Replace("\"gemIndex\":0,", "");
            string checksum;
            using (var sha = System.Security.Cryptography.SHA256.Create()) checksum = Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload)));
            string path = System.IO.Path.GetFullPath("Logs/Checks/Profiles/legacy-" + Guid.NewGuid().ToString("N") + ".json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, JsonUtility.ToJson(new LegacyEnvelope { payload = payload, checksum = checksum }));
            var store = new ProgressStore(path);
            check(store.Ready && store.Snapshot.souls == 37 && store.Snapshot.gemIndex == 0, "Old profile without a gem field retains money and defaults to fire");
        }
    }
}

