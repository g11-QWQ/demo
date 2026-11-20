using UnityEngine;

namespace StarFall
{
    [CreateAssetMenu(menuName = "StarFall/Character Stats")]
    public sealed class CharacterStats : ScriptableObject
    {
        [Min(1)] public float health = 100;
        [Min(0)] public float moveSpeed = 6;
        [Min(0)] public float soulCollectionRadius = 3;
        [Range(0, 100)] public float allElementResistancePercent = 20;
    }
}
