using UnityEngine;

namespace StarFall
{
    public enum DamageElement { Physical, Fire, Lightning, Wind, Ocean, Earth }

    [CreateAssetMenu(menuName = "StarFall/Gem")]
    public sealed class GemDefinition : ScriptableObject
    {
        public string displayName = "火宝石";
        public string attackName = "火球术";
        public DamageElement element = DamageElement.Fire;
        [Min(0)] public float baseDamage = 10;
        [Min(0)] public float attackMultiplier = 0.8f;
        [Min(0.1f)] public float attacksPerSecond = 2;
        [Min(0.1f)] public float projectileSpeed = 18;
        [Min(0.1f)] public float range = 18;
        [Min(0.01f)] public float projectileRadius = 0.23f;
    }
}
