using System;
using System.Collections;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class PrimitiveAugmentSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        IEnumerator Start()
        {
            yield return null;
            try { game.enabled = false; game.ValidatePrimitiveAugments(); BossSmokeTest.Write("PASS: choice unlock/caps, one bounce onto distinct target, fire retained, no target cleanup, spear exclusion, thorns on real damage only, invulnerability/dash protection, thorn damage/range/no fire, boss hit and finale immunity."); Application.Quit(0); }
            catch (Exception e) { BossSmokeTest.Write("FAIL: " + e); Debug.LogException(e); Application.Quit(1); }
        }
    }
    public sealed partial class SurvivalGame
    {
        internal void ValidatePrimitiveAugments()
        {
            void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
            AugmentDefinition Card(AugmentKind kind) => Array.Find(settings.augments, a => a != null && a.kind == kind);
            void Clear()
            {
                foreach (var e in enemies) Destroy(e.body.gameObject); enemies.Clear();
                foreach (var s in shots) Destroy(s.body.gameObject); shots.Clear();
            }
            Enemy Target(Vector2 p) { SpawnEnemy(new EnemyDefinition { health = 1000, speed = 0, damage = 0 }, p); return enemies[enemies.Count - 1]; }
            void Steps(int count) { for (int i = 0; i < count; i++) TickShots(.02f); }
            stacks.Clear(); State = RunState.Playing; Clear();
            Check(CanOfferAugment(Card(AugmentKind.RicochetStone)) && CanOfferAugment(Card(AugmentKind.ThornSkin)), "no prerequisites");
            AddExperience(RequiredExperience); Choices.Clear(); Choices.Add(Card(AugmentKind.RicochetStone)); ChooseAugment(0);
            Check(StackCount(AugmentKind.RicochetStone) == 1 && !CanOfferAugment(Card(AugmentKind.RicochetStone)), "choice grants bounce once");
            ApplyAugment(Card(AugmentKind.FireDiscovery));
            var first = Target(PlayerPosition + Vector2.left * 2);
            var second = Target(PlayerPosition + new Vector2(-2, 2));
            var third = Target(PlayerPosition + new Vector2(-2, 4));
            Fire(Vector2.left, false); Steps(40);
            Check(Mathf.Approximately(first.health, 1000 - settings.stoneDamage * damageMultiplier), "first enemy hit once");
            Check(Mathf.Approximately(second.health, first.health) && third.health == 1000, "one distinct bounce only");
            Check(first.burnRemaining > 0 && second.burnRemaining > 0, "bounced stone carries fire");
            Check(shots.Count == 0, "bounce ends after second hit");
            Clear(); first = Target(PlayerPosition + Vector2.left * 2); Fire(Vector2.left, false); Steps(20);
            Check(shots.Count == 0, "no valid target destroys stone");
            Clear(); Fire(Vector2.left, true); Check(shots[0].bounces == 0, "spear never bounces"); Clear();
            ApplyAugment(Card(AugmentKind.ThornSkin)); Check(!CanOfferAugment(Card(AugmentKind.ThornSkin)), "thorn cap");
            invulnerability = 0; dashRemaining = 0; Health = MaximumHealth;
            DamagePlayer(1); Check(shots.Count == settings.primitive.thornCount, "real hit emits radial thorns");
            DamagePlayer(1); Check(shots.Count == settings.primitive.thornCount, "invulnerable hit cannot retrigger");
            foreach (var shot in shots) Check(!shot.burning && shot.bounces == 0, "thorns not stones or fire weapons");
            first = Target(PlayerPosition + Vector2.right * 2);
            Steps(15); Check(Mathf.Approximately(first.health, 1000 - settings.primitive.thornDamage * damageMultiplier), "thorn deals configured damage");
            Check(first.burnRemaining == 0, "thorn does not ignite"); Steps(40); Check(shots.Count == 0, "thorns expire at range");
            invulnerability = 0; dashRemaining = .1f; DamagePlayer(1); Check(shots.Count == 0, "dash blocks thorn activation"); dashRemaining = 0;
            DamagePlayer(0); Check(shots.Count == 0, "zero damage does not activate thorns");
            if (bossEncounter != null)
            {
                Clear(); player.position = bossEncounter.Position + Vector2.down * 4;
                invulnerability = 0; float hp = bossEncounter.Health; DamagePlayer(1); Steps(30);
                Check(bossEncounter.Health < hp, "thorns hit boss");
                bossEncounter.Damage(float.MaxValue); invulnerability = 0; DamagePlayer(1); Steps(30);
                Check(bossEncounter.IsFinale && !bossEncounter.IsDefeated, "thorns cannot bypass finale");
            }
        }
    }
}
