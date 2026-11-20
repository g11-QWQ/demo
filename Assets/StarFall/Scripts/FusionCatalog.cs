namespace StarFall
{
    public enum FusionKind { None, Judgment, FireTornado, OceanVortex, SteamBurst, Volcano, StormFront, ConductivePool, RailSpear, SandDrill, StoneTomb }
    public static class FusionCatalog
    {
        public static readonly DamageElement[] Primary = { DamageElement.Fire, DamageElement.Fire, DamageElement.Wind, DamageElement.Fire, DamageElement.Fire, DamageElement.Lightning, DamageElement.Lightning, DamageElement.Lightning, DamageElement.Wind, DamageElement.Ocean };
        public static readonly DamageElement[] Secondary = { DamageElement.Lightning, DamageElement.Wind, DamageElement.Ocean, DamageElement.Ocean, DamageElement.Earth, DamageElement.Wind, DamageElement.Ocean, DamageElement.Earth, DamageElement.Earth, DamageElement.Earth };
        private static readonly string[] Titles = { "无结合技", "天罚烙印", "焚风龙卷", "深海涡旋", "沸海蒸爆", "熔岩喷发", "雷暴锋面", "导电潮池", "磁轨岩枪", "碎岩钻风", "沉沦石棺" };
        public static readonly string[] Descriptions = { "刻印目标后落雷", "移动火龙卷与燃烧", "聚怪后推出浪墙", "蒸汽环推敌，冷凝减速", "喷发、熔岩灼烧与岩芯爆裂", "宽幅雷风锋面扫过敌群", "潮池减速，导电逐跳增强", "锁定重击，贯穿后方敌人", "移动钻头集中碎岩打击", "泥沼禁锢后石棺坍缩" };
        public static FusionKind Get(DamageElement a, DamageElement b)
        {
            if (Pair(a, b, DamageElement.Fire, DamageElement.Lightning)) return FusionKind.Judgment;
            if (Pair(a, b, DamageElement.Fire, DamageElement.Wind)) return FusionKind.FireTornado;
            if (Pair(a, b, DamageElement.Wind, DamageElement.Ocean)) return FusionKind.OceanVortex;
            for (int i = 3; i < Primary.Length; i++)
                if (Pair(a, b, Primary[i], Secondary[i])) return (FusionKind)(i + 1);
            return FusionKind.None;
        }
        private static bool Pair(DamageElement a, DamageElement b, DamageElement x, DamageElement y) => (a == x && b == y) || (a == y && b == x);
        public static string Name(FusionKind kind) => Titles[(int)kind];
    }
}
