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
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject dim;
        [SerializeField] private GameObject progressLine;

        /// <param name="firstLocked">못 닿은 첫 줄인가. 이 줄 위끝에 진행 선을 긋는다.</param>
        public void Bind(
            int level, bool reached, bool firstLocked,
            SeasonPassSlotView free, SeasonPassSlotView premium,
            Action onClaimFree, Action onClaimPremium)
        {
            if (levelText != null)
                levelText.SetText(level.ToString());

            // 못 닿은 줄은 통째로 어둡게 덮는다. 칸 색만으로는 "받을 수 있는 곳" 과
            // "아직 먼 곳" 의 경계가 안 읽혔다(사용자 피드백 2026-10-06).
            if (dim != null)
                dim.SetActive(!reached);

            if (progressLine != null)
                progressLine.SetActive(firstLocked);

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

            // 번호 60pt, 글자 중심 (535.5, 838)
            row.levelText = UISeasonPassUIFactory.CreateFittedText(
                "Level", badge.transform, "1", 60f, 36f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(row.levelText.rectTransform, new Vector2(110f, 80f), new Vector2(-0.5f, 5f));

            // 딤 — 칸까지 덮어야 "아직 못 받는 줄" 로 읽힌다. 클릭은 막지 않는다(스크롤이 지나가야 한다).
            Image dim = UISeasonPassUIFactory.CreateImage("Dim", root.transform, UISeasonPassUIFactory.LockedDim);
            dim.raycastTarget = false;
            UISeasonPassUIFactory.Stretch(dim.rectTransform);
            row.dim = dim.gameObject;
            row.dim.SetActive(false);

            row.progressLine = CreateProgressLine(root.transform);
            row.progressLine.SetActive(false);

            return row;
        }

        /// <summary>
        /// 진행 선 — 줄 위끝(이전 줄과의 경계)에 걸친 가로 선과 가운데 번개.
        /// <b>못 닿은 첫 줄의 마지막 자식</b>으로 둔다. 앞 줄의 자식이면 뒤에 그려지는
        /// 이 줄의 딤이 선의 아래 절반을 덮는다.
        /// </summary>
        private static GameObject CreateProgressLine(Transform parent)
        {
            GameObject line = UISeasonPassUIFactory.CreateRect("ProgressLine", parent);
            UISeasonPassUIFactory.SetRect(line.GetComponent<RectTransform>(), new Vector2(1080f, 128f), new Vector2(0f, RowHeight / 2f));

            Image edge = UISeasonPassUIFactory.CreateImage("Edge", line.transform, Color.black);
            edge.raycastTarget = false;
            UISeasonPassUIFactory.SetRect(edge.rectTransform, new Vector2(1080f, 16f), Vector2.zero);

            Image bar = UISeasonPassUIFactory.CreateImage("Bar", line.transform, UISeasonPassUIFactory.PremiumTrackColor);
            bar.raycastTarget = false;
            UISeasonPassUIFactory.SetRect(bar.rectTransform, new Vector2(1080f, 8f), Vector2.zero);

            // 번개 = 패스 포인트 아이콘(위 포인트 칸과 같은 그림). Pass_LevelNumber 를 받침으로 깐다.
            UISeasonPassUIFactory.Picture(line.transform, "Badge", "Pass/Pass_LevelNumber", new Vector2(96f, 96f), Vector2.zero);
            UISeasonPassUIFactory.Picture(line.transform, "Icon", "Gem/Gem_Thunder", new Vector2(96f, 96f), new Vector2(0f, 1f));

            return line;
        }
    }
}
