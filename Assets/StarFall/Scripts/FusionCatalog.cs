namespace StarFall
{
    public enum FusionKind { None, Judgment, FireTornado, OceanVortex }
    public static class FusionCatalog
    {
        public static FusionKind Get(DamageElement a, DamageElement b)
        {
            if (Pair(a, b, DamageElement.Fire, DamageElement.Lightning)) return FusionKind.Judgment;
            if (Pair(a, b, DamageElement.Fire, DamageElement.Wind)) return FusionKind.FireTornado;
            if (Pair(a, b, DamageElement.Wind, DamageElement.Ocean)) return FusionKind.OceanVortex;
            return FusionKind.None;
        }
        private static bool Pair(DamageElement a, DamageElement b, DamageElement x, DamageElement y) => (a == x && b == y) || (a == y && b == x);
        public static string Name(FusionKind kind) => kind == FusionKind.Judgment ? "天罚烙印" : kind == FusionKind.FireTornado ? "焚风龙卷" : kind == FusionKind.OceanVortex ? "深海涡旋" : "无结合技";
    }
}
