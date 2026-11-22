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
    /// 낚시 화면. 표식이 게이지를 왕복하고, 던지는 순간의 위치가 타이밍(퍼펙트/굿/미스)이다.
    /// 표식이 <b>일정한 속도로 왕복</b>하므로 아무렇게나 눌러도 퍼펙트 30% · 굿 50% · 미스 20% 이고, 그것이 평균 점수 장부의 가정이다
    /// (<see cref="FishingRules.TimingFromPosition"/>).
    /// </summary>
    public sealed class UIFishingPage : UIEventMinigameDialog
    {
        private const float GaugeWidth = 760f;

        /// <summary>표식이 한 번 왕복하는 시간(초).</summary>
        private const float GaugePeriod = 1.6f;

        [SerializeField] private TMP_Text rodText;
        [SerializeField] private TMP_Text luckText;
        [SerializeField] private RectTransform marker;
        [SerializeField] private Button[] multiplierButtons;
        [SerializeField] private Image[] multiplierImages;
        [SerializeField] private Button castButton;
        [SerializeField] private TMP_Text castLabel;
        [SerializeField] private Button codexButton;
        [SerializeField] private GameObject codexPanel;
        [SerializeField] private TMP_Text codexText;
        [SerializeField] private Button codexCloseButton;

        private int multiplierIndex;

        protected override EventKind Kind
        {
            get { return EventKind.Fishing; }
        }

        protected override void OnLoadPlay()
        {
            for (int i = 0; i < multiplierButtons.Length; i++)
            {
                int index = i;
                multiplierButtons[i].onClick.AddListener(() => SelectMultiplier(index));
            }

            castButton.onClick.AddListener(OnCast);
            codexButton.onClick.AddListener(OpenCodex);
            codexCloseButton.onClick.AddListener(CloseCodex);
            codexPanel.SetActive(false);
        }

        protected override void OnOpened()
        {
            codexPanel.SetActive(false);
            SetStatus(string.Empty);

            if (Manager != null)
                ScrollListTo(Manager.FishingClaimedCount);
        }

        protected override void OnFrame()
        {
            if (marker == null)
                return;

            Vector2 position = marker.anchoredPosition;
            position.x = (MarkerPosition() - 0.5f) * GaugeWidth;
            marker.anchoredPosition = position;
        }

        /// <summary>표식의 지금 위치(0~1). 시간에 대해 일정한 속도로 왕복한다.</summary>
        private static float MarkerPosition()
        {
            float phase = (Time.unscaledTime / GaugePeriod) % 1f;
            return phase < 0.5f ? phase * 2f : 2f - phase * 2f;
        }

        private void SelectMultiplier(int index)
        {
            multiplierIndex = index;
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

            var rod = new StringBuilder();
            rod.Append("낚싯대 Lv.").Append(level).Append("   점수 ").Append(EventMinigameText.Number(score));
            if (level < FishingRules.MaxRodLevel)
                rod.Append("   (다음 레벨까지 ").Append(EventMinigameText.Number(FishingRules.RodLevelScore[level] - score)).Append("점)");
            rodText.text = rod.ToString();

            int luck = Manager.GetFishingLuck(now);
            luckText.text = luck > 0
                ? "행운 " + luck + "단계  (남은 " + EventMinigameText.Remaining(Manager.FishingLuckRemaining(now)) + ")"
                : "특별 떡밥을 찾으면 행운이 올라요";

            bool open = IsOpenNow;
            int held = HeldCurrency;

            // 고른 배율을 낚싯대가 못 받쳐 주면 살 수 있는 가장 큰 배율로 내린다.
            while (multiplierIndex > 0 && held < FishingRules.Multipliers[multiplierIndex])
                multiplierIndex--;

            for (int i = 0; i < multiplierButtons.Length; i++)
            {
                multiplierButtons[i].interactable = open && held >= FishingRules.Multipliers[i];
                multiplierImages[i].color = i == multiplierIndex ? UIEventMinigameFactory.ButtonSelected : UIEventMinigameFactory.ButtonOff;
            }

            int cost = FishingRules.Multipliers[multiplierIndex];
            castButton.interactable = open && held >= cost;
            castLabel.text = "던지기  (낚싯대 " + cost + "개)";

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

            Transform p = UIEventMinigameFactory.BakeFrame(page, root.transform, font, "낚시", true);

            page.rodText = UIMissionUIFactory.CreateText("RodInfo", p, string.Empty, 30f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.rodText.rectTransform, new Vector2(840f, 44f), new Vector2(0f, 540f));

            page.luckText = UIMissionUIFactory.CreateText("LuckInfo", p, string.Empty, 28f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            UIMissionUIFactory.SetRect(page.luckText.rectTransform, new Vector2(840f, 40f), new Vector2(0f, 494f));

            // 게이지: 바탕 / 굿 구간 / 퍼펙트 구간 / 표식. 구간 폭은 규칙의 반폭(±0.40, ±0.15)과 같아야 한다.
            Image track = UIMissionUIFactory.CreateImage("GaugeTrack", p, UIEventMinigameFactory.TrackColor);
            UIMissionUIFactory.SetRect(track.rectTransform, new Vector2(GaugeWidth, 56f), new Vector2(0f, 390f));
            track.raycastTarget = false;

            Image good = UIMissionUIFactory.CreateImage("GoodZone", track.transform, UIEventMinigameFactory.GoodZone);
            UIMissionUIFactory.SetRect(good.rectTransform, new Vector2(GaugeWidth * FishingRules.GoodHalfWidth * 2f, 56f), Vector2.zero);
            good.raycastTarget = false;

            Image perfect = UIMissionUIFactory.CreateImage("PerfectZone", track.transform, UIEventMinigameFactory.PerfectZone);
            UIMissionUIFactory.SetRect(perfect.rectTransform, new Vector2(GaugeWidth * FishingRules.PerfectHalfWidth * 2f, 56f), Vector2.zero);
            perfect.raycastTarget = false;

            Image markerImage = UIMissionUIFactory.CreateImage("Marker", track.transform, UIEventMinigameFactory.MarkerColor);
            UIMissionUIFactory.SetRect(markerImage.rectTransform, new Vector2(14f, 92f), Vector2.zero);
            markerImage.raycastTarget = false;
            page.marker = markerImage.rectTransform;

            TMP_Text legend = UIMissionUIFactory.CreateText(
                "Legend", p, "초록 = 퍼펙트   파랑 = 굿", 24f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            UIMissionUIFactory.SetRect(legend.rectTransform, new Vector2(600f, 34f), new Vector2(0f, 322f));

            int count = FishingRules.Multipliers.Length;
            page.multiplierButtons = new Button[count];
            page.multiplierImages = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Button button = UIEventMinigameFactory.TextButton(
                    p, "Multiplier" + FishingRules.Multipliers[i], "x" + FishingRules.Multipliers[i],
                    new Vector2(220f, 84f), new Vector2((i - 1) * 250f, 236f),
                    UIEventMinigameFactory.ButtonOff, 38f, font);
                page.multiplierButtons[i] = button;
                page.multiplierImages[i] = button.GetComponent<Image>();
            }

            page.castButton = UIEventMinigameFactory.TextButton(
                p, "CastButton", "던지기", new Vector2(520f, 130f), new Vector2(0f, 110f),
                UIEventMinigameFactory.ButtonOn, 46f, font);
            page.castLabel = page.castButton.GetComponentInChildren<TMP_Text>();

            page.codexButton = UIEventMinigameFactory.TextButton(
                p, "CodexButton", "도감", new Vector2(220f, 64f), new Vector2(0f, -10f),
                UIEventMinigameFactory.ButtonOff, 30f, font);

            // 도감 덮개: 게임 영역 전체를 덮는다. 맨 마지막 자식이라 위에 그려진다.
            Image codex = UIMissionUIFactory.CreateImage("CodexPanel", p, UIMissionUIFactory.Hex(0x1f2030));
            UIMissionUIFactory.SetRect(codex.rectTransform, new Vector2(820f, 760f), new Vector2(0f, 190f));
            page.codexPanel = codex.gameObject;

            page.codexText = UIMissionUIFactory.CreateText(
                "CodexText", codex.transform, string.Empty, 32f, TextAlignmentOptions.TopLeft, Color.white, font);
            page.codexText.textWrappingMode = TextWrappingModes.NoWrap;
            UIMissionUIFactory.SetRect(page.codexText.rectTransform, new Vector2(740f, 600f), new Vector2(0f, 40f));

            page.codexCloseButton = UIEventMinigameFactory.TextButton(
                codex.transform, "CodexClose", "닫기", new Vector2(240f, 70f), new Vector2(0f, -320f),
                UIEventMinigameFactory.ButtonOn, 32f, font);

            return page;
        }
    }
}
