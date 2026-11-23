using System.Collections.Generic;
using UnityEngine;

namespace StarFall
{
    public sealed partial class BattleController
    {
        private sealed class LinkArt
        {
            public FusionKind Kind;
            public Vector3 Origin, Direction;
            public EnemyController Target;
            public int Generation, Strikes;
            public float Age, Power;
            public FusionVisual Visual;
        }
        private readonly List<LinkArt> linkArts = new List<LinkArt>();
        private float artProgress;
        public int LinkArtCasts { get; private set; }
        public int ActiveLinkArts => linkArts.Count;
        public float LinkArtProgress => CurrentFusion == FusionKind.None ? 0 : Mathf.Clamp01(artProgress / LinkArtCatalog.Threshold(CurrentFusion));
        private void ClearLinkArts()
        {
            foreach (var art in linkArts) { art.Visual.gameObject.SetActive(false); fusionPool.Push(art.Visual); }
            linkArts.Clear(); artProgress = 0; LinkArtCasts = 0;
        }
        private void CreditLinkArt(Projectile bullet)
        {
            if (bullet.Derived || bullet.SourceCast == null || bullet.SourceCast.ArtCredited || CurrentFusion == FusionKind.None) return;
            bullet.SourceCast.ArtCredited = true;
            if (LinkArtCatalog.UsesHits(CurrentFusion)) artProgress = Mathf.Min(LinkArtCatalog.Threshold(CurrentFusion), artProgress + 1);
        }
        private EnemyController ArtTarget()
        {
            EnemyController result = null; float best = 144;
            foreach (var enemy in enemies)
            {
                float distance = (enemy.Position - player.Position).sqrMagnitude;
                if (distance < best && navigation.ClearLine(player.Position, enemy.Position, 0)) { best = distance; result = enemy; }
            }
            return result;
        }
        private void StepLinkArts(float delta, float moved)
        {
            var kind = CurrentFusion;
            var target = kind == FusionKind.None ? null : ArtTarget();
            if (target != null)
            {
                float threshold = LinkArtCatalog.Threshold(kind);
                if (!LinkArtCatalog.UsesHits(kind)) artProgress = Mathf.Min(threshold, artProgress + (LinkArtCatalog.UsesMovement(kind) ? moved : delta));
                if (artProgress >= threshold && !player.IsAbsorbing && linkArts.Count < 8)
                {
                    var art = new LinkArt { Kind = kind, Origin = player.Position, Direction = player.AimDirection, Target = target, Generation = target.Generation,
                        Power = FireEfficiency * AttackPower, Visual = AcquireFusionVisual() };
                    if (kind == FusionKind.Judgment || kind == FusionKind.SteamBurst || kind == FusionKind.ConductivePool || kind == FusionKind.StoneTomb) art.Origin = target.Position;
                    art.Origin.y = 0.12f; art.Visual.transform.localScale = Vector3.one;
                    linkArts.Add(art); artProgress = 0; LinkArtCasts++;
                }
            }
            for (int i = linkArts.Count - 1; i >= 0; i--)
            {
                var art = linkArts[i]; art.Age += delta;
                int count = ArtStrikeCount(art.Kind);
                if (art.Strikes < count && art.Age >= 0.2f + art.Strikes * 0.22f)
                {
                    StrikeArt(art, art.Strikes); art.Strikes++;
                }
                art.Visual.DrawLinkArt(art.Kind, art.Age, art.Kind == FusionKind.SandDrill ? player.Position : art.Origin, art.Direction, 1.8f);
                if (art.Age < 1.8f) continue;
                art.Visual.gameObject.SetActive(false); fusionPool.Push(art.Visual); linkArts.RemoveAt(i);
            }
        }
        private static int ArtStrikeCount(FusionKind kind) => kind == FusionKind.SteamBurst || kind == FusionKind.Volcano ? 5 : kind == FusionKind.StoneTomb ? 6 : kind == FusionKind.OceanVortex ? 2 : 3;
        private void StrikeArt(LinkArt art, int strike)
        {
            int index = (int)art.Kind - 1;
            Vector3 center = art.Origin;
            float radius = 2.8f, damage = 12;
            bool corridor = false;
            switch (art.Kind)
            {
                case FusionKind.Judgment: damage = 14; break;
                case FusionKind.FireTornado: center += art.Direction * (2 + strike * 2.2f); radius = 2; break;
                case FusionKind.OceanVortex: radius = 3.5f + strike * 2; damage = 18; break;
                case FusionKind.SteamBurst: center += Quaternion.Euler(0, strike * 72, 0) * Vector3.forward * 1.8f; radius = 1.8f; damage = 16; break;
                case FusionKind.Volcano: center += art.Direction * (1.5f + strike * 1.5f); radius = 1.6f; damage = 18; break;
                case FusionKind.StormFront: corridor = true; damage = 14; break;
                case FusionKind.ConductivePool: radius = 2.5f + strike * 0.7f; damage = 12; break;
                case FusionKind.RailSpear:
                    if (art.Target == null || art.Target.Generation != art.Generation || !enemies.Contains(art.Target) || !navigation.ClearLine(art.Origin, art.Target.Position, 0)) return;
                    LinkDamage(art.Target, 28, DamageElement.Earth, art.Power);
                    LinkDamage(art.Target, 12, DamageElement.Lightning, art.Power);
                    Arc(art.Origin + Vector3.right * (strike - 1), art.Target.Position); return;
                case FusionKind.SandDrill: center = player.Position; radius = 3.4f; damage = 15; break;
                case FusionKind.StoneTomb: center += Quaternion.Euler(0, strike * 60, 0) * Vector3.forward * 1.8f; radius = 1.8f; damage = 15; break;
            }
            foreach (var enemy in enemies.ToArray())
            {
                var offset = enemy.Position - center; offset.y = 0;
                bool inside = corridor ? Vector3.Dot(offset, art.Direction) >= 0 && Vector3.Dot(offset, art.Direction) <= 9 && Vector3.Cross(offset, art.Direction).magnitude <= 1.6f : offset.sqrMagnitude <= radius * radius;
                if (!inside || !navigation.ClearLine(art.Origin, enemy.Position, 0) || !navigation.ClearLine(center, enemy.Position, 0)) continue;
                LinkDamage(enemy, damage * 0.65f, FusionCatalog.Primary[index], art.Power);
                LinkDamage(enemy, damage * 0.35f, FusionCatalog.Secondary[index], art.Power);
                if (enemy.Health <= 0) continue;
                if (art.Kind == FusionKind.Judgment || art.Kind == FusionKind.FireTornado || art.Kind == FusionKind.Volcano) enemy.ApplyBurn(CombatMath.Damage(3, 1, art.Power, enemy.ResistanceFor(DamageElement.Fire)));
                if (art.Kind == FusionKind.OceanVortex || art.Kind == FusionKind.ConductivePool) enemy.ApplySlow(0.4f, 2);
                if (art.Kind == FusionKind.StoneTomb) enemy.ApplyRoot(0.6f);
            }
            Pulse(center, radius, FusionCatalog.Primary[index]);
        }
    }
}
