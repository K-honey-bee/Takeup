using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalHud
    {
        [SerializeField] RectTransform dancePanel, noteRow;
        [SerializeField] Text danceTitle, danceHint;
        [SerializeField] Image timingCursor, timingWindow;
        RhythmJudge displayedJudge;
        readonly System.Collections.Generic.List<Image> noteImages = new System.Collections.Generic.List<Image>();
        readonly System.Collections.Generic.List<Text> noteLabels = new System.Collections.Generic.List<Text>();
        int codexPage;

        void InitializeSkillHud()
        {
            dancePanel = Panel(root, "Stomp Dance", new Color(0.035f, 0.055f, 0.075f, 0.94f),
                new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-450, 76), new Vector2(450, 286));
            if (game.settings.dance.panelSprite != null) dancePanel.GetComponent<Image>().sprite = game.settings.dance.panelSprite;
            danceTitle = Label(dancePanel, "벽사무", 26, gold, new Vector2(20, -8), new Vector2(860, 40), TextAnchor.MiddleCenter);
            noteRow = Fixed(dancePanel, "Notes", new Vector2(20, -54), new Vector2(860, 50), Color.clear);
            var timingBar = Fixed(dancePanel, "Beat timing", new Vector2(100, -121), new Vector2(700, 18), new Color(0.15f, 0.2f, 0.22f));
            timingWindow = Panel(timingBar, "Hit window", new Color(0.35f, 0.65f, 0.45f),
                new Vector2(0.4f, 0), new Vector2(0.6f, 1), Vector2.zero, Vector2.zero).GetComponent<Image>();
            Panel(timingBar, "Beat center", Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(-1, -4), new Vector2(1, 4));
            timingCursor = Panel(timingBar, "Cursor", gold, Vector2.zero, Vector2.up, new Vector2(-3, -3), new Vector2(3, 3)).GetComponent<Image>();
            danceHint = Label(dancePanel, "", 17, Color.white, new Vector2(20, -155), new Vector2(860, 42), TextAnchor.MiddleCenter);
            if (!Application.isPlaying) dancePanel.SetSiblingIndex(overlay.GetSiblingIndex());
            dancePanel.gameObject.SetActive(false);
        }

        void RefreshSkillHud()
        {
            string dash = game.DashCooldown > 0 ? $"{game.DashCooldown:0.0}초" : "준비";
            string dance = !game.HasDance ? "미획득" : game.DanceCooldown > 0 ? $"{game.DanceCooldown:0.0}초" : "준비";
            equipment.text = $"돌{(game.HasSpear ? " + 창" : "")}    Shift 대시 {dash}    Q 벽사무 {dance}";
            bool visible = game.State == RunState.Dancing;
            dancePanel.gameObject.SetActive(visible);
            if (!visible || game.DanceJudge == null) return;
            var judge = game.DanceJudge;
            if (displayedJudge != judge)
            {
                displayedJudge = judge;
                // Scroll the visible note window for custom sequences longer than twelve notes.
                BuildNoteSlots(Mathf.Min(12, judge.Count));
            }
            int first = Mathf.Clamp(judge.Hits - 5, 0, Mathf.Max(0, judge.Count - noteImages.Count));
            for (int i = 0; i < noteImages.Count; i++)
            {
                int index = first + i;
                var sprite = judge.Note(index) == BeatDirection.Left ? game.settings.dance.leftArrow : game.settings.dance.rightArrow;
                Color color = index < judge.Hits ? new Color(0.35f, 1, 0.65f) : index == judge.Hits ? gold : new Color(0.5f, 0.6f, 0.6f);
                noteImages[i].sprite = sprite;
                noteImages[i].color = sprite == null ? Color.clear : color;
                noteLabels[i].text = sprite == null ? (judge.Note(index) == BeatDirection.Left ? "←" : "→") : "";
                noteLabels[i].color = color;
            }
            danceTitle.text = game.DanceResolved ? game.DanceFeedback : $"벽사무  {judge.Hits} / {judge.Count}";
            danceTitle.color = game.DanceResolved && !game.DanceSucceeded ? new Color(1, 0.5f, 0.5f) : gold;
            danceHint.text = game.DanceResolved ? "카메라와 전투가 원래대로 돌아갑니다" : game.DanceFeedback + "\n움직이는 점이 중앙선에 올 때 입력";
            float interval = 60 / Mathf.Max(40, game.settings.dance.bpm);
            float widthRatio = Mathf.Min(0.45f, game.settings.dance.hitWindow / interval) * 0.5f;
            timingWindow.rectTransform.anchorMin = new Vector2(0.5f - widthRatio, 0);
            timingWindow.rectTransform.anchorMax = new Vector2(0.5f + widthRatio, 1);
            float cursor = game.DanceResolved ? 0.5f : Mathf.Clamp01(0.5f + (float)(game.DanceTime - game.settings.dance.inputLatencyOffset - judge.TimeFor(judge.Hits)) / (2 * interval));
            timingCursor.rectTransform.anchorMin = new Vector2(cursor, 0);
            timingCursor.rectTransform.anchorMax = new Vector2(cursor, 1);
        }

        public void ShowCodex() { codexPage = 0; DrawCodex(); }

        void BuildNoteSlots(int count)
        {
            ResetElements(noteRow);
            noteImages.Clear(); noteLabels.Clear();
            float width = Mathf.Min(90, 860f / Mathf.Max(1, count));
            for (int i = 0; i < count; i++)
            {
                var cell = Fixed(noteRow, "Note " + (i + 1), new Vector2((860 - width * count) * 0.5f + i * width, 0), new Vector2(width - 8, 48), new Color(0.16f, 0.24f, 0.25f));
                var icon = Fixed(cell, "Arrow", new Vector2(8, -5), new Vector2(width - 24, 38), Color.clear).GetComponent<Image>();
                icon.preserveAspect = true; noteImages.Add(icon);
                noteLabels.Add(Label(cell, "← / →", 28, Color.white, Vector2.zero, new Vector2(width - 8, 48), TextAnchor.MiddleCenter));
            }
        }

        void DrawCodex()
        {
            ClearContent(codexContent);
            Label(content, "증강 도감  /  이번 플레이의 획득 현황", 28, gold, new Vector2(0, 12), new Vector2(850, 46));
            Button(content, "닫기 [Tab]", new Vector2(875, 12), new Vector2(185, 42), game.ToggleCodex);
            var entries = System.Array.FindAll(game.settings.augments ?? System.Array.Empty<AugmentDefinition>(),
                a => a != null && game.WeaponAllows(a.kind));
            int pages = Mathf.Max(1, Mathf.CeilToInt(entries.Length / 6f));
            codexPage = Mathf.Clamp(codexPage, 0, pages - 1);
            for (int i = 0; i < 6; i++)
            {
                int entryIndex = codexPage * 6 + i;
                bool hasEntry = entryIndex < entries.Length && entries[entryIndex] != null;
                var augment = hasEntry ? entries[entryIndex] : new AugmentDefinition { title = "", description = "" };
                int count = game.StackCount(augment.kind);
                bool owned = count > 0;
                var card = Fixed(content, "Codex card", new Vector2(i % 2 * 540, -50 - i / 2 * 112), new Vector2(520, 102),
                    owned ? new Color(0.13f, 0.28f, 0.23f) : new Color(0.1f, 0.13f, 0.14f));
                card.GetComponent<Image>().color = owned ? new Color(0.13f, 0.28f, 0.23f) : new Color(0.1f, 0.13f, 0.14f);
                var icon = Fixed(card, "Augment icon", new Vector2(12, -22), new Vector2(54, 54), owned ? gold : Color.gray).GetComponent<Image>();
                icon.sprite = augment.icon; icon.preserveAspect = true;
                icon.color = augment.icon == null ? (owned ? gold : Color.gray) : owned ? Color.white : new Color(0.3f, 0.3f, 0.3f);
                Label(card, $"{augment.title}  {(owned ? $"×{count}" : "[미획득]")}", 20, owned ? gold : Color.gray, new Vector2(80, -5), new Vector2(430, 32)).color = owned ? gold : Color.gray;
                Label(card, augment.description, 16, owned ? Color.white : new Color(0.5f, 0.55f, 0.55f), new Vector2(80, -40), new Vector2(430, 56)).color = owned ? Color.white : new Color(0.5f, 0.55f, 0.55f);
                card.gameObject.SetActive(hasEntry);
            }
            Button(content, "이전", new Vector2(0, -404), new Vector2(150, 44), () => { codexPage--; DrawCodex(); });
            Label(content, $"{codexPage + 1} / {pages}   ·   도감이 열린 동안 전투 정지", 18, Color.white, new Vector2(180, -404), new Vector2(650, 44), TextAnchor.MiddleCenter);
            Button(content, "다음", new Vector2(910, -404), new Vector2(150, 44), () => { codexPage++; DrawCodex(); });
        }
    }
}
