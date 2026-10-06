using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 레벨 한 줄. <b>무료 칸 | 레벨 번호 | 유료 칸</b> 이 가로로 놓인다.
    ///
    /// <b>두 트랙을 한 줄에 둔 이유.</b> 스샷처럼 세로 두 열로 그리면 같은 레벨의 무료·유료가
    /// 화면에서 같은 높이에 있어야 하는데, 목록을 둘로 나누면 그 정렬을 스크롤 두 개가
    /// 각자 유지해야 한다. 한 줄이면 레이아웃이 그것을 공짜로 해 준다.
    ///
    /// 트랙 배경색(무료는 어둡게, 유료는 금색)은 줄 안쪽의 띠 두 장이 낸다 —
    /// 스샷의 세로 색 구분이 그것이다.
    /// </summary>
    public sealed class UISeasonPassLevelRow : MonoBehaviour
    {
        internal static readonly Vector2 RowSize = new Vector2(880f, 190f);

        [SerializeField] private UISeasonPassSlot freeSlot;
        [SerializeField] private UISeasonPassSlot premiumSlot;
        [SerializeField] private Image levelBadge;
        [SerializeField] private TMP_Text levelText;

        public void Bind(
            int level, bool reached,
            SeasonPassSlotView free, SeasonPassSlotView premium,
            Action onClaimFree, Action onClaimPremium)
        {
            if (levelText != null)
                levelText.SetText(level.ToString());

            // 닿은 레벨만 뱃지가 금색이다. 어디까지 왔는지를 줄 하나로 읽히게 한다.
            if (levelBadge != null)
            {
                levelBadge.color = reached
                    ? UISeasonPassUIFactory.LevelBadgeColor
                    : UISeasonPassUIFactory.LevelBadgeDimColor;
            }

            freeSlot?.Bind(free, onClaimFree);
            premiumSlot?.Bind(premium, onClaimPremium);
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        internal static UISeasonPassLevelRow Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassLevelRow", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), RowSize, Vector2.zero);

            var layoutElement = root.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = RowSize.y;
            layoutElement.minHeight = RowSize.y;

            var row = root.AddComponent<UISeasonPassLevelRow>();

            // 트랙 띠. 줄 전체 높이를 채워서 위아래 줄과 이어져 보인다 — 스샷의 세로 색 구분이다.
            Image freeTrack = UISeasonPassUIFactory.CreateImage(
                "FreeTrack", root.transform, UISeasonPassUIFactory.FreeTrackColor);
            UISeasonPassUIFactory.SetRect(
                freeTrack.rectTransform, new Vector2(440f, RowSize.y), new Vector2(-220f, 0f));
            freeTrack.raycastTarget = false;

            Image premiumTrack = UISeasonPassUIFactory.CreateImage(
                "PremiumTrack", root.transform, UISeasonPassUIFactory.PremiumTrackColor);
            UISeasonPassUIFactory.SetRect(
                premiumTrack.rectTransform, new Vector2(440f, RowSize.y), new Vector2(220f, 0f));
            premiumTrack.raycastTarget = false;

            row.freeSlot = UISeasonPassSlot.Create(root.transform, font);
            UISeasonPassUIFactory.SetRect(
                (RectTransform)row.freeSlot.transform, UISeasonPassSlot.SlotSize, new Vector2(-230f, 0f));

            row.premiumSlot = UISeasonPassSlot.Create(root.transform, font);
            UISeasonPassUIFactory.SetRect(
                (RectTransform)row.premiumSlot.transform, UISeasonPassSlot.SlotSize, new Vector2(230f, 0f));

            // 레벨 뱃지는 가운데. 두 트랙의 경계에 걸쳐 놓아야 어느 쪽에도 속하지 않아 보인다.
            Image badge = UISeasonPassUIFactory.CreateImage(
                "LevelBadge", root.transform, UISeasonPassUIFactory.LevelBadgeColor);
            UISeasonPassUIFactory.SetRect(badge.rectTransform, new Vector2(96f, 96f), Vector2.zero);
            badge.raycastTarget = false;
            row.levelBadge = badge;

            row.levelText = UISeasonPassUIFactory.CreateText(
                "Level", badge.transform, "1", 40f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.DarkText, font);
            UISeasonPassUIFactory.SetRect(row.levelText.rectTransform, new Vector2(96f, 96f), Vector2.zero);

            return row;
        }
    }
}
