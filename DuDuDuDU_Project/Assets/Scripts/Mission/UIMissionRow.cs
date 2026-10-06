using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.Point;

namespace OJ.Mission
{
    /// <summary>
    /// 목록의 한 줄. <b>일일 미션과 업적이 같은 줄을 쓴다.</b>
    ///
    /// 둘은 화면에서 완전히 같은 모양이고(아이콘·제목·진행 바·받기), 다른 것은
    /// 카운트를 어디서 가져오는가 하나뿐이다. 줄을 둘로 나누면 여백·글자 크기를
    /// 두 벌 관리하게 되고 그 둘은 반드시 어긋난다.
    /// </summary>
    public sealed class UIMissionRow : MonoBehaviour
    {
        /// <summary>줄 하나의 크기. 목록의 세로 레이아웃이 이 높이를 쓴다.</summary>
        internal static readonly Vector2 RowSize = new Vector2(820f, 150f);

        private const float GaugeWidth = 400f;

        [SerializeField] private Image background;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image gaugeFill;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button claimButton;
        [SerializeField] private GameObject doneMark;

        private Action claimAction;

        private void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClickClaim);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(OnClickClaim);
        }

        private void OnClickClaim()
        {
            // 지역 변수로 떼어 낸 뒤 부른다. 수령이 목록을 다시 그리면서 이 줄이
            // 재사용되거나 파괴될 수 있는데, 그때 필드를 다시 읽으면 <b>다음 줄의
            // 수령이 불린다.</b>
            Action action = claimAction;
            action?.Invoke();
        }

        /// <summary>일일 미션 한 줄.</summary>
        public void Bind(DailyMissionView view, Action onClaim)
        {
            Bind(view.Definition.DisplayTitle,
                view.Count,
                view.Definition.requiredCount,
                view.Definition.rewards,
                view.Claimable,
                view.Claimed,
                onClaim);
        }

        /// <summary>
        /// 업적 한 줄. <paramref name="view"/> 는 계열에서 <b>지금 보여야 할 단계</b> 하나다.
        /// 카운트는 누적이라 (10/100) 처럼 이어서 보인다.
        /// </summary>
        public void Bind(AchievementView view, Action onClaim)
        {
            Bind(view.Definition.DisplayTitle,
                view.Count,
                view.Definition.requiredCount,
                view.Definition.rewards,
                view.Claimable,
                view.Claimed || view.SeriesComplete,
                onClaim);
        }

        private void Bind(
            string title, int count, int requiredCount, IReadOnlyList<MissionReward> rewards,
            bool claimable, bool done, Action onClaim)
        {
            claimAction = onClaim;

            if (titleText != null)
                titleText.SetText(title);

            if (progressText != null)
                progressText.SetText(MissionText.Progress(count, requiredCount));

            UIMissionUIFactory.SetGauge(
                gaugeFill, GaugeWidth, MissionRules.Progress(count, requiredCount));

            if (background != null)
            {
                background.color = done
                    ? UIMissionUIFactory.RowDoneColor
                    : UIMissionUIFactory.RowColor;
            }

            // 받을 수 있을 때만 버튼이 있다. 못 받는 상태에서 회색 버튼을 남겨 두면
            // 눌러 보고 아무 일도 안 일어나는 자리가 생긴다 — 그건 고장으로 읽힌다.
            if (claimButton != null)
                claimButton.gameObject.SetActive(claimable);

            if (doneMark != null)
                doneMark.SetActive(done && !claimable);

            BindReward(rewards);
        }

        /// <summary>
        /// 보상 칸. <b>첫 줄만 그린다.</b> 기본 데이터는 보상이 한 종류씩이고,
        /// 여럿이 되는 날 이 칸을 목록으로 바꾸면 된다 — 지금 미리 만들면 쓰지 않는
        /// 레이아웃을 유지하게 된다.
        /// </summary>
        private void BindReward(IReadOnlyList<MissionReward> rewards)
        {
            bool has = rewards != null && rewards.Count > 0;

            if (rewardIcon != null)
            {
                Sprite icon = has ? PointRewardUtility.GetPointIcon(rewards[0].pointType) : null;
                rewardIcon.sprite = icon;

                // 스프라이트가 없으면 칸을 숨긴다. 켜 두면 흰 사각형이 남는데,
                // 그것이 "아이콘이 없다" 인지 "아이콘이 흰색" 인지 구분되지 않는다.
                rewardIcon.enabled = icon != null;
            }

            if (rewardAmountText != null)
                rewardAmountText.SetText(has ? rewards[0].amount.ToString() : string.Empty);
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        /// <summary>
        /// 줄 하나를 조립한다. 에디터 굽기 경로에서만 부른다.
        /// </summary>
        internal static UIMissionRow Create(Transform parent, TMP_FontAsset font)
        {
            Image root = UIMissionUIFactory.CreateImage("UIMissionRow", parent, UIMissionUIFactory.RowColor);
            UIMissionUIFactory.SetRect(root.rectTransform, RowSize, Vector2.zero);

            // 세로 레이아웃 그룹이 높이를 읽는 자리다. 없으면 모든 줄이 같은 높이로
            // 뭉개지거나(childControlHeight) 0 이 된다.
            var layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = RowSize.y;
            layoutElement.minHeight = RowSize.y;

            var row = root.gameObject.AddComponent<UIMissionRow>();
            row.background = root;

            Image icon = UIMissionUIFactory.CreateImage("RewardIcon", root.transform, Color.white);
            UIMissionUIFactory.SetRect(icon.rectTransform, new Vector2(100f, 100f), new Vector2(-340f, 18f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            row.rewardIcon = icon;

            // 수량은 자릿수가 데이터에 달렸다(50 부터 2000000 까지). 고정 크기로 두면
            // 큰 값이 칸 밖으로 흘러 옆 줄 위에 겹쳐 찍힌다 — 실제로 30000 에서 그랬다.
            row.rewardAmountText = UIMissionUIFactory.CreateFittedText(
                "RewardAmount", root.transform, "0", 28f, 16f,
                TextAlignmentOptions.Center, UIMissionUIFactory.DarkText, font);
            UIMissionUIFactory.SetRect(
                row.rewardAmountText.rectTransform, new Vector2(140f, 38f), new Vector2(-338f, -50f));

            row.titleText = UIMissionUIFactory.CreateText(
                "Title", root.transform, "미션", 40f,
                TextAlignmentOptions.Left, UIMissionUIFactory.DarkText, font);
            UIMissionUIFactory.SetRect(
                row.titleText.rectTransform, new Vector2(520f, 48f), new Vector2(-25f, 34f));

            row.gaugeFill = UIMissionUIFactory.CreateGauge(
                "Gauge", root.transform, new Vector2(GaugeWidth, 30f), new Vector2(-105f, -28f),
                UIMissionUIFactory.FillColor);

            row.progressText = UIMissionUIFactory.CreateText(
                "Progress", root.transform, "0/0", 36f,
                TextAlignmentOptions.Center, UIMissionUIFactory.DarkText, font);
            UIMissionUIFactory.SetRect(
                row.progressText.rectTransform, new Vector2(150f, 44f), new Vector2(170f, -28f));

            row.claimButton = UIMissionUIFactory.CreateButton(
                "ClaimButton", root.transform, "받기", new Vector2(150f, 84f), new Vector2(320f, 0f),
                UIMissionUIFactory.ClaimColor, UIMissionUIFactory.DarkText, 38f, font);

            TMP_Text done = UIMissionUIFactory.CreateText(
                "DoneMark", root.transform, "완료", 38f,
                TextAlignmentOptions.Center, UIMissionUIFactory.MutedText, font);
            UIMissionUIFactory.SetRect(done.rectTransform, new Vector2(150f, 84f), new Vector2(320f, 0f));
            row.doneMark = done.gameObject;

            return row;
        }
    }
}
