using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TakeUp.Prototype
{
    public sealed partial class SurvivalHud
    {
        RectTransform weaponContent;
        public void ShowWeaponSelection()
        {
            if (weaponContent == null) weaponContent = ContentRoot("Starting Weapon");
            ClearContent(weaponContent);
            Label(content, "재희", 24, gold, new Vector2(0, 10), new Vector2(1060, 40), TextAnchor.MiddleCenter);
            Label(content, "어떤 무기를 사용하시겠어요?", 36, Color.white, new Vector2(0, -38), new Vector2(1060, 58), TextAnchor.MiddleCenter);
            Card(StartingWeapon.Sword, 90, "[1]  검", "흰색 · 검은색", "넓은 범위를 베는 근접 무기\n\n확장 칼날 · 이중 참격 · 검기\n잔류 참격 · 회전 칼날 · 발도", new Color(.13f, .13f, .13f));
            Card(StartingWeapon.Stone, 550, "[2]  기계 돌", "청록 · 흰색", "거리를 유지하는 원거리 무기\n\n다중 발사 · 도탄\n보조 무기와 화상으로 화력 강화", new Color(.03f, .24f, .23f));
            Label(content, "클릭 또는 숫자 1 / 2로 선택  ·  좌클릭 유지로 공격", 19, Color.white, new Vector2(0, -410), new Vector2(1060, 35), TextAnchor.MiddleCenter);
        }

        void Card(StartingWeapon weapon, float x, string title, string palette, string description, Color color)
        {
            var card = Button(content, "", new Vector2(x, -118), new Vector2(420, 266), () => game.SelectWeapon(weapon));
            card.GetComponent<Image>().color = color;
            Label(card, title, 32, Color.white, new Vector2(26, -18), new Vector2(368, 45));
            Label(card, palette, 18, Color.white, new Vector2(26, -68), new Vector2(368, 30));
            Label(card, description, 21, Color.white, new Vector2(26, -112), new Vector2(368, 140));
            if (weapon == StartingWeapon.Sword && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(card.gameObject);
        }
    }
}
