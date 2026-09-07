using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;
using OJ.Hunting;

namespace OJ.Tower
{
    /// <summary>
    /// 결과 화면의 기여도 막대 한 줄. 기획서 5.6 의 (2) 다.
    ///
    /// <b>비율만 보여 주고 피해량은 적지 않는다.</b> 이 막대가 답해야 하는 질문은
    /// "어떤 선택이 정답이었나" 이지 "몇 대미지가 들어갔나" 가 아니다. 숫자를 같이
    /// 적으면 눈이 큰 숫자로 가고, 편성이 다른 두 판을 비교할 수 없게 된다
    /// (층마다 총 피해량이 다르기 때문이다).
    /// </summary>
    public class UITowerDamageBar : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image gaugeFill;
        [SerializeField] private TMP_Text percentText;

        public void Bind(DiceDamageShare share)
        {
            gameObject.SetActive(true);

            // 막대 색만 다이스 색을 쓴다. 이름표를 색칠하지 않는 것은 인게임과 같고,
            // 어느 막대가 어느 다이스인지는 <b>아이콘과 이름</b>이 말한다.
            Color color = DiceMetaDataProvider.GetColor(share.DiceType);

            Sprite iconSprite = DiceMetaDataProvider.GetIcon(share.DiceType);
            if (icon != null)
            {
                icon.enabled = iconSprite != null;
                icon.sprite = iconSprite;
            }

            if (nameText != null)
                nameText.SetText(TowerDiceText.NameOf(share.DiceType));

            if (gaugeFill != null)
            {
                gaugeFill.color = color;
                UITowerUIFactory.SetGauge(gaugeFill, GaugeWidth, share.Ratio);
            }

            if (percentText != null)
                percentText.SetText("{0}%", Mathf.RoundToInt(share.Ratio * 100f));
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        // 한 줄의 가로 배치: [아이콘][이름][게이지][%]. 인게임 목록(UIDiceGrowthItem)이
        // 아이콘 + 이름으로 다이스를 적으므로 여기도 같게 둔다.
        private const float RowWidth = 880f;
        private const float RowHeight = 48f;
        private const float IconSize = 40f;
        private const float NameWidth = 170f;
        private const float GaugeWidth = 500f;

        internal static UITowerDamageBar Create(Transform parent, Vector2 position, TMP_FontAsset font)
        {
            GameObject root = UITowerUIFactory.CreateRect("DamageBar", parent);
            UITowerUIFactory.SetRect(root.GetComponent<RectTransform>(),
                new Vector2(RowWidth, RowHeight), position);

            var bar = root.AddComponent<UITowerDamageBar>();

            float left = -RowWidth * 0.5f;

            bar.icon = UITowerUIFactory.CreateImage("Icon", root.transform, Color.white);
            UITowerUIFactory.SetRect(bar.icon.rectTransform,
                new Vector2(IconSize, IconSize), new Vector2(left + IconSize * 0.5f + 4f, 0f));
            bar.icon.preserveAspect = true;
            bar.icon.raycastTarget = false;

            bar.nameText = UITowerUIFactory.CreateText("Name", root.transform, "Normal", 24f,
                TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(bar.nameText.rectTransform,
                new Vector2(NameWidth, 40f), new Vector2(left + IconSize + 16f + NameWidth * 0.5f, 0f));
            bar.nameText.textWrappingMode = TextWrappingModes.NoWrap;
            bar.nameText.overflowMode = TextOverflowModes.Ellipsis;

            bar.gaugeFill = UITowerUIFactory.CreateGauge("Gauge", root.transform,
                new Vector2(GaugeWidth, 22f),
                new Vector2(left + IconSize + 24f + NameWidth + GaugeWidth * 0.5f, 0f),
                UITowerUIFactory.Accent);

            bar.percentText = UITowerUIFactory.CreateText("Percent", root.transform, "37%", 26f,
                TextAlignmentOptions.Right, Color.white, font);
            UITowerUIFactory.SetRect(bar.percentText.rectTransform,
                new Vector2(110f, 40f), new Vector2(RowWidth * 0.5f - 58f, 0f));

            return bar;
        }
    }
}
