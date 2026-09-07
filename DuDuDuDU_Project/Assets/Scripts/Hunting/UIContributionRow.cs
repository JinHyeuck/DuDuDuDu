using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;

namespace OJ.Hunting
{
    /// <summary>
    /// 기여도 패널의 한 줄. <b>아이콘과 게이지가 전부다.</b>
    ///
    /// <b>이름과 퍼센트를 뺐다.</b> 이 패널은 전투 중에 <b>곁눈으로</b> 보는 것이라,
    /// 읽어야 하는 글자가 있으면 시선을 뺏긴다. 어느 다이스인지는 아이콘이 말하고
    /// (인게임 <c>UIDice</c> 와 같은 아이콘이다) 얼마나 때렸는지는 막대 길이가 말한다 —
    /// 서로를 견주는 데는 숫자보다 길이가 빠르다.
    ///
    /// <b>파일을 따로 두는 이유.</b> 이 리포는 MonoBehaviour 를 파일당 하나만 둔다
    /// (<c>UITowerFloorCard</c>·<c>UITowerSlotView</c>·<c>UITowerDamageBar</c> 전부 그렇다).
    /// 파일명과 다른 이름의 MonoBehaviour 는 프리팹으로 구울 때 <b>Missing script</b> 로
    /// 앉기 쉽고, 그 사고는 "줄이 하나도 안 보인다" 로만 드러난다.
    /// </summary>
    public class UIContributionRow : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image gaugeFill;

        /// <summary>
        /// 게이지 트랙의 가로 길이. 채움 너비를 이 값에 비율을 곱해 정한다.
        /// <b>굽는 시점에 정해져 프리팹에 저장된다</b> — 런타임에 <c>rect.rect.width</c> 를
        /// 읽으면 레이아웃이 아직 안 돈 첫 프레임에 0 이 나온다.
        /// </summary>
        [SerializeField] private float gaugeWidth = 176f;

        public void Bind(DiceDamageShare share)
        {
            gameObject.SetActive(true);

            Sprite iconSprite = DiceMetaDataProvider.GetIcon(share.DiceType);
            if (icon != null)
            {
                // 아이콘이 아직 없는 다이스는 색 사각형으로 대신한다. 끄면 그 줄이
                // 어느 다이스인지 알 방법이 통째로 사라진다 — 이름을 뺐기 때문이다.
                icon.enabled = true;
                icon.sprite = iconSprite;
                icon.color = iconSprite != null ? Color.white : DiceMetaDataProvider.GetColor(share.DiceType);
            }

            if (gaugeFill != null)
            {
                gaugeFill.color = DiceMetaDataProvider.GetColor(share.DiceType);

                RectTransform rect = gaugeFill.rectTransform;
                Vector2 offset = rect.offsetMax;
                offset.x = gaugeWidth * Mathf.Clamp01(share.Ratio);
                rect.offsetMax = offset;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        internal const float IconSize = 34f;
        internal const float GaugeHeight = 16f;

        private static readonly Color TrackColor = new Color(0.20f, 0.23f, 0.38f, 1f);

        /// <summary>
        /// <paramref name="parent"/> 밑에 한 줄을 만든다.
        /// <b>폰트를 받지 않는다</b> — 이 줄에는 글자가 없다(이름·퍼센트를 뺐다).
        /// </summary>
        internal static UIContributionRow Create(
            Transform parent, Vector2 size, Vector2 position)
        {
            GameObject root = UIDamageContributionPanel.CreateRect("Row", parent);
            UIDamageContributionPanel.SetRect(root.GetComponent<RectTransform>(), size, position);

            var row = root.AddComponent<UIContributionRow>();

            float left = -size.x * 0.5f;
            float gaugeWidth = size.x - IconSize - 10f;

            row.icon = UIDamageContributionPanel.CreateImage("Icon", root.transform, Color.white);
            UIDamageContributionPanel.SetRect(row.icon.rectTransform,
                new Vector2(IconSize, IconSize), new Vector2(left + IconSize * 0.5f, 0f));
            row.icon.preserveAspect = true;
            row.icon.raycastTarget = false;

            // 게이지: 바탕(트랙) 안에 채움. 채움을 트랙의 <b>자식</b>으로 두고 왼쪽에
            // 고정해야 너비 하나로 조절된다 — 형제로 두면 둘의 위치를 같이 맞춰야 한다.
            Image track = UIDamageContributionPanel.CreateImage("Gauge", root.transform, TrackColor);
            UIDamageContributionPanel.SetRect(track.rectTransform,
                new Vector2(gaugeWidth, GaugeHeight),
                new Vector2(left + IconSize + 10f + gaugeWidth * 0.5f, 0f));
            track.raycastTarget = false;

            Image fill = UIDamageContributionPanel.CreateImage("Fill", track.transform, Color.white);
            fill.raycastTarget = false;

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = new Vector2(gaugeWidth, 0f);

            row.gaugeFill = fill;
            row.gaugeWidth = gaugeWidth;

            // 굽는 시점에는 꺼 둔다. 채울 내용이 생겼을 때 Bind 가 켠다.
            row.Hide();
            return row;
        }
    }
}
