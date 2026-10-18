using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class BullSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
        void Set(string field, object value) => typeof(SurvivalGame).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, value);
        void Capture(string name)
        {
            var c = game.gameObject.AddComponent<SurvivalSmokeTest>(); c.game = game; c.enabled = false; Invoke(c, "Capture", name); Destroy(c);
        }
        static void Next(BullBossEncounter bull) => bull.Tick(bull.Remaining + .001f);
        IEnumerator Start()
        {
            yield return null;
            try
            {
                game.enabled = false; var bull = game.bullEncounter;
                Check(bull != null && game.ActiveBoss == bull && bull.walls.Length == 4, "bull bindings");
                Check(game.AliveEnemies == 0, "no survival spawns in bull arena");
                int[] hours = { 5, 7, 9, 11 };
                for (int i = 0; i < 4; i++)
                {
                    float a = (90 - hours[i] * 30) * Mathf.Deg2Rad;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Check(Vector2.Distance(bull.walls[i].transform.position, d * 6.5f) < .01f, "clock wall " + hours[i]);
                    Vector2 stopped = bull.ConstrainMovement(d * 3, d * 9, .4f);
                    Check(Vector2.Dot(stopped, d) < 6.5f, "dash cannot tunnel through wall " + hours[i]);
                }
                Next(bull); Check(bull.Phase == BullPhase.StompWarning, "stomp warning first"); Capture("bull-stomp-warning.png");
                Next(bull); Check(bull.Phase == BullPhase.Shockwave, "stomp starts wave");
                Check(!bull.WaveTouches(Vector2.up * 5, 0, 6) && bull.WaveTouches(Vector2.down * 5, 0, 6), "forward half circle only");
                Set("invulnerability", 0f); float hp = game.Health;
                bull.Tick(.55f); Check(game.Health < hp, "expanding ring damages player"); Capture("bull-shockwave.png");
                Next(bull); Next(bull); Check(bull.Phase == BullPhase.ChargeWarning, "charge warning follows stomp");
                Vector2 locked = bull.AttackDirection; game.scenePlayer.transform.position = new Vector2(4, -4); bull.Tick(.1f);
                Check(bull.AttackDirection == locked, "charge direction locks during warning"); Capture("bull-charge-warning.png");
                for (int i = 0; i < 4; i++)
                {
                    bull.bossBody.position = Vector2.zero;
                    Vector2 wall = bull.walls[i].transform.position;
                    game.scenePlayer.transform.position = wall.normalized * 3;
                    bull.GetType().GetField("nextCharge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bull, true);
                    Invoke(bull, "BeginPattern"); Next(bull);
                    game.scenePlayer.transform.position = -wall.normalized * 3;
                    bull.Tick(1);
                    Check(bull.Phase == BullPhase.Stunned, "charge collides with wall " + hours[i]);
                    hp = bull.Health; bull.Damage(10);
                    Check(Mathf.Approximately(hp - bull.Health, 10 * bull.settings.stunnedDamageMultiplier), "stun bonus damage");
                    if (i == 0) Capture("bull-wall-stun.png");
                }
                bull.bossBody.position = Vector2.zero; game.scenePlayer.transform.position = Vector2.down * 5;
                bull.GetType().GetField("nextCharge", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bull, false);
                Invoke(bull, "BeginPattern"); Next(bull); bull.Tick(.3f);
                Set("invulnerability", 0f); Set("lastDirection", Vector2.up); Check(game.StartDash(), "dash starts"); hp = game.Health;
                for (int i = 0; i < 12; i++) { Invoke(game, "TickDash", .02f); bull.Tick(.02f); }
                Check(game.Health == hp && game.PlayerPosition.y > -3, "actual dash crosses approaching ring safely");
                game.TogglePause(); float remaining = bull.Remaining; Invoke(game, "Update");
                Check(bull.Remaining == remaining, "pause freezes boss"); game.TogglePause();
                hp = bull.Health; Invoke(game, "Fire", (bull.Position - game.PlayerPosition).normalized, false);
                for (int i = 0; i < 40; i++) Invoke(game, "TickShots", .02f);
                Check(bull.Health < hp, "automatic weapon can damage bull through common boss interface");
                Invoke(game, "ApplyAugment", new AugmentDefinition { kind = AugmentKind.FireDiscovery });
                Invoke(game, "IgniteBoss"); hp = bull.Health; Invoke(game, "TickBurns", .5f);
                Check(bull.Health < hp, "fire burns bull");
                bull.Damage(float.MaxValue);
                Check(bull.IsDefeated && game.State == RunState.BossReady && !bull.waveVisual.gameObject.activeSelf && !bull.chargeVisual.gameObject.activeSelf, "victory and hazard cleanup"); Capture("bull-victory.png");
                BossSmokeTest.Write("PASS: white bull scene, 5/7/9/11 walls, wall dash collision, frontal expanding shockwave, real dash evasion, locked charge, all four wall stuns, bonus damage, pause, projectile/fire damage, victory cleanup.");
                Application.Quit(0);
            }
            catch (Exception e) { BossSmokeTest.Write("FAIL: " + e); Debug.LogException(e); Application.Quit(1); }
        }
    }
}
