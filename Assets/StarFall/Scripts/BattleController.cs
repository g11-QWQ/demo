using System.Collections.Generic;
using UnityEngine;

namespace StarFall
{
    public sealed partial class BattleController : MonoBehaviour
    {
        [SerializeField] private BattleConfig config;
        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private EnemyController enemyPrefab;
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private SoulPickup soulPrefab;
        [SerializeField] private Transform aimMarker;
        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private readonly List<Projectile> projectiles = new List<Projectile>();
        private readonly Stack<EnemyController> enemyPool = new Stack<EnemyController>();
        private readonly Stack<Projectile> projectilePool = new Stack<Projectile>();
        private readonly List<SoulPickup> souls = new List<SoulPickup>();
        private readonly Stack<SoulPickup> soulPool = new Stack<SoulPickup>();
        private PlayerController player;
        private MatchSession session;
        private GameAudio audioService;
        private GameUI interfaceView;
        private float shotRemaining, spawnRemaining;
        private float permanentHealth, permanentEfficiency, permanentRadius, staffCollectionBonus;
        public BattleConfig Config => config;
        public PlayerController Player => player;
        public int EnemyCount => enemies.Count;
        public int ProjectileCount => projectiles.Count;
        public IReadOnlyList<EnemyController> Enemies => enemies;
        public IReadOnlyList<Projectile> Projectiles => projectiles;
        public IReadOnlyList<SoulPickup> Souls => souls;
        public int ShotsFired { get; private set; }
        public float ActiveSoulRadius => (config.character.soulCollectionRadius + permanentRadius) * (config.staff.soulCollectionMultiplier + staffCollectionBonus + 0.5f * session.Builds.Rank(BuildId.SoulAttraction));
        public float CollectionRadius => player != null && player.IsAbsorbing ? ActiveSoulRadius : config.PassiveSoulRadius + permanentRadius;
        public float AttackFrequency => ActiveGem.attacksPerSecond * (1 + 0.025f * (session.Builds.Level - 1)) * (1 + 0.2f * session.Builds.Rank(BuildId.SwiftCasting)) * (session.Builds.Form == 3 ? 0.7f : 1) * (session.Builds.Specialization == 2 ? 0.9f : 1);
        public float MoveSpeed => config.playerSpeed + MoveSpeedBonus;
        private float MoveSpeedBonus => config.character.moveSpeed * 0.08f * session.Builds.Rank(BuildId.LightSteps);
        public float SoulTravelSpeed => config.soulTravelSpeed * (1 + 0.2f * session.Builds.Rank(BuildId.SoulAttraction));
        public float FireEfficiency => config.staff.EfficiencyFor(DamageElement.Fire) + permanentEfficiency + 0.2f * session.Builds.Rank(BuildId.FireAffinity);

        public void Initialize(MatchSession match, GameAudio sound)
        {
            session = match;
            audioService = sound;
            interfaceView = GetComponent<GameUI>();
            player = Instantiate(playerPrefab, transform);
            player.gameObject.SetActive(false);
        }

        public void Begin(PermanentProgress progress = null, DamageElement? element = null, bool training = false, bool fullLength = false)
        {
            Clear();
            permanentHealth = progress == null ? 0 : progress.ranks[0] * 10;
            permanentEfficiency = progress == null ? 0 : progress.ranks[1] * 0.05f + (progress.staff == 1 ? -0.15f : 0);
            permanentRadius = progress == null ? 0 : progress.ranks[2] * 0.2f;
            staffCollectionBonus = progress != null && progress.staff == 1 ? 0.75f : 0;
            InitializeSpell(element ?? (DamageElement)((progress == null ? 0 : progress.gemIndex) + 1), training);
            LongTraining = training && fullLength;
            InitializeEncounters();
            player.ResetActor();
            player.gameObject.SetActive(true);
            ShotsFired = 0;
            if (aimMarker != null) aimMarker.gameObject.SetActive(true);
            shotRemaining = 0;
            spawnRemaining = 0.35f;
            session.Start(training && !fullLength ? 60 : config.duration, config.playerHealth + permanentHealth, training && !fullLength ? new[] { 3, 4, 5, 6, 8, 10, 12 } : config.levelExperience, null, ActiveGem.element);
        }

