using UnityEngine;

namespace StarFall
{
    public static class ElementCatalog
    {
        public static readonly string[] Names = { "无", "火焰", "雷电", "风", "海洋", "大地" };
        public static readonly string[] Spells = { "", "火球术", "雷爆术", "风刃术", "潮汐弹", "岩枪术" };
        public static readonly string[] Descriptions = { "", "火球直击8伤害；燃烧2/秒，持续3秒", "雷弹10伤害；命中后半径1.8范围爆发", "每轮三枚风刃，每枚5伤害；扇形扩散", "水弹7伤害；减速25%，持续1.5秒", "岩枪24伤害；每秒0.8发，集中单体打击" };
        public static Color ColorFor(DamageElement element)
        {
            switch (element)
            {
                case DamageElement.Fire: return new Color(1, 0.32f, 0.06f);
                case DamageElement.Lightning: return new Color(0.72f, 0.3f, 1);
                case DamageElement.Wind: return new Color(0.12f, 0.85f, 0.55f);
                case DamageElement.Ocean: return new Color(0.08f, 0.32f, 0.85f);
                case DamageElement.Earth: return new Color(0.77f, 0.57f, 0.22f);
                default: return Color.white;
            }
        }
        public static GemDefinition Create(DamageElement element, GemDefinition fireTemplate)
        {
            var gem = Object.Instantiate(fireTemplate);
            gem.element = element; gem.displayName = Names[(int)element] + "宝石"; gem.attackName = Spells[(int)element];
            if (element == DamageElement.Fire) return gem;
            gem.attackMultiplier = 1;
            switch (element)
            {
                case DamageElement.Lightning: gem.baseDamage = 10; gem.attacksPerSecond = 1.4f; gem.projectileSpeed = 22; break;
                case DamageElement.Wind: gem.baseDamage = 5; gem.attacksPerSecond = 1.5f; gem.projectileSpeed = 24; break;
                case DamageElement.Ocean: gem.baseDamage = 7; gem.attacksPerSecond = 1.6f; gem.projectileSpeed = 16; break;
                case DamageElement.Earth: gem.baseDamage = 24; gem.attacksPerSecond = 0.8f; gem.projectileSpeed = 20; gem.projectileRadius = 0.3f; break;
            }
            return gem;
        }
        public static bool Compatible(DamageElement main, DamageElement other) => FusionCatalog.Get(main, other) != FusionKind.None;
        public static string ResonanceName(DamageElement main, DamageElement other) => !Compatible(main, other) ? "无共鸣"
            : FusionCatalog.Get(main, other) != FusionKind.None ? FusionCatalog.Name(FusionCatalog.Get(main, other)) : "泥沼";
    }
}
