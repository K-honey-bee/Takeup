using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class FireSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        IEnumerator Start()
        {
            yield return null;
            try
            {
                game.enabled = false;
                game.ValidateFireAugments();
                BossSmokeTest.Write("PASS: fire card prerequisites and caps, real choice unlock, stone/spear ignition, refresh without stacking, exact duration damage, damage/duration upgrades, death spread and range, single XP/kill reward, failed/successful fire dance, pause freeze, boss burn/finale immunity.");
                Application.Quit(0);
            }
            catch (Exception e) { BossSmokeTest.Write("FAIL: " + e); Debug.LogException(e); Application.Quit(1); }
        }
    }

    public sealed partial class SurvivalGame
    {
        internal void ValidateFireAugments()
        {
            void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
            AugmentDefinition Card(AugmentKind kind) => Array.Find(settings.augments, a => a != null && a.kind == kind);
            void Grant(AugmentKind kind) => ApplyAugment(Card(kind));
            void ClearTargets()
            {
                foreach (var enemy in enemies) Destroy(enemy.body.gameObject); enemies.Clear();
                foreach (var shot in shots) Destroy(shot.body.gameObject); shots.Clear();
            }
            Enemy Target(Vector2 position, float hp = 1000)
            {
                SpawnEnemy(new EnemyDefinition { health = hp, speed = 0, damage = 0, experience = 2 }, position);
                return enemies[enemies.Count - 1];
            }
            var originalStacks = new System.Collections.Generic.Dictionary<AugmentKind, int>(stacks);
            stacks.Clear(); State = RunState.Playing;
            Check(Card(AugmentKind.FireDiscovery) != null, "fire catalog installed");
            foreach (var kind in new[] { AugmentKind.FireDamage, AugmentKind.FireDuration, AugmentKind.FireSpread, AugmentKind.FireDance })
                Check(!CanOfferAugment(Card(kind)), "locked fire upgrade " + kind);
            AddExperience(RequiredExperience);
            Choices.Clear(); Choices.Add(Card(AugmentKind.FireDiscovery)); ChooseAugment(0);
            Check(HasFire && State == RunState.Playing && !CanOfferAugment(Card(AugmentKind.FireDiscovery)), "choice unlock and discovery cap");
            Check(CanOfferAugment(Card(AugmentKind.FireDamage)) && !CanOfferAugment(Card(AugmentKind.FireDance)), "damage available, dance still locked");
            stacks[AugmentKind.StompDance] = 1;
            Check(CanOfferAugment(Card(AugmentKind.FireDance)), "fire dance requires both unlocks");
            ClearTargets();
            // Aim away from the boss so these projectile checks target ordinary enemies only.
            Vector2 direction = Vector2.left;
            var target = Target(PlayerPosition + direction * 2);
            Fire(direction, false);
            for (int i = 0; i < 10; i++) TickShots(.02f);
            Check(target.burnRemaining > 0 && target.health < 1000, "stone ignites on hit");
            target.burnRemaining = 0; Fire(direction, true);
            for (int i = 0; i < 10; i++) TickShots(.02f);
            Check(target.burnRemaining > 0, "spear ignites on hit");
            TickBurns(.5f); Ignite(target); Ignite(target);
            Check(Mathf.Approximately(target.burnRemaining, BurnDuration), "repeat hits refresh only");
            float hp = target.health, duration = BurnDuration, dps = BurnDamagePerSecond;
            TickBurns(duration + 1);
            Check(Mathf.Abs(hp - target.health - dps * duration) < .01f && target.burnRemaining == 0, "exact clipped duration damage");
            Check(!target.burnVisual.gameObject.activeSelf, "expired ember hidden");
            Grant(AugmentKind.FireDamage); Grant(AugmentKind.FireDuration);
            Check(BurnDamagePerSecond > dps && BurnDuration > duration, "upgrades strengthen burn");
            for (int i = 1; i < Card(AugmentKind.FireDamage).maxStacks; i++) Grant(AugmentKind.FireDamage);
            Check(!CanOfferAugment(Card(AugmentKind.FireDamage)), "damage cap enforced in offer pool");
            Ignite(target); hp = target.health; float remaining = target.burnRemaining;
            TogglePause(); TickBurns(1); Check(target.health == hp && target.burnRemaining == remaining, "pause freezes burn"); TogglePause();
            ClearTargets(); Grant(AugmentKind.FireSpread);
            var source = Target(PlayerPosition + Vector2.left * 3, 1);
            var near = Target((Vector2)source.body.position + Vector2.up);
            var far = Target((Vector2)source.body.position + Vector2.up * (settings.fire.spreadRadius + 2));
            int killsBefore = Kills, gemsBefore = gems.Count;
            Ignite(source); TickBurns(.5f);
            Check(Kills == killsBefore + 1 && gems.Count == gemsBefore + 1, "burn death gives one kill and XP drop");
            Check(near.burnRemaining > 0 && far.burnRemaining == 0, "spread hits nearby only");
            TickBurns(.01f); Check(Kills == killsBefore + 1, "dead target never rewarded twice");
            near.health = 1; Fire(((Vector2)near.body.position - PlayerPosition).normalized, false);
            for (int i = 0; i < 30; i++) TickShots(.02f);
            Check(Kills == killsBefore + 2, "direct hit kills burning target through shared death path");
            ClearTargets(); Grant(AugmentKind.FireDance);
            target = Target(PlayerPosition + Vector2.right * 3, settings.dance.damage * damageMultiplier + 1000);
            DanceCooldown = 0; Check(StartDance(), "dance starts"); ResolveDance(false);
            Check(shockwaves.Count == 0 && target.burnRemaining == 0, "failed dance cannot ignite");
            EndDance(); DanceCooldown = 0; Check(StartDance(), "dance retry starts"); ResolveDance(true);
            TickShockwaves(.25f);
            Check(target.burnRemaining > 0, "successful wave ignites enemy");
            CaptureFireSmoke("fire-dance.png");
            EndDance();
            if (bossEncounter != null)
            {
                IgniteBoss(); hp = bossEncounter.Health; TickBurns(.5f);
                Check(bossEncounter.Health < hp, "boss takes burn damage");
                bossEncounter.Damage(float.MaxValue); IgniteBoss(); TickBurns(100);
                Check(bossEncounter.IsFinale && !bossEncounter.IsDefeated && bossBurnRemaining == 0, "finale discards burn without clearing boss");
            }
            stacks.Clear(); foreach (var pair in originalStacks) stacks[pair.Key] = pair.Value;
        }
        void CaptureFireSmoke(string name)
        {
            var capture = gameObject.AddComponent<SurvivalSmokeTest>(); capture.game = this; capture.enabled = false;
            typeof(SurvivalSmokeTest).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(capture, new object[] { name });
            Destroy(capture);
        }
    }
}
