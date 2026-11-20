using System;

namespace StarFall
{
    public static class CombatMath
    {
        public static float Damage(float baseDamage, float multiplier, float efficiency, float resistancePercent)
            => Math.Max(1f, baseDamage * multiplier * efficiency * (1 - resistancePercent / 100f));
    }
}
