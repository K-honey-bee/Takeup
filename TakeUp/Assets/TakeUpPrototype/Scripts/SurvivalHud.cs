using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalHud : MonoBehaviour
    {
        SurvivalGame game;
        Font font;
        [SerializeField] RectTransform root, overlay, content;
        [SerializeField] Text status, healthLabel, xpLabel, equipment;
        [SerializeField] Image healthFill, xpFill, stageFill;
        [SerializeField] RectTransform choiceContent, pauseContent, victoryContent, defeatContent, codexContent;
        readonly System.Collections.Generic.Dictionary<Transform, int> elementIndices = new System.Collections.Generic.Dictionary<Transform, int>();
        readonly Color ink = new Color(0.035f, 0.065f, 0.065f, 0.97f);
        readonly Color gold = new Color(1, 0.76f, 0.36f);

        public void Initialize(SurvivalGame owner)
        {
            game = owner;
            elementIndices.Clear();
            font = Application.isPlaying ? Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial", "Liberation Sans" }, 22)
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bool newCanvas = root == null;
            var canvasObject = newCanvas ? new GameObject("Canvas - Survival HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)) : root.gameObject;
            if (newCanvas) canvasObject.transform.SetParent(transform, false);
            root = canvasObject.GetComponent<RectTransform>();
            if (newCanvas) canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (newCanvas)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform);
            }
            var header = Panel(root, "Status", ink, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -126), new Vector2(-18, -18));
            Label(header, "시간의 집 / 조선 균열", 22, gold, new Vector2(20, -8), new Vector2(420, 32));
            status = Label(header, "", 20, Color.white, new Vector2(460, -12), new Vector2(740, 32));
            healthFill = Bar(header, "HP", new Vector2(20, -49), new Vector2(400, 18), new Color(0.93f, 0.37f, 0.35f));
            healthLabel = Label(header, "", 13, Color.white, new Vector2(24, -49), new Vector2(390, 18));
            xpFill = Bar(header, "XP", new Vector2(20, -77), new Vector2(400, 14), new Color(0.32f, 0.85f, 0.66f));
            xpLabel = Label(header, "", 12, Color.white, new Vector2(24, -76), new Vector2(390, 18));
            equipment = Label(header, "", 15, new Color(0.7f, 0.78f, 0.76f), new Vector2(460, -50), new Vector2(740, 28));
            stageFill = Bar(header, "Stage", new Vector2(460, -86), new Vector2(740, 5), gold);
            var footer = Panel(root, "Controls", ink, Vector2.zero, new Vector2(1, 0), new Vector2(18, 16), new Vector2(-18, 58));
            Label(footer, "WASD 이동 · Shift 대시 · Q 벽사무 · 춤: A/D 또는 ←/→ · Tab 증강 · ESC 정지", 16,
                new Color(0.75f, 0.81f, 0.79f), new Vector2(20, -6), new Vector2(1200, 30));
            overlay = Panel(root, "Overlay", new Color(0.015f, 0.03f, 0.03f, 0.92f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            choiceContent = ContentRoot("Augment Choices");
            pauseContent = ContentRoot("Pause Menu");
            victoryContent = ContentRoot("Stage Complete");
            defeatContent = ContentRoot("Game Over");
            codexContent = ContentRoot("Augment Codex");
            HideOverlay();
            InitializeSkillHud();
        }

        void Update()
        {
            if (game == null || status == null) return;
            int seconds = Mathf.FloorToInt(game.Elapsed);
            status.text = $"레벨 {game.Level:00}    처치 {game.Kills:000}    웨이브 {game.Wave}    {seconds / 60:00}:{seconds % 60:00}";
            healthLabel.text = $"체력  {Mathf.CeilToInt(game.Health)} / {Mathf.CeilToInt(game.MaximumHealth)}";
            xpLabel.text = $"시간 조각  {game.Experience} / {game.RequiredExperience}";
            Fill(healthFill, game.Health / game.MaximumHealth);
            Fill(xpFill, (float)game.Experience / game.RequiredExperience);
            Fill(stageFill, game.ActiveBoss != null ? 1 - game.ActiveBoss.Health / game.ActiveBoss.MaximumHealth : game.Elapsed / game.settings.stageDuration);
            equipment.text = $"{game.WeaponLabel}  ·  {(game.HasSpear ? "영창 장착" : "영창 미해금")}     |     균열 탐색 {game.settings.stageDuration:0}초";
            RefreshSkillHud();
        }

        public void ShowChoices()
        {
            ClearContent(choiceContent);
            Label(content, $"레벨 {game.Level}  /  새로운 균열 증강", 32, gold, new Vector2(0, 0), new Vector2(1060, 50), TextAnchor.MiddleCenter);
            Label(content, $"하나를 선택하세요. 선택 중에는 전투가 멈춥니다.  (남은 선택 {game.PendingChoices})", 18,
                Color.white, new Vector2(0, -56), new Vector2(1060, 32), TextAnchor.MiddleCenter);
            for (int i = 0; i < game.Choices.Count; i++)
            {
                int index = i;
                var choice = game.Choices[i];
                float x = (1060 - game.Choices.Count * 340 + 20) * 0.5f + i * 340;
                var card = Button(content, "", new Vector2(x, -120), new Vector2(320, 286), () => game.ChooseAugment(index));
                Label(card, $"0{i + 1}", 22, gold, new Vector2(22, -15), new Vector2(276, 36));
                {
                    var icon = Panel(card, "Icon", Color.white, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -66), new Vector2(-22, -18));
                    icon.GetComponent<Image>().sprite = choice.icon;
                    icon.GetComponent<Image>().preserveAspect = true;
                    icon.GetComponent<Image>().enabled = choice.icon != null;
                }
                Label(card, choice.title, 26, Color.white, new Vector2(22, -76), new Vector2(276, 48));
                Label(card, choice.description, 19, new Color(0.74f, 0.83f, 0.79f), new Vector2(22, -132), new Vector2(276, 94));
                Label(card, $"현재 보유 {game.StackCount(choice.kind)}회   ·   선택 [{i + 1}]", 14, gold, new Vector2(22, -240), new Vector2(276, 30));
                if (i == 0 && Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(card.gameObject);
            }
        }

        public void ShowPause()
        {
            ClearContent(pauseContent);
            Label(content, "잠시 쉬어가기", 38, gold, new Vector2(0, -80), new Vector2(1060, 70), TextAnchor.MiddleCenter);
            Button(content, "계속하기  /  ESC", new Vector2(350, -200), new Vector2(360, 60), game.TogglePause);
            Button(content, "처음부터 다시 시작", new Vector2(350, -280), new Vector2(360, 60), game.Restart);
        }

        public void ShowEnd(bool success)
        {
            ClearContent(success ? victoryContent : defeatContent);
            bool bossFight = game.ActiveBoss != null;
            Label(content, success ? (bossFight ? game.ActiveBoss.DisplayName + " 처치 완료!" : "균열 중심 발견  ·  보스 진입 준비") : "이번 파견은 여기까지", 36, gold,
                new Vector2(0, -45), new Vector2(1060, 70), TextAnchor.MiddleCenter);
            Label(content, $"레벨 {game.Level}    ·    처치 {game.Kills}    ·    생존 {game.Elapsed:0}초", 24, Color.white,
                new Vector2(0, -130), new Vector2(1060, 50), TextAnchor.MiddleCenter);
            Label(content, success ? (bossFight ? "균열의 수호자를 제압했습니다.\n다시 도전하거나 조선 균열로 돌아갈 수 있습니다." : "다음 상대: 먹물 요괴\n봉인석 색상 기억 · 촉수 공격 · 먹물 장판\n획득한 증강과 현재 체력을 가지고 진입합니다.") : "패턴을 살피고 다시 도전하세요.", 20,
                new Color(0.74f, 0.83f, 0.79f), new Vector2(0, -195), new Vector2(1060, 100), TextAnchor.MiddleCenter);
            Button(content, success && !bossFight ? "해안 균열 진입" : "다시 도전", new Vector2(350, -330), new Vector2(360, 60),
                success && !bossFight ? game.EnterBossScene : game.Restart);
            Button(content, bossFight ? "조선 균열로 돌아가기" : "다시 파견하기", new Vector2(350, -400), new Vector2(360, 48), game.ReturnToSurvival);
        }

        public void HideOverlay() { if (overlay != null) overlay.gameObject.SetActive(false); }
        void ClearContent(RectTransform target)
        {
            overlay.gameObject.SetActive(true);
            foreach (Transform child in overlay) child.gameObject.SetActive(child == target);
            content = target;
            ResetElements(content);
        }

        RectTransform ContentRoot(string label)
            => Panel(overlay, label, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-530, -230), new Vector2(530, 230));

        void ResetElements(RectTransform parent)
        {
            elementIndices.Clear();
            foreach (Transform child in parent) if (child.GetComponent<HudLayoutSlot>() != null) child.gameObject.SetActive(false);
        }

        // Stable authored slots are reused. Their RectTransforms and styling are never reset.
        GameObject Element(RectTransform parent, string label, System.Type component, out bool created)
        {
            int index = elementIndices.TryGetValue(parent, out int next) ? next : 0;
            elementIndices[parent] = index + 1;
            foreach (Transform child in parent)
            {
                var slot = child.GetComponent<HudLayoutSlot>();
                if (slot == null || slot.index != index) continue;
                created = false; child.gameObject.SetActive(true); return child.gameObject;
            }
            created = true;
            var go = new GameObject(label, typeof(RectTransform), component, typeof(HudLayoutSlot));
            go.transform.SetParent(parent, false);
            go.GetComponent<HudLayoutSlot>().index = index;
            return go;
        }

        public void BakeLayout(SurvivalGame owner)
        {
            if (Application.isPlaying) return;
            Initialize(owner);
            var saved = new System.Collections.Generic.List<AugmentDefinition>(owner.Choices);
            owner.Choices.Clear();
            for (int i = 0; i < 3; i++) owner.Choices.Add(owner.settings.augments != null && i < owner.settings.augments.Length
                ? owner.settings.augments[i] : new AugmentDefinition { title = "증강", description = "증강 설명" });
            ShowChoices(); owner.Choices.Clear(); owner.Choices.AddRange(saved);
            ShowPause(); ShowEnd(true); ShowEnd(false); ShowCodex();
            BuildNoteSlots(Mathf.Min(12, Mathf.Max(1, owner.settings.dance.sequence.Length)));
            HideOverlay();
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
            var button = rect.GetComponent<Button>();
            bool created = button == null;
            if (created) button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.35f, 1.2f);
            colors.selectedColor = colors.highlightedColor;
            if (created) button.colors = colors;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            if (!string.IsNullOrEmpty(text)) Label(rect, text, 22, Color.white, Vector2.zero, size, TextAnchor.MiddleCenter);
            return rect;
        }

        Text Label(RectTransform parent, string text, int size, Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = Element(parent, string.IsNullOrEmpty(text) ? "Live Value" : text.Split('\n')[0], typeof(Text), out bool created);
            var rect = go.GetComponent<RectTransform>();
            var label = go.GetComponent<Text>();
            if (created)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = position; rect.sizeDelta = dimensions;
                label.fontSize = size; label.color = color;
                label.alignment = alignment; label.raycastTarget = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            if (label.font == null || label.font.name == "LegacyRuntime") label.font = font;
            label.text = text;
            return label;
        }

        RectTransform Fixed(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
            => Panel(parent, name, color, new Vector2(0, 1), new Vector2(0, 1), position + new Vector2(0, -size.y), position + new Vector2(size.x, 0));

        RectTransform Panel(RectTransform parent, string name, Color color, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = Element(parent, name, typeof(Image), out bool created);
            var rect = go.GetComponent<RectTransform>();
            if (created)
            {
                rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
                go.GetComponent<Image>().color = color;
            }
            return rect;
        }

        void OnDestroy() { if (font != null && Application.isPlaying) Destroy(font); }
    }
}
