using UnityEngine;

namespace StarFall
{
    public sealed partial class BattleController
    {
        private static float ExtendedLifetime(FusionKind kind)
        {
            switch (kind)
            {
                case FusionKind.SteamBurst: return 2.5f;
                case FusionKind.Volcano: return 4;
                case FusionKind.StormFront: return 3;
                case FusionKind.ConductivePool: return 4.5f;
                case FusionKind.RailSpear: return 1.6f;
                case FusionKind.SandDrill: return 3.6f;
                default: return 3.1f;
            }
        }
        private void PushFusion(EnemyController enemy, Vector3 direction, float distance)
        {
            if (enemy.Health <= 0) return;
            enemy.transform.position = navigation.Move(enemy.Position, direction.normalized * distance * (enemy.Kind == EnemyKind.Normal ? 1 : 0.1f), enemy.Radius);
        }
        private void BurnFusion(Fusion effect, EnemyController enemy, float damage)
        {
            if (enemy.Health > 0)
                enemy.ApplyBurn(CombatMath.Damage(damage, 1, effect.Efficiency, enemy.ResistanceFor(DamageElement.Fire)));
        }
        private void StepExtendedFusion(Fusion effect, float delta)
        {
            effect.Tick -= delta;
            bool tick = effect.Tick <= 0;
            if (tick) effect.Tick += effect.Kind == FusionKind.SandDrill ? 0.3f : 0.5f;
            switch (effect.Kind)
            {
                case FusionKind.SteamBurst: StepSteam(effect); break;
                case FusionKind.Volcano: StepVolcano(effect, tick); break;
                case FusionKind.StormFront: StepStorm(effect, delta, tick); break;
                case FusionKind.ConductivePool: StepConduction(effect, tick); break;
                case FusionKind.RailSpear: StepRail(effect); break;
                case FusionKind.SandDrill: StepDrill(effect, delta, tick); break;
                case FusionKind.StoneTomb: StepTomb(effect, tick); break;
            }
        }
        private void StepSteam(Fusion effect)
        {
            float radius = Mathf.Min(4.5f, effect.Age * 5);
            foreach (var enemy in enemies.ToArray())
            {
                if (!InFusionRange(effect, enemy, radius)) continue;
                if (effect.WaveHits.Add(enemy))
                {
                    FusionDamage(effect, enemy, effect.Main == DamageElement.Fire ? 24 : 18, DamageElement.Fire);
                    FusionDamage(effect, enemy, 10, DamageElement.Ocean);
                    BurnFusion(effect, enemy, 2);
                    PushFusion(enemy, enemy.Position - effect.Position, 1.2f);
                }
                // The expanding hot front clears space, followed by lingering condensation.
                if (effect.Age >= 1 && enemy.Health > 0) enemy.ApplySlow(effect.Main == DamageElement.Ocean ? 0.55f : 0.35f, 0.4f);
            }
        }
        private void StepVolcano(Fusion effect, bool tick)
        {
            if (effect.Age < 0.55f) return;
            bool erupt = !effect.Struck; effect.Struck = true;
            foreach (var enemy in enemies.ToArray())
            {
                if (!InFusionRange(effect, enemy, 3)) continue;
                if (erupt) { FusionDamage(effect, enemy, 18, DamageElement.Earth); PushFusion(enemy, enemy.Position - effect.Position, 0.7f); }
                if (tick && effect.Age < 3.3f)
                { FusionDamage(effect, enemy, effect.Main == DamageElement.Fire ? 6 : 4, DamageElement.Fire); BurnFusion(effect, enemy, 3); }
            }
            if (effect.Age < 3.3f || effect.Bonus > 0) return;
            effect.Bonus = 1;
            // The cooled core shatters against one enemy still nearest the crater center.
            var focus = NearestFusionEnemy(effect, 3);
            if (focus != null) FusionDamage(effect, focus, effect.Main == DamageElement.Earth ? 50 : 36, DamageElement.Earth);
        }
        private EnemyController NearestFusionEnemy(Fusion effect, float radius)
        {
            EnemyController result = null; float best = radius * radius * effect.AreaScale * effect.AreaScale;
            foreach (var enemy in enemies)
            {
                float d = (enemy.Position - effect.Position).sqrMagnitude;
                if (d < best && InFusionRange(effect, enemy, radius)) { result = enemy; best = d; }
            }
            return result;
        }
        private void StepStorm(Fusion effect, float delta, bool tick)
        {
            effect.Position = navigation.Move(effect.Position, effect.Direction * delta * (effect.Main == DamageElement.Wind ? 5 : 4), 0.25f);
            if (!tick) return;
            foreach (var enemy in enemies.ToArray())
            {
                var offset = enemy.Position - effect.Position; offset.y = 0;
                if (Mathf.Abs(Vector3.Dot(offset, effect.Direction)) > 1.3f * effect.AreaScale + enemy.Radius || !InFusionRange(effect, enemy, 4.3f)) continue;
                FusionDamage(effect, enemy, effect.Main == DamageElement.Lightning ? 14 : 10, DamageElement.Lightning);
                FusionDamage(effect, enemy, 5, DamageElement.Wind);
                PushFusion(enemy, effect.Direction, 0.45f);
            }
        }
        private void StepConduction(Fusion effect, bool tick)
        {
            foreach (var enemy in enemies.ToArray())
            {
                if (!InFusionRange(effect, enemy, 3.5f)) { effect.Conductivity.Remove(enemy); continue; }
                enemy.ApplySlow(effect.Main == DamageElement.Ocean ? 0.5f : 0.3f, 0.3f);
                if (!tick) continue;
                effect.Conductivity.TryGetValue(enemy, out int stacks);
                stacks = Mathf.Min(4, stacks + 1); effect.Conductivity[enemy] = stacks;
                FusionDamage(effect, enemy, 2, DamageElement.Ocean);
                FusionDamage(effect, enemy, 2 + stacks * (effect.Main == DamageElement.Lightning ? 2 : 1.5f), DamageElement.Lightning);
            }
        }
        private void StepRail(Fusion effect)
        {
            if (effect.Struck) return;
            bool valid = effect.Target != null && effect.Target.gameObject.activeSelf && effect.Target.Generation == effect.Generation;
            if (valid) { effect.Position = effect.Target.Position; effect.Position.y = 0.08f; }
            if (effect.Age < 0.85f) return;
            effect.Struck = true;
            foreach (var enemy in enemies.ToArray())
            {
                var offset = enemy.Position - effect.Position; offset.y = 0;
                float along = Vector3.Dot(offset, effect.Direction);
                bool primary = valid && enemy == effect.Target;
                if (!primary && (along < -0.6f * effect.AreaScale || along > 8 * effect.AreaScale || Vector3.Cross(offset, effect.Direction).magnitude > 0.65f * effect.AreaScale + enemy.Radius)) continue;
                if (!navigation.ClearLine(effect.Position, enemy.Position, 0)) continue;
                FusionDamage(effect, enemy, primary ? (effect.Main == DamageElement.Earth ? 64 : 48) : 18, DamageElement.Earth);
                FusionDamage(effect, enemy, primary ? (effect.Main == DamageElement.Lightning ? 30 : 18) : 8, DamageElement.Lightning);
            }
        }
        private void StepDrill(Fusion effect, float delta, bool tick)
        {
            effect.Position = navigation.Move(effect.Position, effect.Direction * delta * (effect.Main == DamageElement.Wind ? 3.4f : 2.6f), 0.25f);
            if (!tick) return;
            var focus = NearestFusionEnemy(effect, 2.4f);
            if (focus != null) FusionDamage(effect, focus, effect.Main == DamageElement.Earth ? 17 : 12, DamageElement.Earth);
            foreach (var enemy in enemies.ToArray())
                if (InFusionRange(effect, enemy, 2.4f)) FusionDamage(effect, enemy, 3, DamageElement.Wind);
        }
        private void StepTomb(Fusion effect, bool tick)
        {
            if (effect.Age < 0.6f || effect.Struck) return;
            bool collapse = effect.Age >= 2.4f;
            foreach (var enemy in enemies.ToArray())
            {
                if (!InFusionRange(effect, enemy, 3.4f)) continue;
                if (effect.WaveHits.Add(enemy)) enemy.ApplyRoot(effect.Main == DamageElement.Ocean ? 1.8f : 1.2f);
                if (tick && !collapse) FusionDamage(effect, enemy, 3, DamageElement.Ocean);
                if (collapse)
                {
                    FusionDamage(effect, enemy, effect.Main == DamageElement.Earth ? 44 : 32, DamageElement.Earth);
                    FusionDamage(effect, enemy, 12, DamageElement.Ocean);
                }
            }
            if (collapse) effect.Struck = true;
        }
    }
}
