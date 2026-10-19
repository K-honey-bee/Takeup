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
            if (!Click("Main", "Button")) yield break;
            float deadline = Time.realtimeSinceStartup + 20;
            while (SceneManager.GetActiveScene().name != "Charater_Select" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            if (!Click("Charater_Select", "Image")) yield break;
            deadline = Time.realtimeSinceStartup + 20;
            while (SceneManager.GetActiveScene().name != "PrehistoricPrototype" && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            var game = FindFirstObjectByType<SurvivalGame>();
            bool passed = SceneManager.GetActiveScene().name == "PrehistoricPrototype" && game != null && game.State == RunState.Playing;
            Debug.Log(passed ? "PASS: Main START -> character first Image -> PrehistoricPrototype Playing" : "FAIL: gameplay did not initialize");
            Application.Quit(passed ? 0 : 1);
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
                    throw new Exception("Button is blocked from pointer input: " + objectName);
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
