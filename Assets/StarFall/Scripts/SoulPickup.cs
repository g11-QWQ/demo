using UnityEngine;

namespace StarFall
{
    public sealed class SoulPickup : MonoBehaviour
    {
        public int Value { get; private set; }
        public bool Attracted { get; private set; }
        public Vector3 Position => transform.position;
        private float age;

        public void Spawn(Vector3 position, int value)
        {
            position.y = 0.3f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            Value = value;
            Attracted = false;
            age = 0;
            gameObject.SetActive(true);
        }

        // Attraction latches; releasing Space does not drop souls already in flight.
        public bool Step(float delta, Vector3 player, float radius, float speed)
        {
            age += delta;
            player.y = Position.y;
            if (!Attracted && (player - Position).sqrMagnitude <= radius * radius) Attracted = true;
            if (Attracted)
            {
                var direction = player - Position;
                if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(-direction);
                transform.position = Vector3.MoveTowards(Position, player, speed * delta);
                return (player - Position).sqrMagnitude <= 0.25f * 0.25f;
            }
            var p = Position;
            p.y = 0.3f + Mathf.Sin(age * 2.5f) * 0.08f;
            transform.position = p;
            return false;
        }
    }
}
