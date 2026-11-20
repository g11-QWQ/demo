using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarFall
{
    public sealed partial class BattleController
    {
        private sealed class Fusion
        {
            public FusionKind Kind; public DamageElement Main;
            public Vector3 Position, Direction; public float Age, Lifetime, Tick, Efficiency, Bonus;
            public EnemyController Target; public int Generation; public bool Struck;
            public FusionVisual Visual;
            public readonly HashSet<EnemyController> WaveHits = new HashSet<EnemyController>();
        }
        private readonly List<Fusion> fusions = new List<Fusion>();
        private readonly Stack<FusionVisual> fusionPool = new Stack<FusionVisual>();
        private Material fusionMaterial;
        private Vector3 fusionAim = Vector3.forward * 8;
        public float FusionEnergy { get; private set; }
        public int FusionCastCount { get; private set; }
        public int ActiveFusionCount => fusions.Count;
        public FusionKind CurrentFusion => FusionCatalog.Get(ActiveGem.element, session.Builds.Resonance);
        public bool TrainingShowcase { get; private set; }

        private void CreditFusion(Projectile bullet)
        {
            if (CurrentFusion == FusionKind.None || bullet.SourceCast == null || bullet.SourceCast.ChargeCredited
                || FusionCatalog.Get(bullet.Element, bullet.Resonance) != CurrentFusion) return;
            bullet.SourceCast.ChargeCredited = true;
            FusionEnergy = Mathf.Min(100, FusionEnergy + bullet.SourceCast.ChargeValue);
        }
        private void TryCastFusion()
        {
            var kind = CurrentFusion;
            if (kind == FusionKind.None || FusionEnergy < 99.99f || fusions.Count >= 4) return;
            if (Mathf.Abs(fusionAim.x) > config.arenaSize.x / 2 || Mathf.Abs(fusionAim.z) > config.arenaSize.y / 2) return;
            var origin = player.Position;
            EnemyController target = null;
            if (kind == FusionKind.Judgment)
            {
                float nearest = ActiveGem.range;
                foreach (var enemy in enemies)
                {
                    var offset = enemy.Position - origin; offset.y = 0;
                    float along = Vector3.Dot(offset, player.AimDirection);
                    float lateral = Vector3.Cross(offset, player.AimDirection).magnitude;
                    if (along > 0 && along < nearest && lateral <= enemy.Radius + 0.9f && navigation.ClearLine(origin, enemy.Position, 0))
                    { nearest = along; target = enemy; }
                }
                if (target == null) return;
            }
            Vector3 destination = origin + Vector3.ClampMagnitude(fusionAim - origin, 12); destination.y = 0.08f;
            var groundOrigin = new Vector3(origin.x, 0.08f, origin.z);
            if (navigation.FirstWallHit(groundOrigin, destination, 0.25f, out float wall)) destination = Vector3.Lerp(groundOrigin, destination, Mathf.Max(0, wall - 0.02f));
            var position = kind == FusionKind.Judgment ? target.Position : kind == FusionKind.FireTornado ? navigation.Move(groundOrigin, player.AimDirection * 0.8f, 0.25f) : destination;
            position.y = 0.08f;
            var effect = new Fusion { Kind = kind, Main = ActiveGem.element, Position = position, Direction = player.AimDirection,
                Lifetime = kind == FusionKind.Judgment ? 1.35f : kind == FusionKind.FireTornado ? (ActiveGem.element == DamageElement.Fire ? 4.4f : 3.8f) : 3.9f,
                Efficiency = FireEfficiency, Target = target, Generation = target != null ? target.Generation : 0, Tick = 0.35f };
            effect.Visual = AcquireFusionVisual(); effect.Visual.Draw(kind, 0, position, effect.Direction, effect.Lifetime);
            fusions.Add(effect); FusionEnergy = 0; FusionCastCount++; ResonanceTriggers++;
        }
        private FusionVisual AcquireFusionVisual()
        {
            if (fusionPool.Count > 0) { var reused = fusionPool.Pop(); reused.gameObject.SetActive(true); return reused; }
            if (fusionMaterial == null)
            {
                fusionMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                fusionMaterial.SetFloat("_Surface", 1); fusionMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                fusionMaterial.SetFloat("_DstBlend", (float)BlendMode.One); fusionMaterial.SetFloat("_ZWrite", 0);
                fusionMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); fusionMaterial.SetOverrideTag("RenderType", "Transparent"); fusionMaterial.renderQueue = 3000;
            }
            var go = new GameObject("CombinedSpell"); go.transform.SetParent(transform, false);
            var visual = go.AddComponent<FusionVisual>(); visual.Initialize(fusionMaterial); return visual;
        }
        private void AccumulateMark(EnemyController enemy, float damage)
        {
            foreach (var effect in fusions)
                if (effect.Kind == FusionKind.Judgment && !effect.Struck && effect.Target == enemy && effect.Generation == enemy.Generation)
                    effect.Bonus = Mathf.Min(24, effect.Bonus + damage * 0.35f);
        }
        private void FusionDamage(Fusion effect, EnemyController enemy, float damage, DamageElement element)
        {
            if (enemy.Health <= 0 || !enemies.Contains(enemy)) return;
            if (enemy.TakeDamage(CombatMath.Damage(damage, 1, effect.Efficiency, enemy.ResistanceFor(element)), element)) KillEnemy(enemy);
        }
        private bool InFusionRange(Fusion effect, EnemyController enemy, float radius)
        {
            Vector3 offset = enemy.Position - effect.Position; offset.y = 0;
            return offset.sqrMagnitude <= radius * radius && navigation.ClearLine(effect.Position, enemy.Position, 0);
        }
        private void Pull(Fusion effect, EnemyController enemy, float delta, float speed)
        {
            Vector3 offset = effect.Position - enemy.Position; offset.y = 0;
            if (offset.magnitude < 0.5f) return;
            float strength = enemy.Kind == EnemyKind.Normal ? 1 : 0.12f;
            enemy.transform.position = navigation.Move(enemy.Position, offset.normalized * Mathf.Min(offset.magnitude - 0.5f, speed * strength * delta), enemy.Radius);
        }
        private void StepFusions(float delta)
        {
            for (int i = fusions.Count - 1; i >= 0; i--)
            {
                var effect = fusions[i]; effect.Age += delta;
                if (effect.Kind == FusionKind.Judgment)
                {
                    if (!effect.Struck && effect.Target != null && effect.Target.gameObject.activeSelf && effect.Target.Generation == effect.Generation)
                    { effect.Position = effect.Target.Position; effect.Position.y = 0.08f; }
                    if (!effect.Struck && effect.Age >= 0.65f)
                    {
                        effect.Struck = true;
                        foreach (var enemy in enemies.ToArray())
                        {
                            if (!InFusionRange(effect, enemy, 2.6f)) continue;
                            bool primary = enemy == effect.Target && enemy.Generation == effect.Generation;
                            FusionDamage(effect, enemy, (primary ? (effect.Main == DamageElement.Lightning ? 48 : 36) + effect.Bonus : 18), DamageElement.Lightning);
                            FusionDamage(effect, enemy, 8, DamageElement.Fire);
                            if (enemy.Health > 0) enemy.ApplyBurn(CombatMath.Damage(effect.Main == DamageElement.Fire ? 3 : 1, 1, effect.Efficiency, enemy.ResistanceFor(DamageElement.Fire)));
                        }
                    }
                }
                else if (effect.Kind == FusionKind.FireTornado)
                {
                    effect.Position = navigation.Move(effect.Position, effect.Direction * delta * (effect.Main == DamageElement.Wind ? 4.2f : 3), 0.25f);
                    effect.Tick -= delta; bool tick = effect.Tick <= 0; if (tick) effect.Tick += 0.35f;
                    foreach (var enemy in enemies.ToArray())
                    {
                        if (!InFusionRange(effect, enemy, 2.2f)) continue;
                        Pull(effect, enemy, delta, 2);
                        if (!tick) continue;
                        FusionDamage(effect, enemy, 3, DamageElement.Fire); FusionDamage(effect, enemy, 1, DamageElement.Wind);
                        if (enemy.Health > 0) enemy.ApplyBurn(CombatMath.Damage(2, 1, effect.Efficiency, enemy.ResistanceFor(DamageElement.Fire)));
                    }
                }
                else
                {
                    effect.Tick -= delta; bool tick = effect.Tick <= 0; if (tick) effect.Tick += 0.5f;
                    if (effect.Age < 3.2f)
                    {
                        foreach (var enemy in enemies.ToArray())
                        {
                            if (!InFusionRange(effect, enemy, 3.5f)) continue;
                            Pull(effect, enemy, delta, effect.Main == DamageElement.Wind ? 3.3f : 2.7f);
                            enemy.ApplySlow(effect.Main == DamageElement.Ocean ? 0.4f : 0.25f, 0.3f);
                            if (tick) FusionDamage(effect, enemy, 2, DamageElement.Ocean);
                        }
                    }
                    else
                    {
                        var waveCenter = effect.Position + effect.Direction * (Mathf.Clamp01((effect.Age - 3.2f) / 0.7f) * 6);
                        foreach (var enemy in enemies.ToArray())
                        {
                            Vector3 offset = enemy.Position - waveCenter; offset.y = 0;
                            if (effect.WaveHits.Contains(enemy) || offset.sqrMagnitude > 9 || !navigation.ClearLine(effect.Position, enemy.Position, 0)) continue;
                            effect.WaveHits.Add(enemy);
                            FusionDamage(effect, enemy, 16, DamageElement.Ocean); FusionDamage(effect, enemy, 8, DamageElement.Wind);
                            if (enemy.Health > 0) enemy.transform.position = navigation.Move(enemy.Position, effect.Direction * (enemy.Kind == EnemyKind.Normal ? 2.5f : 0.25f), enemy.Radius);
                        }
                    }
                }
                effect.Visual.Draw(effect.Kind, effect.Age, effect.Position, effect.Direction, effect.Lifetime);
                if (effect.Age >= effect.Lifetime) { effect.Visual.gameObject.SetActive(false); fusionPool.Push(effect.Visual); fusions.RemoveAt(i); }
            }
        }
        private void ClearFusions()
        {
            foreach (var effect in fusions) { effect.Visual.gameObject.SetActive(false); fusionPool.Push(effect.Visual); }
            fusions.Clear(); FusionEnergy = 0;
        }
        public void TrainingChargeFusion() { if (Training && session.State == MatchState.Playing && CurrentFusion != FusionKind.None) FusionEnergy = 100; }
        public void TrainingTargets()
        {
            if (!Training || session.State != MatchState.Playing) return;
            foreach (var enemy in enemies) { enemy.gameObject.SetActive(false); enemyPool.Push(enemy); } enemies.Clear();
            foreach (var bullet in projectiles) { bullet.gameObject.SetActive(false); projectilePool.Push(bullet); } projectiles.Clear();
            foreach (var soul in souls) { soul.gameObject.SetActive(false); soulPool.Push(soul); } souls.Clear();
            ClearWarnings(); ClearSpellEffects(); interfaceView?.ClearDamageNumbers();
            TrainingShowcase = true; Boss = null; pendingClear = false; spawnRemaining = float.PositiveInfinity;
            player.transform.position = navigation.ClosestFree(new Vector3(0, 0.6f, -3), config.playerRadius); player.Aim(Vector3.forward);
            fusionAim = new Vector3(0, 0.08f, 4);
            for (int i = 0; i < 12; i++)
            {
                SpawnEnemy(); var enemy = enemies[enemies.Count - 1];
                enemy.transform.position = navigation.ClosestFree(new Vector3((i % 4 - 1.5f) * 1.5f, 0.6f, 2 + i / 4 * 2), enemy.Radius);
                enemy.PrepareTrainingTarget(250, 0.35f);
            }
        }
    }
}
