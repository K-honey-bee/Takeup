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
        [Header("대시 / Shift")]
        [Min(0.1f)] public float dashDistance = 3.5f;
        [Min(0.05f)] public float dashDuration = 0.18f;
        [Min(0.1f)] public float dashCooldown = 2;
        public AudioClip dashSound;
        [Header("전설 스킬 / 발구르기 댄스")]
        public StompDanceSettings dance = new StompDanceSettings();
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

    public enum AugmentKind { Damage, AttackSpeed, MoveSpeed, MaxHealth, PickupRadius, ExtraStone, Spear, Regeneration, StompDance }

    public enum BeatDirection { Left, Right }

    [Serializable]
    public sealed class StompDanceSettings
    {
        [Tooltip("프로토타입에서 Q로 즉시 테스트. 끄면 증강을 획득해야 합니다.")]
        public bool startUnlocked = true;
        public BeatDirection[] sequence = { BeatDirection.Left, BeatDirection.Right, BeatDirection.Left,
            BeatDirection.Right, BeatDirection.Left, BeatDirection.Left, BeatDirection.Right };
        [Range(40, 240)] public float bpm = 110;
        [Tooltip("음악 시작부터 첫 입력 박자까지의 초. 음악 BPM과 함께 맞춰주세요.")]
        [Min(0.5f)] public float firstBeatTime = 1.5f;
        [Range(0.04f, 0.25f)] public float hitWindow = 0.16f;
        [Range(0.01f, 0.5f)] public float enemyTimeScale = 0.15f;
        [Range(0.25f, 0.95f)] public float finalZoomRatio = 0.5f;
        [Min(0)] public float beatZoomKick = 0.25f;
        [Min(0.1f)] public float cameraReturnSpeed = 7;
        [Min(0.1f)] public float cooldown = 12;
        [Min(1)] public float damage = 100;
        [Min(1)] public float radius = 14;
        [Min(1)] public float waveSpeed = 20;
        public Color shockwaveColor = new Color(1, 0.78f, 0.3f, 0.85f);
        [Header("이미지 / 비워 두면 기본 도형 사용")]
        public Sprite[] danceFrames;
        public Sprite shockwaveSprite;
        public Sprite leftArrow;
        public Sprite rightArrow;
        public Sprite panelSprite;
        [Header("오디오 / 비워 두면 임시 박자음 사용")]
        public AudioClip music;
        public AudioClip beatSound;
        public AudioClip hitSound;
        public AudioClip successSound;
        public AudioClip failureSound;
        [Range(0, 1)] public float volume = 0.6f;
        public bool playMetronome = true;
        [Tooltip("이 값만큼 입력 판정 시간을 보정합니다. 양수는 늦게 누른 입력을 보정.")]
        [Range(-0.3f, 0.3f)] public float inputLatencyOffset;
    }

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
