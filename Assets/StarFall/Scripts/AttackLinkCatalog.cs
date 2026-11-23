namespace StarFall
{
    public static class AttackLinkCatalog
    {
        public static readonly string[] Names = { "烙火电花", "分岔焰刃", "回旋潮刃", "沸腾泡珠", "熔芯碎片", "跃迁电刃", "潮印传电", "磁晶追击", "砂晶散射", "凝晶潮刺" };
        private static readonly string[] Mechanics = {
            "命中同一目标3轮：连锁雷击并引燃。", "每第三轮命中向前分出两枚焰刃，\n火风混合伤害。", "每第三轮命中发射一枚回旋潮刃，\n去程和回程各命中一次。", "每第三轮命中附着沸腾泡，\n延迟破裂；燃烧目标更快引爆。", "每第三轮命中留下熔芯碎片，\n短暂持续火焰与岩伤。", "每第三轮命中释放跳跃电刃，\n最多折向另外两名敌人。", "每第三轮命中附加潮印减速，\n传电至附近受减速的敌人。", "每第三轮命中留下磁晶，\n延迟追击原目标，造成岩雷混伤。", "每第三轮命中向前散射砂晶，\n近处集中、远处散开。", "每第三轮命中留下小晶簇，\n延迟刺出并短暂禁锢。" };
        private static readonly string[] EvolutionNames = {
            "电花蔓延", "集中烙印", "余烬回响", "燎原", "贯焰", "回燃", "三重潮刃", "重压潮刃", "疾返潮刃",
            "泡珠扩散", "高压沸泡", "二重破泡", "熔片铺地", "致密熔芯", "余热复燃", "电刃漫游", "双点雷击", "回跳电刃",
            "潮印网络", "高压导电", "余电回流", "散晶追击", "重型磁晶", "双晶追击", "砂暴扇面", "贯风砂枪", "回旋砂晶",
            "晶簇丛生", "重晶刺", "二重晶刺" };
        public static string Name(FusionKind kind) => kind == FusionKind.None ? "未获得联动" : Names[(int)kind - 1];
        public static BuildDefinition Present(BuildId id, BuildProgression builds)
        {
            var d = BuildCatalog.Get(id);
            var kind = FusionCatalog.Get(builds.Element, builds.Resonance);
            if (BuildCatalog.IsResonance(id))
            {
                kind = FusionCatalog.Get(builds.Element, BuildCatalog.ResonanceElement(id));
                return new BuildDefinition(id, Name(kind), ElementCatalog.Names[(int)BuildCatalog.ResonanceElement(id)] + "联动", 1,
                    Mechanics[(int)kind - 1] + "\n\n" + LinkArtCatalog.Names[(int)kind - 1] + "\n" + LinkArtCatalog.Descriptions[(int)kind - 1]);
            }
            if (id >= BuildId.EvolveSpread && id <= BuildId.EvolveEcho && kind != FusionKind.None)
            {
                int variant = (int)id - (int)BuildId.EvolveSpread;
                string description;
                if (kind == FusionKind.FireTornado || kind == FusionKind.SandDrill)
                    description = variant == 0 ? "分裂增加至4枚，扩大扇面，\n单枚伤害降低。" : variant == 1 ? "每4轮生成一枚重型贯穿弹，\n额外贯穿3名敌人。" : "分裂弹会返回，\n回程可以再次命中。";
                else if (kind == FusionKind.Judgment)
                    description = variant == 0 ? "电弧最多连接4名额外敌人，\n每跳伤害降低。" : variant == 1 ? "2轮命中触发一次集中雷击，\n取消额外跳跃。" : "电花触发后追加一次\n延迟的火雷追击。";
                else if (kind == FusionKind.OceanVortex)
                    description = variant == 0 ? "回旋潮刃改为三枚扇射，\n单枚伤害降低。" : variant == 1 ? "每4轮释放一枚强力潮刃，\n集中造成更高伤害。" : "回旋潮刃速度提高50%，\n更快完成去程与回程打击。";
                else if (kind == FusionKind.Volcano && variant == 2) description = "熔芯额外灼烧2次。";
                else description = variant == 0 ? "扩大联动范围或目标数，\n单次伤害降低。" : variant == 1 ? "联动改为每4轮触发，\n减少范围、提高单体伤害。" : "增加一次延迟追击。";
                return new BuildDefinition(id, EvolutionNames[((int)kind - 1) * 3 + variant], d.Route, 1, description);
            }
            return d;
        }
    }
}