        public void Clear()
        {
            ClearEncounters();
            interfaceView?.ClearDamageNumbers();
            ClearSpellEffects();
            foreach (var enemy in enemies) { enemy.gameObject.SetActive(false); enemyPool.Push(enemy); }
            foreach (var bullet in projectiles) { bullet.gameObject.SetActive(false); projectilePool.Push(bullet); }
            foreach (var soul in souls) { soul.gameObject.SetActive(false); soulPool.Push(soul); }
            enemies.Clear();
            projectiles.Clear();
            souls.Clear();
            if (aimMarker != null) aimMarker.gameObject.SetActive(false);
            if (player != null) player.gameObject.SetActive(false);
        }

        public void SetPresentationActive(bool active)
        {
            if (aimMarker != null) aimMarker.gameObject.SetActive(active);
            if (!active && player != null) player.SetAbsorbing(false, 0);
        }

        public void Step(float delta, Vector2 movement, Vector3? aimPoint = null, bool absorb = false, bool ultimate = false)
        {
            if (delta <= 0) return;
            if (session.State == MatchState.ClaimingReward) { interfaceView?.StepDamageNumbers(delta); player.Move(movement, delta, config, MoveSpeedBonus, navigation); return; }
            if (session.State != MatchState.Playing) return;
            
            // Small simulation steps preserve firing cadence and collection during slow frames.
            while (delta > 0.000001f && session.State == MatchState.Playing)
            {
                float step = Mathf.Min(delta, 1f / 60);
                Simulate(step, movement, aimPoint, absorb, ultimate); ultimate = false;
                delta -= step;
            }
        }

        private void Simulate(float delta, Vector2 movement, Vector3? aimPoint, bool absorb, bool ultimate)
        {
            interfaceView?.StepDamageNumbers(delta);
            var previousPosition = player.Position;
            player.Move(movement, delta, config, MoveSpeedBonus, navigation);
            if (aimPoint.HasValue)
            {
                fusionAim = aimPoint.Value;
                player.Aim(aimPoint.Value - player.Position);
                if (aimMarker != null) aimMarker.position = new Vector3(aimPoint.Value.x, 0.08f, aimPoint.Value.z);
            }
            player.SetAbsorbing(absorb, ActiveSoulRadius);
            SyncUltimateUnlock(); harmonyRemaining = Mathf.Max(0, harmonyRemaining - delta);
            if (ultimate) TryReleaseUltimate();
            StepSpellEffects(delta);
            StepEncounters(delta);
            if (session.State != MatchState.Playing) return;
            spawnRemaining -= delta;
            if (spawnRemaining <= 0)
            {
                int batch = config.enableEnemyRoles && (!Training || LongTraining) ? (session.Elapsed >= 300 ? 3 : session.Elapsed >= 150 ? 2 : 1) : 1;
                for (int n = 0; n < batch && !finalSpawned && enemies.Count < NormalEnemyLimit; n++) SpawnEnemy();
                spawnRemaining = Mathf.Lerp(config.initialSpawnInterval, config.finalSpawnInterval,
                    Mathf.Clamp01(StageTime / 180));
            }

            foreach (var enemy in enemies) enemy.Step(delta, player.Position, config, navigation);
            StepFusions(delta);
            StepAttackLinks(delta);
            StepLinkArts(delta, Vector3.Distance(previousPosition, player.Position));
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var enemy = enemies[i];
                if (enemy.StepBurn(delta)) KillEnemy(enemy);
            }
            // Move existing bullets before firing so new bullets never hit a target's past pose.
            StepProjectiles(delta);
            if (pendingClear) { CompleteFinalBoss(); return; }
            StepSouls(delta);
            if (session.State != MatchState.Playing) return;
            foreach (var enemy in enemies)
            {
                if (navigation.ClearLine(enemy.Position, player.Position, 0) && enemy.CanContact(player, config) && player.TryHurt(session, config)) audioService?.PlayHurt();
                if (session.State != MatchState.Playing) return;
            }

            shotRemaining -= delta;
            if (absorb) shotRemaining = Mathf.Max(0, shotRemaining);
            else if (shotRemaining <= 0)
            {
                CastSpell();
                shotRemaining += 1f / AttackFrequency;
                ShotsFired++;
                audioService?.PlayShot();
            }
            session.Advance(delta);
        }

