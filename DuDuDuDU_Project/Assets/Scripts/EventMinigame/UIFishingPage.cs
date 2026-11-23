using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Mission;
using OJ.UI;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 낚시 화면. 레퍼런스처럼 <b>전체 화면 연못 장면</b>이고 가운데에 찌가 떠 있다. 아래 독에서 던지고(큰 버튼), 배율을 돌리고(x1→x10→x100),
    /// 도감과 보상 목록을 연다. 표식이 게이지를 <b>일정한 속도로 왕복</b>하므로 아무렇게나 눌러도 퍼펙트 30% · 굿 50% · 미스 20% 이고,
    /// 그것이 평균 점수 장부의 가정이다(<see cref="FishingRules.TimingFromPosition"/>).
    /// </summary>
    public sealed class UIFishingPage : UIEventMinigameDialog
    {
        private const float GaugeWidth = 760f;

        /// <summary>표식이 한 번 왕복하는 시간(초).</summary>
        private const float GaugePeriod = 1.6f;

        private const float BobberBaseY = 340f;

        [SerializeField] private TMP_Text rodText;
        [SerializeField] private TMP_Text nextLevelText;
        [SerializeField] private TMP_Text luckText;
        [SerializeField] private TMP_Text nextRewardText;
        [SerializeField] private RectTransform marker;
        [SerializeField] private RectTransform bobber;
        [SerializeField] private RectTransform ripple;
        [SerializeField] private Button multiplierButton;
        [SerializeField] private TMP_Text multiplierLabel;
        [SerializeField] private Button castButton;
        [SerializeField] private Button codexButton;
        [SerializeField] private GameObject codexPanel;
        [SerializeField] private TMP_Text codexText;
        [SerializeField] private Button codexCloseButton;

        private int multiplierIndex;

        /// <summary>던진 직후 1 에서 0 으로 식는 값. 찌가 튀어 오르는 연출이 쓴다.</summary>
        private float castPulse;

        protected override EventKind Kind
        {
            get { return EventKind.Fishing; }
        }

        protected override void OnLoadPlay()
        {
            multiplierButton.onClick.AddListener(CycleMultiplier);
            castButton.onClick.AddListener(OnCast);
            codexButton.onClick.AddListener(OpenCodex);
            codexCloseButton.onClick.AddListener(CloseCodex);
            codexPanel.SetActive(false);
        }

        protected override void OnOpened()
        {
            codexPanel.SetActive(false);
            SetStatus(string.Empty);
        }

        protected override int SheetFocusRow()
        {
            return Manager != null ? Manager.FishingClaimedCount : 0;
        }

        protected override void OnFrame()
        {
            if (marker != null)
            {
                Vector2 position = marker.anchoredPosition;
                position.x = (MarkerPosition() - 0.5f) * GaugeWidth;
                marker.anchoredPosition = position;
            }

            if (bobber != null)
            {
                castPulse = Mathf.Max(0f, castPulse - Time.unscaledDeltaTime * 2.2f);
                float t = Time.unscaledTime;
                bobber.anchoredPosition = new Vector2(0f, BobberBaseY + Mathf.Sin(t * 2.2f) * 9f + castPulse * 60f);
                bobber.localScale = Vector3.one * (1f + castPulse * 0.35f);
            }

            if (ripple != null)
                ripple.localScale = new Vector3(1f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.06f + castPulse * 0.4f, 1f, 1f);
        }

        /// <summary>표식의 지금 위치(0~1). 시간에 대해 일정한 속도로 왕복한다.</summary>
        private static float MarkerPosition()
        {
            float phase = (Time.unscaledTime / GaugePeriod) % 1f;
            return phase < 0.5f ? phase * 2f : 2f - phase * 2f;
        }

        /// <summary>x1 → x10 → x100 → x1. 낚싯대가 모자란 배율은 건너뛰고 x1 로 돌아간다.</summary>
        private void CycleMultiplier()
        {
            int next = (multiplierIndex + 1) % FishingRules.Multipliers.Length;
            if (HeldCurrency < FishingRules.Multipliers[next])
                next = 0;

            multiplierIndex = next;
            RefreshPlay();
        }

        private void OnCast()
        {
            if (!IsOpenNow || Manager == null)
                return;

            FishTiming timing = FishingRules.TimingFromPosition(MarkerPosition());
            FishingCastResult result = Manager.Cast(FishingRules.Multipliers[multiplierIndex], timing, UtcNow);

            if (!result.Valid)
            {
                SetStatus("낚싯대가 모자라요");
                return;
            }

            castPulse = 1f;
            SetStatus(EventMinigameText.FishingResultLine(result));
            ShowRewards(result.Rewards, "낚시 보상");
        }

        protected override void RefreshPlay()
        {
            if (Manager == null)
                return;

            DateTime now = UtcNow;
            int level = Manager.FishingRodLevel;
            long score = Manager.FishingScore;

            rodText.text = "낚싯대 Lv." + level + "   점수 " + EventMinigameText.Number(score);
            nextLevelText.text = level < FishingRules.MaxRodLevel
                ? "다음 레벨까지 " + EventMinigameText.Number(FishingRules.RodLevelScore[level] - score) + "점"
                : "최고 레벨이에요";

            int luck = Manager.GetFishingLuck(now);
            luckText.text = luck > 0
                ? "행운 " + luck + "단계 (" + EventMinigameText.Remaining(Manager.FishingLuckRemaining(now)) + ")"
                : "특별 떡밥을 찾으면 행운 UP";

            EventMilestone[] table = EventRewardTables.Fishing;
            int claimed = Manager.FishingClaimedCount;
            nextRewardText.text = claimed < table.Length
                ? "다음 보상 " + EventMinigameText.Number(table[claimed].Threshold) + "점: " + EventMinigameText.Describe(table[claimed].Default)
                : "모든 보상을 받았어요 (넘친 점수는 핀볼로)";

            bool open = IsOpenNow;
            int held = HeldCurrency;

            // 고른 배율을 낚싯대가 못 받쳐 주면 살 수 있는 가장 큰 배율로 내린다.
            while (multiplierIndex > 0 && held < FishingRules.Multipliers[multiplierIndex])
                multiplierIndex--;

            int cost = FishingRules.Multipliers[multiplierIndex];
            multiplierLabel.text = "x" + cost;
            multiplierButton.interactable = open;
            castButton.interactable = open && held >= cost;

            if (codexPanel.activeSelf)
                RefreshCodex();
        }

        protected override void RefreshList()
        {
            if (Manager == null)
                return;

            EventMilestone[] table = EventRewardTables.Fishing;
            var list = EnsureRows(table.Length);
            int claimed = Manager.FishingClaimedCount;

            for (int i = 0; i < table.Length; i++)
            {
                string label = EventMinigameText.Number(table[i].Threshold) + "점";
                int rodLevel = Array.IndexOf(FishingRules.RodLevelScore, table[i].Threshold);
                if (rodLevel > 0)
                    label += "\n낚싯대 Lv." + (rodLevel + 1);

                UIEventMilestoneRow.RowState state = i < claimed
                    ? UIEventMilestoneRow.RowState.Reached
                    : (i == claimed ? UIEventMilestoneRow.RowState.Next : UIEventMilestoneRow.RowState.Locked);

                list[i].Bind(label, EventMinigameText.Describe(table[i].Default), state);
            }
        }

        // ── 도감 ───────────────────────────────────────────────────────

        private void OpenCodex()
        {
            codexPanel.SetActive(true);
            RefreshCodex();
        }

        private void CloseCodex()
        {
            codexPanel.SetActive(false);
        }

        private void RefreshCodex()
        {
            var text = new StringBuilder();
            for (int tier = FishingRules.TierCount - 1; tier >= 0; tier--)
            {
                for (int species = 0; species < FishingRules.SpeciesPerTier; species++)
                {
                    int record = Manager != null ? Manager.GetFishingRecord(FishingRules.SpeciesId(tier, species)) : 0;
                    text.AppendLine(EventMinigameText.CodexLine(tier, species, record));
                }
            }

            codexText.text = text.ToString();
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UIFishingPage Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UIFishingPage", parent);
            UIMissionUIFactory.Stretch(root.GetComponent<RectTransform>());

            var page = root.AddComponent<UIFishingPage>();
            page.BakeOpenStyle(DialogOpenStyle.Popup);

            Transform p = UIEventMinigameFactory.BakeScreen(page, root.transform, font, "낚시 이벤트", "StageBg/Map_2");

            page.rodText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "RodInfo", 0, 32f, Color.white, font);
            page.nextLevelText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "NextLevel", 1, 26f, UIEventMinigameFactory.MutedText, font);
            page.luckText = UIEventMinigameFactory.BadgeLine(p, "InfoRight", "LuckInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);
            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(860f, 220f), new Vector2(0f, 70f));

            // 연못 장면: 물결 + 찌. 찌는 위아래로 떠다니고 던지면 튀어 오른다.
            // 물결은 얇고 넓은 띠 둘(바깥 것이 천천히 숨 쉬듯 늘었다 준다).
            Image water = UIMissionUIFactory.Sliced(p, "Ripple", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(420f, 26f), new Vector2(0f, 262f), new Color(0.8f, 0.94f, 1f, 0.35f));
            water.raycastTarget = false;
            page.ripple = water.rectTransform;

            Image waterInner = UIMissionUIFactory.Sliced(p, "RippleInner", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(250f, 20f), new Vector2(0f, 262f), new Color(0.8f, 0.94f, 1f, 0.5f));
            waterInner.raycastTarget = false;

            GameObject bobberRoot = UIMissionUIFactory.CreateRect("Bobber", p);
            UIMissionUIFactory.SetRect(bobberRoot.GetComponent<RectTransform>(), new Vector2(120f, 150f), new Vector2(0f, BobberBaseY));
            page.bobber = bobberRoot.GetComponent<RectTransform>();

            // 찌: 가는 막대 + 빨강/하양 두 칸.
            Image stick = UIMissionUIFactory.Sliced(bobberRoot.transform, "Stick", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(12f, 90f), new Vector2(0f, 70f), Color.white);
            stick.raycastTarget = false;
            Image top = UIMissionUIFactory.Sliced(bobberRoot.transform, "Top", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(76f, 64f), new Vector2(0f, 10f), UIMissionUIFactory.Hex(0xe6574a));
            top.raycastTarget = false;
            Image bottom = UIMissionUIFactory.Sliced(bobberRoot.transform, "Bottom", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(76f, 56f), new Vector2(0f, -48f), Color.white);
            bottom.raycastTarget = false;

            // 타이밍 게이지(물가에 놓인 띠). 업그레이드 게이지의 검은 둥근 틀 위에 굿(파랑)·퍼펙트(초록)·표식.
            Image track = UIMissionUIFactory.Sliced(p, "GaugeTrack", UIEventMinigameFactory.GaugeBack, 3f,
                new Vector2(GaugeWidth + 16f, 72f), new Vector2(0f, -190f), Color.white);
            track.raycastTarget = false;

            Image good = UIMissionUIFactory.Sliced(track.transform, "GoodZone", UIEventMinigameFactory.SmallBox, 0.5f,
                new Vector2(GaugeWidth * FishingRules.GoodHalfWidth * 2f, 52f), Vector2.zero, UIEventMinigameFactory.GoodZone);
            good.raycastTarget = false;

            Image perfect = UIMissionUIFactory.Sliced(track.transform, "PerfectZone", UIEventMinigameFactory.SmallBox, 0.5f,
                new Vector2(GaugeWidth * FishingRules.PerfectHalfWidth * 2f, 52f), Vector2.zero, UIEventMinigameFactory.PerfectZone);
            perfect.raycastTarget = false;

            Image markerImage = UIMissionUIFactory.Sliced(track.transform, "Marker", UIEventMinigameFactory.SmallBox, 0.5f,
                new Vector2(16f, 96f), Vector2.zero, UIEventMinigameFactory.MarkerColor);
            markerImage.raycastTarget = false;
            page.marker = markerImage.rectTransform;

            TMP_Text legend = UIMissionUIFactory.CreateText(
                "Legend", p, "초록 = 퍼펙트   파랑 = 굿", 24f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(legend.rectTransform, new Vector2(600f, 34f), new Vector2(0f, -252f));

            // 다음 보상 한 줄(레퍼런스의 "개인 누적 보상").
            Image nextChip = UIEventMinigameFactory.Chip(p, "NextRewardChip", new Vector2(930f, 76f), new Vector2(0f, -400f));
            page.nextRewardText = UIMissionUIFactory.CreateText(
                "NextReward", nextChip.transform, string.Empty, 27f, TextAlignmentOptions.Center, Color.white, font);
            page.nextRewardText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.nextRewardText.rectTransform, new Vector2(880f, 62f), Vector2.zero);

            // 아래 독: 도감(왼쪽) · 던지기(가운데) · x배율 · 보상(오른쪽, 공통).
            page.codexButton = UIEventMinigameFactory.DockLeftButton(p, "CodexButton", "도감", font);
            page.castButton = UIEventMinigameFactory.DockMainButton(p, "CastButton", "던지기", font);
            page.multiplierButton = UIEventMinigameFactory.DockMultiplierButton(p, font);
            page.multiplierLabel = page.multiplierButton.GetComponentInChildren<TMP_Text>();

            // 보상 판은 독·장면 위로 오게 마지막에, 도감 덮개는 그 위에 얹는다.
            UIEventMinigameFactory.BakeRewardSheet(page, p, font);

            Image veil = UIMissionUIFactory.CreateImage("CodexPanel", p, new Color(0f, 0f, 0f, 0.62f));
            UIMissionUIFactory.Stretch(veil.rectTransform);
            page.codexPanel = veil.gameObject;

            Image codexBack = UIMissionUIFactory.Sliced(veil.transform, "Panel", "Upgrade/Ui_Popup_Bg", 4f,
                new Vector2(900f, 1000f), new Vector2(0f, 60f), Color.white);
            codexBack.raycastTarget = true;

            TMP_Text codexTitle = UIMissionUIFactory.CreateText(
                "CodexTitle", veil.transform, "도감", 46f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(codexTitle.rectTransform, new Vector2(500f, 64f), new Vector2(0f, 500f));

            page.codexText = UIMissionUIFactory.CreateText(
                "CodexText", veil.transform, string.Empty, 34f, TextAlignmentOptions.TopLeft, Color.white, font);
            page.codexText.textWrappingMode = TextWrappingModes.NoWrap;
            UIMissionUIFactory.SetRect(page.codexText.rectTransform, new Vector2(780f, 760f), new Vector2(0f, 90f));

            page.codexCloseButton = UIEventMinigameFactory.KitButton(
                veil.transform, "CodexClose", "닫기", EventButtonKind.Primary,
                new Vector2(280f, 92f), new Vector2(0f, -400f), 38f, font);

            return page;
        }
    }
}
