namespace StarFall
{
    public static class LinkArtCatalog
    {
        public static readonly string[] Names = { "雷焰星冠", "焰翼巡游", "踏浪双环", "沸星连爆", "熔晶地脉", "闪岚穿阵", "潮雷脉冲", "三棱磁矛", "环身砂刃", "潮晶花开" };
        public static readonly string[] Descriptions = {
            "命中6轮：星冠连续落下3次雷焰。", "战斗6秒：三道焰翼向前扫荡。", "移动16米：向外推出两圈潮刃。",
            "命中6轮：五颗沸星依次炸开。", "战斗7秒：五簇熔晶沿瞄准方向喷发。", "移动18米：雷风折线贯穿前方。",
            "战斗6秒：三重潮雷波减速敌群。", "命中5轮：三支磁矛追击同一目标。", "移动16米：砂刃环绕切割三次。", "战斗7秒：六瓣潮晶依次绽放并禁锢。"
        };
        public static bool UsesHits(FusionKind kind) => kind == FusionKind.Judgment || kind == FusionKind.SteamBurst || kind == FusionKind.RailSpear;
        public static bool UsesMovement(FusionKind kind) => kind == FusionKind.OceanVortex || kind == FusionKind.StormFront || kind == FusionKind.SandDrill;
        public static float Threshold(FusionKind kind) => UsesHits(kind) ? (kind == FusionKind.RailSpear ? 5 : 6) : UsesMovement(kind) ? (kind == FusionKind.StormFront ? 18 : 16) : (kind == FusionKind.Volcano || kind == FusionKind.StoneTomb ? 7 : 6);
    }
}
