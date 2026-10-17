using System;
using UnityEngine;

namespace TakeUp.Prototype
{
    [CreateAssetMenu(menuName = "TakeUp/Survival Settings")]
    public sealed class SurvivalSettings : ScriptableObject
    {
        [Header("플레이어 / Player")]
        public Sprite playerSprite;
        public Color playerTint = new Color(0.97f, 0.74f, 0.35f);
        [Min(0.1f)] public float playerSize = 0.8f;
        [Min(1)] public float maxHealth = 100;
        [Min(0.1f)] public float moveSpeed = 5;
        [Min(0.1f)] public float hurtInvulnerability = 0.6f;
        [Header("돌 / Starting stone weapon")]
        public Sprite stoneSprite;
        public Color stoneTint = new Color(0.85f, 0.88f, 0.86f);
        [Min(1)] public float stoneDamage = 18;
        [Min(0.05f)] public float stoneInterval = 0.65f;
        [Min(1)] public float stoneSpeed = 12;
        [Min(1)] public float attackRange = 10;
        [Min(0.05f)] public float stoneSize = 0.25f;
        [Header("창 / Unlockable spear")]
        public Sprite spearSprite;
        public Color spearTint = new Color(1, 0.8f, 0.45f);
        [Min(1)] public float spearDamage = 32;
        [Min(0.1f)] public float spearInterval = 1.5f;
        [Min(1)] public float spearSpeed = 16;
        public Vector2 spearSize = new Vector2(0.8f, 0.18f);
        [Header("경험치 / Experience")]
        public Sprite experienceSprite;
        public Color experienceTint = new Color(0.3f, 1f, 0.7f);
        [Min(1)] public int firstLevelExperience = 6;
        [Min(1)] public int experienceGrowth = 4;
        [Min(0.1f)] public float pickupRadius = 2;
        [Min(0.1f)] public float pickupSpeed = 10;
        [Header("맵과 카메라 / Arena")]
        public Sprite groundSprite;
        public Color groundTint = new Color(0.12f, 0.19f, 0.16f);
        public Vector2 arenaHalfSize = new Vector2(32, 24);
        [Min(3)] public float cameraSize = 9;
        [Header("웨이브 / Waves")]
        [Min(1)] public int maxAliveEnemies = 100;
        [Min(1)] public float waveDuration = 30;
        [Min(10)] public float stageDuration = 180;
        [Min(0.05f)] public float initialSpawnInterval = 0.9f;
        [Min(0.05f)] public float minimumSpawnInterval = 0.18f;
        [Range(0.1f, 1)] public float spawnIntervalMultiplierPerWave = 0.78f;
        [Min(0)] public float healthGrowthPerWave = 0.25f;
        [Min(0)] public float damageGrowthPerWave = 0.12f;
        public EnemyDefinition[] enemies;
        [Header("레벨업 카드 / Three random choices")]
        public AugmentDefinition[] augments;
    }

    [Serializable]
    public sealed class EnemyDefinition
    {
        public string displayName = "야생 생물";
        public Sprite sprite;
        public Color tint = new Color(0.8f, 0.34f, 0.3f);
        [Min(0.1f)] public float size = 0.75f;
        [Min(1)] public float health = 28;
        [Min(0.1f)] public float speed = 1.8f;
        [Min(1)] public float damage = 10;
        [Min(1)] public int experience = 1;
        [Min(1)] public int firstWave = 1;
        [Min(0.01f)] public float spawnWeight = 1;
    }

    public enum AugmentKind { Damage, AttackSpeed, MoveSpeed, MaxHealth, PickupRadius, ExtraStone, Spear, Regeneration }

    [Serializable]
    public sealed class AugmentDefinition
    {
        public string title;
        [TextArea] public string description;
        public Sprite icon;
        public AugmentKind kind;
        [Tooltip("비율 강화는 0.2 = 20%. 체력/회복/개수는 절대값.")]
        public float amount = 0.2f;
        [Min(1)] public int maxStacks = 5;
    }
}
