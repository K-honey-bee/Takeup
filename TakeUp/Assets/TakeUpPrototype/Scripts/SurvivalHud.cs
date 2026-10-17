using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed class SurvivalHud : MonoBehaviour
    {
        SurvivalGame game;
        Font font;
        RectTransform root, overlay, content;
        Text status, healthLabel, xpLabel, equipment;
        Image healthFill, xpFill, stageFill;
        readonly Color ink = new Color(0.035f, 0.065f, 0.065f, 0.97f);
        readonly Color gold = new Color(1, 0.76f, 0.36f);

        public void Initialize(SurvivalGame owner)
        {
            game = owner;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial", "Liberation Sans" }, 22);
            var canvasObject = new GameObject("Survival HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            root = canvasObject.GetComponent<RectTransform>();
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
            }
            var header = Panel(root, "Status", ink, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -126), new Vector2(-18, -18));
            Label(header, "TAKEUP  /  선사시대", 22, gold, new Vector2(20, -8), new Vector2(420, 32));
            status = Label(header, "", 20, Color.white, new Vector2(460, -12), new Vector2(740, 32));
            healthFill = Bar(header, "HP", new Vector2(20, -49), new Vector2(400, 18), new Color(0.93f, 0.37f, 0.35f));
            healthLabel = Label(header, "", 13, Color.white, new Vector2(24, -49), new Vector2(390, 18));
            xpFill = Bar(header, "XP", new Vector2(20, -77), new Vector2(400, 14), new Color(0.32f, 0.85f, 0.66f));
            xpLabel = Label(header, "", 12, Color.white, new Vector2(24, -76), new Vector2(390, 18));
            equipment = Label(header, "", 15, new Color(0.7f, 0.78f, 0.76f), new Vector2(460, -50), new Vector2(740, 28));
            stageFill = Bar(header, "Stage", new Vector2(460, -86), new Vector2(740, 5), gold);
            var footer = Panel(root, "Controls", ink, Vector2.zero, new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 58));
            Label(footer, "WASD / 방향키  이동     ·     자동 공격     ·     1 / 2 / 3  증강 선택     ·     ESC  일시정지", 16,
                new Color(0.75f, 0.81f, 0.79f), new Vector2(20, -6), new Vector2(1200, 30));
            overlay = Panel(root, "Overlay", new Color(0.015f, 0.03f, 0.03f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            content = Panel(overlay, "Content", Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-530, -230), new Vector2(530, 230));
            HideOverlay();
        }

        void Update()
        {
            if (game == null || status == null) return;
            int seconds = Mathf.FloorToInt(game.Elapsed);
            status.text = $"레벨 {game.Level:00}    처치 {game.Kills:000}    웨이브 {game.Wave}    {seconds / 60:00}:{seconds % 60:00}";
            healthLabel.text = $"체력  {Mathf.CeilToInt(game.Health)} / {Mathf.CeilToInt(game.MaximumHealth)}";
            xpLabel.text = $"경험치  {game.Experience} / {game.RequiredExperience}";
            Fill(healthFill, game.Health / game.MaximumHealth);
            Fill(xpFill, (float)game.Experience / game.RequiredExperience);
            Fill(stageFill, game.Elapsed / game.settings.stageDuration);
            equipment.text = $"돌 던지기  ·  {(game.HasSpear ? "관통 창 장착" : "창 미해금")}     |     생존 목표 {game.settings.stageDuration:0}초";
        }

        public void ShowChoices()
        {
            ClearContent();
            Label(content, $"레벨 {game.Level}  /  새로운 생존 기술", 32, gold, new Vector2(0, 0), new Vector2(1060, 50), TextAnchor.MiddleCenter);
            Label(content, $"하나를 선택하세요. 선택 중에는 전투가 멈춥니다.  (남은 선택 {game.PendingChoices})", 18,
                Color.white, new Vector2(0, -56), new Vector2(1060, 32), TextAnchor.MiddleCenter);
            for (int i = 0; i < game.Choices.Count; i++)
            {
                int index = i;
                var choice = game.Choices[i];
                float x = (1060 - game.Choices.Count * 340 + 20) * 0.5f + i * 340;
                var card = Button(content, "", new Vector2(x, -120), new Vector2(320, 286), () => game.ChooseAugment(index));
                Label(card, $"0{i + 1}", 22, gold, new Vector2(22, -15), new Vector2(276, 36));
                if (choice.icon != null)
                {
                    var icon = Panel(card, "Icon", Color.white, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -66), new Vector2(-22, -18));
                    icon.GetComponent<Image>().sprite = choice.icon;
                    icon.GetComponent<Image>().preserveAspect = true;
                }
                Label(card, choice.title, 26, Color.white, new Vector2(22, -76), new Vector2(276, 48));
                Label(card, choice.description, 19, new Color(0.74f, 0.83f, 0.79f), new Vector2(22, -132), new Vector2(276, 94));
                Label(card, $"현재 보유 {game.StackCount(choice.kind)}회   ·   선택 [{i + 1}]", 14, gold, new Vector2(22, -240), new Vector2(276, 30));
                if (i == 0) EventSystem.current.SetSelectedGameObject(card.gameObject);
            }
        }

        public void ShowPause()
        {
            ClearContent();
            Label(content, "잠시 쉬어가기", 38, gold, new Vector2(0, -80), new Vector2(1060, 70), TextAnchor.MiddleCenter);
            Button(content, "계속하기  /  ESC", new Vector2(350, -200), new Vector2(360, 60), game.TogglePause);
            Button(content, "처음부터 다시 시작", new Vector2(350, -280), new Vector2(360, 60), game.Restart);
        }

        public void ShowEnd(bool success)
        {
            ClearContent();
            Label(content, success ? "생존 완료  ·  보스 진입 준비" : "이번 생존은 여기까지", 36, gold,
                new Vector2(0, -45), new Vector2(1060, 70), TextAnchor.MiddleCenter);
            Label(content, $"레벨 {game.Level}    ·    처치 {game.Kills}    ·    생존 {game.Elapsed:0}초", 24, Color.white,
                new Vector2(0, -130), new Vector2(1060, 50), TextAnchor.MiddleCenter);
            Label(content, success ? "다음 제작 단계: 문어 보스\n색상 순서 바위 숨기 · 촉수 공격 · 잉크 장판\n이 프로토타입에는 아직 보스 전투가 포함되지 않습니다." : "경험치를 모으고 증강을 조합해 다시 도전하세요.", 20,
                new Color(0.74f, 0.83f, 0.79f), new Vector2(0, -195), new Vector2(1060, 100), TextAnchor.MiddleCenter);
            Button(content, "다시 도전", new Vector2(350, -330), new Vector2(360, 60), game.Restart);
        }

        public void HideOverlay() { if (overlay != null) overlay.gameObject.SetActive(false); }
        void ClearContent()
        {
            overlay.gameObject.SetActive(true);
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }

        static void Fill(Image image, float value) => image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);

        Image Bar(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var background = Fixed(parent, name, position, size, new Color(0.12f, 0.18f, 0.18f));
            return Panel(background, "Fill", color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
        }

        RectTransform Button(RectTransform parent, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var rect = Fixed(parent, "Button " + text, position, size, new Color(0.12f, 0.23f, 0.21f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.35f, 1.2f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);
            if (!string.IsNullOrEmpty(text)) Label(rect, text, 22, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter);
            return rect;
        }

        Text Label(RectTransform parent, string text, int size, Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var label = go.GetComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = alignment; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }

        RectTransform Fixed(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
            => Panel(parent, name, color, new Vector2(0, 1), new Vector2(0, 1), position + new Vector2(0, -size.y), position + new Vector2(size.x, 0));

        RectTransform Panel(RectTransform parent, string name, Color color, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        void OnDestroy() { if (font != null) Destroy(font); }
    }
}
