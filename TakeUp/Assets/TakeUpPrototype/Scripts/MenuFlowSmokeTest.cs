using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed class MenuFlowSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--takeup-menu-smoke") < 0) return;
            var runner = new GameObject("Menu flow validation");
            DontDestroyOnLoad(runner); runner.AddComponent<MenuFlowSmokeTest>();
        }

        IEnumerator Start()
        {
            yield return null;
            var ghosts = FindFirstObjectByType<MainMenuGhosts>();
            if (ghosts != null)
            {
                try { ghosts.ValidateForSmokeTest(); }
                catch (Exception error) { Debug.LogException(error); Application.Quit(1); yield break; }
            }
            if (!Click("Main", "Button")) yield break;
            float deadline = Time.realtimeSinceStartup + 20;
            var loading = FindFirstObjectByType<SceneLoader>();
            while (loading != null && loading.screenGroup.alpha < 1 && Time.realtimeSinceStartup < deadline) yield return null;
            if (!ValidateLoading(loading)) yield break;
            while ((SceneManager.GetActiveScene().name != "Charater_Select" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            var story = FindFirstObjectByType<StoryDialogue>();
            if (story != null)
            {
                if (!ValidateStory(story, false)) yield break;
                SceneLoader.Load("Charater_Select");
                deadline = Time.realtimeSinceStartup + 20;
                while (SceneLoader.IsLoading && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                if (!ValidateStory(FindFirstObjectByType<StoryDialogue>(), true)) yield break;
            }
            yield return null;
            if (!Click("Charater_Select", "Image")) yield break;
            deadline = Time.realtimeSinceStartup + 20;
            while ((SceneManager.GetActiveScene().name != "PrehistoricPrototype" || SceneLoader.IsLoading) && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            var game = FindFirstObjectByType<SurvivalGame>();
            bool passed = SceneManager.GetActiveScene().name == "PrehistoricPrototype" && game != null && game.State == RunState.Playing;
            Debug.Log(passed ? "PASS: Main START -> character first Image -> PrehistoricPrototype Playing" : "FAIL: gameplay did not initialize");
            Application.Quit(passed ? 0 : 1);
        }

        bool ValidateStory(StoryDialogue story, bool skip)
        {
            try
            {
                if (story == null) throw new Exception("Story controller missing");
                if (story.lines.Length == 0) return story.Finished;
                if (story.startButton.activeSelf) throw new Exception("Start button exposed before story ends");
                if (skip)
                {
                    story.UpdateSpaceHold(true, story.holdSpaceSeconds * .5f);
                    story.UpdateSpaceHold(false, 0);
                    if (story.Finished) throw new Exception("Short space press skipped story");
                    story.UpdateSpaceHold(true, story.holdSpaceSeconds + .1f);
                }
                else
                {
                    int before = story.CurrentLine;
                    if (story.IsTyping)
                    {
                        story.Advance();
                        if (story.IsTyping || story.CurrentLine != before) throw new Exception("Ctrl must reveal text before advancing");
                    }
                    for (int i = 0; i < story.lines.Length * 2 + 2 && !story.Finished; i++) story.Advance();
                }
                if (!story.Finished || !story.startButton.activeSelf || story.dialoguePanel.activeSelf
                    || story.voiceSource.isPlaying || story.voiceVideo.isPlaying || SceneLoader.IsLoading)
                    throw new Exception("Story completion must stop voice and reveal button without starting game");
                Debug.Log(skip ? "PASS: hold Space skips, short press resets, no auto-start" : "PASS: Ctrl reveal/advance, story completion reveals start button");
                return true;
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); return false; }
        }

        bool ValidateLoading(SceneLoader loading)
        {
            try
            {
                if (loading == null || !SceneLoader.IsLoading || loading.screenGroup.alpha != 1
                    || loading.blackBackground.color != Color.black || !loading.screenGroup.blocksRaycasts
                    || !loading.blackBackground.raycastTarget)
                    throw new Exception("Loading screen must be fully opaque black and block clicks");
                var rect = loading.blackBackground.rectTransform;
                if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one
                    || rect.offsetMin != Vector2.zero || rect.offsetMax != Vector2.zero)
                    throw new Exception("Black background must cover entire canvas");
                if (SceneLoader.Load("Charater_Select")) throw new Exception("Duplicate transition accepted");
                // Exercise optional artwork and random tip selection without requiring user assets.
                var previous = loading.tips;
                loading.tips = new[] { new LoadingTip { tip = "A" }, new LoadingTip { tip = "B" } };
                var choose = typeof(SceneLoader).GetMethod("ChooseTip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                choose.Invoke(loading, null);
                string first = loading.tipText.text;
                choose.Invoke(loading, null);
                if (first == loading.tipText.text || loading.artwork.enabled)
                    throw new Exception("Tips must alternate without immediate repetition; empty artwork must be hidden");
                loading.tips = previous;
                choose.Invoke(loading, null);
                Debug.Log("PASS: opaque full-screen black loading overlay, duplicate guard, random tips, optional artwork");
                return true;
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); return false; }
        }

        bool Click(string scene, string objectName)
        {
            try
            {
                if (SceneManager.GetActiveScene().name != scene) throw new Exception("Expected scene: " + scene);
                var button = GameObject.Find("Canvas/" + objectName).GetComponent<Button>();
                if (button == null || !button.interactable || button.onClick.GetPersistentEventCount() != 1)
                    throw new Exception("Missing wired button: " + objectName);
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)button.transform;
                var canvas = button.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var pointer = new PointerEventData(EventSystem.current) {
                    position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                    button = PointerEventData.InputButton.Left
                };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button)
                    throw new Exception("Button is blocked from pointer input: " + objectName
                        + " / first hit: " + (hits.Count == 0 ? "(none)" : hits[0].gameObject.name));
                ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                return true;
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); return false; }
        }

#if UNITY_EDITOR
        public static void BuildValidationPlayer()
        {
            var scenes = new List<string>();
            foreach (var scene in UnityEditor.EditorBuildSettings.scenes) if (scene.enabled) scenes.Add(scene.path);
            if (!scenes[0].EndsWith("/Main.unity")) throw new Exception("Main must be the first build scene");
            var result = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions {
                scenes = scenes.ToArray(), locationPathName = "Builds/MenuValidation/TakeUpMenu.exe",
                target = UnityEditor.BuildTarget.StandaloneWindows64, options = UnityEditor.BuildOptions.Development
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Menu validation build failed");
        }
#endif
    }
}
