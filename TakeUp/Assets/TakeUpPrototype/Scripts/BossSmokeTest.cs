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
                HideBehind(boss, boss.CurrentMemoryColor); hp = game.Health; Capture("boss-cover.png"); Next(boss);
                Require(game.Health == hp, "correct cover blocks blast");
                Next(boss); game.scenePlayer.transform.position = new Vector3(10, -7); ClearInvulnerability();
                hp = game.Health; Next(boss); Require(game.Health < hp, "wrong cover takes damage");
                Next(boss); HideBehind(boss, boss.CurrentMemoryColor); Next(boss); Next(boss); HideBehind(boss, boss.CurrentMemoryColor); Next(boss); Next(boss);
                Require(boss.Phase == OctopusPhase.Recovery, "four blasts complete");
                Next(boss); Require(boss.Phase == OctopusPhase.SweepWarning, "sweep telegraph"); Capture("boss-sweep-warning.png");
                Next(boss); game.scenePlayer.transform.position = boss.Position + Vector2.down * 5;
                ClearInvulnerability(); hp = game.Health; boss.Tick(boss.settings.sweepDuration * .5f);
                Require(game.Health < hp, "sweeping tentacle damage");
                Next(boss); Next(boss); Require(boss.Phase == OctopusPhase.SlamWarning, "double slam telegraph"); Capture("boss-slam-warning.png");
                Require(!OctopusBossEncounter.InsideZone(Vector2.down * 3, boss.slamZones[0]) && !OctopusBossEncounter.InsideZone(Vector2.down * 3, boss.slamZones[1]), "slam safe central gap");
                game.scenePlayer.transform.position = boss.slamZones[0].transform.position; ClearInvulnerability(); hp = game.Health;
                Next(boss); Require(game.Health < hp, "slam deals damage");
                Next(boss); Next(boss); Require(boss.Phase == OctopusPhase.MemoryShow, "memory and tentacles alternate above half health");
                game.TogglePause(); float remaining = boss.Remaining;
                Require(game.State == RunState.Paused, "pause enabled");
                game.TogglePause(); Require(boss.Remaining == remaining && game.State == RunState.Playing, "pause freezes pattern");
                game.scenePlayer.transform.position = new Vector3(0, -6);
                Require(game.StartDance(), "dance usable in boss scene");
                hp = boss.Health; Invoke(game, "ResolveDance", true);
                for (int i = 0; i < 100; i++) Invoke(game, "TickShockwaves", .02f);
                Require(Mathf.Approximately(hp - boss.Health, game.settings.dance.damage), "dance wave hits boss once");
                boss.Damage(boss.Health - boss.settings.health * .5f);
                Require(!boss.IsEnraged, "exactly 50 percent remains normal");
                boss.Damage(1);
                Require(boss.IsEnraged && boss.Phase == OctopusPhase.RageWarning, "below half immediately switches to rage");
                Capture("boss-rage-warning.png");
                for (int i = 0; i < 8; i++)
                {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var center = (Vector2)boss.GetType().GetField("rageCenter", flags).GetValue(boss);
                    var direction = (Vector2)boss.GetType().GetField("rageDirection", flags).GetValue(boss);
                    game.scenePlayer.transform.position = center + new Vector2(-direction.y, direction.x) * 3;
                    Next(boss); Require(boss.Phase == OctopusPhase.RageImpact, "rage impact");
                    Next(boss); Next(boss); Require(boss.Phase == OctopusPhase.RageWarning, "rage only repeats tentacles");
                }
                boss.Damage(100000);
                Require(boss.IsFinale && !boss.IsDefeated && boss.Health == 0, "zero HP starts finale, not victory");
                boss.Damage(100000); Require(boss.Phase == OctopusPhase.FinaleShow, "finale ignores further damage");
                for (int i = 0; i < 5; i++) Next(boss);
                Require(boss.Phase == OctopusPhase.FinaleChoose, "exactly five color flashes");
                MoveToFinalColor(boss, (boss.CurrentMemoryColor + 1) % 4); Next(boss);
                Require(Mathf.Approximately(boss.Health, boss.settings.health * .3f) && !boss.IsFinale, "wrong color revives at 30 percent");
                Next(boss); Require(boss.Phase == OctopusPhase.RageWarning, "revival resumes rage");
                boss.Damage(100000);
                for (int i = 0; i < 5; i++) Next(boss);
                Capture("boss-finale-circle.png");
                hp = game.Health;
                for (int i = 0; i < 5; i++)
                {
                    MoveToFinalColor(boss, boss.CurrentMemoryColor); Next(boss);
                    Require(boss.Phase == OctopusPhase.FinaleImpact, "correct color accepted " + i);
                    Next(boss);
                }
                Require(game.Health == hp, "correct tentacles do not damage player");
                Require(boss.IsDefeated && game.State == RunState.BossReady, "five correct hits clear boss");
                Require(!boss.sweepArm.gameObject.activeSelf && !boss.slamZones[0].gameObject.activeSelf, "victory clears hazards");
                Capture("boss-victory.png");
                Write("PASS: random color cover, projectiles, sweep/slam, pause/dance, exact 50% boundary, rage repetition, five finale flashes, damage immunity, wrong color revives 30%, retry, five correct contacts win.");
                Application.Quit(0);
            }
            catch (Exception exception) { Write("FAIL: " + exception); Debug.LogException(exception); Application.Quit(1); }
        }
        void MoveToFinalColor(OctopusBossEncounter boss, int color)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                Vector2 point = boss.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 5;
                if (boss.FinaleColorAt(point) == color) { game.scenePlayer.transform.position = point; return; }
            }
            throw new Exception("No reachable color sector");
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
