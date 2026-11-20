using UnityEngine;

namespace StarFall
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform facing;
        [SerializeField] private Transform staffVisual;
        [SerializeField] private LineRenderer collectionRing;
        private float hurtRemaining;
        public Vector3 Position => transform.position;
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public bool IsAbsorbing { get; private set; }
        public void SetGemColor(DamageElement element)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "FireGem")
                {
                    var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", ElementCatalog.ColorFor(element)); renderer.SetPropertyBlock(block);
                }
        }

        public void ResetActor()
        {
            transform.SetPositionAndRotation(new Vector3(0, 0.6f, 0), Quaternion.identity);
            hurtRemaining = 0;
            transform.localScale = Vector3.one;
            AimDirection = Vector3.forward;
            Aim(AimDirection);
            SetAbsorbing(false, 0);
        }

        public void Move(Vector2 input, float delta, BattleConfig config, float bonusSpeed = 0, ArenaNavigation navigation = null)
        {
            hurtRemaining = Mathf.Max(0, hurtRemaining - delta);
            var movement = Vector2.ClampMagnitude(input, 1) * ((config.playerSpeed + bonusSpeed) * delta);
            var p = Position + new Vector3(movement.x, 0, movement.y);
            var half = config.arenaSize * 0.5f - Vector2.one * config.playerRadius;
            p.x = Mathf.Clamp(p.x, -half.x, half.x);
            p.z = Mathf.Clamp(p.z, -half.y, half.y);
            transform.position = navigation == null ? p : navigation.Move(Position, new Vector3(movement.x, 0, movement.y), config.playerRadius);
            var pulse = hurtRemaining > 0 ? 1 + Mathf.Sin(hurtRemaining * 50) * 0.08f : 1;
            transform.localScale = Vector3.one * pulse;
        }

        public void Aim(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude <= 0.0001f) return;
            AimDirection = direction.normalized;
            if (facing != null) facing.rotation = Quaternion.LookRotation(AimDirection);
        }

        public void SetAbsorbing(bool absorbing, float radius)
        {
            IsAbsorbing = absorbing;
            if (staffVisual != null)
            {
                staffVisual.localRotation = Quaternion.Euler(absorbing ? 15 : 55, 0, 0);
                staffVisual.localPosition = new Vector3(0.55f, absorbing ? 0.6f : 0, 0.1f);
            }
            if (collectionRing == null) return;
            collectionRing.gameObject.SetActive(absorbing);
            if (!absorbing) return;
            for (int i = 0; i < collectionRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / collectionRing.positionCount;
                collectionRing.SetPosition(i, new Vector3(Position.x + Mathf.Cos(angle) * radius, 0.03f, Position.z + Mathf.Sin(angle) * radius));
            }
        }

        public bool TryHurt(MatchSession session, BattleConfig config)
        {
            return TryDamage(session, config, config.contactDamage, config.contactElement);
        }

        public bool TryDamage(MatchSession session, BattleConfig config, float amount, DamageElement element)
        {
            float resistance = element == DamageElement.Physical ? 0 : config.character.allElementResistancePercent;
            float damage = CombatMath.Damage(amount, 1, 1, resistance);
            if (hurtRemaining > 0 || !session.Damage(damage)) return false;
            hurtRemaining = config.hurtGraceSeconds;
            return true;
        }
    }
}

