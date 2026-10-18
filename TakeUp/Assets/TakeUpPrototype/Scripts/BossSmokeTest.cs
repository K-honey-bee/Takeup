using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TakeUp.Prototype
{
    public sealed class BossSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        IEnumerator Start()
        {
            yield return null;
            try
            {
                var boss = game.bossEncounter; game.enabled = false;
                Require(boss != null && boss.Health > 0, "boss initialized");
                for (int i = 0; i < 4; i++)
                {
                    var rock = boss.rocks[i]; Vector2 away = ((Vector2)rock.transform.position - boss.Position).normalized;
                    Vector2 safe = (Vector2)rock.transform.position + away * 2;
                    Require(boss.IsCovered(safe, i), "cover shadow " + i);
                    Require(!boss.IsCovered(safe, (i + 1) % 4), "wrong color " + i);
                    Require(!boss.IsCovered((Vector2)rock.transform.position - away * 2, i), "front of rock unsafe " + i);
                    Vector2 stopped = boss.ConstrainMovement(safe, (Vector2)rock.transform.position - away * 2, .4f);
                    Require(Vector2.Dot(stopped - (Vector2)rock.transform.position, away) > 0, "dash cannot tunnel through rock");
                }
                float hp = boss.Health;
                Invoke(game, "Fire", (boss.Position - game.PlayerPosition).normalized, false);
                for (int i = 0; i < 100; i++) Invoke(game, "TickShots", .02f);
                Require(boss.Health < hp, "stone projectile damages boss");
                Invoke(game, "Fire", (boss.Position - game.PlayerPosition).normalized, true);
                hp = boss.Health;
                for (int i = 0; i < 100; i++) Invoke(game, "TickShots", .02f);
                Require(boss.Health < hp, "spear projectile damages boss");
                Next(boss); Require(boss.Phase == OctopusPhase.MemoryShow, "starts color sequence");
                for (int i = 0; i < 4; i++) Next(boss);
                Require(boss.Phase == OctopusPhase.MemoryHide && boss.MemoryStep == 0, "memory sequence starts red");
                HideBehind(boss, 0); hp = game.Health; Capture("boss-cover.png"); Next(boss);
                Require(game.Health == hp, "correct cover blocks blast");
                Next(boss); game.scenePlayer.transform.position = new Vector3(10, -7); ClearInvulnerability();
                hp = game.Health; Next(boss); Require(game.Health < hp, "wrong cover takes damage");
                Next(boss); HideBehind(boss, 2); Next(boss); Next(boss); HideBehind(boss, 3); Next(boss); Next(boss);
                Require(boss.Phase == OctopusPhase.Recovery, "four blasts complete");
                Next(boss); Require(boss.Phase == OctopusPhase.SweepWarning, "sweep telegraph"); Capture("boss-sweep-warning.png");
                Next(boss); game.scenePlayer.transform.position = boss.Position + Vector2.down * 5;
                ClearInvulnerability(); hp = game.Health; boss.Tick(boss.settings.sweepDuration * .5f);
                Require(game.Health < hp, "sweeping tentacle damage");
                Next(boss); Next(boss); Require(boss.Phase == OctopusPhase.SlamWarning, "double slam telegraph"); Capture("boss-slam-warning.png");
                Require(!OctopusBossEncounter.InsideZone(Vector2.down * 3, boss.slamZones[0]) && !OctopusBossEncounter.InsideZone(Vector2.down * 3, boss.slamZones[1]), "slam safe central gap");
                game.scenePlayer.transform.position = boss.slamZones[0].transform.position; ClearInvulnerability(); hp = game.Health;
                Next(boss); Require(game.Health < hp, "slam deals damage");
                Next(boss); Next(boss); Require(boss.Phase == OctopusPhase.InkWarning, "ink telegraph");
                Vector2 inkPoint = game.PlayerPosition; Next(boss);
                Require(boss.MovementMultiplier(inkPoint) < 1 && boss.MovementMultiplier(new Vector2(11, 7)) == 1, "ink slows only inside puddle");
                Capture("boss-ink.png");
                game.TogglePause(); float remaining = boss.Remaining;
                Require(game.State == RunState.Paused, "pause enabled");
                game.TogglePause(); Require(boss.Remaining == remaining && game.State == RunState.Playing, "pause freezes pattern");
                game.scenePlayer.transform.position = new Vector3(0, -6);
                Require(game.StartDance(), "dance usable in boss scene");
                hp = boss.Health; Invoke(game, "ResolveDance", true);
                for (int i = 0; i < 100; i++) Invoke(game, "TickShockwaves", .02f);
                Require(Mathf.Approximately(hp - boss.Health, game.settings.dance.damage), "dance wave hits boss once");
                boss.Damage(100000); Require(boss.IsDefeated && game.State == RunState.BossReady, "boss victory");
                Require(!boss.sweepArm.gameObject.activeSelf && !boss.slamZones[0].gameObject.activeSelf, "victory clears hazards");
                Capture("boss-victory.png");
                Write("PASS: correct/wrong/front cover, rock collision, stone/spear hits, four-color sequence, sweep and double slam telegraphs/damage, ink slow, pause, dance single-hit wave, boss defeat and hazard cleanup.");
                Application.Quit(0);
            }
            catch (Exception exception) { Write("FAIL: " + exception); Debug.LogException(exception); Application.Quit(1); }
        }
        void ClearInvulnerability() => typeof(SurvivalGame).GetField("invulnerability", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, 0f);
        void HideBehind(OctopusBossEncounter boss, int index)
        {
            var rock = boss.rocks[index]; Vector2 away = ((Vector2)rock.transform.position - boss.Position).normalized;
            game.scenePlayer.transform.position = (Vector2)rock.transform.position + away * 1.5f;
        }
        static void Next(OctopusBossEncounter boss) => boss.Tick(boss.Remaining + .001f);
        static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        void Capture(string name)
        {
            var capture = game.gameObject.AddComponent<SurvivalSmokeTest>(); capture.game = game; capture.enabled = false;
            Invoke(capture, "Capture", name); Destroy(capture);
        }
        static void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
        public static void Write(string text)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "--takeup-report");
            if (i >= 0 && i + 1 < args.Length) File.WriteAllText(args[i + 1], text);
            Debug.Log(text);
        }
    }
}
