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
    /// 둘은 화면에서 완전히 같은 모양이고(아이콘·제목·진행 바·버튼), 다른 것은
    /// 카운트를 어디서 가져오는가 하나뿐이다. 줄을 둘로 나누면 여백·글자 크기를
    /// 두 벌 관리하게 되고 그 둘은 반드시 어긋난다.
    ///
    /// <b>버튼 자리는 상태가 셋이다.</b> 받을 수 있음(받기) · 아직 못 받음(바로가기) ·
    /// 이미 받음(수령 완료). 시안에서 못 받은 줄에는 "바로가기" 가 놓여 있다.
    /// </summary>
    public sealed class UIMissionRow : MonoBehaviour
    {
        /// <summary>
        /// 줄 하나의 크기. 목록의 세로 레이아웃이 이 높이를 쓴다.
        /// 폭은 목록 폭을 따라가고(판은 가운데에 <see cref="PlateSize"/> 로 앉는다), 높이가 판 높이다.
        /// </summary>
        internal static readonly Vector2 RowSize = new Vector2(857f, 184f);

        /// <summary>줄 판(7DaysEvent_Btn x4)의 보이는 크기 — 시안 823x184.</summary>
        private static readonly Vector2 PlateSize = new Vector2(823f, 184f);

        /// <summary>
        /// 게이지 안쪽 폭. 바깥 356 에서 검은 테두리 3px 씩을 뺀 값이다 —
        /// 채움은 이 폭까지 달린다(<see cref="UIMissionUIFactory.CreateGauge(string,Transform,Vector2,Vector2,Color,Color,float)"/>).
        /// </summary>
        private const float GaugeBorder = 3f;
        private const float GaugeOuterWidth = 356f;
        private const float GaugeWidth = GaugeOuterWidth - 2f * GaugeBorder;

        [SerializeField] private Image background;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image gaugeFill;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button goButton;
        [SerializeField] private GameObject doneMark;

        private Action claimAction;
        private Action goAction;

        private void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClickClaim);

            if (goButton != null)
                goButton.onClick.AddListener(OnClickGo);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(OnClickClaim);

            if (goButton != null)
                goButton.onClick.RemoveListener(OnClickGo);
        }

        private void OnClickClaim()
        {
            // 지역 변수로 떼어 낸 뒤 부른다. 수령이 목록을 다시 그리면서 이 줄이
            // 재사용되거나 파괴될 수 있는데, 그때 필드를 다시 읽으면 <b>다음 줄의
            // 수령이 불린다.</b>
            Action action = claimAction;
            action?.Invoke();
        }

        private void OnClickGo()
        {
            Action action = goAction;
            action?.Invoke();
        }

        /// <summary>일일 미션 한 줄.</summary>
        public void Bind(DailyMissionView view, Action onClaim, Action onGo)
        {
            Bind(view.Definition.DisplayTitle,
                view.Count,
                view.Definition.requiredCount,
                view.Definition.rewards,
                view.Claimable,
                view.Claimed,
                onClaim,
                onGo);
        }

        /// <summary>
        /// 업적 한 줄. <paramref name="view"/> 는 계열에서 <b>지금 보여야 할 단계</b> 하나다.
        /// 카운트는 누적이라 (10/100) 처럼 이어서 보인다.
        /// </summary>
        public void Bind(AchievementView view, Action onClaim, Action onGo)
        {
            Bind(view.Definition.DisplayTitle,
                view.Count,
                view.Definition.requiredCount,
                view.Definition.rewards,
                view.Claimable,
                view.Claimed || view.SeriesComplete,
                onClaim,
                onGo);
        }

        private void Bind(
            string title, int count, int requiredCount, IReadOnlyList<MissionReward> rewards,
            bool claimable, bool done, Action onClaim, Action onGo)
        {
            claimAction = onClaim;
            goAction = onGo;

            if (titleText != null)
                titleText.SetText(title);

            // 다 채웠으면 "완료", 아니면 n/m. 받기 버튼이 떠 있는 줄도 게이지는 가득 차 있다.
            bool complete = claimable || done;
            if (progressText != null)
                progressText.SetText(complete ? MissionText.CompleteLabel : MissionText.Progress(count, requiredCount));

            UIMissionUIFactory.SetGauge(
                gaugeFill, GaugeWidth, complete ? 1f : MissionRules.Progress(count, requiredCount));

            if (background != null)
            {
                background.color = done
                    ? UIMissionUIFactory.DoneTint
                    : Color.white;
            }

            // 버튼 자리는 하나다 — 셋 중 하나만 켠다. 받을 수 있으면 받기, 받았으면 수령 완료,
            // 아직이면 바로가기. 못 받는 줄에 회색 받기를 남기면 눌러 보고 아무 일도 안 일어난다.
            if (claimButton != null)
                claimButton.gameObject.SetActive(claimable);

            if (doneMark != null)
                doneMark.SetActive(done && !claimable);

            if (goButton != null)
                goButton.gameObject.SetActive(!claimable && !done);

            BindReward(rewards, done);
        }

        /// <summary>
        /// 보상 칸. <b>첫 줄만 그린다.</b> 기본 데이터는 보상이 한 종류씩이고,
        /// 여럿이 되는 날 이 칸을 목록으로 바꾸면 된다 — 지금 미리 만들면 쓰지 않는
        /// 레이아웃을 유지하게 된다.
        /// </summary>
        private void BindReward(IReadOnlyList<MissionReward> rewards, bool done)
        {
            bool has = rewards != null && rewards.Count > 0;

            if (rewardIcon != null)
            {
                Sprite icon = has ? PointRewardUtility.GetPointIcon(rewards[0].pointType) : null;
                rewardIcon.sprite = icon;
                rewardIcon.color = done ? UIMissionUIFactory.DoneTint : Color.white;

                // 스프라이트가 없으면 칸을 숨긴다. 켜 두면 흰 사각형이 남는데,
                // 그것이 "아이콘이 없다" 인지 "아이콘이 흰색" 인지 구분되지 않는다.
                rewardIcon.enabled = icon != null;
            }

            if (rewardAmountText != null)
                rewardAmountText.SetText(has ? rewards[0].amount.ToString() : string.Empty);
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 좌표는 줄 판(보이는 823x184)의 중심 기준, 위가 +. PSD 의 첫 줄(126,809)~(949,993)
        // 레이어 bbox 에서 옮겼다(판 중심 (537.5, 901)).

        /// <summary>
        /// 줄 하나를 조립한다. 에디터 굽기 경로에서만 부른다.
        /// </summary>
        internal static UIMissionRow Create(Transform parent, TMP_FontAsset font)
        {
            GameObject rootObject = UIMissionUIFactory.CreateRect("UIMissionRow", parent);
            var rootRect = rootObject.GetComponent<RectTransform>();
            UIMissionUIFactory.SetRect(rootRect, RowSize, Vector2.zero);

            // 세로 레이아웃 그룹이 높이를 읽는 자리다. 없으면 모든 줄이 같은 높이로
            // 뭉개지거나(childControlHeight) 0 이 된다.
            var layoutElement = rootObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = RowSize.y;
            layoutElement.minHeight = RowSize.y;

            var row = rootObject.AddComponent<UIMissionRow>();
            Transform t = rootObject.transform;

            // 판 — 7DaysEvent_Btn x4. 여백 없는 그림이라 보이는 크기 그대로.
            row.background = UIMissionUIFactory.Sliced(
                t, "Plate", "7DaysEvent/7DaysEvent_Btn_Normal", 4f, PlateSize, Vector2.zero, Color.white);

            // 아이콘 뒤 — Mail_Mark(시안 주석 x4, 실측 3.6: 보이는 40px 가 144px). 틴트 c4b5e3.
            Image mark = UIMissionUIFactory.Picture(
                t, "MailMark", "Mail/Mail_Mark", new Vector2(230.4f, 230.4f), new Vector2(-323.5f, 6f));
            mark.color = UIMissionUIFactory.MailMarkTint;

            // 칸 — Itme_Slot_0 x2.25(실측: 보이는 45px 가 101px, 주석 2.3).
            UIMissionUIFactory.Picture(
                t, "Slot", "ItemSlot/Itme_Slot_0", new Vector2(144f, 144f), new Vector2(-323f, 5.5f));

            Image icon = UIMissionUIFactory.CreateImage("RewardIcon", t, Color.white);
            UIMissionUIFactory.SetRect(icon.rectTransform, new Vector2(80f, 80f), new Vector2(-323f, 8f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            // 그림이 없는 채로 켜 두면 흰 사각형이 칸을 덮는다. 굽기 상태에서는 끄고 Bind 가 켠다.
            icon.enabled = false;
            row.rewardIcon = icon;

            // 수량은 자릿수가 데이터에 달렸다(50 부터 2000000 까지). 고정 크기로 두면
            // 큰 값이 칸 밖으로 흘러 옆 줄 위에 겹쳐 찍힌다 — 실제로 30000 에서 그랬다.
            row.rewardAmountText = UIMissionUIFactory.CreateFittedText(
                "RewardAmount", t, "0", 28f, 14f,
                TextAlignmentOptions.Right, Color.white, font);
            UIMissionUIFactory.SetRect(
                row.rewardAmountText.rectTransform, new Vector2(84f, 34f), new Vector2(-318f, -24f));

            // 제목 — 35pt, 왼쪽 끝 x 320(판 기준 -217.5).
            row.titleText = UIMissionUIFactory.CreateText(
                "Title", t, "미션", 35f,
                TextAlignmentOptions.Left, Color.white, font);
            UIMissionUIFactory.SetRect(
                row.titleText.rectTransform, new Vector2(470f, 48f), new Vector2(17.5f, 33f));

            // 게이지 — 시안 사각형 356x49 (검은 테두리 3, 바탕 45465f, 채움 연두).
            row.gaugeFill = UIMissionUIFactory.CreateGauge(
                "Gauge", t, new Vector2(GaugeOuterWidth, 49f), new Vector2(-42.5f, -25.5f),
                UIMissionUIFactory.LimeColor, UIMissionUIFactory.DarkBoxColor, GaugeBorder);

            row.progressText = UIMissionUIFactory.CreateText(
                "Progress", t, "0/0", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(
                row.progressText.rectTransform, new Vector2(300f, 42f), new Vector2(-42.5f, -25f));

            // 버튼 셋 — 보이는 227x115(시안), 그림 Big_Btn 여백(좌우 4·위 6·아래 2) x3 → 251x139,
            // 그림이 6 아래로 치우쳐 있어 위로 6. 글자 40pt.
            Vector2 buttonSize = new Vector2(251f, 139f);
            Vector2 buttonPosition = new Vector2(270f, 11f);
            Vector2 labelOffset = new Vector2(4.5f, 0f);

            Image claim = UIMissionUIFactory.Sliced(
                t, "ClaimButton", "Ingame/Big_Btn_Yellow", 3f, buttonSize, buttonPosition, Color.white);
            row.claimButton = UIMissionUIFactory.SpriteButton(claim);
            BakeButtonLabel(claim.transform, "받기", labelOffset, font);

            Image go = UIMissionUIFactory.Sliced(
                t, "GoButton", "Ingame/Btn_Gray", 3f, buttonSize, buttonPosition, Color.white);
            row.goButton = UIMissionUIFactory.SpriteButton(go);
            BakeButtonLabel(go.transform, "바로가기", labelOffset, font);

            // 수령 완료 — 눌 수 없는 회색 판. 시안에 없는 상태라 같은 그림을 눌러 둔다.
            Image done = UIMissionUIFactory.Sliced(
                t, "DoneMark", "Ingame/Btn_Gray", 3f, buttonSize, buttonPosition, UIMissionUIFactory.DoneTint);
            BakeButtonLabel(done.transform, MissionText.ClaimedLabel, labelOffset, font);
            row.doneMark = done.gameObject;

            return row;
        }

        private static void BakeButtonLabel(Transform button, string text, Vector2 offset, TMP_FontAsset font)
        {
            TMP_Text label = UIMissionUIFactory.CreateFittedText(
                "Label", button, text, 40f, 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(label.rectTransform, new Vector2(210f, 60f), offset);
        }
    }
}
