using System.Collections.Generic;
using UnityEngine;

namespace StarFall
{
    public sealed partial class BattleController
    {
        private ArenaNavigation navigation;
        private GameObject arenaVisuals, chest;
        private Material encounterMaterial;
        private bool miniSpawned, miniDefeated, finalSpawned, pendingClear;
        private float bossAttackRemaining;
        private int waveSpawns;
        private Vector3 rewardPosition;
        private sealed class Warning { public GameObject Visual; public Vector3 Position; public float Remaining, Radius; public DamageElement Element; public int Damage; }
        private readonly List<Warning> warnings = new List<Warning>();
        public EnemyController Boss { get; private set; }
        public int WarningCount => warnings.Count;
        public bool ChestAvailable => chest != null && session.State == MatchState.ClaimingReward;
        public Vector3 ChestPosition => rewardPosition;
        public int NormalEnemyLimit => Mathf.Min(config.maxEnemies, config.enableEnemyRoles && (!Training || LongTraining) ? (session.Elapsed < 90 ? 14 : session.Elapsed < 180 ? 20 : session.Elapsed < 300 ? 26 : 30) : StageTime < 60 ? 12 : StageTime < 120 ? 20 : 30);
        public string EncounterObjective => session.FinalBossDefeated ? "靠近金色宝箱，按 E 获取通关之证" : Boss != null ? (Boss.Kind == EnemyKind.MiniBoss ? "小 Boss" : "关底 Boss") + $"  {Boss.Health:0}/{Boss.MaxHealth:0}" : miniDefeated ? "清理怪群，等待关底 Boss" : "迎战怪群与小 Boss";

        private void InitializeEncounters()
        {
            navigation = new ArenaNavigation(config);
            miniSpawned = miniDefeated = finalSpawned = pendingClear = false;
            Boss = null; bossAttackRemaining = config.bossAttackInterval;
            waveSpawns = 0;
            arenaVisuals = new GameObject("ArenaObstacles");
            arenaVisuals.transform.SetParent(transform, false);
            foreach (var box in navigation.Obstacles)
                Shape("StoneBarrier", PrimitiveType.Cube, new Vector3(box.center.x, 0.6f, box.center.y), new Vector3(box.width, 1.2f, box.height), new Color(0.28f, 0.36f, 0.44f), arenaVisuals.transform);
        }

        private GameObject Shape(string label, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            if (encounterMaterial == null) encounterMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var go = GameObject.CreatePrimitive(type);
            go.name = label; go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = encounterMaterial;
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); renderer.SetPropertyBlock(block);
            return go;
        }

        private void ClearWarnings()
        {
            foreach (var warning in warnings) { warning.Visual.SetActive(false); Destroy(warning.Visual); }
            warnings.Clear();
        }

        private void ClearEncounters()
        {
            ClearWarnings(); Boss = null;
            if (arenaVisuals != null) { arenaVisuals.SetActive(false); Destroy(arenaVisuals); }
            if (chest != null) { chest.SetActive(false); Destroy(chest); chest = null; }
        }

