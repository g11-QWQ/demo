using UnityEngine;

namespace StarFall
{
    public enum EnemyKind { Normal, MiniBoss, FinalBoss }
    public sealed class EnemyController : MonoBehaviour
    {
        public EnemyKind Kind { get; private set; }
        public float Radius { get; private set; }
        public float Speed { get; private set; }
        public float Resistance { get; private set; }
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public int Generation { get; private set; }
        public System.Action<Vector3, float, DamageElement> Damaged;
        public Vector3 PreviousPosition { get; private set; }
        public Vector3 Position => transform.position;
        private float contactRemaining;
        private float burnRemaining, burnTickRemaining, burnDamage;
        private Renderer body;
        private MaterialPropertyBlock properties;
        private Color baseColor;
        public bool IsBurning => burnRemaining > 0;
        public float BurnRemaining => burnRemaining;
        public float SlowRemaining { get; private set; }
        public float SlowStrength { get; private set; }
        public float RootRemaining { get; private set; }
        private float rootImmunity, markRemaining;
        public int EarthMarks { get; private set; }
        private LineRenderer burnRing, controlRing;
        public float ResistanceFor(DamageElement element) => element == DamageElement.Physical ? 0 : Kind == EnemyKind.FinalBoss && element != DamageElement.Fire ? 0 : Resistance;
        public void ApplySlow(float strength, float duration)
        {
            if (Kind != EnemyKind.Normal) strength *= 0.5f;
            if (SlowRemaining > 0 && strength < SlowStrength) return;
            if (SlowRemaining <= 0 || strength > SlowStrength) { SlowStrength = strength; SlowRemaining = duration; }
            else SlowRemaining = Mathf.Max(SlowRemaining, duration);
        }
        public void ApplyRoot(float duration)
        {
            if (rootImmunity > 0) return;
            RootRemaining = Kind == EnemyKind.Normal ? duration : duration * 0.25f;
            rootImmunity = 4;
        }
        public void AddEarthMark() { EarthMarks = Mathf.Min(3, EarthMarks + 1); markRemaining = 4; }
        public void PrepareTrainingTarget(float health, float speed) { MaxHealth = Health = health; Speed = speed; PreviousPosition = Position; }

        public void Spawn(Vector3 position, BattleConfig config, EnemyKind kind = EnemyKind.Normal)
        {
            Generation++;
            transform.position = position;
            PreviousPosition = position;
            Kind = kind;
            Radius = kind == EnemyKind.Normal ? config.enemyRadius : kind == EnemyKind.MiniBoss ? config.miniBossRadius : config.finalBossRadius;
            Speed = kind == EnemyKind.Normal ? config.enemySpeed : kind == EnemyKind.MiniBoss ? config.miniBossSpeed : config.finalBossSpeed;
            Resistance = kind == EnemyKind.FinalBoss ? config.finalBossFireResistance : config.enemyElementResistancePercent;
            MaxHealth = Health = kind == EnemyKind.Normal ? config.enemyHealth : kind == EnemyKind.MiniBoss ? config.miniBossHealth : config.finalBossHealth;
            transform.localScale = Vector3.one * (Radius / config.enemyRadius);
            contactRemaining = 0;
            SlowRemaining = SlowStrength = RootRemaining = rootImmunity = markRemaining = 0; EarthMarks = 0;
            burnRemaining = burnTickRemaining = burnDamage = 0;
            SetBurnVisual(false);
            gameObject.SetActive(true);
        }

        public void Step(float delta, Vector3 target, BattleConfig config, ArenaNavigation navigation = null)
        {
            PreviousPosition = Position;
            contactRemaining = Mathf.Max(0, contactRemaining - delta);
            SlowRemaining = Mathf.Max(0, SlowRemaining - delta); RootRemaining = Mathf.Max(0, RootRemaining - delta);
            rootImmunity = Mathf.Max(0, rootImmunity - delta); markRemaining -= delta;
            if (markRemaining <= 0) EarthMarks = 0;
            float movementSpeed = RootRemaining > 0 ? 0 : Speed * (SlowRemaining > 0 ? 1 - SlowStrength : 1);
            UpdateStatusRings();
            target.y = Position.y;
            transform.position = navigation == null ? Vector3.MoveTowards(Position, target, movementSpeed * delta) : navigation.Move(Position, navigation.Direction(Position, target, Radius) * movementSpeed * delta, Radius);
            var direction = target - Position;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        public bool CanContact(PlayerController player, BattleConfig config)
        {
            var direction = Position - player.Position;
            direction.y = 0;
            float radius = config.playerRadius + Radius;
            if (contactRemaining > 0 || direction.sqrMagnitude > radius * radius) return false;
            contactRemaining = config.contactCooldown;
            return true;
        }

        public bool TakeDamage(float amount, DamageElement element = DamageElement.Physical)
        {
            if (Health <= 0) return true;
            if (amount <= 0) return false;
            Health = Mathf.Max(0, Health - amount);
            Damaged?.Invoke(Position, amount, element);
            return Health == 0;
        }

        public void ApplyBurn(float damagePerSecond)
        {
            if (Health <= 0 || damagePerSecond <= 0) return;
            if (!IsBurning) burnTickRemaining = 1;
            burnRemaining = 3;
            burnDamage = damagePerSecond;
            SetBurnVisual(true);
        }

        public bool StepBurn(float delta)
        {
            if (!IsBurning || delta <= 0) return false;
            float activeTime = Mathf.Min(delta, burnRemaining);
            burnRemaining = Mathf.Max(0, burnRemaining - activeTime);
            burnTickRemaining -= activeTime;
            // Refreshing duration never delays the next tick, even under rapid repeated hits.
            while (burnTickRemaining <= 0.00001f)
            {
                burnTickRemaining += 1;
                if (TakeDamage(burnDamage, DamageElement.Fire)) { burnRemaining = 0; return true; }
            }
            if (!IsBurning) SetBurnVisual(false);
            return false;
        }

        private void SetBurnVisual(bool enabled)
        {
            if (body == null)
            {
                var child = transform.Find("Body");
                if (child == null) return;
                body = child.GetComponent<Renderer>();
                properties = new MaterialPropertyBlock();
                baseColor = body.sharedMaterial.GetColor("_BaseColor");
            }
            properties.SetColor("_BaseColor", Kind == EnemyKind.MiniBoss ? new Color(1, 0.68f, 0.12f) : Kind == EnemyKind.FinalBoss ? new Color(0.65f, 0.15f, 0.9f) : baseColor);
            body.SetPropertyBlock(properties);
            UpdateStatusRings();
        }
        private void UpdateStatusRings()
        {
            if (body == null) return;
            if (burnRing == null) burnRing = MakeRing("BurnRing", ElementCatalog.ColorFor(DamageElement.Fire), 0.7f);
            if (controlRing == null) controlRing = MakeRing("ControlRing", new Color(0.15f, 0.55f, 1), 0.85f);
            burnRing.enabled = IsBurning; controlRing.enabled = SlowRemaining > 0 || RootRemaining > 0;
        }
        private LineRenderer MakeRing(string label, Color color, float radius)
        {
            var child = new GameObject(label); child.transform.SetParent(transform, false);
            var ring = child.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true;
            ring.sharedMaterial = body.sharedMaterial; ring.widthMultiplier = 0.07f;
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", color); ring.SetPropertyBlock(block);
            ring.positionCount = 24;
            for (int i = 0; i < 24; i++) { float angle = i * Mathf.PI * 2 / 24; ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, -0.4f, Mathf.Sin(angle) * radius)); }
            return ring;
        }
    }
}

