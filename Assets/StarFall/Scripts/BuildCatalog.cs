using System;
using System.Collections.Generic;

namespace StarFall
{
    public enum BuildId { SwiftCasting, FireAffinity, Vitality, Embers, SoulAttraction, SoulFire, LightSteps, SoulRecovery, FlameBurst, ThunderArea, ThunderChain, WindFan, WindPierce, OceanChill, OceanPressure, EarthCore, EarthMark, ResFire, ResLightning, ResWind, ResOcean, ResEarth, FormPierce, FormFan, FormHeavy, EvolveSpread, EvolveFocus, EvolveEcho, UltimateBurst, UltimateField, UltimateCycle, MasterAttack, MasterUltimate, MasterHarmony }

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
            new BuildDefinition(BuildId.SwiftCasting, "迅捷咏唱", "持续施法", 3, "攻击频率 +20%。"),
            new BuildDefinition(BuildId.FireAffinity, "元素亲和", "元素强化", 3, "全元素效能 +20%。"),
            new BuildDefinition(BuildId.Vitality, "强健体魄", "生存续航", 3, "生命上限 +20，恢复20生命。"),
            new BuildDefinition(BuildId.Embers, "余烬", "燃烬蔓延", 3, "燃烧伤害每秒 +1，持续3秒。"),
            new BuildDefinition(BuildId.SoulAttraction, "灵魂牵引", "灵魂共鸣", 2, "主动吸魂范围倍率 +50%。\n灵魂飞行速度 +20%。"),
            new BuildDefinition(BuildId.SoulFire, "灵魂灌注", "灵魂共鸣", 1, "主动吸魂积攒最多5次强化施法，\n每次伤害 +30%。"),
            new BuildDefinition(BuildId.LightSteps, "轻灵步伐", "生存续航", 3, "移动速度 +8%。"),
            new BuildDefinition(BuildId.SoulRecovery, "魂息回流", "生存续航", 1, "每收集10灵魂，恢复2生命。"),
            new BuildDefinition(BuildId.FlameBurst, "烈焰迸发", "火焰专属", 3, "对燃烧目标的直击伤害 +15%。"),
            new BuildDefinition(BuildId.ThunderArea, "雷域扩张", "雷电专属", 3, "雷爆半径 +15%。"),
            new BuildDefinition(BuildId.ThunderChain, "电弧跳跃", "雷电专属", 2, "电弧多跳跃1个目标，\n造成50%伤害。"),
            new BuildDefinition(BuildId.WindFan, "展开翼面", "风专属", 2, "每轮多发射1枚风刃，\n新增风刃造成60%伤害。"),
            new BuildDefinition(BuildId.WindPierce, "穿林风", "风专属", 2, "风刃多穿透1个目标，\n穿透后伤害衰减25%。"),
            new BuildDefinition(BuildId.OceanChill, "寒潮", "海洋专属", 3, "减速强度 +10%。\n对Boss效果减半。"),
            new BuildDefinition(BuildId.OceanPressure, "深水压力", "海洋专属", 3, "对减速或禁锢目标伤害 +15%。"),
            new BuildDefinition(BuildId.EarthCore, "岩芯", "大地专属", 3, "岩枪基础伤害 +4。"),
            new BuildDefinition(BuildId.EarthMark, "破甲印记", "大地专属", 3, "命中叠加最多3层印记。\n每级使每层增伤 +5%，持续4秒。"),
            new BuildDefinition(BuildId.ResFire, "火焰共鸣", "元素共鸣", 1, "雷：天罚烙印；风：焚风龙卷\n海洋：沸海蒸爆；大地：熔岩喷发\n获得普攻联动；Lv.12解锁终极。"),
            new BuildDefinition(BuildId.ResLightning, "雷电共鸣", "元素共鸣", 1, "火：天罚烙印；风：雷暴锋面\n海洋：导电潮池；大地：磁轨岩枪\n获得普攻联动；Lv.12解锁终极。"),
            new BuildDefinition(BuildId.ResWind, "风之共鸣", "元素共鸣", 1, "火：焚风龙卷；雷：雷暴锋面\n海洋：深海涡旋；大地：碎岩钻风\n获得普攻联动；Lv.12解锁终极。"),
            new BuildDefinition(BuildId.ResOcean, "海洋共鸣", "元素共鸣", 1, "火：沸海蒸爆；雷：导电潮池\n风：深海涡旋；大地：沉沦石棺\n获得普攻联动；Lv.12解锁终极。"),
            new BuildDefinition(BuildId.ResEarth, "大地共鸣", "元素共鸣", 1, "火：熔岩喷发；雷：磁轨岩枪\n风：碎岩钻风；海洋：沉沦石棺\n获得普攻联动；Lv.12解锁终极。"),
            new BuildDefinition(BuildId.FormPierce, "贯穿形态", "基础形态", 1, "额外穿透2个目标。\n单次伤害85%。"),
            new BuildDefinition(BuildId.FormFan, "散射形态", "基础形态", 1, "额外发射2枚弹丸。\n单枚伤害65%。"),
            new BuildDefinition(BuildId.FormHeavy, "重型形态", "基础形态", 1, "伤害165%，攻速70%。\n弹丸爆裂；风刃改为回旋。"),
            new BuildDefinition(BuildId.EvolveSpread, "扩散进化", "联动进化", 1, "联动覆盖更多目标，单次伤害降低。"),
            new BuildDefinition(BuildId.EvolveFocus, "集中进化", "联动进化", 1, "减少联动范围，增强单体打击。"),
            new BuildDefinition(BuildId.EvolveEcho, "回响进化", "联动进化", 1, "获得二次攻击或返回效果。"),
            new BuildDefinition(BuildId.UltimateBurst, "终极·集中爆发", "解锁终极", 1, "终极伤害 +40%。\n有效命中充能：12秒。"),
            new BuildDefinition(BuildId.UltimateField, "终极·持续覆盖", "解锁终极", 1, "范围 +25%，持续区域时长 +25%。\n伤害85%，有效命中充能10秒。"),
            new BuildDefinition(BuildId.UltimateCycle, "终极·快速轮转", "解锁终极", 1, "有效命中充能：8秒。\n终极伤害75%。"),
            new BuildDefinition(BuildId.MasterAttack, "普攻专精", "终局专精", 1, "普攻与联动伤害 +35%。\n终极伤害 -15%。"),
            new BuildDefinition(BuildId.MasterUltimate, "终极专精", "终局专精", 1, "终极伤害 +40%。\n普攻频率 -10%。"),
            new BuildDefinition(BuildId.MasterHarmony, "协同专精", "终局专精", 1, "释放终极后6秒，\n普攻与联动伤害 +50%。\n充能时间 -15%。")
        });

        public static BuildDefinition Get(BuildId id)
        {
            foreach (var item in All) if (item.Id == id) return item;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
        public static bool IsMilestone(BuildId id) => id >= BuildId.FormPierce;
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
