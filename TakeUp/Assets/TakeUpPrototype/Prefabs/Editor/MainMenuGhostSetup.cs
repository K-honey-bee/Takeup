using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TakeUp.Prototype
{
    public static class MainMenuGhostSetup
    {
        [MenuItem("TakeUp/Setup Ghost Main Menu")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene("Assets/TakeUpPrototype/Scenes/Main.unity");
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) throw new System.Exception("Main Canvas missing");
            var existing = canvas.transform.Find("Ghost Background");
            if (existing == null)
            {
                var root = new GameObject("Ghost Background", typeof(RectTransform), typeof(MainMenuGhosts));
                var rect = (RectTransform)root.transform;
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling();
            }
            EditorSceneManager.SaveScene(scene);
        }

        public static void InstallAndBuild()
        {
            Install();
            MenuFlowSmokeTest.BuildValidationPlayer();
        }
    }
}
