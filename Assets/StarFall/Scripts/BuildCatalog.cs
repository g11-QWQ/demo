using System;
using System.Collections.Generic;

namespace StarFall
{
    public enum BuildId { SwiftCasting, FireAffinity, Vitality, Embers, SoulAttraction, SoulFire, LightSteps, SoulRecovery, FlameBurst, ThunderArea, ThunderChain, WindFan, WindPierce, OceanChill, OceanPressure, EarthCore, EarthMark, ResFire, ResLightning, ResWind, ResOcean, ResEarth }

    public sealed class BuildDefinition
    {
        public BuildId Id { get; }
        public string Name { get; }
        public string Route { get; }
        public int MaxRank { get; }
        public string Description { get; }
        public BuildDefinition(BuildId id, string name, string route, int maxRank, string description)
        { Id = id; Name = name; Route = route; MaxRank = maxRank; Description = description; }
    }

    public static class BuildCatalog
    {
        public static IReadOnlyList<BuildDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new BuildDefinition(BuildId.SwiftCasting, "迅捷咏唱", "持续施法", 3, "攻击频率提高基础值的20%。\n按当前宝石的基础施法频率计算。"),
            new BuildDefinition(BuildId.FireAffinity, "元素亲和", "元素强化", 3, "法杖全元素效能增加20个百分点。\n增强直接伤害和元素衍生伤害。"),
            new BuildDefinition(BuildId.Vitality, "强健体魄", "生存续航", 3, "最大生命增加20，\n同时恢复20点生命。"),
            new BuildDefinition(BuildId.Embers, "余烬", "燃烬蔓延", 3, "基础燃烧每秒伤害增加1/2/3。\n持续3秒，刷新时长，不叠层。"),
            new BuildDefinition(BuildId.SoulAttraction, "灵魂牵引", "灵魂共鸣", 2, "主动吸魂范围倍率\n增加50个百分点。\n灵魂飞行速度提高基础值的20%。"),
            new BuildDefinition(BuildId.SoulFire, "灵魂灌注", "灵魂共鸣", 1, "主动吸魂每到账1点经验，\n获得1层魂火，最多5层。\n每次施法消耗1层，该次施法直接伤害提高30%。"),
            new BuildDefinition(BuildId.LightSteps, "轻灵步伐", "生存续航", 3, "移动速度增加角色基础值的8%。\n每级增加0.48单位/秒。"),
            new BuildDefinition(BuildId.SoulRecovery, "魂息回流", "生存续航", 1, "获得后每累计收集10点灵魂经验，\n恢复2点生命。\n满血时消耗该次治疗，不储存。"),
            new BuildDefinition(BuildId.FlameBurst, "烈焰迸发", "火焰专属", 3, "对燃烧目标的火球直接伤害\n每级提高15%。"),
            new BuildDefinition(BuildId.ThunderArea, "雷域扩张", "雷电专属", 3, "雷爆半径每级增加15%。"),
            new BuildDefinition(BuildId.ThunderChain, "电弧跳跃", "雷电专属", 2, "雷爆额外连接1/2个范围外目标。\n每次造成50%伤害，不重复命中。"),
            new BuildDefinition(BuildId.WindFan, "展开翼面", "风专属", 2, "每轮增加1/2枚弱风刃。\n新增风刃伤害为基础的60%。"),
            new BuildDefinition(BuildId.WindPierce, "穿林风", "风专属", 2, "风刃额外穿透1/2个目标。\n每次穿透后伤害降低25%。"),
            new BuildDefinition(BuildId.OceanChill, "寒潮", "海洋专属", 3, "减速强度每级增加10个百分点。\nBoss受到的减速减半。"),
            new BuildDefinition(BuildId.OceanPressure, "深水压力", "海洋专属", 3, "对已受减速或禁锢的目标\n每级增加15%直接伤害。"),
            new BuildDefinition(BuildId.EarthCore, "岩芯", "大地专属", 3, "岩枪基础伤害每级增加4。"),
            new BuildDefinition(BuildId.EarthMark, "破甲印记", "大地专属", 3, "岩枪命中叠加印记，最多3层。\n每层使后续岩枪伤害增加5%/10%/15%，持续4秒。"),
            new BuildDefinition(BuildId.ResFire, "火焰共鸣", "元素共鸣", 1, "雷电：解锁天罚烙印。\n风：解锁焚风龙卷。\n有效命中充能，满能量施法释放。"),
            new BuildDefinition(BuildId.ResLightning, "雷电共鸣", "元素共鸣", 1, "火焰：解锁天罚烙印。\n锁定目标、蓄势落雷和火焰冲击。\n有效命中充能，满能量施法释放。"),
            new BuildDefinition(BuildId.ResWind, "风之共鸣", "元素共鸣", 1, "火焰：解锁焚风龙卷。\n海洋：解锁深海涡旋。\n有效命中充能，满能量施法释放。"),
            new BuildDefinition(BuildId.ResOcean, "海洋共鸣", "元素共鸣", 1, "风：解锁深海涡旋，充能释放。\n大地：每第三次施法生成泥沼。\n每局只能选择一种共鸣。"),
            new BuildDefinition(BuildId.ResEarth, "大地共鸣", "元素共鸣", 1, "海洋：每第三次施法命中生成泥沼，\n减速并短暂禁锢停留者。\n每局只能选择一种共鸣。")
        });

        public static BuildDefinition Get(BuildId id)
        {
            foreach (var item in All) if (item.Id == id) return item;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
        public static bool IsResonance(BuildId id) => id >= BuildId.ResFire && id <= BuildId.ResEarth;
        public static DamageElement ResonanceElement(BuildId id) => IsResonance(id) ? (DamageElement)((int)id - (int)BuildId.ResFire + 1) : DamageElement.Physical;
        public static DamageElement ExclusiveElement(BuildId id)
        {
            switch (id)
            {
                case BuildId.Embers: case BuildId.FlameBurst: return DamageElement.Fire;
                case BuildId.ThunderArea: case BuildId.ThunderChain: return DamageElement.Lightning;
                case BuildId.WindFan: case BuildId.WindPierce: return DamageElement.Wind;
                case BuildId.OceanChill: case BuildId.OceanPressure: return DamageElement.Ocean;
                case BuildId.EarthCore: case BuildId.EarthMark: return DamageElement.Earth;
                default: return DamageElement.Physical;
            }
        }
    }
}



