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
        static AudioClip attackSound;
        static Sprite hitFlash, hurtSprite;
        static float hurtInvulnerability;
        IEnumerator Start()
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
            if (SceneLoader.IsLoading)
            {
                Debug.LogError("Boss transition test timed out waiting for loading");
                Application.Quit(1);
                yield break;
            }
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
                    attackSound = game.settings.attackSound;
                    hitFlash = game.settings.hitEffect.flashSprite;
                    hurtSprite = game.settings.playerHurtSprite;
                    hurtInvulnerability = game.settings.hurtInvulnerability;
                    game.FinishStage(); phase = 2; game.EnterBossScene(); yield break;
                }
                if (phase == 2)
                {
                    if (game.settings.attackSound != attackSound || game.settings.hitEffect.flashSprite != hitFlash ||
                        game.settings.playerHurtSprite != hurtSprite || game.settings.hurtInvulnerability != hurtInvulnerability)
                        throw new Exception("Boss entry lost player audio, hit effects or hurt settings");
                    if (game.settings == game.bossPlayerSource)
                        throw new Exception("Boss must use a runtime settings copy");
                    var late = typeof(SurvivalGame).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
                    Vector3 cameraPosition = game.worldCamera.transform.position;
                    game.ShakeBossAttack();
                    late.Invoke(game, null);
                    if ((game.worldCamera.transform.position - cameraPosition).magnitude > game.bossShakeStrength * 1.5f)
                        throw new Exception("Camera shake exceeded bounded amplitude");
                    typeof(SurvivalGame).GetField("bossShakeRemaining", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, 0f);
                    late.Invoke(game, null);
                    if (Vector3.Distance(game.worldCamera.transform.position, cameraPosition) > .001f)
                        throw new Exception("Fixed boss camera drifted after shake");
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
