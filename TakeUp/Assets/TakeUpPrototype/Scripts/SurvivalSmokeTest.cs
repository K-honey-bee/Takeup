using System;
using System.IO;
using UnityEngine;

namespace TakeUp.Prototype
{
    // Only enabled by an explicit command-line flag; never runs during normal play.
    public sealed class SurvivalSmokeTest : MonoBehaviour
    {
        public SurvivalGame game;
        float started;
        int phase;
        static bool restarting;
        bool captured;
        void Start() { started = Time.realtimeSinceStartup; }
        void Update()
        {
            try
            {
                if (phase == 0)
                {
                    if (restarting)
                    {
                        Require(game.Level == 1 && game.Kills == 0 && game.Health == game.MaximumHealth && game.State == RunState.Playing, "restart reset");
                        Write("PASS: lethal damage, death state, restart resets run.");
                        Application.Quit(0); enabled = false; return;
                    }
                    Vector2 before = game.PlayerPosition;
                    game.MovePlayer(Vector2.one, 0.1f);
                    Require(Vector2.Distance(before, game.PlayerPosition) > 0.1f, "movement");
                    game.AddExperience(game.RequiredExperience + game.settings.firstLevelExperience + game.settings.experienceGrowth);
                    Require(game.Level == 3 && game.PendingChoices == 2, "multiple level rewards");
                    Require(game.State == RunState.ChoosingAugment && game.Choices.Count == 3, "three cards and pause");
                    Capture("augment-choices.png");
                    phase = 10;
                }
                if (phase == 10)
                {
                    if (Time.realtimeSinceStartup - started < 1) return;
                    game.ChooseAugment(0); Require(game.PendingChoices == 1, "queued selection");
                    game.ChooseAugment(0); Require(game.State == RunState.Playing, "resume combat");
                    var original = game.settings.augments;
                    game.settings.augments = new[] { new AugmentDefinition { title = "Test spear", kind = AugmentKind.Spear, amount = 1, maxStacks = 1 } };
                    game.AddExperience(game.RequiredExperience); game.ChooseAugment(0);
                    Require(game.HasSpear, "spear unlock");
                    game.settings.augments = original;
                    game.TogglePause();
                    Vector2 before = game.PlayerPosition; game.MovePlayer(Vector2.right, 1);
                    Require(game.PlayerPosition == before && game.State == RunState.Paused, "pause blocks movement");
                    game.TogglePause();
                    phase = 1;
                }
                if (phase == 1)
                {
                    if (game.State == RunState.ChoosingAugment) game.ChooseAugment(0);
                    if (!captured && Time.realtimeSinceStartup - started > 16) { Capture("combat.png"); captured = true; }
                    if (Time.realtimeSinceStartup - started < 22) return;
                    Require(game.Kills > 0 && game.AliveEnemies > 0, "spawn chase auto attack kill");
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-death") >= 0)
                    {
                        game.DamagePlayer(100000);
                        if (game.State != RunState.Dead) return; // Wait out any active contact invulnerability.
                        restarting = true; game.Restart(); enabled = false; return;
                    }
                    game.FinishStage(); Require(game.State == RunState.BossReady, "stage completion");
                    Capture("stage-complete.png");
                    Write("PASS: movement, multi-level XP, 3 choices, queued rewards, spear unlock, pause, spawn/chase/combat/kills, stage completion. Kills=" + game.Kills);
                    phase = 2;
                }
                if (phase == 2 && Time.realtimeSinceStartup - started > 24) Application.Quit(0);
            }
            catch (Exception error) { Write("FAIL: " + error); Debug.LogException(error); Application.Quit(1); enabled = false; }
        }
        static void Require(bool condition, string label) { if (!condition) throw new Exception(label); }
        void Capture(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--takeup-capture");
            if (index < 0 || index + 1 >= args.Length) return;
            // Hidden test windows do not reliably present a framebuffer. Render explicitly,
            // including the HUD, without changing normal gameplay's overlay canvas.
            var canvas = game.GetComponentInChildren<Canvas>();
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldDistance = canvas.planeDistance;
            var oldOrder = canvas.sortingOrder;
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = game.worldCamera;
                canvas.planeDistance = 1;
                canvas.sortingOrder = 1000;
                Canvas.ForceUpdateCanvases();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(game.worldCamera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(args[index + 1], name), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                canvas.sortingOrder = oldOrder;
                target.Release(); Destroy(target); if (image != null) Destroy(image);
            }
        }
        static void Write(string text)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--takeup-report");
            string path = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.persistentDataPath, "smoke-report.txt");
            File.WriteAllText(path, text);
        }
    }
}
