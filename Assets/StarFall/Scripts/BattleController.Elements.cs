using System.Collections.Generic;
using UnityEngine;

namespace StarFall
{
    public sealed partial class BattleController
    {
        private GemDefinition activeGem;
        public GemDefinition ActiveGem => activeGem != null ? activeGem : config.gem;
        public bool Training { get; private set; }
        private float StageTime => session.Elapsed * (Training ? 3 : 1);
        public int ResonanceTriggers { get; private set; }
        public int MudCount => mud.Count;
        private sealed class Flash { public LineRenderer Ring; public float Remaining, Duration, Radius; public bool Arc; }
        private sealed class Mud { public GameObject Visual; public Vector3 Position; public float Remaining; public readonly Dictionary<EnemyController, float> Dwell = new Dictionary<EnemyController, float>(); }
        private readonly List<Flash> flashes = new List<Flash>();
        private readonly Stack<LineRenderer> flashPool = new Stack<LineRenderer>();
        private readonly List<Mud> mud = new List<Mud>();
        private readonly Stack<GameObject> mudPool = new Stack<GameObject>();
        private void InitializeSpell(DamageElement element, bool training)
        {
            if (activeGem != null) Destroy(activeGem);
            activeGem = ElementCatalog.Create(element, config.gem);
            player.SetGemColor(element);
            Training = training; TrainingShowcase = false; ResonanceTriggers = 0; FusionCastCount = 0; fusionAim = Vector3.forward * 8;
        }
        private int Rank(BuildId id) => session.Builds.Rank(id);
        private void CastSpell()
        {
            TryCastFusion();
            int extra = ActiveGem.element == DamageElement.Wind ? Rank(BuildId.WindFan) : 0;
            int count = ActiveGem.element == DamageElement.Wind ? 3 + extra : 1;
            var cast = new Projectile.Cast { Number = ShotsFired + 1, ChargeValue = 100f / (AttackFrequency * 6) };
            float charge = session.ConsumeSoulFire();
            for (int i = 0; i < count; i++)
            {
                var bullet = projectilePool.Count > 0 ? projectilePool.Pop() : Instantiate(projectilePrefab, transform);
                bool additional = i >= 3;
                float angle = count == 1 ? 0 : i < 3 ? (i - 1) * 12 : i == 3 ? -24 : 24;
                bullet.Configure(ActiveGem, config.staff, permanentEfficiency + 0.2f * Rank(BuildId.FireAffinity), charge * (additional ? 0.6f : 1), 2 + Rank(BuildId.Embers));
                bullet.SourceCast = cast; bullet.Resonance = session.Builds.Resonance;
                switch (ActiveGem.element)
                {
                    case DamageElement.Fire: bullet.SpecialRank = Rank(BuildId.FlameBurst); break;
                    case DamageElement.Lightning: bullet.SpecialRank = Rank(BuildId.ThunderArea); bullet.SecondaryRank = Rank(BuildId.ThunderChain); break;
                    case DamageElement.Wind: bullet.PiercesLeft = Rank(BuildId.WindPierce); break;
                    case DamageElement.Ocean: bullet.SpecialRank = Rank(BuildId.OceanChill); bullet.SecondaryRank = Rank(BuildId.OceanPressure); break;
                    case DamageElement.Earth: bullet.AddBaseDamage(4 * Rank(BuildId.EarthCore)); bullet.SpecialRank = Rank(BuildId.EarthMark); break;
                }
                bullet.Launch(player.Position, Quaternion.Euler(0, angle, 0) * player.AimDirection, ActiveGem.range);
                projectiles.Add(bullet);
            }
        }
        private void StepProjectiles(float delta)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var bullet = projectiles[i];
                Vector3 start = bullet.Position, next = bullet.NextPosition(bullet.Speed * delta);
                bool wall = navigation.FirstWallHit(start, next, bullet.Radius, out float wallTime);
                bool consumed = false;
                while (true)
                {
                    EnemyController hit = null; float earliest = wallTime;
                    foreach (var enemy in enemies)
                    {
                        if (bullet.Hits.Contains(enemy)) continue;
                        var motion = enemy.Position - enemy.PreviousPosition;
                        if (Projectile.Sweep(start, next - motion, enemy.PreviousPosition, enemy.Radius + bullet.Radius, out float time) && time < earliest)
                        { hit = enemy; earliest = time; }
                    }
                    if (hit == null) break;
                    bullet.Hits.Add(hit);
                    ResolveHit(bullet, hit);
                    if (bullet.PiercesLeft <= 0) { consumed = true; break; }
                    bullet.PiercesLeft--; bullet.Attenuate();
                }
                bullet.MoveTo(wall ? Vector3.Lerp(start, next, wallTime) : next);
                if (!consumed && !wall && bullet.DistanceLeft > 0.001f) continue;
                bullet.gameObject.SetActive(false); projectilePool.Push(bullet); projectiles.RemoveAt(i);
            }
        }
        private void ResolveHit(Projectile bullet, EnemyController target)
        {
            CreditFusion(bullet);
            var center = target.Position;
            if (bullet.Element == DamageElement.Lightning)
            {
                float radius = 1.8f * (1 + 0.15f * bullet.SpecialRank);
                var victims = new HashSet<EnemyController>();
                foreach (var enemy in enemies.ToArray())
                    if ((enemy.Position - center).sqrMagnitude <= radius * radius && navigation.ClearLine(center, enemy.Position, 0))
                    { victims.Add(enemy); DamageTarget(bullet, enemy); }
                Pulse(center, radius, DamageElement.Lightning);
                Vector3 from = center;
                for (int hop = 0; hop < bullet.SecondaryRank; hop++)
                {
                    EnemyController nearest = null; float distance = 16;
                    foreach (var enemy in enemies)
                    {
                        float d = (enemy.Position - from).sqrMagnitude;
                        if (!victims.Contains(enemy) && d < distance && navigation.ClearLine(from, enemy.Position, 0)) { nearest = enemy; distance = d; }
                    }
                    if (nearest == null) break;
                    var position = nearest.Position; victims.Add(nearest);
                    Arc(from, position);
                    DamageTarget(bullet, nearest, 0.5f); from = position;
                }
            }
            else { DamageTarget(bullet, target); Pulse(center, bullet.Element == DamageElement.Earth ? 0.7f : 0.4f, bullet.Element); }

            if (bullet.SourceCast == null || bullet.SourceCast.ResonanceTriggered) return;
            bool swamp = (bullet.Element == DamageElement.Ocean && bullet.Resonance == DamageElement.Earth) || (bullet.Element == DamageElement.Earth && bullet.Resonance == DamageElement.Ocean);
            if (swamp && bullet.SourceCast.Number % 3 == 0)
            {
                bullet.SourceCast.ResonanceTriggered = true; ResonanceTriggers++; SpawnMud(center);
            }
        }
        private void DamageTarget(Projectile bullet, EnemyController enemy, float scale = 1)
        {
            float bonus = bullet.Element == DamageElement.Fire && enemy.IsBurning ? 1 + bullet.SpecialRank * 0.15f
                : bullet.Element == DamageElement.Ocean && (enemy.SlowRemaining > 0 || enemy.RootRemaining > 0) ? 1 + bullet.SecondaryRank * 0.15f
                : bullet.Element == DamageElement.Earth ? 1 + enemy.EarthMarks * bullet.SpecialRank * 0.05f : 1;
            float damage = CombatMath.Damage(bullet.BaseDamage, bullet.Multiplier * scale * bonus, bullet.Efficiency, enemy.ResistanceFor(bullet.Element));
            AccumulateMark(enemy, damage);
            if (enemy.TakeDamage(damage, bullet.Element))
            { KillEnemy(enemy); return; }
            if (bullet.Element == DamageElement.Fire) enemy.ApplyBurn(CombatMath.Damage(bullet.BurnBaseDamage, 1, bullet.Efficiency, enemy.ResistanceFor(DamageElement.Fire)));
            if (bullet.Element == DamageElement.Ocean) enemy.ApplySlow(0.25f + bullet.SpecialRank * 0.1f, 1.5f);
            if (bullet.Element == DamageElement.Earth && bullet.SpecialRank > 0) enemy.AddEarthMark();
        }
        private void Pulse(Vector3 center, float radius, DamageElement element)
        {
            if (flashes.Count >= 128) return;
            LineRenderer ring;
            if (flashPool.Count > 0) ring = flashPool.Pop();
            else
            {
                if (encounterMaterial == null) encounterMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var go = new GameObject("SpellImpact"); go.transform.SetParent(transform, false);
                ring = go.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true;
                ring.sharedMaterial = encounterMaterial; ring.positionCount = 24;
            }
            ring.gameObject.SetActive(true); ring.loop = true; ring.positionCount = 24; ring.transform.position = new Vector3(center.x, 0.12f, center.z);
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", ElementCatalog.ColorFor(element)); ring.SetPropertyBlock(block);
            flashes.Add(new Flash { Ring = ring, Remaining = 0.3f, Duration = 0.3f, Radius = radius });
            SetPulse(ring, radius * 0.5f);
        }
        private void Arc(Vector3 start, Vector3 end)
        {
            if (flashes.Count >= 128) return;
            Pulse(start, 1, DamageElement.Lightning);
            var flash = flashes[flashes.Count - 1]; flash.Arc = true;
            var ring = flash.Ring; ring.loop = false; ring.positionCount = 9;
            Vector3 direction = end - start; direction.y = 0;
            Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);
            for (int i = 0; i < 9; i++) { float t = i / 8f; ring.SetPosition(i, direction * t + side * (i % 2 == 0 ? 0.22f : -0.22f) * Mathf.Sin(t * Mathf.PI)); }
        }
        private static void SetPulse(LineRenderer ring, float radius)
        {
            ring.widthMultiplier = 0.09f;
            for (int i = 0; i < ring.positionCount; i++) { float a = i * Mathf.PI * 2 / ring.positionCount; ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius)); }
        }
        private void SpawnMud(Vector3 center)
        {
            if (mud.Count >= 8) return;
            var visual = mudPool.Count > 0 ? mudPool.Pop() : Shape("Swamp", PrimitiveType.Cylinder, Vector3.zero, new Vector3(4, 0.015f, 4), new Color(0.3f, 0.34f, 0.16f), transform);
            visual.SetActive(true); visual.transform.position = new Vector3(center.x, 0.04f, center.z);
            mud.Add(new Mud { Visual = visual, Position = center, Remaining = 2.5f });
        }
        private void StepSpellEffects(float delta)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var flash = flashes[i]; flash.Remaining -= delta;
                if (flash.Remaining <= 0) { flash.Ring.gameObject.SetActive(false); flashPool.Push(flash.Ring); flashes.RemoveAt(i); }
                else if (flash.Arc) flash.Ring.widthMultiplier = 0.09f * flash.Remaining / flash.Duration;
                else SetPulse(flash.Ring, flash.Radius * (1 - flash.Remaining / flash.Duration * 0.5f));
            }
            for (int i = mud.Count - 1; i >= 0; i--)
            {
                var zone = mud[i]; zone.Remaining -= delta;
                if (zone.Remaining <= 0) { zone.Visual.SetActive(false); mudPool.Push(zone.Visual); mud.RemoveAt(i); continue; }
                foreach (var enemy in enemies)
                {
                    if ((enemy.Position - zone.Position).sqrMagnitude > 4 || !navigation.ClearLine(zone.Position, enemy.Position, 0)) { zone.Dwell.Remove(enemy); continue; }
                    enemy.ApplySlow(0.35f, 0.2f);
                    zone.Dwell.TryGetValue(enemy, out float time); time += delta; zone.Dwell[enemy] = time;
                    if (time >= 0.7f) enemy.ApplyRoot(0.5f);
                }
            }
        }
        private void ClearSpellEffects()
        {
            ClearFusions();
            foreach (var flash in flashes) { flash.Ring.gameObject.SetActive(false); flashPool.Push(flash.Ring); } flashes.Clear();
            foreach (var zone in mud) { zone.Visual.SetActive(false); mudPool.Push(zone.Visual); } mud.Clear();
        }
        public void TrainingBoss()
        {
            if (!Training || session.State != MatchState.Playing) return;
            TrainingShowcase = false; interfaceView?.ClearDamageNumbers();
            foreach (var enemy in enemies) { enemy.gameObject.SetActive(false); enemyPool.Push(enemy); } enemies.Clear();
            foreach (var bullet in projectiles) { bullet.gameObject.SetActive(false); projectilePool.Push(bullet); } projectiles.Clear();
            foreach (var soul in souls) { soul.gameObject.SetActive(false); soulPool.Push(soul); } souls.Clear();
            ClearWarnings(); ClearSpellEffects();
            miniSpawned = miniDefeated = finalSpawned = true; pendingClear = false;
            session.Advance(Mathf.Max(0, session.Duration - session.Elapsed));
            SpawnEnemy(EnemyKind.FinalBoss); bossAttackRemaining = config.bossAttackInterval;
        }
    }
}

