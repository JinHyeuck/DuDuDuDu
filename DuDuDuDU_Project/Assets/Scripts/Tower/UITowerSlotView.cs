using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;

namespace OJ.Tower
{
    /// <summary>
    /// 편성 화면 "선택한 조합" 줄의 칸 하나. 기획서 5.3 의 (2), 아트 시안 Infinity_Layout_Skill.
    ///
    /// <b>빈 칸을 남긴다.</b> "아직 채울 자리가 있다" 를 시각화하는 것이 이 줄의 존재
    /// 이유이고(기획서 5.3), 그것이 곧 4.2 의 "미보유 = 빈 슬롯" 이 화면에서 읽히는
    /// 방식이다. 빈 칸을 접어 버리면 7칸짜리 편성이 4칸짜리로 보여 <b>무엇이 부족한지가 사라진다.</b>
    /// </summary>
    public class UITowerSlotView : MonoBehaviour
    {
        [SerializeField] private Image emptyFrame;
        [SerializeField] private Image plusIcon;
        [SerializeField] private Image icon;

        /// <summary>아이콘 밑의 성급("x4"). 칸이 작아 이름은 넣지 않는다.</summary>
        [SerializeField] private TMP_Text label;

        public void BindFilled(TowerLoadoutEntry entry)
        {
            if (emptyFrame != null)
                emptyFrame.enabled = false;

            if (plusIcon != null)
                plusIcon.enabled = false;

            // 정체는 아이콘이 나른다(인게임 UIDice 와 같다). 스프라이트가 없으면
            // 아이콘을 끄고 이름을 대신 띄운다 — 빈 칸처럼 보이면 안 된다.
            Sprite iconSprite = DiceMetaDataProvider.GetIcon(entry.DiceType, entry.Star);
            bool hasIcon = iconSprite != null;

            if (icon != null)
            {
                icon.enabled = hasIcon;
                icon.sprite = iconSprite;
            }

            if (label != null)
            {
                label.gameObject.SetActive(true);
                string starText = TowerDiceText.StarOf(entry.DiceType, entry.Star);
                label.SetText(hasIcon ? starText : TowerDiceText.NameWithStar(entry.DiceType, entry.Star));
            }
        }

        public void BindEmpty()
        {
            if (emptyFrame != null)
                emptyFrame.enabled = true;

            if (plusIcon != null)
                plusIcon.enabled = true;

            if (icon != null)
                icon.enabled = false;

            if (label != null)
                label.gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 칸 하나. <paramref name="size"/> 는 아이콘 칸(시안 88), 성급 글자는 그 아래에 붙는다.
        /// </summary>
        internal static UITowerSlotView Create(
            Transform parent, Vector2 size, Vector2 position, TMP_FontAsset font)
        {
            GameObject root = UITowerUIFactory.CreateRect("Slot", parent);
            UITowerUIFactory.SetRect(root.GetComponent<RectTransform>(), size, position);

            var view = root.AddComponent<UITowerSlotView>();

            // 빈 칸: 테두리(x2) + 더하기 아이콘(x1). 아이콘은 글자로 쓰지 않는다 —
            // "＋" 같은 기호는 폰트 아틀라스에 없으면 두부가 된다.
            view.emptyFrame = UITowerUIFactory.CreateSprite("EmptyFrame", root.transform,
                UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Skill_Selcet_Nonel"), 2f,
                size + new Vector2(36f, 36f), Vector2.zero, true);   // 보이는 88 + 여백 9px x2
            view.emptyFrame.color = new Color(0.55f, 0.53f, 0.72f, 1f);

            view.plusIcon = UITowerUIFactory.CreateSprite("Plus", root.transform,
                UITowerUIFactory.LoadSprite("Ingame/Icon_Plus"), 1f,
                new Vector2(32f, 32f), Vector2.zero, false);
            view.plusIcon.color = new Color(0.55f, 0.53f, 0.72f, 1f);

            // 채운 칸: 다이스 아이콘(시안 "스킬 아이콘 x2"). 스프라이트가 64px 안에 45px 그림이라
            // 128 로 잡아야 보이는 그림이 칸(88)을 채운다.
            view.icon = UITowerUIFactory.CreateImage("Icon", root.transform, Color.white);
            UITowerUIFactory.SetRect(view.icon.rectTransform, new Vector2(128f, 128f), Vector2.zero);
            view.icon.preserveAspect = true;
            view.icon.raycastTarget = false;

            // 성급 — 칸 아래(시안 "x1" 25pt 흰색). 칸 높이는 폰트 x1.3 이상이어야
            // 글자가 잘리지 않고 통째로 사라지지도 않는다.
            view.label = UITowerUIFactory.CreateText("Star", root.transform, "x1", 25f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(view.label.rectTransform,
                new Vector2(size.x + 12f, 34f), new Vector2(0f, -size.y * 0.5f - 19f));

            // 굽는 시점의 기본 모습은 빈 칸이다. 채워진 모습으로 저장하면 프리팹을
            // 열었을 때 실제와 다른 화면이 보인다.
            view.BindEmpty();

            return view;
        }
    }
}
