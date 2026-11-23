using UnityEngine;

namespace StarFall
{
    [CreateAssetMenu(menuName = "StarFall/Battle Config")]
    public sealed class BattleConfig : ScriptableObject
    {
        [Header("Match")]
        [Min(1)] public float duration = 480f;
        public Vector2 arenaSize = new Vector2(42, 26);
        [Min(0.1f)] public float resultDisplaySeconds = 4;
        [Header("Character and equipped staff / gem")]
        public CharacterStats character;
        public StaffDefinition staff;
        public GemDefinition gem;
        public float playerHealth => character.health;
        public float playerSpeed => character.moveSpeed * staff.moveSpeedMultiplier;
        public float PassiveSoulRadius => character.soulCollectionRadius;
        public float ActiveSoulRadius => character.soulCollectionRadius * staff.soulCollectionMultiplier;
        [Min(0.1f)] public float playerRadius = 0.55f;
        [Min(0)] public float hurtGraceSeconds = 0.65f;
        [Header("Souls")]
        [Min(0.1f)] public float soulTravelSpeed = 9;
        [Min(1)] public int enemySoulValue = 1;
        [Header("Build progression: experience required per level")]
        public int[] levelExperience = { 6, 8, 12, 14, 16, 24, 32, 44, 64, 88, 120, 160, 210, 270 };
        [Header("Enemy")]
        public bool enableEnemyRoles = true;
        [Min(1)] public float enemyHealth = 24;
        [Range(0, 100)] public float enemyElementResistancePercent = 0;
        [Min(0)] public float enemySpeed = 2.4f;
        [Min(0.1f)] public float enemyRadius = 0.6f;
        [Min(1)] public int contactDamage = 10;
        public DamageElement contactElement = DamageElement.Physical;
        [Min(0.1f)] public float contactCooldown = 1;
        [Min(0.1f)] public float initialSpawnInterval = 1f;
        [Min(0.1f)] public float finalSpawnInterval = 0.4f;
        [Min(1)] public int maxEnemies = 30;
        [Header("Arena obstacles: x/z center, width/depth")]
        public bool enableObstacles = true;
        public Vector4[] obstacles = { new Vector4(-8, -3, 3, 5), new Vector4(7, 4, 4, 3), new Vector4(-6, 7, 5, 2), new Vector4(9, -7, 4, 2) };
        [Header("Encounters: duration is final boss arrival time, not a win timer")]
        public bool enableBossEncounters = true;
        [Min(1)] public float miniBossTime = 240;
        [Min(1)] public float miniBossHealth = 420;
        [Min(0)] public float miniBossSpeed = 1.9f;
        [Min(0.1f)] public float miniBossRadius = 1;
        [Min(1)] public int miniBossSoulValue = 30;
        [Min(1)] public float finalBossHealth = 1500;
        [Min(0)] public float finalBossSpeed = 1.4f;
        [Min(0.1f)] public float finalBossRadius = 1.5f;
        [Range(0, 100)] public float finalBossFireResistance = 20;
        [Min(0.1f)] public float bossAttackInterval = 5;
        [Min(0.1f)] public float bossWarningTime = 1.4f;
        [Min(0.1f)] public float chestInteractionRadius = 6;
    }
}
