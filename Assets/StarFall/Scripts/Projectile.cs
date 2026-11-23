using UnityEngine;
using System.Collections.Generic;

namespace StarFall
{
    public sealed class Projectile : MonoBehaviour
    {
        public sealed class Cast { public int Number; public bool ResonanceTriggered, ChargeCredited, LinkTriggered, ArtCredited; public float ChargeValue; }
        public bool Derived, Heavy, Returning, OnReturn;
        public int LinkEvolution;
        public DamageElement SecondaryHit;
        public Cast SourceCast { get; set; }
        public DamageElement Resonance { get; set; }
        public int SpecialRank { get; set; }
        public int SecondaryRank { get; set; }
        public int PiercesLeft { get; set; }
        public float Speed { get; private set; }
        public float Radius { get; private set; }
        public readonly HashSet<EnemyController> Hits = new HashSet<EnemyController>();
        private Renderer[] originalRenderers;
        private LineRenderer accent;
        private LineRenderer linkAccent;
        public Vector3 Position => transform.position;
        public Vector3 Direction { get; private set; }
        public float DistanceLeft { get; private set; }
        public float BaseDamage { get; private set; }
        public float Multiplier { get; private set; }
        public float Efficiency { get; private set; }
        public DamageElement Element { get; private set; }
        public float BurnBaseDamage { get; private set; }

        public void Configure(GemDefinition gem, StaffDefinition staff, float fireEfficiencyBonus = 0, float directMultiplier = 1, float burnBaseDamage = 0)
        {
            BaseDamage = gem.baseDamage;
            Multiplier = gem.attackMultiplier * directMultiplier;
            Efficiency = staff.EfficiencyFor(gem.element) + fireEfficiencyBonus;
            Element = gem.element;
            BurnBaseDamage = gem.element == DamageElement.Fire ? burnBaseDamage : 0;
            Speed = gem.projectileSpeed; Radius = gem.projectileRadius;
            Hits.Clear(); SpecialRank = SecondaryRank = PiercesLeft = 0; Resonance = DamageElement.Physical;
            Derived = Heavy = Returning = OnReturn = false; LinkEvolution = 0;
            SecondaryHit = DamageElement.Physical; if (linkAccent != null) linkAccent.enabled = false;
            SetVisual(gem.element);
        }
        public void AddBaseDamage(float value) => BaseDamage += value;
        public void MakeLink(DamageElement element, float damage, float power, float speed)
        { Element = element; BaseDamage = damage; Multiplier = 1; Efficiency = power; Speed = speed; BurnBaseDamage = element == DamageElement.Fire ? 2 : 0; SetVisual(element); }
        public void TintLink(DamageElement secondary)
        {
            if (linkAccent == null)
            {
                var go = new GameObject("LinkRibbon"); go.transform.SetParent(transform, false);
                linkAccent = go.AddComponent<LineRenderer>(); linkAccent.useWorldSpace = false;
                linkAccent.sharedMaterial = originalRenderers[0].sharedMaterial;
            }
            var color = ElementCatalog.ColorFor(secondary); var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color);
            linkAccent.SetPropertyBlock(block); linkAccent.enabled = true; linkAccent.widthMultiplier = 0.055f; linkAccent.positionCount = 12;
            for (int i = 0; i < 12; i++) linkAccent.SetPosition(i, new Vector3(Mathf.Sin(i * 0.8f) * 0.28f, 0.1f, -i * 0.12f));
        }
        public void Attenuate() => Multiplier *= 0.75f;
        public void BeginReturn(Vector3 target) { OnReturn = true; Hits.Clear(); DistanceLeft = 24; AimReturn(target); }
        public void AimReturn(Vector3 target) { var d = target - Position; d.y = 0; if (d.magnitude < 0.7f) DistanceLeft = 0; else Direction = d.normalized; }
        private void SetVisual(DamageElement element)
        {
            if (originalRenderers == null) originalRenderers = GetComponentsInChildren<Renderer>();
            Color color = ElementCatalog.ColorFor(element);
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color);
            foreach (var renderer in originalRenderers)
            {
                renderer.enabled = element != DamageElement.Wind && (element == DamageElement.Fire || renderer.name != "FlameTail");
                renderer.SetPropertyBlock(block);
            }
            transform.localScale = element == DamageElement.Earth ? new Vector3(0.85f, 0.85f, 2.7f) : element == DamageElement.Ocean ? new Vector3(1, 0.8f, 1.6f) : Vector3.one;
            if (accent == null)
            {
                var child = new GameObject("ElementAccent"); child.transform.SetParent(transform, false);
                accent = child.AddComponent<LineRenderer>(); accent.useWorldSpace = false;
                accent.sharedMaterial = originalRenderers[0].sharedMaterial; accent.widthMultiplier = 0.09f;
            }
            accent.enabled = element == DamageElement.Wind || element == DamageElement.Lightning;
            accent.SetPropertyBlock(block); accent.startColor = accent.endColor = color; accent.positionCount = 9;
            for (int i = 0; i < 9; i++)
            {
                float angle = (i / 8f - 0.5f) * Mathf.PI;
                accent.SetPosition(i, element == DamageElement.Wind ? new Vector3(Mathf.Sin(angle) * 0.65f, 0, Mathf.Cos(angle) * 0.35f)
                    : new Vector3(i % 2 == 0 ? -0.14f : 0.14f, 0, -i * 0.13f));
            }
        }

        public void Launch(Vector3 origin, Vector3 direction, float range)
        {
            transform.position = origin;
            Direction = direction.normalized;
            if (Direction.sqrMagnitude > 0) transform.rotation = Quaternion.LookRotation(Direction);
            DistanceLeft = range;
            gameObject.SetActive(true);
        }

        public Vector3 NextPosition(float distance) => Position + Direction * Mathf.Min(distance, DistanceLeft);
        public void MoveTo(Vector3 position)
        {
            DistanceLeft -= Vector3.Distance(Position, position);
            transform.position = position;
        }

        // Earliest segment/circle contact, including a projectile born inside a target.
        public static bool Sweep(Vector3 start, Vector3 end, Vector3 center, float radius, out float time)
        {
            start.y = end.y = center.y = 0;
            var offset = start - center;
            var motion = end - start;
            float c = Vector3.Dot(offset, offset) - radius * radius;
            time = 0;
            if (c <= 0) return true;
            float a = Vector3.Dot(motion, motion);
            if (a < 0.000001f) return false;
            float b = Vector3.Dot(offset, motion);
            float discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            time = (-b - Mathf.Sqrt(discriminant)) / a;
            return time >= 0 && time <= 1;
        }
    }
}
