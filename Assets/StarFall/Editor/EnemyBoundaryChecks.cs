using System;
using UnityEditor;
using UnityEngine;

namespace StarFall.Editor
{
    public static class EnemyBoundaryChecks
    {
        public static void Run(GameApplication app, Action<bool, string> check)
        {
            var battle = app.Battle; var original = battle.Config;
            var config = UnityEngine.Object.Instantiate(original);
            config.enableObstacles = false;
            var nav = new ArenaNavigation(config);
            foreach (float radius in new[] { config.enemyRadius, config.enemyRadius * 1.3f, config.miniBossRadius, config.finalBossRadius })
            {
                float x = config.arenaSize.x / 2 - radius, z = config.arenaSize.y / 2 - radius;
                foreach (var point in new[] { new Vector3(x, 0.6f, 0), new Vector3(-x, 0.6f, 0), new Vector3(0, 0.6f, z), new Vector3(0, 0.6f, -z), new Vector3(x, 0.6f, z), new Vector3(-x, 0.6f, z), new Vector3(x, 0.6f, -z), new Vector3(-x, 0.6f, -z) })
                {
                    var center = new Vector3(0, 0.6f, 0);
                    check(nav.ClearLine(point, center, radius), "All inset boundary edges allow inward travel");
                    var moved = nav.Move(point, nav.Direction(point, center, radius) * 0.2f, radius);
                    check(nav.IsFree(moved, radius) && Vector3.Distance(moved, center) < Vector3.Distance(point, center), "Every enemy size moves inward from all eight boundary positions");
                    check(nav.FirstWallHit(point, point * 2, radius, out _), "Outward travel remains blocked");
                }
                var recovered = nav.Move(new Vector3(x + 0.1f, 0.6f, z + 0.1f), Vector3.zero, radius);
                check(nav.IsFree(recovered, radius), "Invalid corner position recovers to a walkable point");
            }
            config.maxEnemies = 6; config.enableBossEncounters = true; config.miniBossTime = 1.2f;
            config.enemySpeed = config.miniBossSpeed = 0;
            config.initialSpawnInterval = config.finalSpawnInterval = 0.1f;
            var serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = config; serialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                app.ReturnToBase(); app.StartGrowthTraining();
                battle.Step(1.1f, Vector2.zero, null, true);
                check(battle.EnemyCount == 6 && battle.Boss == null, "Ordinary wave fills configured cap");
                for (int i = 0; i < 100; i++)
                {
                    battle.Step(0.02f, Vector2.zero, null, true);
                    check(battle.EnemyCount <= 6, "All spawn paths respect total cap including boss");
                }
                check(battle.Boss != null && app.Session.Kills == 0 && battle.Souls.Count == 0, "Boss makes room without awarding kills or souls");
                battle.TrainingTargets();
                check(battle.EnemyCount == 6, "Training target reset also respects configured cap");
            }
            finally
            {
                app.ReturnToBase(); serialized = new SerializedObject(battle); serialized.FindProperty("config").objectReferenceValue = original; serialized.ApplyModifiedPropertiesWithoutUndo(); UnityEngine.Object.Destroy(config);
            }
        }
    }
}
