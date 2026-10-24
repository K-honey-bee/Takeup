using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalHud
    {
        RectTransform instantRoot;
        InstantSlashGraphic instantGraphic;
        Text instantTitle, instantHint;
        Image instantPlayer;
        readonly List<Image> instantTargetImages = new List<Image>();
        readonly Dictionary<GameObject, bool> instantHiddenHud = new Dictionary<GameObject, bool>();

        public void ShowInstantSlashHud()
        {
            if (instantRoot == null)
            {
                var obj = new GameObject("Instant Slash Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(InstantSlashGraphic));
                instantRoot = obj.GetComponent<RectTransform>(); instantRoot.SetParent(root, false);
                instantRoot.anchorMin = Vector2.zero; instantRoot.anchorMax = Vector2.one;
                instantRoot.offsetMin = instantRoot.offsetMax = Vector2.zero;
                instantGraphic = obj.GetComponent<InstantSlashGraphic>(); instantGraphic.game = game; instantGraphic.raycastTarget = false;
                instantPlayer = InstantImage("Player in darkness");
                for (int i = 0; i < 12; i++) instantTargetImages.Add(InstantImage("Falling core " + i));
                instantTitle = Label(instantRoot, "순간베기", 32, Color.white, Vector2.zero, new Vector2(800, 52), TextAnchor.MiddleCenter);
                instantTitle.rectTransform.anchorMin = instantTitle.rectTransform.anchorMax = new Vector2(.5f, 1);
                instantTitle.rectTransform.pivot = new Vector2(.5f, 1); instantTitle.rectTransform.anchoredPosition = new Vector2(0, -20);
                instantHint = Label(instantRoot, "", 21, Color.white, Vector2.zero, new Vector2(1000, 60), TextAnchor.MiddleCenter);
                instantHint.rectTransform.anchorMin = instantHint.rectTransform.anchorMax = new Vector2(.5f, 0);
                instantHint.rectTransform.pivot = new Vector2(.5f, 0); instantHint.rectTransform.anchoredPosition = new Vector2(0, 28);
            }
            if (instantHiddenHud.Count == 0)
                foreach (Transform child in root)
                    if (child != instantRoot && child != overlay)
                    { instantHiddenHud[child.gameObject] = child.gameObject.activeSelf; child.gameObject.SetActive(false); }
            instantRoot.gameObject.SetActive(true); instantRoot.SetAsLastSibling();
            RefreshInstantSlashHud();
        }

        Image InstantImage(string label)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(instantRoot, false);
            var image = obj.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        public void HideInstantSlashHud()
        {
            if (instantRoot != null) instantRoot.gameObject.SetActive(false);
            foreach (var entry in instantHiddenHud) if (entry.Key != null) entry.Key.SetActive(entry.Value);
            instantHiddenHud.Clear();
        }

        void RefreshInstantSlashHud()
        {
            if (instantRoot == null) return;
            bool active = game.State == RunState.InstantSlashing;
            instantRoot.gameObject.SetActive(active);
            if (!active) return;
            instantGraphic.SetVerticesDirty();
            instantTitle.text = !game.InstantResolved ? "순간베기  /  정지된 시간" : game.InstantSucceeded ? "순간베기" : "베기 실패";
            instantHint.text = !game.InstantResolved
                ? $"좌클릭을 누른 채 낙하물을 가로질러 베세요   {game.InstantHits} / {game.InstantTargetCount}   ·   {Mathf.Max(0, game.InstantTimeLimit - game.InstantTime):0.0}초   ·   ESC 일시정지"
                : game.InstantSucceeded ? "일섬 — 화면 안의 적을 베어냅니다" : "낙하물을 놓쳤습니다 · 잠시 후 전투 재개";
            instantPlayer.gameObject.SetActive(!game.InstantResolved);
            instantPlayer.sprite = game.InstantPlayerSprite;
            PlaceInstantImage(instantPlayer, game.InstantPlayerViewport, .095f);
            for (int i = 0; i < instantTargetImages.Count; i++)
            {
                bool show = !game.InstantResolved && i < game.InstantTargets.Count && !game.InstantTargets[i].sliced && game.settings.instantSlash.fallingSprite != null;
                var image = instantTargetImages[i]; image.gameObject.SetActive(show);
                if (!show) continue;
                image.sprite = game.settings.instantSlash.fallingSprite;
                PlaceInstantImage(image, game.InstantTargets[i].position, game.settings.instantSlash.targetRadius * 2);
            }
        }

        void PlaceInstantImage(Image image, Vector2 viewport, float height)
        {
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = viewport;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = Vector2.one * instantRoot.rect.height * height;
        }
    }
}
