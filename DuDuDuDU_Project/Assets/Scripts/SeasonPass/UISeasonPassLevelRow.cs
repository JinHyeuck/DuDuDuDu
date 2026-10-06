using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 레벨 한 줄. <b>무료 칸 | 레벨 번호 | 유료 칸</b> 이 가로로 놓인다.
    ///
    /// <b>두 트랙을 한 줄에 둔 이유.</b> 세로 두 열로 그리면 같은 레벨의 무료·유료가
    /// 화면에서 같은 높이에 있어야 하는데, 목록을 둘로 나누면 그 정렬을 스크롤 두 개가
    /// 각자 유지해야 한다. 한 줄이면 레이아웃이 그것을 공짜로 해 준다.
    ///
    /// 트랙 바탕색은 줄이 아니라 창이 깐다(움직이지 않는 바탕) — PSD 처럼 두 색이
    /// 목록 위아래 끝까지 이어져야 해서, 줄마다 띠를 두면 마지막 줄 아래가 비어 보인다.
    /// </summary>
    public sealed class UISeasonPassLevelRow : MonoBehaviour
    {
        /// <summary>줄 간격. PSD 칸 y 738 → 1020 → 1302.</summary>
        internal const float RowHeight = 282f;

        // PSD x 중심: 무료 칸 242 · 레벨 536 · 유료 칸 825 (칸은 보이는 외곽 기준)
        private static readonly Vector2 FreeSlotPosition = new Vector2(242f - 540f, 0f);
        private static readonly Vector2 PremiumSlotPosition = new Vector2(825f - 540f, 0f);

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

            // 닿은 레벨만 뱃지가 밝다. 어디까지 왔는지를 줄 하나로 읽히게 한다.
            if (levelBadge != null)
                levelBadge.color = reached ? Color.white : UISeasonPassUIFactory.DimTint;

            freeSlot?.Bind(free, onClaimFree);
            premiumSlot?.Bind(premium, onClaimPremium);
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        internal static UISeasonPassLevelRow Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassLevelRow", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(1080f, RowHeight), Vector2.zero);

            var layoutElement = root.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = RowHeight;
            layoutElement.minHeight = RowHeight;

            var row = root.AddComponent<UISeasonPassLevelRow>();

            row.freeSlot = UISeasonPassSlot.Create(root.transform, font);
            ((RectTransform)row.freeSlot.transform).anchoredPosition = FreeSlotPosition;

            row.premiumSlot = UISeasonPassSlot.Create(root.transform, font);
            ((RectTransform)row.premiumSlot.transform).anchoredPosition = PremiumSlotPosition;

            // 레벨 뱃지 — Pass_LevelNumber x5, 보이는 150x155 (461,763). 칸 중심(828)보다 15 아래다.
            // 두 트랙의 경계에 걸쳐 놓아야 어느 쪽에도 속하지 않아 보인다.
            Image badge = UISeasonPassUIFactory.Picture(
                root.transform, "LevelBadge", "Pass/Pass_LevelNumber", new Vector2(160f, 160f), new Vector2(-4f, -15f));
            row.levelBadge = badge;

            // 번호 60pt, 글자 중심 (535.5, 838)
            row.levelText = UISeasonPassUIFactory.CreateFittedText(
                "Level", badge.transform, "1", 60f, 36f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(row.levelText.rectTransform, new Vector2(110f, 80f), new Vector2(-0.5f, 5f));

            return row;
        }
    }
}
