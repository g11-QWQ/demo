using System;
using UnityEditor;
using UnityEngine;

namespace StarFall.Editor
{
    public static class PhaseOneChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check, Action<GameApplication, string> capture)
        {
            check(Mathf.Abs(CombatMath.Damage(10, 0.8f, 1, 0) - 8) < 0.0001f, "Fire damage is 8 without resistance");
            check(Mathf.Abs(CombatMath.Damage(10, 0.8f, 1, 20) - 6.4f) < 0.0001f, "20 percent resistance gives fractional 6.4 damage");
            check(CombatMath.Damage(10, 0.8f, 1, 100) == 1, "Damage minimum is one");
            check(Mathf.Abs(CombatMath.Damage(10, 0.8f, 1.5f, 20) - 9.6f) < 0.0001f, "Staff elemental efficiency participates in damage");
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
                check(config.staff.gemSlots == 1 && config.character.allElementResistancePercent == 20, "Staff slot and character resistance configured");
                check(config.PassiveSoulRadius == 3 && config.ActiveSoulRadius == 6.75f, "Passive and active radii are 3 and 6.75");
                Restart(app);
                var right = Vector3.right * 15;
                battle.Step(0.01f, Vector2.zero, right);
                check(battle.ShotsFired == 1 && battle.EnemyCount == 0, "Auto cast works without enemies");
                check(Vector3.Dot(battle.Projectiles[0].Direction, Vector3.right) > 0.999f, "Fireball follows manual aim");
                int shots = battle.ShotsFired;
                battle.Step(1, Vector2.zero, right);
                check(battle.ShotsFired - shots == 2, "Two fireballs per second across simulation steps");
                battle.Enemies[0].transform.position = new Vector3(0, 0.6f, 12);
                battle.Step(0.1f, Vector2.zero, right);
                check(Vector3.Dot(battle.Player.AimDirection, Vector3.right) > 0.999f, "Enemy position does not override manual aim");
                battle.Step(0.01f, Vector2.zero, battle.Player.Position);
                check(Vector3.Dot(battle.Player.AimDirection, Vector3.right) > 0.999f, "Overlapping mouse keeps last direction");
                shots = battle.ShotsFired;
                var position = battle.Player.Position;
                battle.Step(1.2f, Vector2.right, right, true);
                check(battle.ShotsFired == shots && battle.Player.IsAbsorbing, "Holding absorption blocks new fireballs");
                check(battle.ProjectileCount == 0, "Existing fireballs continue and expire while absorbing");
                check(battle.Player.Position.x > position.x && battle.CollectionRadius == 6.75f, "Can move while absorbing at expanded radius");

                Restart(app);
                battle.Step(0.01f, Vector2.zero, right);
                battle.Step(0.1f, Vector2.zero, right, true);
                battle.Step(0.1f, Vector2.zero, right, false);
                check(battle.ShotsFired == 1, "Toggling absorption cannot reset shot cooldown");
                battle.Step(0.31f, Vector2.zero, right, false);
                check(battle.ShotsFired == 2, "Shooting resumes on original cooldown");

                config.enemyElementResistancePercent = 20;
                Restart(app);
                battle.Step(0.36f, Vector2.zero, Vector3.forward * 10, true);
                var enemy = battle.Enemies[0];
                enemy.transform.position = new Vector3(0, 0.6f, 4);
                battle.Step(0.3f, Vector2.zero, Vector3.forward * 10);
                check(Mathf.Abs(enemy.Health - 17.6f) < 0.001f, "Actual projectile applies fractional elemental damage");
                config.contactElement = DamageElement.Fire;
                enemy.transform.position = battle.Player.Position;
                battle.Step(0.01f, Vector2.zero, Vector3.back * 10, true);
                check(Mathf.Abs(app.Session.Health - 92) < 0.001f, "Character elemental resistance reduces magic contact damage");
                config.contactElement = DamageElement.Physical;
                config.enemyElementResistancePercent = 0;

                CreateDrop(app, check);
                var soul = battle.Souls[0];
                soul.Spawn(new Vector3(4, 0.3f, 0), 1);
                battle.Step(0.1f, Vector2.zero, Vector3.back * 10);
                check(!soul.Attracted && app.Session.SoulExperience == 0, "Outside passive radius stays on ground");
                battle.Step(0.02f, Vector2.zero, Vector3.back * 10, true);
                check(soul.Attracted && app.Session.SoulExperience == 0, "Expanded radius attracts but does not instantly award experience");
                app.Session.Pause();
                position = soul.Position;
                battle.Step(1, Vector2.one, right, true);
                check(soul.Position == position && app.Session.SoulExperience == 0, "Pause freezes soul flight");
                app.Resume();
                battle.Step(0.7f, Vector2.zero, Vector3.back * 10, false);
                check(app.Session.SoulExperience == 1 && battle.Souls.Count == 0, "Latched soul continues after release and credits on arrival");
                battle.Step(1, Vector2.zero, Vector3.back * 10);
                check(app.Session.SoulExperience == 1, "Collected soul cannot be credited twice");

                CreateDrop(app, check);
                battle.Souls[0].Spawn(new Vector3(2, 0.3f, 0), 1);
                battle.Step(0.5f, Vector2.zero, Vector3.back * 10);
                check(app.Session.SoulExperience == 1, "Normal movement collects souls within base radius");

                CreateDrop(app, check);
                battle.Souls[0].Spawn(new Vector3(5, 0.3f, 0), 1);
                var mesh = battle.Souls[0].GetComponentInChildren<MeshFilter>().sharedMesh;
                check(mesh.bounds.size.z > mesh.bounds.size.y * 3 && mesh.vertexCount > 100, "Soul uses a long-tailed tear mesh");
                battle.Step(0.01f, Vector2.zero, new Vector3(8, 0, 5), true);
                app.UI.Refresh(app.Session, 0);
                capture(app, "07-soul-absorption");
                var camera = Camera.main;
                float size = camera.orthographicSize;
                camera.orthographicSize = 8;
                capture(app, "08-soul-detail");
                camera.orthographicSize = size;
                app.ReturnToMenu();
                check(battle.Souls.Count == 0 && battle.ProjectileCount == 0, "Return clears souls and fireballs");
                app.NewGame(); app.Deploy();
                check(app.Session.SoulExperience == 0 && !battle.Player.IsAbsorbing, "New round resets experience and absorption");
                Debug.Log("STARFALL_PHASE_ONE_RULES_OK: manual aim, fire cadence, resistance, soul collection and channel transitions.");
            }
            finally
            {
                app.ReturnToMenu();
                SetConfig(battle, original);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void CreateDrop(GameApplication app, Action<bool, string> check)
        {
            Restart(app);
            var battle = app.Battle;
            battle.Step(0.36f, Vector2.zero, Vector3.forward * 10, true);
            battle.Enemies[0].transform.position = new Vector3(0, 0.6f, 6);
            battle.Step(1.5f, Vector2.zero, Vector3.forward * 10);
            check(app.Session.Kills == 1 && battle.Souls.Count == 1, "Enemy death drops exactly one soul");
        }

        private static void Restart(GameApplication app) { app.ReturnToMenu(); app.NewGame(); app.Deploy(); }
        private static void SetConfig(BattleController battle, BattleConfig config)
        {
            var serialized = new SerializedObject(battle);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}


