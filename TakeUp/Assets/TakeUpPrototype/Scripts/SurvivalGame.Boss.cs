using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalGame
    {
        [Header("보스 씬 전용 / 생존 씬에서는 비움")]
        public OctopusBossEncounter bossEncounter;
        sealed class BossEntry
        {
            public int level, kills, stones;
            public float hp, maxHp, speed, damage, attackSpeed, pickup, regeneration;
            public bool spear;
            public Dictionary<AugmentKind, int> stacks;
        }
        static BossEntry bossEntry;

        void InitializeBossMode()
        {
            if (bossEncounter == null) { bossEntry = null; return; }
            if (bossEntry != null)
            {
                Level = bossEntry.level; Kills = bossEntry.kills; stoneCount = bossEntry.stones;
                MaximumHealth = bossEntry.maxHp; Health = bossEntry.hp; moveSpeed = bossEntry.speed;
                damageMultiplier = bossEntry.damage; attackSpeed = bossEntry.attackSpeed;
                pickupRadius = bossEntry.pickup; regeneration = bossEntry.regeneration; HasSpear = bossEntry.spear;
                stacks.Clear(); foreach (var pair in bossEntry.stacks) stacks[pair.Key] = pair.Value;
            }
            bossEncounter.Initialize();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--takeup-boss-smoke") >= 0)
                gameObject.AddComponent<BossSmokeTest>().game = this;
        }

        public void EnterBossScene()
        {
            if (bossEncounter != null || State != RunState.BossReady) return;
            bossEntry = new BossEntry { level = Level, kills = Kills, stones = stoneCount, hp = Health, maxHp = MaximumHealth,
                speed = moveSpeed, damage = damageMultiplier, attackSpeed = attackSpeed, pickup = pickupRadius,
                regeneration = regeneration, spear = HasSpear, stacks = new Dictionary<AugmentKind, int>(stacks) };
            SceneManager.LoadScene("OctopusBossPrototype");
        }

        public void ReturnToSurvival() { bossEntry = null; SceneManager.LoadScene("PrehistoricPrototype"); }

        public void CompleteBossFight()
        {
            if (bossEncounter == null || State == RunState.Dead) return;
            if (musicSource != null) musicSource.Stop();
            RestorePlayerArt(); Kills++;
            State = RunState.BossReady; hud.ShowEnd(true);
        }
    }
}
