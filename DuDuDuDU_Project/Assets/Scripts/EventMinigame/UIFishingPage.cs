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
    /// 낚시 화면. 레퍼런스 영상의 진행 그대로다 — <b>던지기 → 기다림(찌가 뜸) → 입질(버튼을 눌러 당기세요, 줄어드는 제한 시간) →
    /// 당기기(타이밍 게이지, 두 번 누름) → 결과(획득한 아이템 + 점수 × 배율 → 수령)</b>.
    ///
    /// 버튼은 하나(큰 원형 자리)이고 단계마다 뜻이 바뀐다. 게이지는 두 번 나온다:
    /// 첫 번째는 낚싯대를 거는 단계라 판정 글자(Cool/Great/Perfect)만 터지고 점수에는 안 들어가며,
    /// <b>두 번째 판정이 점수 배율</b>이다 — 평균 점수 장부(<see cref="FishingRules.AssumedTimingPermille"/>)가 "판정 한 번"을 가정하기 때문이다.
    /// 게이지 안 노란 띠의 폭은 판정 규칙(<see cref="FishingRules.GoodHalfWidth"/>/<see cref="FishingRules.PerfectHalfWidth"/>)과 같고,
    /// 띠의 가운데만 매번 조금씩 옮긴다(띠가 게이지 안에 들어가는 범위라 맞출 확률은 그대로다).
    ///
    /// 낚싯대는 <b>결과가 정해지는 순간(두 번째 누름) 한 번에</b> 나간다 — 던진 뒤 창을 닫거나 입질을 놓치면 아무것도 쓰지 않는다.
    /// </summary>
    public sealed class UIFishingPage : UIEventMinigameDialog
    {
        private enum Phase
        {
            Idle,
            Casting,
            Waiting,
            Bite,
            Reel1,
            Reel2,
            Resolve,
            Result,
        }

        private const float GaugeWidth = 760f;

        /// <summary>던지는 데 걸리는 시간 / 입질을 기다리는 시간 범위 / 입질에 반응할 제한 시간.</summary>
        private const float CastSeconds = 0.8f;
        private const float WaitMin = 1.4f;
        private const float WaitMax = 3.2f;
        private const float BiteWindow = 1.6f;

        /// <summary>표식이 한 번 왕복하는 시간(초). 두 번째가 더 빠르다.</summary>
        private const float Period1 = 1.5f;
        private const float Period2 = 1.15f;

        private const float ResolveSeconds = 1.0f;

        // 장면 좌표(1080x1920 중심 기준).
        private static readonly Vector2 RodBase = new Vector2(-430f, -760f);
        private const float RodLength = 1100f;
        private const float RodRest = -12f;
        private const float RodReel = -4f;
        private static readonly Vector2 BobberRest = new Vector2(80f, 60f);

        [SerializeField] private TMP_Text rodText;
        [SerializeField] private TMP_Text nextLevelText;
        [SerializeField] private TMP_Text luckText;
        [SerializeField] private TMP_Text nextRewardText;
        [SerializeField] private RectTransform rod;
        [SerializeField] private RectTransform line;
        [SerializeField] private RectTransform bobber;
        [SerializeField] private RectTransform ripple;
        [SerializeField] private GameObject biteBanner;
        [SerializeField] private Image biteFill;
        [SerializeField] private GameObject gaugeRoot;
        [SerializeField] private RectTransform goodZone;
        [SerializeField] private RectTransform perfectZone;
        [SerializeField] private RectTransform marker;
        [SerializeField] private TMP_Text burstText;
        [SerializeField] private Button multiplierButton;
        [SerializeField] private TMP_Text multiplierLabel;
        [SerializeField] private Button castButton;
        [SerializeField] private Image castImage;
        [SerializeField] private TMP_Text castLabel;
        [SerializeField] private Button codexButton;
        [SerializeField] private GameObject codexPanel;
        [SerializeField] private TMP_Text codexText;
        [SerializeField] private Button codexCloseButton;

        [SerializeField] private GameObject resultRoot;
        [SerializeField] private Image fishPanel;
        [SerializeField] private TMP_Text tierText;
        [SerializeField] private TMP_Text fishNameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text multiplierPopText;
        [SerializeField] private TMP_Text weightText;
        [SerializeField] private TMP_Text judgeText;
        [SerializeField] private TMP_Text noteText;
        [SerializeField] private Button collectButton;

        private static readonly Color[] TierColors =
        {
            new Color(0.62f, 0.68f, 0.80f, 1f),
            new Color(0.30f, 0.66f, 1f, 1f),
            new Color(0.75f, 0.38f, 1f, 1f),
            new Color(1f, 0.72f, 0.20f, 1f),
        };

        private static readonly Color CoolColor = new Color(0.37f, 0.82f, 1f, 1f);
        private static readonly Color GreatColor = new Color(1f, 0.37f, 0.82f, 1f);
        private static readonly Color PerfectColor = new Color(1f, 0.82f, 0.25f, 1f);

        private Phase phase;
        private float phaseTime;
        private float waitSeconds;
        private int multiplierIndex;

        // 게이지
        private float gaugeClock;
        private float zoneCenter = 0.5f;

        // 연출
        private float bobberDip;
        private float splash;
        private float burstTime = 99f;
        private FishingCastResult result;
        private FishTiming resultTiming;

        protected override EventKind Kind
        {
            get { return EventKind.Fishing; }
        }

        protected override void OnLoadPlay()
        {
            multiplierButton.onClick.AddListener(CycleMultiplier);
            castButton.onClick.AddListener(OnCastButton);
            codexButton.onClick.AddListener(OpenCodex);
            codexCloseButton.onClick.AddListener(CloseCodex);
            collectButton.onClick.AddListener(Collect);
            codexPanel.SetActive(false);
        }

        protected override void OnOpened()
        {
            codexPanel.SetActive(false);
            ResetToIdle();
            SetStatus(string.Empty);
        }

        protected override int SheetFocusRow()
        {
            return Manager != null ? Manager.FishingClaimedCount : 0;
        }

        // ── 진행 ───────────────────────────────────────────────────────

        private void OnCastButton()
        {
            switch (phase)
            {
                case Phase.Idle:
                    StartCast();
                    break;
                case Phase.Bite:
                    StartReel();
                    break;
                case Phase.Reel1:
                    TapFirst();
                    break;
                case Phase.Reel2:
                    TapSecond();
                    break;
            }
        }

        private void StartCast()
        {
            if (!IsOpenNow || Manager == null)
                return;

            if (HeldCurrency < FishingRules.Multipliers[multiplierIndex])
            {
                SetStatus("낚싯대가 모자라요");
                return;
            }

            SetStatus(string.Empty);
            waitSeconds = UnityEngine.Random.Range(WaitMin, WaitMax);
            EnterPhase(Phase.Casting);
        }

        private void StartReel()
        {
            biteBanner.SetActive(false);
            NewZone();
            gaugeClock = 0f;
            gaugeRoot.SetActive(true);
            EnterPhase(Phase.Reel1);
        }

        private void NewZone()
        {
            zoneCenter = UnityEngine.Random.Range(0.4f, 0.6f);

            float good = GaugeWidth * FishingRules.GoodHalfWidth * 2f;
            float perfectWidth = GaugeWidth * FishingRules.PerfectHalfWidth * 2f;
            SetZone(goodZone, good);
            SetZone(perfectZone, perfectWidth);
        }

        private void SetZone(RectTransform zone, float width)
        {
            zone.sizeDelta = new Vector2(width, zone.sizeDelta.y);
            zone.anchoredPosition = new Vector2((zoneCenter - 0.5f) * GaugeWidth, 0f);
        }

        private FishTiming JudgeNow()
        {
            // 띠 가운데를 0.5 로 옮겨서 판정 규칙(0.5 기준 거리)을 그대로 쓴다.
            return FishingRules.TimingFromPosition(0.5f + (MarkerPosition() - zoneCenter));
        }

        private void TapFirst()
        {
            Burst(JudgeNow());
            EnterPhase(Phase.Resolve);
            phaseTime = -0.1f; // 짧은 숨 고르기 뒤 두 번째 게이지로 간다(아래 Update 에서 처리).
            reelSecond = true;
        }

        private bool reelSecond;

        private void TapSecond()
        {
            FishTiming timing = JudgeNow();
            Burst(timing);

            FishingCastResult cast = Manager != null
                ? Manager.Cast(FishingRules.Multipliers[multiplierIndex], timing, UtcNow)
                : null;

            if (cast == null || !cast.Valid)
            {
                SetStatus("낚싯대가 모자라요");
                ResetToIdle();
                return;
            }

            result = cast;
            resultTiming = timing;
            reelSecond = false;
            EnterPhase(Phase.Resolve);
        }

        private void EnterPhase(Phase next)
        {
            phase = next;
            phaseTime = 0f;

            switch (next)
            {
                case Phase.Casting:
                    splash = 0f;
                    break;
                case Phase.Waiting:
                    splash = 1f;
                    break;
                case Phase.Bite:
                    biteBanner.SetActive(true);
                    bobberDip = 1f;
                    splash = 1f;
                    break;
            }

            ApplyControls();
        }

        private void ResetToIdle()
        {
            phase = Phase.Idle;
            phaseTime = 0f;
            reelSecond = false;
            result = null;
            bobberDip = 0f;
            splash = 0f;

            if (biteBanner != null) biteBanner.SetActive(false);
            if (gaugeRoot != null) gaugeRoot.SetActive(false);
            if (resultRoot != null) resultRoot.SetActive(false);
            if (burstText != null) burstText.text = string.Empty;

            burstTime = 99f;
            ApplyControls();
        }

        /// <summary>단계에 맞게 큰 버튼의 그림·글자·활성과 배율 버튼을 정한다.</summary>
        private void ApplyControls()
        {
            if (castButton == null)
                return;

            bool idle = phase == Phase.Idle;
            bool open = IsOpenNow;
            int cost = FishingRules.Multipliers[multiplierIndex];

            EventButtonKind kind;
            string label;
            bool interactable;

            switch (phase)
            {
                case Phase.Idle:
                    kind = EventButtonKind.Positive;
                    label = "던지기";
                    interactable = open && HeldCurrency >= cost;
                    break;
                case Phase.Casting:
                case Phase.Waiting:
                    kind = EventButtonKind.Secondary;
                    label = phase == Phase.Casting ? "던지는 중" : "기다리는 중";
                    interactable = false;
                    break;
                case Phase.Bite:
                    kind = EventButtonKind.Danger;
                    label = "당기기!";
                    interactable = true;
                    break;
                case Phase.Reel1:
                case Phase.Reel2:
                    kind = EventButtonKind.Primary;
                    label = "지금!";
                    interactable = true;
                    break;
                default:
                    kind = EventButtonKind.Secondary;
                    label = phase == Phase.Resolve ? "당기는 중" : string.Empty;
                    interactable = false;
                    break;
            }

            castImage.sprite = ButtonSprite(kind);
            castLabel.text = label;
            castButton.interactable = interactable;
            multiplierButton.interactable = idle && open;
        }

        // ── 매 프레임 ──────────────────────────────────────────────────

        protected override void OnFrame()
        {
            float dt = Time.unscaledDeltaTime;
            phaseTime += dt;

            switch (phase)
            {
                case Phase.Casting:
                    if (phaseTime >= CastSeconds)
                        EnterPhase(Phase.Waiting);
                    break;

                case Phase.Waiting:
                    if (phaseTime >= waitSeconds)
                        EnterPhase(Phase.Bite);
                    break;

                case Phase.Bite:
                    UIMissionUIFactory.SetGauge(biteFill, BiteBarWidth, 1f - phaseTime / BiteWindow);
                    if (phaseTime >= BiteWindow)
                    {
                        // 입질을 놓쳤다. 낚싯대는 아직 안 나갔으니 그냥 처음으로.
                        SetStatus("물고기가 도망갔어요. 다시 던져 보세요");
                        ResetToIdle();
                    }
                    break;

                case Phase.Reel1:
                case Phase.Reel2:
                    gaugeClock += dt / (phase == Phase.Reel1 ? Period1 : Period2);
                    break;

                case Phase.Resolve:
                    if (reelSecond)
                    {
                        // 첫 판정이 터진 뒤 잠깐 쉬고 두 번째 게이지.
                        if (phaseTime >= 0.5f)
                        {
                            NewZone();
                            gaugeClock = 0f;
                            EnterPhase(Phase.Reel2);
                        }
                    }
                    else if (phaseTime >= ResolveSeconds)
                    {
                        ShowResult();
                    }
                    break;
            }

            AnimateScene(dt);
            AnimateBurst(dt);
            AnimateResult(dt);
        }

        /// <summary>표식의 지금 위치(0~1). 일정한 속도로 왕복한다.</summary>
        private float MarkerPosition()
        {
            float phase01 = gaugeClock % 1f;
            return phase01 < 0.5f ? phase01 * 2f : 2f - phase01 * 2f;
        }

        private const float BiteBarWidth = 560f;

        private void AnimateScene(float dt)
        {
            bool reeling = phase == Phase.Reel1 || phase == Phase.Reel2 || (phase == Phase.Resolve);

            // 낚싯대: 평소엔 기대 있고, 당기는 동안 몸 쪽으로 휜다(각도로 흉내).
            float restAngle = reeling ? RodReel : RodRest;
            if (phase == Phase.Casting)
                restAngle = RodRest + Mathf.Sin(Mathf.Clamp01(phaseTime / CastSeconds) * Mathf.PI) * -16f;
            if (phase == Phase.Bite)
                restAngle = RodRest + Mathf.Sin(Time.unscaledTime * 40f) * 1.2f;

            float angle = Mathf.LerpAngle(rod.localEulerAngles.z, restAngle, 1f - Mathf.Exp(-14f * dt));
            rod.localEulerAngles = new Vector3(0f, 0f, angle);
            Vector2 tip = RodTip(angle);

            // 찌: 던지는 동안 호를 그리며 날아가고, 기다릴 땐 물결에 뜨고, 입질엔 가라앉았다 튄다.
            Vector2 position = BobberRest;
            bool visible = true;

            if (phase == Phase.Casting)
            {
                float t = Mathf.Clamp01(phaseTime / CastSeconds);
                position = Vector2.Lerp(tip, BobberRest, t);
                position.y += Mathf.Sin(t * Mathf.PI) * 300f;
            }
            else if (phase == Phase.Idle)
            {
                position = BobberRest;
            }

            bobberDip = Mathf.Max(0f, bobberDip - dt * 1.4f);
            if (phase == Phase.Waiting || phase == Phase.Idle)
                position.y += Mathf.Sin(Time.unscaledTime * 2.2f) * 7f;
            if (phase == Phase.Bite)
                position.y -= 22f + Mathf.Sin(Time.unscaledTime * 38f) * 6f;
            if (reeling)
                position = Vector2.Lerp(BobberRest, new Vector2(-80f, 10f), 0.5f);

            position.y -= bobberDip * 30f;
            bobber.anchoredPosition = position;
            bobber.gameObject.SetActive(visible);

            // 줄: 낚싯대 끝에서 찌까지.
            Vector2 delta = position - tip;
            line.anchoredPosition = tip;
            line.sizeDelta = new Vector2(line.sizeDelta.x, delta.magnitude);
            line.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(-delta.x, delta.y) * Mathf.Rad2Deg);

            // 물결: 찌 밑에 깔리고, 튈 때 번쩍 커졌다 가라앉는다.
            splash = Mathf.Max(0f, splash - dt * 1.8f);
            ripple.anchoredPosition = new Vector2(position.x, BobberRest.y - 14f);
            ripple.localScale = new Vector3(1f + splash * 0.7f + Mathf.Sin(Time.unscaledTime * 1.6f) * 0.05f, 1f + splash * 0.5f, 1f);
            ripple.gameObject.SetActive(phase != Phase.Casting || phaseTime > CastSeconds * 0.9f);

            if (marker != null && gaugeRoot.activeSelf)
            {
                Vector2 m = marker.anchoredPosition;
                m.x = (MarkerPosition() - 0.5f) * GaugeWidth;
                marker.anchoredPosition = m;
            }
        }

        private static Vector2 RodTip(float angleDegrees)
        {
            float r = angleDegrees * Mathf.Deg2Rad;
            return RodBase + new Vector2(-Mathf.Sin(r), Mathf.Cos(r)) * RodLength;
        }

        private void Burst(FishTiming timing)
        {
            burstText.text = EventMinigameText.TimingBurst(timing);
            burstText.color = timing == FishTiming.Perfect ? PerfectColor : (timing == FishTiming.Good ? GreatColor : CoolColor);
            burstTime = 0f;
        }

        private void AnimateBurst(float dt)
        {
            if (burstText == null)
                return;

            burstTime += dt;
            float pop = burstTime < 0.18f ? Mathf.Lerp(1.7f, 1f, burstTime / 0.18f) : 1f;
            burstText.rectTransform.localScale = Vector3.one * pop;

            Color c = burstText.color;
            c.a = burstTime < 1.1f ? 1f : Mathf.Clamp01(1f - (burstTime - 1.1f) / 0.3f);
            burstText.color = c;
        }

        // ── 결과 ───────────────────────────────────────────────────────

        private float resultTime;

        private void ShowResult()
        {
            if (result == null)
            {
                ResetToIdle();
                return;
            }

            phase = Phase.Result;
            phaseTime = 0f;
            resultTime = 0f;

            gaugeRoot.SetActive(false);
            resultRoot.SetActive(true);

            Color tierColor = TierColors[Mathf.Clamp(result.Tier, 0, TierColors.Length - 1)];
            fishPanel.color = tierColor;
            tierText.text = EventMinigameText.TierName(result.Tier);
            tierText.color = tierColor;
            fishNameText.text = FishingRules.SpeciesNames[result.Tier][result.Species];
            weightText.text = EventMinigameText.Weight(result.WeightGrams);

            judgeText.text = EventMinigameText.TimingBurst(resultTiming);
            judgeText.color = resultTiming == FishTiming.Perfect ? PerfectColor : (resultTiming == FishTiming.Good ? GreatColor : CoolColor);

            multiplierPopText.text = result.Multiplier > 1 ? "x" + result.Multiplier : string.Empty;
            scoreText.text = EventMinigameText.Number(result.FishScore);

            var note = new StringBuilder();
            if (result.NewRecord)
                note.Append("신기록! 이 크기는 상위 ").Append(EventMinigameText.PercentFromPermille(result.TopPermille)).Append('%');
            if (result.Baits > 0)
            {
                if (note.Length > 0) note.Append('\n');
                note.Append("특별 떡밥 발견! 행운 ").Append(result.LuckStacks).Append("단계");
            }
            if (result.RodLevelAfter > result.RodLevelBefore)
            {
                if (note.Length > 0) note.Append('\n');
                note.Append("낚싯대가 Lv.").Append(result.RodLevelAfter).Append(" 이 됐어요");
            }

            noteText.text = note.ToString();
            ApplyControls();
        }

        /// <summary>결과 판의 연출: 한 마리 점수 → (배율이 있으면) 잠시 뒤 "xN" 이 튀고 점수가 배율 점수로 올라간다.</summary>
        private void AnimateResult(float dt)
        {
            if (phase != Phase.Result || result == null)
                return;

            resultTime += dt;

            const float multiplierAt = 0.7f;
            const float countSeconds = 0.8f;

            if (result.Multiplier <= 1)
            {
                scoreText.text = EventMinigameText.Number(result.FishScore);
                return;
            }

            if (resultTime < multiplierAt)
            {
                scoreText.text = EventMinigameText.Number(result.FishScore);
                multiplierPopText.rectTransform.localScale = Vector3.zero;
                return;
            }

            float t = Mathf.Clamp01((resultTime - multiplierAt) / countSeconds);
            multiplierPopText.rectTransform.localScale = Vector3.one * (t < 0.25f ? Mathf.Lerp(2f, 1f, t / 0.25f) : 1f);

            long value = (long)Mathf.Lerp(result.FishScore, result.CastScore, t);
            scoreText.text = EventMinigameText.Number(value);
        }

        private void Collect()
        {
            if (phase != Phase.Result || result == null)
                return;

            FishingCastResult done = result;
            SetStatus(string.Empty);
            ResetToIdle();
            RefreshPlay();
            ShowRewards(done.Rewards, "낚시 보상");
        }

        // ── 배율·화면 갱신 ─────────────────────────────────────────────

        /// <summary>x1 → x10 → x100 → x1. 낚싯대가 모자란 배율은 건너뛰고 x1 로 돌아간다.</summary>
        private void CycleMultiplier()
        {
            if (phase != Phase.Idle)
                return;

            int next = (multiplierIndex + 1) % FishingRules.Multipliers.Length;
            if (HeldCurrency < FishingRules.Multipliers[next])
                next = 0;

            multiplierIndex = next;
            RefreshPlay();
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

            // 고른 배율을 낚싯대가 못 받쳐 주면(던지는 중엔 손대지 않는다) 살 수 있는 가장 큰 배율로 내린다.
            if (phase == Phase.Idle)
            {
                int held = HeldCurrency;
                while (multiplierIndex > 0 && held < FishingRules.Multipliers[multiplierIndex])
                    multiplierIndex--;
            }

            multiplierLabel.text = "x" + FishingRules.Multipliers[multiplierIndex];
            ApplyControls();

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

            Transform p = UIEventMinigameFactory.BakeScreen(page, root.transform, font, "낚시 이벤트", "StageBg/Map_2", false);

            page.rodText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "RodInfo", 0, 32f, Color.white, font);
            page.nextLevelText = UIEventMinigameFactory.BadgeLine(p, "InfoLeft", "NextLevel", 1, 26f, UIEventMinigameFactory.MutedText, font);
            page.luckText = UIEventMinigameFactory.BadgeLine(p, "InfoRight", "LuckInfo", 1, 26f, UIEventMinigameFactory.MutedText, font);

            // 한 줄 상태·재화 글자는 장면 아래쪽(버튼 밑)으로.
            UIMissionUIFactory.SetRect(page.StatusTextInternal.rectTransform, new Vector2(860f, 120f), new Vector2(0f, -250f));
            UIMissionUIFactory.SetRect(page.CurrencyText.rectTransform, new Vector2(520f, 52f), new Vector2(0f, -800f));

            // ── 장면: 물결 · 줄 · 낚싯대 · 찌 ────────────────────────
            Image water = UIMissionUIFactory.Sliced(p, "Ripple", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(380f, 26f), new Vector2(BobberRest.x, BobberRest.y - 14f), new Color(0.8f, 0.94f, 1f, 0.42f));
            water.raycastTarget = false;
            page.ripple = water.rectTransform;

            Image lineImage = UIMissionUIFactory.CreateImage("Line", p, new Color(1f, 1f, 1f, 0.9f));
            lineImage.raycastTarget = false;
            RectTransform lineRect = lineImage.rectTransform;
            lineRect.pivot = new Vector2(0.5f, 0f);
            lineRect.sizeDelta = new Vector2(3f, 400f);
            page.line = lineRect;

            Image rodImage = UIMissionUIFactory.CreateImage("Rod", p, UIMissionUIFactory.Hex(0x7a45d6));
            rodImage.raycastTarget = false;
            RectTransform rodRect = rodImage.rectTransform;
            rodRect.pivot = new Vector2(0.5f, 0f);
            rodRect.anchoredPosition = RodBase;
            rodRect.sizeDelta = new Vector2(20f, RodLength);
            rodRect.localEulerAngles = new Vector3(0f, 0f, RodRest);
            page.rod = rodRect;

            Image rodShine = UIMissionUIFactory.CreateImage("Shine", rodImage.transform, UIMissionUIFactory.Hex(0xb895ff));
            rodShine.raycastTarget = false;
            RectTransform shineRect = rodShine.rectTransform;
            shineRect.anchorMin = new Vector2(0.5f, 0f);
            shineRect.anchorMax = new Vector2(0.5f, 1f);
            shineRect.sizeDelta = new Vector2(6f, 0f);
            shineRect.anchoredPosition = new Vector2(-4f, 0f);

            GameObject bobberRoot = UIMissionUIFactory.CreateRect("Bobber", p);
            UIMissionUIFactory.SetRect(bobberRoot.GetComponent<RectTransform>(), new Vector2(120f, 150f), BobberRest);
            page.bobber = bobberRoot.GetComponent<RectTransform>();

            Image stick = UIMissionUIFactory.Sliced(bobberRoot.transform, "Stick", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(12f, 70f), new Vector2(0f, 62f), Color.white);
            stick.raycastTarget = false;
            Image top = UIMissionUIFactory.Sliced(bobberRoot.transform, "Top", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(70f, 52f), new Vector2(0f, 12f), UIMissionUIFactory.Hex(0xe6574a));
            top.raycastTarget = false;
            Image bottom = UIMissionUIFactory.Sliced(bobberRoot.transform, "Bottom", UIEventMinigameFactory.SmallBox, 1f,
                new Vector2(70f, 46f), new Vector2(0f, -34f), Color.white);
            bottom.raycastTarget = false;

            // ── 입질 알림: "버튼을 눌러 낚싯대를 당기세요!" + 줄어드는 제한 시간 ──
            Image banner = UIMissionUIFactory.Sliced(p, "BiteBanner", UIEventMinigameFactory.SmallBox, 1.5f,
                new Vector2(660f, 140f), new Vector2(0f, -330f), UIMissionUIFactory.Hex(0x6b3fc4));
            banner.raycastTarget = false;
            page.biteBanner = banner.gameObject;

            TMP_Text bannerText = UIMissionUIFactory.CreateText(
                "BannerText", banner.transform, "버튼을 눌러 낚싯대를 당기세요!", 34f, TextAlignmentOptions.Center, UIEventMinigameFactory.GoldText, font);
            UIMissionUIFactory.SetRect(bannerText.rectTransform, new Vector2(620f, 52f), new Vector2(0f, 30f));

            page.biteFill = UIMissionUIFactory.CreateGauge("BiteBar", banner.transform,
                new Vector2(BiteBarWidth, 30f), new Vector2(0f, -28f), UIMissionUIFactory.Hex(0xffd23f), UIMissionUIFactory.TrackColor, 3f);
            banner.gameObject.SetActive(false);

            // ── 타이밍 게이지(버튼 위) ────────────────────────────────
            Image track = UIMissionUIFactory.Sliced(p, "GaugeTrack", UIEventMinigameFactory.GaugeBack, 3f,
                new Vector2(GaugeWidth + 16f, 72f), new Vector2(0f, -330f), Color.white);
            track.raycastTarget = false;
            page.gaugeRoot = track.gameObject;

            Image good = UIMissionUIFactory.Sliced(track.transform, "GoodZone", UIEventMinigameFactory.SmallBox, 0.5f,
                new Vector2(GaugeWidth * FishingRules.GoodHalfWidth * 2f, 52f), Vector2.zero, UIMissionUIFactory.Hex(0xf2d325));
            good.raycastTarget = false;
            page.goodZone = good.rectTransform;

            Image perfect = UIMissionUIFactory.Sliced(track.transform, "PerfectZone", UIEventMinigameFactory.SmallBox, 0.5f,
                new Vector2(GaugeWidth * FishingRules.PerfectHalfWidth * 2f, 52f), Vector2.zero, UIMissionUIFactory.Hex(0xff8a1f));
            perfect.raycastTarget = false;
            page.perfectZone = perfect.rectTransform;

            Image markerImage = UIMissionUIFactory.Sliced(track.transform, "Marker", UIEventMinigameFactory.SmallBox, 0.5f,
                new Vector2(22f, 100f), Vector2.zero, UIMissionUIFactory.Hex(0xe03c3c));
            markerImage.raycastTarget = false;
            page.marker = markerImage.rectTransform;
            track.gameObject.SetActive(false);

            // 판정 글자(Cool!/Great!/Perfect!)
            page.burstText = UIMissionUIFactory.CreateText(
                "Burst", p, string.Empty, 130f, TextAlignmentOptions.Center, Color.white, font);
            page.burstText.raycastTarget = false;
            UIMissionUIFactory.SetRect(page.burstText.rectTransform, new Vector2(900f, 170f), new Vector2(0f, -150f));

            // 다음 보상 한 줄(레퍼런스의 "개인 누적 보상").
            Image nextChip = UIEventMinigameFactory.Chip(p, "NextRewardChip", new Vector2(930f, 76f), new Vector2(0f, -890f));
            page.nextRewardText = UIMissionUIFactory.CreateText(
                "NextReward", nextChip.transform, string.Empty, 27f, TextAlignmentOptions.Center, Color.white, font);
            page.nextRewardText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.nextRewardText.rectTransform, new Vector2(880f, 62f), Vector2.zero);

            // ── 조작: 큰 던지기 버튼(가운데) · 배율 · 도감(왼쪽) · 보상(오른쪽) ──
            Button cast = UIEventMinigameFactory.KitButton(p, "CastButton", "던지기", EventButtonKind.Positive,
                new Vector2(360f, 210f), new Vector2(0f, -640f), 58f, font);
            page.castButton = cast;
            page.castImage = cast.GetComponent<Image>();
            page.castLabel = cast.GetComponentInChildren<TMP_Text>();

            page.multiplierButton = UIEventMinigameFactory.DockMultiplierButton(p, font);
            UIMissionUIFactory.SetRect(page.multiplierButton.GetComponent<RectTransform>(), new Vector2(150f, 84f), new Vector2(215f, -520f));
            page.multiplierLabel = page.multiplierButton.GetComponentInChildren<TMP_Text>();

            page.codexButton = UIEventMinigameFactory.KitButton(p, "CodexButton", "도감", EventButtonKind.Secondary,
                new Vector2(220f, 116f), new Vector2(-400f, -700f), 38f, font);
            page.SetRewardButton(UIEventMinigameFactory.KitButton(p, "RewardButton", "보상", EventButtonKind.Secondary,
                new Vector2(220f, 116f), new Vector2(400f, -700f), 38f, font));

            BakeResult(page, p, font);

            // 보상 판은 장면 위로 오게 마지막 쪽에, 도감 덮개는 그 위에 얹는다.
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

        /// <summary>"획득한 아이템" 판: 어두운 덮개 위에 어종 그림(자리)·점수·무게·판정·"수령".</summary>
        private static void BakeResult(UIFishingPage page, Transform p, TMP_FontAsset font)
        {
            Image veil = UIMissionUIFactory.CreateImage("ResultSheet", p, new Color(0f, 0f, 0f, 0.93f));
            UIMissionUIFactory.Stretch(veil.rectTransform);
            page.resultRoot = veil.gameObject;
            Transform r = veil.transform;

            TMP_Text title = UIMissionUIFactory.CreateText(
                "Title", r, "획득한 아이템", 72f, TextAlignmentOptions.Center, UIEventMinigameFactory.GoldText, font);
            UIMissionUIFactory.SetRect(title.rectTransform, new Vector2(900f, 100f), new Vector2(0f, 560f));

            page.tierText = UIMissionUIFactory.CreateText(
                "Tier", r, string.Empty, 40f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.tierText.rectTransform, new Vector2(500f, 56f), new Vector2(0f, 460f));

            // 어종 그림 자리: 등급 색 판 + 이름. (그림이 생기면 이 판 위에 얹는다.)
            page.fishPanel = UIMissionUIFactory.Sliced(r, "FishPanel", UIEventMinigameFactory.SlotGold, 4f,
                new Vector2(420f, 420f), new Vector2(0f, 220f), Color.white);
            page.fishPanel.raycastTarget = false;

            page.fishNameText = UIMissionUIFactory.CreateText(
                "FishName", page.fishPanel.transform, string.Empty, 60f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.fishNameText.rectTransform, new Vector2(380f, 140f), Vector2.zero);

            page.scoreText = UIMissionUIFactory.CreateText(
                "Score", r, string.Empty, 84f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.scoreText.rectTransform, new Vector2(600f, 110f), new Vector2(0f, -40f));

            page.multiplierPopText = UIMissionUIFactory.CreateText(
                "MultiplierPop", r, string.Empty, 90f, TextAlignmentOptions.Left, UIEventMinigameFactory.GoldText, font);
            UIMissionUIFactory.SetRect(page.multiplierPopText.rectTransform, new Vector2(280f, 110f), new Vector2(380f, -40f));

            Image weightChip = UIEventMinigameFactory.Chip(r, "WeightChip", new Vector2(420f, 84f), new Vector2(0f, -150f));
            page.weightText = UIMissionUIFactory.CreateText(
                "Weight", weightChip.transform, string.Empty, 46f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.weightText.rectTransform, new Vector2(400f, 64f), Vector2.zero);

            page.judgeText = UIMissionUIFactory.CreateText(
                "Judge", r, string.Empty, 80f, TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(page.judgeText.rectTransform, new Vector2(700f, 110f), new Vector2(0f, -270f));

            page.noteText = UIMissionUIFactory.CreateText(
                "Note", r, string.Empty, 34f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            page.noteText.textWrappingMode = TextWrappingModes.Normal;
            UIMissionUIFactory.SetRect(page.noteText.rectTransform, new Vector2(860f, 150f), new Vector2(0f, -400f));

            page.collectButton = UIEventMinigameFactory.KitButton(r, "CollectButton", "수령", EventButtonKind.Positive,
                new Vector2(380f, 130f), new Vector2(0f, -620f), 56f, font);

            veil.gameObject.SetActive(false);
        }
    }
}