        private void SpawnEnemy(EnemyKind kind = EnemyKind.Normal)
        {
            if (enemies.Count >= Mathf.Max(1, config.maxEnemies))
            {
                if (kind == EnemyKind.Normal) return;
                // Reserve room for required bosses without kills, drops or extra actors.
                int index = enemies.FindLastIndex(e => e.Kind == EnemyKind.Normal);
                if (index < 0) return;
                var retired = enemies[index]; enemies.RemoveAt(index);
                retired.gameObject.SetActive(false); enemyPool.Push(retired);
            }
            var half = config.arenaSize * 0.5f - Vector2.one * config.enemyRadius;
            Vector3 position = Vector3.zero;
            // Prefer a boundary far from the player; prevent enemies appearing on top of them.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float t = Random.Range(-1f, 1f);
                switch (Random.Range(0, 4))
                {
                    case 0: position = new Vector3(-half.x, 0.6f, t * half.y); break;
                    case 1: position = new Vector3(half.x, 0.6f, t * half.y); break;
                    case 2: position = new Vector3(t * half.x, 0.6f, -half.y); break;
                    default: position = new Vector3(t * half.x, 0.6f, half.y); break;
                }
                if ((position - player.Position).sqrMagnitude >= 64) break;
            }
            if ((position - player.Position).sqrMagnitude < 64)
                position = new Vector3(player.Position.x >= 0 ? -half.x : half.x, 0.6f,
                    player.Position.z >= 0 ? -half.y : half.y);
            var enemy = enemyPool.Count > 0 ? enemyPool.Pop() : Instantiate(enemyPrefab, transform);
            foreach (var bullet in projectiles) bullet.Hits.Remove(enemy);
            attackMarks.Remove(enemy);
            foreach (var zone in mud) zone.Dwell.Remove(enemy);
            foreach (var effect in fusions) { effect.WaveHits.Remove(enemy); effect.Conductivity.Remove(enemy); }
            float radius = kind == EnemyKind.Normal ? config.enemyRadius : kind == EnemyKind.MiniBoss ? config.miniBossRadius : config.finalBossRadius;
            enemy.Damaged = (point, amount, element) => interfaceView?.ShowDamage(point, amount, element);
            enemy.Spawn(navigation.ClosestFree(position, radius), config, kind);
            if (kind == EnemyKind.Normal && (!Training || LongTraining) && config.enableBossEncounters && config.enableEnemyRoles)
            {
                waveSpawns++;
                var role = session.Elapsed >= 90 && waveSpawns % 18 == 0 ? EnemyRole.Elite
                    : session.Elapsed >= 90 && waveSpawns % 7 == 0 ? EnemyRole.Caster : session.Elapsed >= 60 && waveSpawns % 5 == 0 ? EnemyRole.Charger : EnemyRole.Melee;
                enemy.ConfigureWave(role, 1 + Mathf.Min(4, Mathf.Floor(session.Elapsed / 120)) * 0.08f, session.Elapsed >= config.miniBossTime ? 1 : 0);
                enemy.transform.position = navigation.ClosestFree(enemy.Position, enemy.Radius);
            }
            if (kind != EnemyKind.Normal) Boss = enemy;
            enemies.Add(enemy);
        }

        private void DropSoul(Vector3 position, int value)
        {
            var soul = soulPool.Count > 0 ? soulPool.Pop() : Instantiate(soulPrefab, transform);
            soul.Spawn(position, value);
            souls.Add(soul);
        }

        private void StepSouls(float delta)
        {
            for (int i = souls.Count - 1; i >= 0; i--)
            {
                var soul = souls[i];
                if (!soul.Step(delta, player.Position, CollectionRadius, SoulTravelSpeed)) continue;
                int value = soul.Value;
                bool activelyAbsorbing = player.IsAbsorbing;
                soul.gameObject.SetActive(false);
                soulPool.Push(soul);
                souls.RemoveAt(i);
                session.CollectSoul(value, activelyAbsorbing);
                if (session.State != MatchState.Playing) return;
            }
        }

        private void KillEnemy(EnemyController enemy)
        {
            if (!enemies.Remove(enemy)) return;
            if (enemy.Kind == EnemyKind.FinalBoss) { pendingClear = true; rewardPosition = enemy.Position; }
            else DropSoul(enemy.Position, enemy.Kind == EnemyKind.MiniBoss ? config.miniBossSoulValue : config.enemySoulValue + enemy.SoulBonus);
            if (enemy.Kind == EnemyKind.MiniBoss) miniDefeated = true;
            if (Boss == enemy) Boss = null;
            enemy.gameObject.SetActive(false);
            enemyPool.Push(enemy);
            session.AddKill();
        }
    }
}
