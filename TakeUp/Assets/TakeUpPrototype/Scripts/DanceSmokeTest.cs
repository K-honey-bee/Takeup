using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TakeUp.Prototype
{
    // Explicit opt-in integration test for a development player; never enabled during normal play.
    public sealed class DanceSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        int phase, killsBefore;
        float since, started;
        double pauseTime;
        Vector2 startPosition;
        void Start() { started = since = Time.realtimeSinceStartup; game.settings.dance.cooldown = 0.2f; }
        void Update()
        {
            try
            {
                if (Time.realtimeSinceStartup - started > 30) throw new Exception("Dance smoke timeout");
                switch (phase)
                {
                    case 0:
                        Require(game.StartDash() && !game.StartDash(), "dash cooldown");
                        float health = game.Health; game.DamagePlayer(10); Require(game.Health == health, "dash protection");
                        game.ToggleCodex(); Require(game.State == RunState.Codex, "codex opens");
                        startPosition = game.PlayerPosition; game.MovePlayer(Vector2.one, 1);
                        Require(game.PlayerPosition == startPosition, "codex pauses movement");
                        Capture("codex.png"); game.ToggleCodex(); phase = 1; break;
                    case 1:
                        if (game.IsDashing) break;
                        Require(Vector2.Distance(game.PlayerPosition, startPosition) > 3, "dash displacement");
                        Require(game.StartDance(), "dance starts");
                        startPosition = game.PlayerPosition; game.MovePlayer(Vector2.right, 1);
                        Require(game.PlayerPosition == startPosition && !game.StartDash(), "dance locks movement and dash");
                        game.SubmitDanceInput(BeatDirection.Right);
                        Require(game.DanceResolved && !game.DanceSucceeded, "wrong input aborts");
                        Capture("dance-failure.png"); since = Time.realtimeSinceStartup; phase = 2; break;
                    case 2:
                        if (Time.realtimeSinceStartup - since < 1.2f || game.State != RunState.Playing || game.DanceCooldown > 0) break;
                        Require(game.State == RunState.Playing && Mathf.Abs(game.worldCamera.orthographicSize - game.settings.cameraSize) < 0.1f, "failure restores world and camera");
                        Require(game.StartDance(), "retry starts"); phase = 3; break;
                    case 3:
                        if (game.DanceTime < game.DanceJudge.TimeFor(0)) break;
                        game.SubmitDanceInput(game.DanceJudge.Note(0));
                        Require(game.DanceJudge.Hits == 1, "first beat hit");
                        game.TogglePause(); pauseTime = game.DanceTime; since = Time.realtimeSinceStartup; phase = 4; break;
                    case 4:
                        if (Time.realtimeSinceStartup - since < 0.4f) break;
                        Require(game.State == RunState.Paused && Math.Abs(game.DanceTime - pauseTime) < 0.001, "pause freezes DSP judgement");
                        game.TogglePause(); Require(game.State == RunState.Dancing, "pause resumes dance"); phase = 5; break;
                    case 5:
                        Require(!game.DanceResolved, "no unintended misses");
                        int next = game.DanceJudge.Hits;
                        if (game.DanceTime < game.DanceJudge.TimeFor(next)) break;
                        if (next == game.DanceJudge.Count - 1)
                        {
                            Require(game.worldCamera.orthographicSize < game.settings.cameraSize * 0.8f, "progressive camera zoom");
                            var enemy = new EnemyDefinition { health = 10, speed = 0.01f, damage = 1, experience = 1 };
                            typeof(SurvivalGame).GetMethod("SpawnEnemy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game,
                                new object[] { enemy, game.PlayerPosition + Vector2.right * 3 });
                            killsBefore = game.Kills;
                        }
                        game.SubmitDanceInput(game.DanceJudge.Note(next));
                        if (game.DanceJudge.Complete)
                        {
                            Require(game.DanceSucceeded, "perfect success"); Capture("dance-zoom.png"); since = Time.realtimeSinceStartup; phase = 6;
                        }
                        break;
                    case 6:
                        if (Time.realtimeSinceStartup - since < 0.35f) break;
                        Capture("dance-shockwave.png");
                        Require(game.Kills > killsBefore, "expanding wave damages enemies"); phase = 7; break;
                    case 7:
                        if (Time.realtimeSinceStartup - since < 1.5f || game.State != RunState.Playing || game.DanceCooldown > 0) break;
                        Require(game.State == RunState.Playing && Mathf.Abs(game.worldCamera.orthographicSize - game.settings.cameraSize) < 0.1f, "success restores camera and combat");
                        Require(game.PlayerPosition == startPosition, "dance stays in place");
                        Require(game.StartDance(), "miss test starts"); since = Time.realtimeSinceStartup; phase = 8; break;
                    case 8:
                        if (!game.DanceResolved) break;
                        Require(!game.DanceSucceeded, "missing input fails");
                        Write("PASS: codex pause, dash/cooldown/protection, stationary dance, wrong/missed input, pause/resume audio clock, progressive zoom, perfect sequence, AoE damage, return to combat.");
                        Application.Quit(0); enabled = false; break;
                }
            }
            catch (Exception exception) { Write("FAIL: " + exception); Debug.LogException(exception); Application.Quit(1); enabled = false; }
        }
        void Capture(string name)
        {
            // Reuse the verified offscreen capture path without running the old smoke test.
            var capture = game.gameObject.AddComponent<SurvivalSmokeTest>(); capture.enabled = false; capture.game = game;
            typeof(SurvivalSmokeTest).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(capture, new object[] { name });
            Destroy(capture);
        }
        static void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
        static void Write(string result)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--takeup-report");
            if (index >= 0 && index + 1 < args.Length) File.WriteAllText(args[index + 1], result);
        }
    }
}
