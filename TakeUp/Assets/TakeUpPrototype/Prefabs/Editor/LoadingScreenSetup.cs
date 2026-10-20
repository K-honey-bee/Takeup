using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public static class LoadingScreenSetup
    {
        const string Folder = "Assets/TakeUpPrototype/Prefabs/Resources";
        [MenuItem("TakeUp/Setup Loading Screen")]
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/TakeUpPrototype/Prefabs", "Resources");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/LoadingScreen.prefab") != null) return;
            var root = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(SceneLoader));
            try
            {
                var loader = root.GetComponent<SceneLoader>();
                loader.screenCanvas = root.GetComponent<Canvas>();
                loader.screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                loader.screenCanvas.sortingOrder = 32767;
                loader.screenGroup = root.GetComponent<CanvasGroup>();
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = .5f;
                loader.blackBackground = Make<Image>(root.transform, "Black Background", Vector2.zero, Vector2.zero);
                var black = loader.blackBackground.rectTransform;
                black.anchorMin = Vector2.zero; black.anchorMax = Vector2.one;
                black.offsetMin = black.offsetMax = Vector2.zero;
                loader.blackBackground.color = Color.black;
                loader.blackBackground.raycastTarget = true;
                loader.artwork = Make<Image>(black, "Your Artwork", new Vector2(0, 80), new Vector2(1100, 650));
                loader.artwork.preserveAspect = true;
                loader.artwork.raycastTarget = false;
                loader.artwork.enabled = false;
                loader.loadingText = Label(black, "Loading Text", "로딩 중…", new Vector2(0, -330), 32);
                loader.tipText = Label(black, "Tip Text", "", new Vector2(0, -420), 26);
                loader.tips = new[] { new LoadingTip() };
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/LoadingScreen.prefab");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        static T Make<T>(Transform parent, string name, Vector2 position, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return go.GetComponent<T>();
        }

        static Text Label(Transform parent, string name, string value, Vector2 position, int size)
        {
            var text = Make<Text>(parent, name, position, new Vector2(1500, 100));
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        public static void InstallAndBuild() { Install(); MenuFlowSmokeTest.BuildValidationPlayer(); }
    }
}
