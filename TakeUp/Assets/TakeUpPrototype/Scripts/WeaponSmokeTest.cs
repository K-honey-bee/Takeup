using System;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class WeaponSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        static int phase;
        System.Collections.IEnumerator Start()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
            try
            {
                if (SceneLoader.IsLoading) throw new Exception("Weapon test loading timeout");
                if (phase == 0)
                {
                    game.ValidateWeapons(); phase = 1;
                    game.FinishStage(); game.EnterBossScene(); yield break;
                }
                if (phase == 1 || phase == 2)
                {
                    if (game.ActiveBoss == null || game.SelectedWeapon != StartingWeapon.Sword ||
                        game.State != RunState.Playing || game.StackCount(AugmentKind.SwordWave) != 1 ||
                        !Array.Exists(game.settings.augments, a => a.kind == AugmentKind.SwordWave))
                        throw new Exception("Boss entry/retry lost sword loadout");
                    if (phase++ == 1) game.Restart(); else game.ReturnToSurvival();
                    yield break;
                }
                if (game.State != RunState.ChoosingWeapon || game.StackCount(AugmentKind.SwordWave) != 0)
                    throw new Exception("Fresh run did not reset weapon choice and augments");
                Debug.Log("WEAPON TEST PASS: selection, freeze, filtering, six augments, sector damage, boss entry, retry and fresh run.");
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }
    }

    public sealed partial class SurvivalGame
    {
        public void ValidateWeapons()
        {
            void Check(bool condition, string label) { if (!condition) throw new Exception("Weapon test: " + label); }
            Check(State == RunState.ChoosingWeapon, "fresh run requires weapon selection");
            var before = player.position;
            MovePlayer(Vector2.right, 1);
            DamagePlayer(10);
            Check(player.position == before && Health == MaximumHealth && Elapsed == 0, "selection freezes movement and damage");
            SelectWeapon(StartingWeapon.Sword);
            Check(State == RunState.Playing && SelectedWeapon == StartingWeapon.Sword, "sword selection");
            SelectWeapon(StartingWeapon.Stone);
            Check(SelectedWeapon == StartingWeapon.Sword, "cannot switch during combat");
            Check(!CanOfferAugment(new AugmentDefinition { kind = AugmentKind.ExtraStone }) &&
                !CanOfferAugment(new AugmentDefinition { kind = AugmentKind.RicochetStone }), "sword excludes stone cards");
            int swordCards = 0;
            foreach (var card in settings.augments)
                if (card.kind >= AugmentKind.SwordReach && card.kind <= AugmentKind.SwordOverload)
                { swordCards++; Check(CanOfferAugment(card), "sword card available"); }
            Check(swordCards == 6, "six registered sword cards");
            Check(SwordContains(Vector2.zero, Vector2.right, 3, 120, new Vector2(2, 0), .2f), "front hit");
            Check(!SwordContains(Vector2.zero, Vector2.right, 3, 120, new Vector2(-2, 0), .2f), "rear miss");
            Check(!SwordContains(Vector2.zero, Vector2.right, 3, 120, new Vector2(4, 0), .2f), "range miss");
            Check(SwordContains(Vector2.zero, Vector2.right, 3, 360, new Vector2(-2, 0), .2f), "spin hits rear");
            int shotCount = shots.Count;
            SwingSword(Vector2.right);
            Check(swordStrikes.Count == 1 && shots.Count == shotCount, "base sword does not throw stones");
            float baseRange = swordStrikes[0].range;
            swordStrikes.Clear();
            foreach (var card in settings.augments)
                if (card.kind >= AugmentKind.SwordReach && card.kind <= AugmentKind.SwordOverload) ApplyAugment(card);
            swordAttacks = 19;
            SwingSword(Vector2.right);
            Check(swordStrikes.Count == 5, "double, echo, spin and overload activate");
            Check(swordStrikes[0].range > baseRange && shots.Count == shotCount + 1, "reach and projectile wave");
            Check(swordStrikes.Exists(s => s.angle == 360) && swordStrikes.Exists(s => s.angle == 28), "spin and overload geometry");
            var target = player.position + Vector3.right;
            SpawnEnemy(settings.enemies[0], target);
            var enemy = enemies[enemies.Count - 1]; enemy.health = 100000;
            float hp = enemy.health;
            TickSwordEffects(.01f);
            Check(enemy.health < hp && swordVisuals.Count > 0, "actual sword damage and visuals");
            hp = enemy.health;
            TickSwordEffects(.4f);
            Check(enemy.health < hp && swordStrikes.Count == 0, "delayed attacks resolve");
            State = RunState.ChoosingWeapon; SelectWeapon(StartingWeapon.Stone);
            Check(!WeaponAllows(AugmentKind.SwordWave) && WeaponAllows(AugmentKind.ExtraStone), "stone excludes sword cards");
            State = RunState.ChoosingWeapon; SelectWeapon(StartingWeapon.Sword);
        }
    }
}
