using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;

namespace OJ.Tower
{
    /// <summary>
    /// 편성 화면 위쪽 슬롯 바의 칸 하나. 기획서 5.3 의 (2) 다.
    ///
    /// <b>빈 칸을 점선으로 남긴다.</b> "아직 채울 자리가 있다" 를 시각화하는 것이
    /// 이 바의 존재 이유이고(기획서 5.3), 그것이 곧 4.2 의 "미보유 = 빈 슬롯" 이
    /// 화면에서 읽히는 방식이다. 빈 칸을 접어 버리면 7칸짜리 편성이 4칸짜리로 보여
    /// <b>무엇이 부족한지가 사라진다.</b>
    /// </summary>
    public class UITowerSlotView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image emptyOutline;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text label;

        /// <summary>
        /// 칸을 채운다. <b>아이콘 + 성급</b>이 전부다 — 칸이 108px 이라 이름까지 넣으면
        /// 일곱 칸이 전부 잘린 글자가 된다. 이름이 필요한 곳은 후보 그리드이고,
        /// 여기는 "지금 무엇을 골랐나" 를 한눈에 보는 자리다.
        /// </summary>
        public void BindFilled(TowerLoadoutEntry entry)
        {
            if (background != null)
                background.enabled = true;

            if (emptyOutline != null)
                emptyOutline.enabled = false;

            // 정체는 아이콘이 나른다(인게임 UIDice 와 같다). 스프라이트가 없으면
            // 아이콘을 끄고 이름을 대신 띄운다 — 빈 칸처럼 보이면 안 된다.
            Sprite iconSprite = DiceMetaDataProvider.GetIcon(entry.DiceType);
            bool hasIcon = iconSprite != null;

            if (icon != null)
            {
                icon.enabled = hasIcon;
                icon.sprite = iconSprite;
                icon.color = Color.white;
            }

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.color = Color.white;

                // 아이콘이 있으면 성급만("x4"), 없으면 이름으로 대신한다.
                string starText = TowerDiceText.StarOf(entry.DiceType, entry.Star);
                label.SetText(hasIcon ? starText : TowerDiceText.NameWithStar(entry.DiceType, entry.Star));
            }
        }

        public void BindEmpty()
        {
            if (background != null)
                background.enabled = false;

            if (emptyOutline != null)
                emptyOutline.enabled = true;

            if (icon != null)
                icon.enabled = false;

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.SetText("＋");
                label.color = UITowerUIFactory.MutedText;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        internal static UITowerSlotView Create(
            Transform parent, Vector2 size, Vector2 position, TMP_FontAsset font)
        {
            // 빈 칸 표시를 배경의 <b>형제로, 배경보다 먼저</b> 만든다. 배경이 켜지면
            // 그 위를 덮어 자연스럽게 가려지고, 꺼지면 드러난다 — enabled 두 개를
            // 반대로 켜는 것만으로 두 모습이 나온다.
            GameObject root = UITowerUIFactory.CreateRect("Slot", parent);
            UITowerUIFactory.SetRect(root.GetComponent<RectTransform>(), size, position);

            var view = root.AddComponent<UITowerSlotView>();

            view.emptyOutline = UITowerUIFactory.CreateImage("Empty", root.transform, UITowerUIFactory.TrackColor);
            UITowerUIFactory.SetRect(view.emptyOutline.rectTransform, size, Vector2.zero);
            view.emptyOutline.raycastTarget = false;

            view.background = UITowerUIFactory.CreateImage("Fill", root.transform, UITowerUIFactory.CardColor);
            UITowerUIFactory.SetRect(view.background.rectTransform, size, Vector2.zero);
            view.background.raycastTarget = false;

            view.icon = UITowerUIFactory.CreateImage("Icon", root.transform, Color.white);
            UITowerUIFactory.SetRect(view.icon.rectTransform,
                new Vector2(size.x - 26f, size.y - 26f), new Vector2(0f, 6f));
            view.icon.preserveAspect = true;
            view.icon.raycastTarget = false;

            // 성급은 아이콘 아래 모서리에 얹는다. 아이콘 위에 겹치면 작은 칸에서
            // 둘 다 안 읽힌다.
            view.label = UITowerUIFactory.CreateText("Label", root.transform, "＋", 22f,
                TextAlignmentOptions.Bottom, Color.white, font);
            UITowerUIFactory.SetRect(view.label.rectTransform,
                new Vector2(size.x - 8f, 28f), new Vector2(0f, -size.y * 0.5f + 15f));

            // 굽는 시점의 기본 모습은 빈 칸이다. 채워진 모습으로 저장하면 프리팹을
            // 열었을 때 실제와 다른 화면이 보인다.
            view.BindEmpty();

            return view;
        }
    }
}
