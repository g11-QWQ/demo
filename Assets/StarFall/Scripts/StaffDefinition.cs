using UnityEngine;

namespace StarFall
{
    [CreateAssetMenu(menuName = "StarFall/Staff")]
    public sealed class StaffDefinition : ScriptableObject
    {
        public string displayName = "基础法杖";
        [Min(1)] public int gemSlots = 1;
        [Tooltip("倍率：1 = 100%")][Min(0)] public float allElementEfficiency = 1;
        [Min(0)] public float soulCollectionMultiplier = 2.25f;
        [Min(0)] public float moveSpeedMultiplier = 1;
        public float EfficiencyFor(DamageElement element) => element == DamageElement.Physical ? 1 : allElementEfficiency;
    }
}
