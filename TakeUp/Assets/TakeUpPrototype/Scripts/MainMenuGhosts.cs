using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MainMenuGhosts : MonoBehaviour
    {
        [Header("메인 배경")]

        public Color backgroundColor = new Color(.46f, .48f, .50f, 1);
        public Color ghostColor = new Color(.90f, .97f, .93f, 1);
        public Color outlineColor = new Color(.19f, .24f, .25f, 1);

        [Header("또잉 등장")]

        [Min(.1f)] public float popDuration = .8f;
        [Min(0)] public float firstDelay = .35f;
        [Min(0)] public float stagger = .65f;
        [Range(.2f, .65f)] public float sizeRatio = .48f;
        [Range(.35f, .85f)] public float visibleFraction = .65f;
        [Min(0)] public float stayDuration = 1.5f;
        [Min(0)] public float hiddenDuration = 0.5f;

        [Header("마우스를 따라가는 눈")]
        [Range(0, 16)] public float pupilTravel = 9;
        [Min(.1f)] public float eyeFollowSpeed = 12;

        sealed class Ghost
        {
            public RectTransform root;
            public RectTransform[] pupils;
            public Vector2[] eyeCenters;
        }
        readonly Ghost[] ghosts = new Ghost[3];
        RectTransform area;
        Canvas canvas;
        float age;

        void Awake()
        {
            area = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            var backdrop = gameObject.AddComponent<Image>();
            backdrop.color = backgroundColor;
            backdrop.raycastTarget = false;
            gameObject.AddComponent<RectMask2D>();
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = CreateGhost(i);
            LayoutGhosts(0);
        }

        Ghost CreateGhost(int index)
        {
            var root = new GameObject("Ghost " + (index + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.sizeDelta = new Vector2(300, 300);
            root.localRotation = Quaternion.Euler(0, 0, index == 0 ? 180 : index == 1 ? 90 : 0);
            Oval(root, "Outline", Vector2.zero, new Vector2(300, 300), outlineColor);
            Oval(root, "Face", Vector2.zero, new Vector2(290, 290), ghostColor);
            // The face sits toward the edge that peeks into the screen.
            Vector2[] centers = { new Vector2(-48, 42), new Vector2(48, 42) };
            var result = new Ghost { root = root, eyeCenters = centers, pupils = new RectTransform[2] };
            for (int i = 0; i < 2; i++)
            {
                Oval(root, "Eye outline", centers[i], new Vector2(55, 66), outlineColor);
                Oval(root, "Eye white", centers[i], new Vector2(47, 58), Color.white);
                result.pupils[i] = Oval(root, "Pupil", centers[i], new Vector2(22, 30), outlineColor);
                Oval(result.pupils[i], "Eye glint", new Vector2(-4, 6), new Vector2(6, 7), Color.white);
            }
            Oval(root, "Smile", new Vector2(0, -12), new Vector2(35, 25), outlineColor);
            Oval(root, "Tongue", new Vector2(0, -19), new Vector2(18, 8), new Color(.91f, .61f, .64f));
            Oval(root, "Left cheek", new Vector2(-89, 5), new Vector2(30, 15), new Color(.65f, .84f, .76f));
            Oval(root, "Right cheek", new Vector2(89, 5), new Vector2(30, 15), new Color(.65f, .84f, .76f));
            return result;
        }

        static RectTransform Oval(Transform parent, string label, Vector2 position, Vector2 size, Color color)
        {
            var graphic = new GameObject(label, typeof(RectTransform),typeof(CanvasRenderer), typeof(MenuGhostOval)).GetComponent<MenuGhostOval>();
            graphic.transform.SetParent(parent, false);
            graphic.rectTransform.anchoredPosition = position;
            graphic.rectTransform.sizeDelta = size;
            graphic.color = color;
            graphic.raycastTarget = false;
            return graphic.rectTransform;
        }

        void Update()
        {
            age += Time.unscaledDeltaTime;
            LayoutGhosts(age);
            Vector2 pointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * .5f, Screen.height * .5f);
            UpdateEyes(pointer, Time.unscaledDeltaTime);
        }

        void LayoutGhosts(float time)
        {
            float duration = Mathf.Max(.1f, popDuration);

            // 등장 → 대기 → 퇴장 → 숨기
            float cycleDuration =
                duration + stayDuration + duration + hiddenDuration;

            float elapsed = time - firstDelay;

            // 처음에는 모두 숨긴다.
            if (elapsed < 0)
            {
                foreach (var ghost in ghosts)
                    ghost.root.gameObject.SetActive(false);

                return;
            }

            int cycle = Mathf.FloorToInt(elapsed / cycleDuration);
            float phaseTime = elapsed - cycle * cycleDuration;

            // 아래(2) → 오른쪽(1) → 위(0) 순서로 반복.
            int activeIndex = 2 - cycle % 3;

            float progress;

            if (phaseTime < duration)
            {
                // 등장: 목표 위치를 살짝 넘었다가 돌아오는 '또잉'.
                float t = phaseTime / duration;
                float x = t - 1f;

                progress =
                    1f + 2.70158f * x * x * x
                       + 1.70158f * x * x;
            }
            else if (phaseTime < duration + stayDuration)
            {
                // 나와서 마우스를 바라본다.
                progress = 1f;
            }
            else if (phaseTime < duration * 2f + stayDuration)
            {
                // 퇴장: 점점 빠르게 쏙 들어간다.
                float t =
                    (phaseTime - duration - stayDuration) / duration;

                progress = 1f - t * t;
            }
            else
            {
                progress = 0f;
            }

            float diameter = Mathf.Clamp(
                Mathf.Min(area.rect.width, area.rect.height) * sizeRatio,
                120f,
                520f
            );

            for (int i = 0; i < ghosts.Length; i++)
            {
                RectTransform root = ghosts[i].root;

                bool visible = i == activeIndex && progress > 0f;
                root.gameObject.SetActive(visible);

                if (!visible)
                    continue;

                Vector2 anchor =
                    i == 0 ? new Vector2(.68f, 1f) :
                    i == 1 ? new Vector2(1f, .53f) :
                             new Vector2(.66f, 0f);

                root.anchorMin = root.anchorMax = anchor;
                root.localScale = Vector3.one * (diameter / 300f);

                float peek = diameter * visibleFraction * progress;
                float outside = diameter * .5f + 2f;

                root.anchoredPosition =
                    i == 0 ? new Vector2(0f, outside - peek) :
                    i == 1 ? new Vector2(outside - peek, 0f) :
                             new Vector2(0f, -outside + peek);
            }
        }

        void UpdateEyes(Vector2 screenPoint, float dt)
        {
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var ghost in ghosts)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(ghost.root, screenPoint, camera, out Vector2 local)) continue;
                for (int i = 0; i < ghost.pupils.Length; i++)
                {
                    Vector2 offset = Vector2.ClampMagnitude((local - ghost.eyeCenters[i]) * .08f, Mathf.Min(9, pupilTravel));
                    ghost.pupils[i].anchoredPosition = Vector2.Lerp(ghost.pupils[i].anchoredPosition,
                        ghost.eyeCenters[i] + offset, 1 - Mathf.Exp(-eyeFollowSpeed * dt));
                }
            }
        }

        public void ValidateForSmokeTest()
        {
            if (ghosts[0] == null || ghosts[1] == null || ghosts[2] == null) throw new System.Exception("Ghost faces missing");
            foreach (var graphic in GetComponentsInChildren<Graphic>())
                if (graphic.raycastTarget) throw new System.Exception("Ghost background blocks menu input");
            LayoutGhosts(10);
            Canvas.ForceUpdateCanvases();
            UpdateEyes(new Vector2(Screen.width, Screen.height), 1);
            Vector2 first = ghosts[2].pupils[0].anchoredPosition;
            UpdateEyes(Vector2.zero, 1);
            if (Vector2.Distance(first, ghosts[2].pupils[0].anchoredPosition) < 1)
                throw new System.Exception("Ghost pupils do not follow pointer");
            foreach (var ghost in ghosts)
                for (int i = 0; i < 2; i++)
                    if (Vector2.Distance(ghost.pupils[i].anchoredPosition, ghost.eyeCenters[i]) > 9.01f)
                        throw new System.Exception("Pupil leaves eye bounds");
            Debug.Log("PASS: three menu ghosts, mouse tracking, bounded pupils, nonblocking graphics");
        }
    }
}
