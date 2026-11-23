using System.Collections.Generic;
using UnityEngine;

namespace StarFall
{
    public sealed partial class BattleController
    {
        private sealed class AttackLink
        {
            public FusionKind Kind;
            public Vector3 Position;
            public EnemyController Target;
            public int Generation, Evolution, Ticks;
            public float Remaining, Interval, Power;
        }
        private readonly List<AttackLink> attackLinks = new List<AttackLink>();
        private readonly Dictionary<EnemyController, int> attackMarks = new Dictionary<EnemyController, int>();
        public int AttackLinkTriggers { get; private set; }
        public int ActiveAttackLinkCount => attackLinks.Count;
        private void ClearAttackLinks() { attackLinks.Clear(); attackMarks.Clear(); AttackLinkTriggers = 0; ClearLinkArts(); }
        private void LinkDamage(EnemyController enemy, float damage, DamageElement element, float power)
        {
            if (enemy.Health <= 0 || !enemies.Contains(enemy)) return;
            if (enemy.TakeDamage(CombatMath.Damage(damage, 1, power, enemy.ResistanceFor(element)), element)) KillEnemy(enemy);
        }
        private void TriggerAttackLink(Projectile bullet, EnemyController target)
        {
            if (bullet.Derived || bullet.SourceCast == null || bullet.SourceCast.LinkTriggered) return;
            var kind = FusionCatalog.Get(bullet.Element, bullet.Resonance);
            if (kind == FusionKind.None) return;
            bullet.SourceCast.LinkTriggered = true;
            int evolution = bullet.LinkEvolution;
            float power = bullet.Efficiency * AttackPower;
            if (kind == FusionKind.Judgment)
            {
                attackMarks.TryGetValue(target, out int marks); marks++;
                attackMarks[target] = marks;
                Pulse(target.Position, 0.35f + marks * 0.12f, DamageElement.Fire);
                if (marks < (evolution == 2 ? 2 : 3)) return;
                attackMarks[target] = 0; AttackLinkTriggers++;
                JumpLink(target, evolution == 1 ? 4 : evolution == 2 ? 0 : 2, evolution == 2 ? 12 : evolution == 1 ? 4 : 6, power, true);
                if (evolution == 3) ScheduleLink(kind, target, evolution, power, 0.35f);
                return;
            }
            if (bullet.SourceCast.Number % (evolution == 2 ? 4 : 3) != 0) return;
            AttackLinkTriggers++;
            switch (kind)
            {
                case FusionKind.FireTornado:
                case FusionKind.SandDrill:
                    int count = evolution == 1 ? 4 : evolution == 2 ? 1 : 2;
                    for (int i = 0; i < count; i++)
                    {
                        float angle = count == 1 ? 0 : Mathf.Lerp(-35, 35, i / (float)(count - 1));
                        SpawnLinkProjectile(target.Position, Quaternion.Euler(0, angle, 0) * bullet.Direction,
                            kind == FusionKind.FireTornado ? DamageElement.Fire : DamageElement.Earth, DamageElement.Wind,
                            evolution == 2 ? 14 : evolution == 1 ? 4 : 6, power, evolution == 3, evolution == 2 ? 3 : 0, target);
                    }
                    break;
                case FusionKind.OceanVortex:
                    int blades = evolution == 1 ? 3 : 1;
                    for (int i = 0; i < blades; i++)
                        SpawnLinkProjectile(player.Position, Quaternion.Euler(0, (i - (blades - 1) * 0.5f) * 24, 0) * bullet.Direction,
                            DamageElement.Ocean, DamageElement.Wind, evolution == 2 ? 13 : evolution == 1 ? 4 : 7, power, true, 0, null,
                            evolution == 3 ? 30 : 20);
                    break;
                case FusionKind.StormFront:
                    JumpLink(target, evolution == 1 ? 4 : evolution == 2 ? 1 : 2, evolution == 2 ? 12 : 5, power, false);
                    if (evolution == 3) ScheduleLink(kind, target, evolution, power, 0.3f);
                    break;
                case FusionKind.ConductivePool:
                    target.ApplySlow(0.3f, 2);
                    LinkDamage(target, 3, DamageElement.Ocean, power);
                    int jumps = 0;
                    foreach (var enemy in enemies.ToArray())
                    {
                        if (enemy == target || enemy.SlowRemaining <= 0 || (enemy.Position - target.Position).sqrMagnitude > 16 || !navigation.ClearLine(target.Position, enemy.Position, 0)) continue;
                        Arc(target.Position, enemy.Position); LinkDamage(enemy, evolution == 2 ? 12 : 6, DamageElement.Lightning, power);
                        if (++jumps >= (evolution == 1 ? 4 : evolution == 2 ? 1 : 2)) break;
                    }
                    LinkDamage(target, evolution == 2 ? 12 : 6, DamageElement.Lightning, power);
                    if (evolution == 3) ScheduleLink(kind, target, evolution, power, 0.4f);
                    break;
                default:
                    ScheduleLink(kind, target, evolution, power, kind == FusionKind.SteamBurst && target.IsBurning ? 0.12f : 0.6f);
                    if (kind == FusionKind.RailSpear && evolution == 1)
                    {
                        int crystals = 0;
                        foreach (var enemy in enemies)
                        {
                            if (enemy == target || (enemy.Position - target.Position).sqrMagnitude > 9 || !navigation.ClearLine(target.Position, enemy.Position, 0)) continue;
                            ScheduleLink(kind, enemy, evolution, power, 0.6f);
                            if (++crystals == 2) break;
                        }
                    }
                    break;
            }
        }
        private void JumpLink(EnemyController target, int hops, float damage, float power, bool fire)
        {
            Vector3 from = target.Position;
            var visited = new HashSet<EnemyController> { target };
            LinkDamage(target, damage, DamageElement.Lightning, power);
            if (fire && target.Health > 0) target.ApplyBurn(CombatMath.Damage(2, 1, power, target.ResistanceFor(DamageElement.Fire)));
            for (int i = 0; i < hops; i++)
            {
                EnemyController nearest = null; float best = 16;
                foreach (var enemy in enemies)
                {
                    float d = (enemy.Position - from).sqrMagnitude;
                    if (!visited.Contains(enemy) && d < best && navigation.ClearLine(from, enemy.Position, 0)) { best = d; nearest = enemy; }
                }
                if (nearest == null) break;
                Vector3 point = nearest.Position; visited.Add(nearest); Arc(from, point);
                LinkDamage(nearest, damage, DamageElement.Lightning, power);
                LinkDamage(nearest, fire ? 2 : 3, fire ? DamageElement.Fire : DamageElement.Wind, power);
                if (fire && nearest.Health > 0) nearest.ApplyBurn(CombatMath.Damage(2, 1, power, nearest.ResistanceFor(DamageElement.Fire)));
                from = point;
            }
        }
        private void ScheduleLink(FusionKind kind, EnemyController target, int evolution, float power, float delay)
        {
            if (attackLinks.Count >= 48) return;
            attackLinks.Add(new AttackLink { Kind = kind, Target = target, Generation = target.Generation, Position = target.Position,
                Evolution = evolution, Power = power, Remaining = delay, Interval = kind == FusionKind.Volcano ? 0.5f : 0.3f,
                Ticks = kind == FusionKind.Volcano ? (evolution == 3 ? 5 : 3) :
                    kind == FusionKind.Judgment || kind == FusionKind.StormFront || kind == FusionKind.ConductivePool ? 1 : evolution == 3 ? 2 : 1 });
            Pulse(target.Position, kind == FusionKind.RailSpear ? 0.45f : 0.8f, kind == FusionKind.Volcano ? DamageElement.Fire : DamageElement.Ocean);
        }
        private void StepAttackLinks(float delta)
        {
            for (int i = attackLinks.Count - 1; i >= 0; i--)
            {
                var effect = attackLinks[i]; effect.Remaining -= delta;
                if (effect.Remaining > 0) continue;
                bool alive = effect.Target != null && effect.Target.Generation == effect.Generation && enemies.Contains(effect.Target);
                float power = effect.Power * (effect.Evolution == 1 ? 0.7f : effect.Evolution == 2 ? 1.6f : 1);
                if (effect.Kind == FusionKind.RailSpear || effect.Kind == FusionKind.Judgment || effect.Kind == FusionKind.StormFront || effect.Kind == FusionKind.ConductivePool)
                {
                    if (alive && navigation.ClearLine(effect.Position, effect.Target.Position, 0))
                    {
                        Arc(effect.Position + Vector3.right * 0.6f, effect.Target.Position);
                        LinkDamage(effect.Target, effect.Kind == FusionKind.RailSpear ? 10 : 4,
                            effect.Kind == FusionKind.RailSpear ? DamageElement.Earth : DamageElement.Lightning, power);
                        LinkDamage(effect.Target, 3, effect.Kind == FusionKind.RailSpear ? DamageElement.Lightning : effect.Kind == FusionKind.Judgment ? DamageElement.Fire : effect.Kind == FusionKind.ConductivePool ? DamageElement.Ocean : DamageElement.Wind, power);
                    }
                }
                else
                {
                    float radius = effect.Evolution == 1 ? 2 : effect.Evolution == 2 ? 0.8f : 1.3f;
                    foreach (var enemy in enemies.ToArray())
                    {
                        if ((enemy.Position - effect.Position).sqrMagnitude > radius * radius || !navigation.ClearLine(effect.Position, enemy.Position, 0)) continue;
                        var element = effect.Kind == FusionKind.StoneTomb ? DamageElement.Earth : DamageElement.Fire;
                        LinkDamage(enemy, effect.Kind == FusionKind.Volcano ? 3 : 8, element, power);
                        LinkDamage(enemy, 3, effect.Kind == FusionKind.Volcano ? DamageElement.Earth : DamageElement.Ocean, power);
                        if (enemy.Health > 0 && effect.Kind == FusionKind.StoneTomb) enemy.ApplyRoot(0.35f);
                    }
                    Pulse(effect.Position, radius, effect.Kind == FusionKind.StoneTomb ? DamageElement.Earth : DamageElement.Fire);
                    Pulse(effect.Position, radius * 0.7f, effect.Kind == FusionKind.Volcano ? DamageElement.Earth : DamageElement.Ocean);
                }
                if (--effect.Ticks <= 0) attackLinks.RemoveAt(i); else effect.Remaining += effect.Interval;
            }
        }
        private void SpawnLinkProjectile(Vector3 origin, Vector3 direction, DamageElement element, DamageElement secondary,
            float damage, float power, bool returning, int pierces, EnemyController ignore, float speed = 20)
        {
            if (projectiles.Count >= 160) return;
            var bullet = projectilePool.Count > 0 ? projectilePool.Pop() : Instantiate(projectilePrefab, transform);
            bullet.Configure(ActiveGem, config.staff);
            bullet.MakeLink(element, damage, power, speed);
            bullet.SourceCast = null; bullet.Derived = true; bullet.Returning = returning; bullet.PiercesLeft = pierces;
            bullet.SecondaryHit = secondary; bullet.TintLink(secondary);
            bullet.Launch(origin, direction, ActiveGem.range);
            if (ignore != null) bullet.Hits.Add(ignore);
            projectiles.Add(bullet);
        }
    }
}
