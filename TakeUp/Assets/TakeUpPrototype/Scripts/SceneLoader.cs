using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    [Serializable]
    public sealed class LoadingTip
    {
        [Tooltip("선택 사항. 비워두면 검정 배경에 글자만 표시됩니다.")]
        public Sprite image;
        [TextArea(2, 5)] public string tip;
    }

    public sealed class SceneLoader : MonoBehaviour
    {
        [Header("여기에 그림과 팁 추가 / 전환할 때 한 항목을 무작위 선택")]
        public LoadingTip[] tips = Array.Empty<LoadingTip>();
        [Header("전환 시간 / 로딩 중에는 항상 완전히 검정")]
        [Min(0)] public float fadeDuration = .2f;
        [Min(0)] public float minimumDisplayTime = .7f;
        [Header("프리팹 UI 연결")]
        public Canvas screenCanvas;
        public CanvasGroup screenGroup;
        public Image blackBackground;
        public Image artwork;
        public Text loadingText;
        public Text tipText;

        static SceneLoader instance;
        public static bool IsLoading => instance != null && instance.busy;
        bool busy;
        int lastTip = -1;
        Font runtimeFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenCanvas.sortingOrder = 32767;
            runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 28);
            loadingText.font = tipText.font = runtimeFont;
            // The art is an independent overlay: its transparency never exposes the old scene.
            blackBackground.color = Color.black;
            blackBackground.raycastTarget = true;
            Hide();
        }

        public static bool Load(string sceneName)
        {
            if (IsLoading) return false;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("Cannot load scene: " + sceneName);
                return false;
            }
            if (instance == null)
            {
                var prefab = Resources.Load<SceneLoader>("LoadingScreen");
                if (prefab == null) { Debug.LogError("LoadingScreen prefab missing from Resources"); return false; }
                Instantiate(prefab);
            }
            instance.busy = true;
            instance.StartCoroutine(instance.Transition(sceneName));
            return true;
        }

        void ChooseTip()
        {
            artwork.sprite = null;
            artwork.enabled = false;
            tipText.text = "";
            int count = tips == null ? 0 : tips.Length;
            if (count == 0) return;
            int index = UnityEngine.Random.Range(0, count);
            if (count > 1 && index == lastTip) index = (index + UnityEngine.Random.Range(1, count)) % count;
            lastTip = index;
            var entry = tips[index];
            if (entry == null) return;
            artwork.sprite = entry.image;
            artwork.enabled = entry.image != null;
            artwork.preserveAspect = true;
            tipText.text = entry.tip ?? "";
        }

        IEnumerator Transition(string sceneName)
        {
            Time.timeScale = 1;
            ChooseTip();
            loadingText.text = "로딩 중…";
            blackBackground.color = Color.black;
            screenCanvas.enabled = true;
            screenGroup.blocksRaycasts = true;
            screenGroup.interactable = false;
            yield return Fade(0, 1);
            // Give the fully opaque overlay a frame before beginning scene loading.
            Canvas.ForceUpdateCanvases();
            yield return null;
            float visibleSince = Time.unscaledTime;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(sceneName); }
            catch (Exception error) { Debug.LogException(error); }
            if (operation == null) { yield return Fade(1, 0); Hide(); yield break; }
            operation.allowSceneActivation = false;
            while (operation.progress < .9f || Time.unscaledTime - visibleSince < minimumDisplayTime)
                yield return null;
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            // Keep the new scene covered while its Awake/Start and UI layout finish.
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return Fade(1, 0);
            Hide();
        }

        IEnumerator Fade(float from, float to)
        {
            screenGroup.alpha = from;
            float elapsed = 0;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                screenGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            screenGroup.alpha = to;
        }

        void Hide()
        {
            screenGroup.alpha = 0;
            screenGroup.blocksRaycasts = false;
            screenCanvas.enabled = false;
            busy = false;
        }

        void OnDestroy()
        {
            if (runtimeFont != null) Destroy(runtimeFont);
            if (instance == this) instance = null;
        }
    }
}
