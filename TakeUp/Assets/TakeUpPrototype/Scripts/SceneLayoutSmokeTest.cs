using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed class SceneLayoutSmokeTest : MonoBehaviour
    {
        SurvivalGame game;
        RectTransform header, card, pause, codex, dance;
        Vector2 headerPosition, cardPosition, pausePosition, codexPosition, dancePosition;
        Vector3 playerPosition, playerScale, cameraPosition;
        int slots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-layout-smoke") < 0) return;
            var owner = FindFirstObjectByType<SurvivalGame>();
            owner.gameObject.AddComponent<SceneLayoutSmokeTest>().Prepare(owner);
        }

        void Prepare(SurvivalGame owner)
        {
            game = owner;
            // Simulate edits made before pressing Play, before SurvivalGame.Start executes.
            game.scenePlayer.transform.position = playerPosition = new Vector3(3, 2, 0);
            game.scenePlayer.transform.localScale = playerScale = Vector3.one * 1.15f;
            game.worldCamera.orthographicSize = 11;
            game.worldCamera.transform.position = cameraPosition = new Vector3(4, 3, -10);
            game.followPlayer = false;
            header = FindRect("Status"); header.anchoredPosition += new Vector2(7, -9); headerPosition = header.anchoredPosition;
            card = FindRect("Augment Choices"); card.anchoredPosition += new Vector2(20, 15); cardPosition = card.anchoredPosition;
            pause = FindRect("Pause Menu"); pause.anchoredPosition += new Vector2(-25, 10); pausePosition = pause.anchoredPosition;
            codex = FindRect("Augment Codex"); codex.anchoredPosition += new Vector2(15, -10); codexPosition = codex.anchoredPosition;
            dance = FindRect("Stomp Dance"); dance.anchoredPosition += new Vector2(12, 8); dancePosition = dance.anchoredPosition;
            slots = game.hud.GetComponentsInChildren<HudLayoutSlot>(true).Length;
        }

        RectTransform FindRect(string name)
        {
            foreach (var rect in game.hud.GetComponentsInChildren<RectTransform>(true)) if (rect.name == name) return rect;
            throw new Exception("Missing scene UI: " + name);
        }

        void Update()
        {
            try
            {
                Require(game.PlayerPosition == (Vector2)playerPosition && game.scenePlayer.transform.localScale == playerScale, "player authored position/scale");
                Require(game.BaseCameraSize == 11 && game.worldCamera.transform.position == cameraPosition, "authored camera");
                Require(header.anchoredPosition == headerPosition, "header rect survives initialization");
                for (int i = 0; i < 3; i++)
                {
                    game.ToggleCodex();
                    foreach (var button in codex.GetComponentsInChildren<Button>(true))
                        if (button.name == "Button 다음") { button.onClick.Invoke(); break; }
                    foreach (var button in codex.GetComponentsInChildren<Button>(true))
                        if (button.name == "Button 이전") { button.onClick.Invoke(); break; }
                    game.ToggleCodex();
                    game.TogglePause(); game.TogglePause();
                    game.AddExperience(game.RequiredExperience); game.ChooseAugment(0);
                }
                Require(card.anchoredPosition == cardPosition && pause.anchoredPosition == pausePosition && codex.anchoredPosition == codexPosition, "modal layouts preserved");
                Require(game.hud.GetComponentsInChildren<HudLayoutSlot>(true).Length == slots, "no UI duplicates after choices/codex/pause");
                game.westBoundary.position = new Vector3(1, 0); game.eastBoundary.position = new Vector3(5, 0);
                game.southBoundary.position = new Vector3(0, 1); game.northBoundary.position = new Vector3(0, 4);
                game.MovePlayer(Vector2.one, 100);
                Require(game.PlayerPosition.x < 5 && game.PlayerPosition.y < 4 && game.PlayerPosition.x > 4, "edited arena clamp");
                Require(game.StartDash(), "dash starts");
                Call("TickDash", 1f);
                Require(game.PlayerPosition.x < 5 && game.PlayerPosition.y < 4, "dash respects edited boundaries");
                var template = Instantiate(game.stonePrefab);
                template.transform.localScale = new Vector3(0.43f, 0.57f, 1);
                template.color = Color.magenta; template.gameObject.SetActive(false);
                game.stonePrefab = template;
                int before = game.runtimeRoot.childCount;
                Call("Fire", Vector2.right, false);
                var projectile = game.runtimeRoot.GetChild(before).GetComponent<SpriteRenderer>();
                Require(projectile != null && projectile.color == Color.magenta && projectile.transform.localScale == template.transform.localScale, "projectile prefab scale and color preserved");
                var enemy = new EnemyDefinition { prefab = template, health = 10, speed = 1 };
                before = game.runtimeRoot.childCount;
                Call("SpawnEnemy", enemy, new Vector2(10, 10));
                Require(game.runtimeRoot.GetChild(before).localScale == template.transform.localScale, "enemy prefab scale preserved");
                Destroy(template.gameObject);
                Require(game.StartDance(), "dance starts after dash");
                typeof(SurvivalHud).GetMethod("RefreshSkillHud", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.hud, null);
                Require(dance.anchoredPosition == dancePosition, "dance layout preserved");
                Require(game.hud.GetComponentsInChildren<HudLayoutSlot>(true).Length == slots, "dance reuses authored notes");
                Write("PASS: pre-Play player/camera/UI edits preserved; repeated choices, codex pagination and pause reuse saved slots; edited boundaries constrain movement and dash; enemy/projectile prefab appearance retained; dance reuses authored UI.");
                Application.Quit(0);
            }
            catch (Exception error) { Write("FAIL: " + error); Debug.LogException(error); Application.Quit(1); }
            enabled = false;
        }

        void Call(string method, params object[] arguments) => typeof(SurvivalGame).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, arguments);
        static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        static void Write(string text)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--takeup-report");
            if (index >= 0 && index + 1 < args.Length) File.WriteAllText(args[index + 1], text);
            Debug.Log(text);
        }
    }
}
