using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class BossTransitionSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        static int phase;
        static float health;
        IEnumerator Start()
        {
            yield return null;
            try
            {
                if (phase == 0) { phase = 1; game.ReturnToSurvival(); yield break; }
                if (phase == 1)
                {
                    if (game.bossEncounter != null) throw new Exception("Failed to enter survival scene");
                    var apply = typeof(SurvivalGame).GetMethod("ApplyAugment", BindingFlags.NonPublic | BindingFlags.Instance);
                    apply.Invoke(game, new object[] { new AugmentDefinition { kind = AugmentKind.Damage, amount = .25f } });
                    apply.Invoke(game, new object[] { new AugmentDefinition { kind = AugmentKind.Spear, amount = 1 } });
                    foreach (var kind in new[] { AugmentKind.FireDiscovery, AugmentKind.FireDamage, AugmentKind.FireDuration, AugmentKind.FireSpread, AugmentKind.FireDance, AugmentKind.RicochetStone, AugmentKind.ThornSkin })
                        apply.Invoke(game, new object[] { new AugmentDefinition { kind = kind, amount = 1 } });
                    game.DamagePlayer(10); health = game.Health;
                    game.FinishStage(); phase = 2; game.EnterBossScene(); yield break;
                }
                if (phase == 2)
                {
                    if (game.bossEncounter == null || !game.HasSpear || game.StackCount(AugmentKind.Damage) != 1 || game.StackCount(AugmentKind.RicochetStone) != 1 || game.StackCount(AugmentKind.ThornSkin) != 1 || !game.HasFire || game.StackCount(AugmentKind.FireDuration) != 1 || game.StackCount(AugmentKind.FireDance) != 1 || game.Health != health)
                        throw new Exception("Boss entry did not retain health/augments/weapons");
                    game.bossEncounter.Damage(100); phase = 3; game.Restart(); yield break;
                }
                if (phase == 3)
                {
                    if (game.bossEncounter.Health != game.bossEncounter.settings.health || !game.HasSpear || game.StackCount(AugmentKind.RicochetStone) != 1 || game.StackCount(AugmentKind.ThornSkin) != 1 || !game.HasFire || game.StackCount(AugmentKind.FireSpread) != 1 || game.Health != health)
                        throw new Exception("Boss retry did not reset encounter and restore entry loadout");
                    phase = 4; game.ReturnToSurvival(); yield break;
                }
                if (game.bossEncounter != null || game.HasSpear || game.StackCount(AugmentKind.Damage) != 0 || game.HasFire)
                    throw new Exception("Returning to survival did not reset the run");
                BossSmokeTest.Write("PASS: boss → survival → boss entry retains health, augment stacks and spear; boss retry resets health and preserves entry loadout; return to survival starts a fresh run.");
                Application.Quit(0);
            }
            catch (Exception exception) { BossSmokeTest.Write("FAIL: " + exception); Debug.LogException(exception); Application.Quit(1); }
        }
    }
}