        private void StepEncounters(float delta)
        {
            if (!config.enableBossEncounters || TrainingShowcase) return;
            if (!miniSpawned && session.Elapsed >= (Training && !LongTraining ? 15 : config.miniBossTime)) { miniSpawned = true; SpawnEnemy(EnemyKind.MiniBoss); bossAttackRemaining = config.bossAttackInterval; }
            if (!finalSpawned && miniDefeated && session.Elapsed >= session.Duration) { finalSpawned = true; ClearWarnings(); SpawnEnemy(EnemyKind.FinalBoss); bossAttackRemaining = config.bossAttackInterval; }
            for (int i = warnings.Count - 1; i >= 0; i--)
            {
                var warning = warnings[i]; warning.Remaining -= delta;
                if (warning.Remaining > 0) continue;
                var offset = player.Position - warning.Position; offset.y = 0;
                if (offset.magnitude <= warning.Radius + config.playerRadius && player.TryDamage(session, config, warning.Damage > 0 ? warning.Damage : warning.Element == DamageElement.Physical ? 18 : 25, warning.Element)) audioService?.PlayHurt();
                warning.Visual.SetActive(false); Destroy(warning.Visual); warnings.RemoveAt(i);
            }
            foreach (var enemy in enemies)
            {
                if (enemy.Role != EnemyRole.Caster || enemy.RootRemaining > 0) continue;
                enemy.RoleAttackRemaining -= delta;
                if (enemy.RoleAttackRemaining > 0 || warnings.Count >= 24 || !navigation.ClearLine(enemy.Position, player.Position, 0)) continue;
                enemy.RoleAttackRemaining = 4;
                var position = player.Position; position.y = 0.025f;
                warnings.Add(new Warning { Position = position, Radius = 1.1f, Remaining = 1.2f, Element = DamageElement.Ocean, Damage = 8,
                    Visual = Shape("CasterWarning", PrimitiveType.Cylinder, position, new Vector3(2.2f, 0.02f, 2.2f), new Color(0.3f, 0.55f, 1), transform) });
            }
            if (Boss == null || session.State != MatchState.Playing) return;
            bossAttackRemaining -= delta;
            if (bossAttackRemaining > 0) return;
            bossAttackRemaining = config.bossAttackInterval;
            int count = Boss.Kind == EnemyKind.FinalBoss ? 3 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = player.Position + Vector3.right * (i == 0 ? 0 : i == 1 ? -3 : 3);
                position.y = 0.025f;
                const float radius = 2;
                warnings.Add(new Warning { Position = position, Radius = radius, Remaining = config.bossWarningTime + i * 0.3f,
                    Element = count == 1 ? DamageElement.Physical : DamageElement.Fire,
                    Visual = Shape("BossAttackWarning", PrimitiveType.Cylinder, position, new Vector3(radius * 2, 0.015f, radius * 2), count == 1 ? new Color(1, 0.65f, 0.15f) : new Color(1, 0.22f, 0.4f), transform) });
            }
        }

        private void CompleteFinalBoss()
        {
            pendingClear = false;
            foreach (var enemy in enemies) { session.AddKill(); enemy.gameObject.SetActive(false); enemyPool.Push(enemy); }
            enemies.Clear();
            foreach (var bullet in projectiles) { bullet.gameObject.SetActive(false); projectilePool.Push(bullet); }
            projectiles.Clear();
            foreach (var soul in souls) { soul.gameObject.SetActive(false); soulPool.Push(soul); }
            souls.Clear(); ClearWarnings(); ClearSpellEffects();
            rewardPosition = navigation.ClosestFree(rewardPosition, config.playerRadius);
            chest = Shape("ClearanceChest", PrimitiveType.Cube, new Vector3(rewardPosition.x, 0.5f, rewardPosition.z), new Vector3(1.4f, 1, 1), new Color(1, 0.72f, 0.12f), transform);
            Shape("ChestSeal", PrimitiveType.Cube, chest.transform.position + Vector3.up * 0.55f, new Vector3(0.35f, 0.12f, 0.5f), Color.cyan, chest.transform);
            player.SetAbsorbing(false, 0);
            session.DefeatFinalBoss();
        }

        public bool TryOpenChest()
        {
            if (!ChestAvailable) return false;
            var offset = player.Position - rewardPosition; offset.y = 0;
            if (offset.magnitude > config.chestInteractionRadius || !navigation.ClearLine(player.Position, rewardPosition, 0)) return false;
            if (!session.ClaimClearanceProof()) return false;
            chest.SetActive(false);
            return true;
        }

        private void OnDestroy() { if (encounterMaterial != null) Destroy(encounterMaterial); if (activeGem != null) Destroy(activeGem); if (fusionMaterial != null) Destroy(fusionMaterial); }
    }
}
