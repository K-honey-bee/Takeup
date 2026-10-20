using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        [Header("보스 씬 전용 / 생존 씬에서는 비움")]
        public OctopusBossEncounter bossEncounter;
        public BullBossEncounter bullEncounter;
        [Tooltip("보스전에서도 사용할 일반 맵의 플레이어 설정입니다.")]
        public SurvivalSettings bossPlayerSource;
        [Min(0)] public float bossShakeStrength = .16f;
        [Min(.01f)] public float bossShakeDuration = .18f;
        SurvivalSettings runtimeBossSettings;
        Vector3 bossShakeOffset;
        float bossShakeRemaining;

        void PrepareBossPlayer()
        {
            if (ActiveBoss == null) return;
            var source = bossEntry != null ? bossEntry.playerSettings : bossPlayerSource;
            if (source == null) return;
            runtimeBossSettings = Instantiate(source);
            runtimeBossSettings.arenaHalfSize = settings.arenaHalfSize;
            runtimeBossSettings.cameraSize = settings.cameraSize;
            runtimeBossSettings.groundSprite = settings.groundSprite;
            runtimeBossSettings.groundTint = settings.groundTint;
            settings = runtimeBossSettings;
            if (scenePlayer != null && source.playerSprite != null)
            {
                scenePlayer.sprite = source.playerSprite;
            }
        }

        public void ShakeBossAttack()
        {
            bossShakeRemaining = bossShakeDuration;
        }

        void ApplyBossShake()
        {
            if (State != RunState.Playing && State != RunState.Dancing)
            {
                bossShakeRemaining = 0;
                return;
            }
            if (bossShakeRemaining <= 0) return;
            bossShakeRemaining = Mathf.Max(0, bossShakeRemaining - Time.unscaledDeltaTime);
            float strength = bossShakeStrength * bossShakeRemaining / Mathf.Max(.01f, bossShakeDuration);
            float time = Time.unscaledTime * 45;
            bossShakeOffset = new Vector3(Mathf.Sin(time), Mathf.Sin(time * 1.37f)) * strength;
            worldCamera.transform.position += bossShakeOffset;
        }
        public IBossEncounter ActiveBoss => bullEncounter != null ? (IBossEncounter)bullEncounter : bossEncounter;
        sealed class BossEntry
        {
            public int level, kills, stones;
            public float hp, maxHp, speed, damage, attackSpeed, pickup, regeneration;
            public bool spear;
            public OrbitBoneSettings orbitBone;
            public SurvivalSettings playerSettings;
            public Dictionary<AugmentKind, int> stacks;
        }
        static BossEntry bossEntry;

        void InitializeBossMode()
        {
            if (ActiveBoss == null) { bossEntry = null; return; }
            if (bossEntry != null)
            {
                Level = bossEntry.level; Kills = bossEntry.kills; stoneCount = bossEntry.stones;
                MaximumHealth = bossEntry.maxHp; Health = bossEntry.hp; moveSpeed = bossEntry.speed;
                damageMultiplier = bossEntry.damage; attackSpeed = bossEntry.attackSpeed;
                pickupRadius = bossEntry.pickup; regeneration = bossEntry.regeneration; HasSpear = bossEntry.spear;
                stacks.Clear(); foreach (var pair in bossEntry.stacks) stacks[pair.Key] = pair.Value;
                if (bossEntry.orbitBone != null) settings.orbitBone = bossEntry.orbitBone;
            }
            ActiveBoss.Initialize();
            if (bullEncounter != null && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--takeup-bull-smoke") >= 0)
                gameObject.AddComponent<BullSmokeTest>().game = this;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--takeup-boss-smoke") >= 0)
                gameObject.AddComponent<BossSmokeTest>().game = this;
        }

        public void EnterBossScene()
        {
            if (ActiveBoss != null || State != RunState.BossReady) return;
            bossEntry = new BossEntry { level = Level, kills = Kills, stones = stoneCount, hp = Health, maxHp = MaximumHealth,
                speed = moveSpeed, damage = damageMultiplier, attackSpeed = attackSpeed, pickup = pickupRadius,
                regeneration = regeneration, spear = HasSpear, playerSettings = settings, orbitBone = settings.orbitBone, stacks = new Dictionary<AugmentKind, int>(stacks) };
            SceneLoader.Load("OctopusBossPrototype");
        }

        public void ReturnToSurvival() { bossEntry = null; SceneLoader.Load("PrehistoricPrototype"); }

        public void CompleteBossFight()
        {
            if (ActiveBoss == null || State == RunState.Dead) return;
            if (musicSource != null) musicSource.Stop();
            RestorePlayerArt(); Kills++;
            State = RunState.BossReady; hud.ShowEnd(true);
        }
    }
}
