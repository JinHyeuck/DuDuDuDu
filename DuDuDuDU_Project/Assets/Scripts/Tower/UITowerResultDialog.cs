using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Hunting;
using OJ.Point;
using OJ.UI;

namespace OJ.Tower
{
    /// <summary>
    /// 결과 화면이 보여 줄 것 전부. <c>GameManager</c> 가 채우고 이 창이 읽는다.
    ///
    /// <b>구조체가 아니라 클래스인 이유.</b> 필드가 열둘이라 구조체로 넘기면 복사가
    /// 커지고, 무엇보다 <c>List</c> 두 개를 들고 있어 값 의미가 어차피 성립하지 않는다.
    /// </summary>
    public sealed class TowerResultData
    {
        public TowerFloorPlan Plan;
        public bool IsClear;

        public int ElapsedMilliseconds;
        public int RemainingHpPercent;

        /// <summary>
        /// 이번 판 <b>전</b>의 가장 좋았던 잔여 체력 비율. 100 이면 첫 도전이다.
        ///
        /// <b>이 값 하나로 클리어와 실패의 비교 줄이 모두 만들어진다.</b>
        /// 반복이 없어 같은 층을 두 번 클리어할 수 없으므로, "이전 기록" 이 있다면
        /// 그것은 언제나 <b>지난번 실패</b>다 — 기획서 5.6 목업의
        /// "이전 기록 · 실패 (보스 체력 18% 잔여)" 가 정확히 이 값이다.
        /// </summary>
        public int PreviousRemainingHpPercent;

        public bool IsNewRecord;

        /// <summary>이번 클리어로 열린 다이스. 없으면 <c>DiceType.Max</c>.</summary>
        public DiceType UnlockedDice;

        /// <summary>
        /// 이 층이 준 다이스. <b>처음 얻었을 때와 환급됐을 때 둘 다</b> 담긴다 —
        /// <see cref="UnlockedDice"/> 는 처음 얻은 경우만 세워지므로 환급을 표현할 수 없다.
        /// </summary>
        public DiceRewardView DiceReward;

        /// <summary>위 <see cref="DiceReward"/> 가 유효한가. 이 층에 다이스가 없으면 거짓.</summary>
        public bool HasDiceReward;

        public List<PointRewardEntry> Rewards;
        public List<DiceDamageShare> DamageShares;
    }

    /// <summary>
    /// 결과 화면. 기획서 5.6 — <b>성장 체감의 마지막 지점</b>이다.
    ///
    /// <b>"이겼다" 가 아니라 "지난번보다 이만큼 나아졌다" 를 보여준다.</b>
    /// 그래서 배치가 비교 → 원인 → 다음 목표 순서다.
    /// <list type="number">
    /// <item>기록 비교 — 이번 기록과 이전 기록을 한 덩어리로 대비</item>
    /// <item>다이스별 피해 기여도 — 어떤 선택이 정답이었는지</item>
    /// <item>보상·해금 진행도 — 다음 도전의 동기</item>
    /// <item>다음 층 미리보기 — 편성을 바꿀지 즉시 판단</item>
    /// </list>
    ///
    /// <b>실패도 같은 레이아웃을 쓴다.</b> (1)이 "적 체력 12% 남김 · 이전보다 6%p 개선" 으로,
    /// (2)가 "부족했던 대응" 으로 바뀔 뿐이다. 실패 전용 화면을 따로 만들면
    /// 실패가 <b>진척이 아니라 사고</b>로 읽힌다.
    /// </summary>
    public class UITowerResultDialog : DialogBase
    {
        private const int MaxDamageBars = 4;

        [Header("(1) 기록 비교")]
        [SerializeField] private TMP_Text headlineText;
        [SerializeField] private TMP_Text newRecordBadge;
        [SerializeField] private TMP_Text primaryValueText;
        [SerializeField] private TMP_Text comparisonText;

        [Header("(2) 기여도")]
        [SerializeField] private TMP_Text contributionCaption;
        [SerializeField] private List<UITowerDamageBar> damageBars = new List<UITowerDamageBar>();
        [SerializeField] private TMP_Text missingCounterText;

        [Header("(3) 보상 · 해금")]
        [SerializeField] private TMP_Text rewardText;

        /// <summary>
        /// 보상 아이콘이 들어갈 자리. 다른 화면과 <b>같은 칸</b>(<c>UIRewardElement</c>)을 쓴다 —
        /// 탑만 글자로 적으면 같은 보상이 화면마다 다르게 보인다.
        /// </summary>
        [SerializeField] private RectTransform rewardRoot;

        /// <summary>복제해 쓸 칸. 굽는 도구가 공용 프리팹을 여기 꽂는다.</summary>
        [SerializeField] private UIRewardElement rewardElementTemplate;

        private readonly List<UIRewardElement> rewardElements = new List<UIRewardElement>();
        [SerializeField] private TMP_Text unlockCaptionText;
        [SerializeField] private Image unlockGaugeFill;
        [SerializeField] private TMP_Text unlockProgressText;

        [Header("(4) 다음 층")]
        [SerializeField] private TMP_Text nextFloorText;

        [Header("하단")]
        [SerializeField] private Button retryButton;
        [SerializeField] private TMP_Text retryLabel;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextLabel;

        private TowerResultData data;
        private Action returnToLobby;

        protected override void OnLoad()
        {
            if (retryButton != null)
                retryButton.onClick.AddListener(OnClickRetry);

            if (nextButton != null)
                nextButton.onClick.AddListener(OnClickNext);
        }

        protected override void OnDestroy()
        {
            if (retryButton != null)
                retryButton.onClick.RemoveListener(OnClickRetry);

            if (nextButton != null)
                nextButton.onClick.RemoveListener(OnClickNext);

            base.OnDestroy();
        }

        /// <summary>
        /// 결과를 띄운다. <paramref name="onReturnToLobby"/> 는 두 버튼이 모두 쓴다 —
        /// 탑의 편성은 로비에서 하므로(기획서 5.1) 어느 쪽을 눌러도 일단 로비로 나간다.
        /// </summary>
        public void Open(TowerResultData resultData, Action onReturnToLobby)
        {
            data = resultData;
            returnToLobby = onReturnToLobby;
            Enter();
        }

        protected override void OnEnter()
        {
            transform.SetAsLastSibling();
            Refresh();
        }

        private void Refresh()
        {
            if (data == null || data.Plan == null)
                return;

            RefreshHeadline();
            RefreshContribution();
            RefreshRewards();
            RefreshNextFloor();
        }

        // ── (1) 기록 비교 ───────────────────────────────────────────────

        private void RefreshHeadline()
        {
            int floor = data.Plan.Floor;

            if (headlineText != null)
                headlineText.SetText(floor + "층 " + (data.IsClear ? "클리어" : "실패"));

            if (newRecordBadge != null)
            {
                newRecordBadge.gameObject.SetActive(data.IsNewRecord);
                newRecordBadge.SetText(data.IsClear ? "NEW RECORD" : "기록 갱신");
            }

            if (data.IsClear)
            {
                if (primaryValueText != null)
                    primaryValueText.SetText(UITowerUIFactory.FormatSeconds(data.ElapsedMilliseconds));

                if (comparisonText != null)
                    comparisonText.SetText(BuildClearComparison());

                return;
            }

            if (primaryValueText != null)
                primaryValueText.SetText("적 체력 " + data.RemainingHpPercent + "% 남음");

            if (comparisonText != null)
                comparisonText.SetText(BuildFailComparison());
        }

        /// <summary>
        /// 클리어했을 때의 비교 줄. 이전이 실패였다면 <b>그 실패를 적는다</b> —
        /// "이전 기록 · 실패 (적 체력 18% 잔여)" 가 기획서 5.6 목업의 문구다.
        /// </summary>
        private string BuildClearComparison()
        {
            // <b>여기 오는 클리어는 언제나 그 층의 첫 클리어다</b>(반복 없음).
            // 그래서 비교 상대는 "지난번 기록" 이 아니라 <b>지난번 실패</b>다 —
            // 기획서 5.6 목업의 "이전 기록 · 실패 (보스 체력 18% 잔여)" 가 정확히 그것이다.
            if (data.PreviousRemainingHpPercent < 100)
                return "이전 도전 · 실패 (적 체력 " + data.PreviousRemainingHpPercent + "% 잔여)";

            return "첫 도전에 클리어";
        }

        /// <summary>
        /// 실패했을 때의 비교 줄. <b>실패를 진척으로 읽히게</b> 만드는 자리다(기획서 5.6).
        /// 나아졌으면 몇 %p 나아졌는지 적고, 아니면 지난 최고를 적어 목표를 남긴다.
        /// </summary>
        private string BuildFailComparison()
        {
            if (data.PreviousRemainingHpPercent >= 100)
                return "첫 도전";

            int delta = data.PreviousRemainingHpPercent - data.RemainingHpPercent;
            if (delta > 0)
                return "이전보다 " + delta + "%p 개선";

            return "최고 기록 · 적 체력 " + data.PreviousRemainingHpPercent + "% 남김";
        }

        // ── (2) 기여도 ──────────────────────────────────────────────────

        private void RefreshContribution()
        {
            List<DiceDamageShare> shares = data.DamageShares ?? new List<DiceDamageShare>();

            if (contributionCaption != null)
                contributionCaption.SetText("다이스별 피해 기여도");

            for (int i = 0; i < damageBars.Count; i++)
            {
                UITowerDamageBar bar = damageBars[i];
                if (bar == null)
                    continue;

                if (i < shares.Count)
                    bar.Bind(shares[i]);
                else
                    bar.Hide();
            }

            if (missingCounterText == null)
                return;

            // 실패했을 때만 "부족했던 대응" 을 적는다. 기획서 5.6 이 (2)를 그것으로
            // 치환하라고 했고, 클리어한 판에서 부족을 말하면 성취를 깎는다.
            if (data.IsClear)
            {
                missingCounterText.gameObject.SetActive(false);
                return;
            }

            string missing = BuildMissingCounterText();
            missingCounterText.gameObject.SetActive(!string.IsNullOrEmpty(missing));
            missingCounterText.SetText(missing);
        }

        /// <summary>
        /// 이 층이 요구한 대응 중 <b>편성에 없던 것</b>을 짚는다.
        ///
        /// 기획서 8.2 가 "실패 원인을 알 수 없는 결과 화면" 을 금지 목록에 올렸다.
        /// 여기서 하는 일은 정답을 알려 주는 것이 아니라, 층이 이미 편성 화면에서
        /// 보여 준 <b>대응 키워드</b>와 실제 편성을 대조해 빠진 것을 다시 말해 주는 것뿐이다.
        /// </summary>
        private string BuildMissingCounterText()
        {
            IReadOnlyList<TowerFloorConcept> concepts = data.Plan.Concepts;
            var missing = new List<string>();

            for (int i = 0; i < concepts.Count; i++)
            {
                if (HasCounterFor(concepts[i]))
                    continue;

                string tag = TowerConceptText.CounterOf(concepts[i]);
                if (!string.IsNullOrEmpty(tag) && !missing.Contains(tag))
                    missing.Add(tag);
            }

            if (missing.Count == 0)
                return "대응은 갖췄어요. 화력이 조금 모자랐어요";

            return "부족했던 대응 : " + string.Join(" · ", missing);
        }

        /// <summary>
        /// 이번 편성이 그 콘셉트의 대응을 갖고 있었는가.
        ///
        /// <b>피해를 넣은 다이스로 판정한다.</b> 편성 목록을 보는 것이 아니라
        /// 기여도에 이름이 오른 다이스를 보는 이유는, 넣어 두고 한 대도 못 때린
        /// 다이스는 대응한 것이 아니기 때문이다.
        /// </summary>
        private bool HasCounterFor(TowerFloorConcept concept)
        {
            List<DiceDamageShare> shares = data.DamageShares;
            if (shares == null)
                return false;

            for (int i = 0; i < shares.Count; i++)
            {
                if (CountersConcept(shares[i].DiceType, concept))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 다이스 ↔ 대응 키워드 표. 기획서 6.2 의 "유효한 대응" 칸을 실제 다이스에 붙인 것이다.
        ///
        /// <b>여기서만 적는다.</b> 추천 편성(<c>TowerProgressManager.PreferredBaseFor</c>)은
        /// 기본 다이스 다섯 종만 다루지만 이쪽은 열다섯 종을 전부 봐야 해서, 두 표를
        /// 하나로 합치면 한쪽이 다른 쪽의 필요를 끌고 다니게 된다.
        /// </summary>
        private static bool CountersConcept(DiceType diceType, TowerFloorConcept concept)
        {
            switch (concept)
            {
                case TowerFloorConcept.Swarm:
                    return diceType == DiceType.Fire || diceType == DiceType.KingFire
                        || diceType == DiceType.Thunder || diceType == DiceType.KingThunder
                        || diceType == DiceType.Tornado;

                case TowerFloorConcept.Rush:
                    return diceType == DiceType.Ice || diceType == DiceType.KingIce
                        || diceType == DiceType.Stun || diceType == DiceType.Wind
                        || diceType == DiceType.Time;

                case TowerFloorConcept.Armored:
                    return diceType == DiceType.ArmorBreak || diceType == DiceType.KingFire
                        || diceType == DiceType.KingNormal;

                case TowerFloorConcept.Elite:
                case TowerFloorConcept.Regenerating:
                    return diceType == DiceType.Poison || diceType == DiceType.KingPoison;

                case TowerFloorConcept.Shielded:
                    return diceType == DiceType.Thunder || diceType == DiceType.KingThunder
                        || diceType == DiceType.KingNormal || diceType == DiceType.Wind;

                case TowerFloorConcept.Splitting:
                    return diceType == DiceType.Fire || diceType == DiceType.KingFire
                        || diceType == DiceType.Normal || diceType == DiceType.KingNormal;

                // 혼합 부대는 "역할이 다른 다이스 조합" 이라 특정 다이스로 판정할 수 없다.
                // 두 종류 이상이 실제로 피해를 넣었으면 대응한 것으로 본다 — 그 판정은
                // 호출부에서 못 하므로 여기서는 언제나 참을 돌려주고 넘어간다.
                default:
                    return true;
            }
        }

        // ── (3) 보상 · 해금 ─────────────────────────────────────────────

        private void RefreshRewards()
        {
            // 칸이 배선돼 있으면 아이콘으로 그리고 글자 줄은 감춘다.
            // 아직 안 구웠으면 예전처럼 글자로 나온다 — 굽기 전에도 화면이 비지 않게 한다.
            bool useElements = BindRewardElements();

            if (rewardText != null)
            {
                rewardText.gameObject.SetActive(!useElements);
                if (!useElements)
                    rewardText.SetText(BuildRewardText());
            }

            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return;

            DiceUnlockDefinition next = progress.GetNextUnlock();

            if (data.UnlockedDice != DiceType.Max)
            {
                // 이번 판이 해금을 만들었다. <b>다음 목표보다 이것을 먼저 보여 준다</b> —
                // 기획서 7.3 의 핵심 피드백이 "새로 얻은 다이스가 클리어를 만들었다" 인데,
                // 그 반대 방향(클리어가 다이스를 만들었다)도 같은 인과의 한쪽이다.
                if (unlockCaptionText != null)
                    unlockCaptionText.SetText(TowerDiceText.NameOf(data.UnlockedDice) + " 해금!");

                UITowerUIFactory.SetGauge(unlockGaugeFill, GaugeWidth, 1f);

                if (unlockProgressText != null)
                    unlockProgressText.SetText("편성 화면에서 바로 넣어 보세요");

                return;
            }

            if (next == null)
            {
                if (unlockCaptionText != null)
                    unlockCaptionText.SetText("모든 다이스 사용 가능");

                UITowerUIFactory.SetGauge(unlockGaugeFill, GaugeWidth, 1f);

                if (unlockProgressText != null)
                    unlockProgressText.SetText(string.Empty);

                return;
            }

            if (unlockCaptionText != null)
                unlockCaptionText.SetText(next.diceType + " 해금 진행도");

            UITowerUIFactory.SetGauge(unlockGaugeFill, GaugeWidth,
                TowerFormula.UnlockProgress01(progress.HighestClearedFloor, next.towerFloor));

            if (unlockProgressText != null)
            {
                unlockProgressText.SetText(next.towerFloor + "층 클리어 시 해금 · " +
                                           progress.HighestClearedFloor + " / " + next.towerFloor);
            }
        }

        /// <summary>
        /// 재화와 다이스를 아이콘 칸으로 그린다.
        ///
        /// <b>환급은 재화 칸으로 또 그리지 않는다.</b> 이미 가진 다이스가 재화로 돌아온
        /// 경우 그 사실을 말하는 것은 딤 처리된 다이스 칸이다 — 다른 화면과 같은 규칙이다.
        /// </summary>
        /// <returns>칸으로 그렸으면 true. 배선이 없으면 false 라 호출부가 글자로 물러선다.</returns>
        private bool BindRewardElements()
        {
            if (rewardRoot == null || rewardElementTemplate == null)
                return false;

            List<PointRewardEntry> merged = PointRewardUtility.MergeRewards(data.Rewards);
            bool hasDice = data.HasDiceReward;

            if (hasDice && !data.DiceReward.WasNew && data.DiceReward.RefundAmount > 0)
            {
                for (int i = 0; i < merged.Count; i++)
                {
                    if (merged[i].PointType != data.DiceReward.RefundCurrency)
                        continue;

                    int left = merged[i].Amount - data.DiceReward.RefundAmount;
                    if (left > 0)
                        merged[i] = new PointRewardEntry(data.DiceReward.RefundCurrency, left);
                    else
                        merged.RemoveAt(i);

                    break;
                }
            }

            int total = merged.Count + (hasDice ? 1 : 0);
            EnsureRewardElements(total);

            for (int i = 0; i < rewardElements.Count; i++)
            {
                UIRewardElement element = rewardElements[i];
                if (element == null)
                    continue;

                bool show = i < total;
                element.gameObject.SetActive(show);
                if (!show)
                    continue;

                if (i < merged.Count)
                {
                    PointRewardEntry reward = merged[i];
                    element.Bind(PointRewardUtility.GetPointIcon(reward.PointType), reward.Amount, "x{0:#,##0}");
                    continue;
                }

                Sprite diceSprite = DiceMetaDataProvider.GetIcon(data.DiceReward.DiceType);
                if (data.DiceReward.WasNew)
                    element.BindDice(diceSprite);
                else
                    element.BindDice(diceSprite,
                        PointRewardUtility.GetPointIcon(data.DiceReward.RefundCurrency),
                        data.DiceReward.RefundAmount);
            }

            return true;
        }

        private void EnsureRewardElements(int count)
        {
            while (rewardElements.Count < count)
            {
                UIRewardElement created = Instantiate(rewardElementTemplate, rewardRoot);
                created.gameObject.SetActive(true);
                rewardElements.Add(created);
            }
        }

        private string BuildRewardText()
        {
            List<PointRewardEntry> rewards = data.Rewards;
            if (rewards == null || rewards.Count == 0)
            {
                // 클리어에는 반드시 보상이 있으므로(반복이 없어 언제나 최초 클리어다)
                // 여기 오는 것은 실패뿐이다. 빈 칸으로 두면 지급이 누락된 것처럼 보인다.
                return "획득 보상 없음";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                if (i > 0)
                    sb.Append("   ");

                sb.Append(PointNameOf(rewards[i].PointType))
                    .Append(" ×")
                    .Append(ShortNumberFormat.Format(rewards[i].Amount));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 다음 층에서 받을 것. 다음 층은 아직 깨지 않은 층이므로 언제나 보상이 있다.
        /// </summary>
        private static string BuildNextRewardText(int nextFloor)
        {
            var sb = new StringBuilder();
            sb.Append("골드 ").Append(TowerFormula.FirstClearGold(nextFloor));

            if (TowerFormula.IsBandLastFloor(nextFloor))
            {
                sb.Append(" · 다이아 ").Append(TowerFormula.BandRewardDia(nextFloor));
                sb.Append(" · 신화 스크롤 ").Append(TowerFormula.BandRewardMaterial(nextFloor));
            }

            DiceType unlock = DiceUnlockDatabaseProvider.Database.GetUnlockAtFloor(nextFloor);
            if (unlock != DiceType.Max)
                sb.Append(" · ").Append(TowerDiceText.NameOf(unlock)).Append(" 해금");

            return sb.ToString();
        }

        private static string PointNameOf(PointType pointType)
        {
            switch (pointType)
            {
                case PointType.Gold: return "골드";
                case PointType.FreeGem: return "무료젬";
                case PointType.PaidGem: return "유료젬";
                case PointType.MythicScroll: return "신화석";
                case PointType.SpecialDiceCore: return "레어석";

                // 탑은 더 이상 이것을 주지 않는다(사라지는 재화였다). 다른 경로가
                // 결과창을 타고 들어올 수 있어 이름만 남겨 둔다.
                case PointType.BattleEnhanceStone: return "강화석";
                default: return pointType.ToString();
            }
        }

        // ── (4) 다음 층 ─────────────────────────────────────────────────

        private void RefreshNextFloor()
        {
            int nextFloor = TowerFormula.ClampFloor(data.Plan.Floor + 1);
            bool hasNext = data.IsClear && nextFloor != data.Plan.Floor;

            if (nextFloorText != null)
            {
                if (!hasNext)
                {
                    nextFloorText.SetText(data.IsClear
                        ? "탑의 꼭대기예요"
                        : data.Plan.Floor + "층 · " + data.Plan.DisplayName);
                }
                else
                {
                    // 콘셉트와 <b>보상</b>을 같이 적는다. 기획서 7.3 이 결과 화면에
                    // 표시할 것으로 "다음 층 적 콘셉트 미리보기" 와 "다음 층 보상" 을
                    // 둘 다 들었고, 둘이 함께 있어야 "한 층 더 갈까" 가 판단이 된다.
                    TowerFloorPlan next = TowerDatabaseProvider.GetPlan(nextFloor);
                    nextFloorText.SetText(nextFloor + "층 · " + next.DisplayName +
                                          "\n" + BuildNextRewardText(nextFloor));
                }
            }

            if (nextFloorText != null)
                nextFloorText.textWrappingMode = TextWrappingModes.Normal;

            // 왼쪽은 나가기, 오른쪽은 계속. <b>기획서 5.6 의 "편성 변경 / 26층 도전" 과
            // 다르다</b> — 그 목업은 클리어한 층을 다시 도전할 수 있다는 전제였고,
            // 반복이 없어진 지금 두 버튼이 <b>같은 층의 같은 편성 화면</b>으로 가게 된다.
            // 버튼 둘이 같은 일을 하면 하나는 없는 것만 못하다.
            //
            // 그래서 축을 "어느 층으로" 가 아니라 "계속할까 말까" 로 바꿨다.
            // 편성은 어차피 다음 도전의 편성 화면에서 바꾼다.
            if (retryLabel != null)
                retryLabel.SetText("그만하기");

            if (nextLabel != null)
            {
                nextLabel.SetText(data.IsClear
                    ? (hasNext ? nextFloor + "층 도전" : "탑 나가기")
                    : "다시 도전");
            }
        }

        // ── 버튼 ────────────────────────────────────────────────────────
        //
        // 둘 다 로비로 돌아간다. 탑의 편성은 로비에서 하므로(기획서 5.1) 전투 씬에
        // 편성 화면을 띄우면 <b>같은 창이 두 씬에 존재</b>하게 되고, 그 창이 만드는
        // 출전 요청이 어느 씬에서 소비되는지가 흐려진다.
        //
        // 다른 것은 <b>편성 화면까지 이어 가느냐</b> 뿐이다. 그 의사는
        // <c>TowerProgressManager.RequestLobbyLoadout</c> 이 씬을 넘어 들고 간다.

        /// <summary>그만하기. 로비로만 돌아가고 편성 화면은 열지 않는다.</summary>
        private void OnClickRetry()
        {
            LeaveToLobby();
        }

        /// <summary>
        /// 계속하기. 다음 도전의 편성 화면을 로비에서 바로 연다.
        ///
        /// <b>다음 도전이 어느 층인지는 결과가 정한다</b> — 클리어했으면 다음 층,
        /// 실패했으면 같은 층이다. 반복이 없으므로 그 둘 말고는 갈 곳이 없다.
        /// </summary>
        private void OnClickNext()
        {
            // data 가 없으면 이 창은 <see cref="Open"/> 없이 떠 있는 것이다. 그래도
            // 로비로는 내보낸다 — 여기서 멈추면 이미 끝난 판의 전투 씬에 갇힌다.
            if (data == null || data.Plan == null)
            {
                LeaveToLobby();
                return;
            }

            int nextFloor = data.IsClear
                ? TowerFormula.ClampFloor(data.Plan.Floor + 1)
                : data.Plan.Floor;

            // 꼭대기를 깬 경우에는 이어 갈 층이 없다. 로비로만 나간다.
            if (data.IsClear && nextFloor == data.Plan.Floor)
            {
                LeaveToLobby();
                return;
            }

            TowerProgressManager.Instance?.RequestLobbyLoadout(nextFloor);
            LeaveToLobby();
        }

        private void LeaveToLobby()
        {
            Exit();
            returnToLobby?.Invoke();
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        private const float PanelWidth = 980f;
        private const float PanelHeight = 1500f;
        private const float GaugeWidth = 880f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UITowerResultDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UITowerUIFactory.CreateRect("UITowerResultDialog", parent);
            UITowerUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UITowerUIFactory.CreateRect("DialogView", root.transform);
            UITowerUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UITowerResultDialog>();
            dialog.dialogView = view;

            // 백키로 닫지 않는다. 결과 화면을 닫으면 전투 씬에 남는데, 그 씬에는
            // 이미 끝난 판밖에 없다. 나가는 길은 아래 두 버튼뿐이다.
            dialog.UseBackBtn = false;

            Image blocker = UITowerUIFactory.CreateImage("Backdrop", view.transform, UITowerUIFactory.Backdrop);
            UITowerUIFactory.Stretch(blocker.rectTransform);
            var blockerButton = blocker.gameObject.AddComponent<Button>();
            blockerButton.targetGraphic = blocker;
            blockerButton.transition = Selectable.Transition.None;

            Image panel = UITowerUIFactory.CreateImage("Panel", view.transform, UITowerUIFactory.PanelColor);
            UITowerUIFactory.SetRect(panel.rectTransform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            Transform p = panel.transform;

            float top = PanelHeight * 0.5f;

            // 헤더
            dialog.headlineText = UITowerUIFactory.CreateText("Headline", p, "25층 클리어", 44f,
                TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.headlineText.rectTransform,
                new Vector2(560f, 60f), new Vector2(-190f, top - 58f));

            dialog.newRecordBadge = UITowerUIFactory.CreateText("NewRecord", p, "NEW RECORD", 28f,
                TextAlignmentOptions.Right, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.newRecordBadge.rectTransform,
                new Vector2(380f, 60f), new Vector2(250f, top - 58f));

            // (1) 기록 비교
            Image recordBox = UITowerUIFactory.CreateImage("RecordBox", p, UITowerUIFactory.AccentSoft);
            UITowerUIFactory.SetRect(recordBox.rectTransform, new Vector2(920f, 230f), new Vector2(0f, top - 230f));

            TMP_Text recordCaption = UITowerUIFactory.CreateText("Caption", recordBox.transform, "클리어 시간", 26f,
                TextAlignmentOptions.Center, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(recordCaption.rectTransform, new Vector2(880f, 32f), new Vector2(0f, 78f));

            dialog.primaryValueText = UITowerUIFactory.CreateText("Primary", recordBox.transform, "42.3초", 76f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(dialog.primaryValueText.rectTransform,
                new Vector2(880f, 90f), new Vector2(0f, 10f));

            dialog.comparisonText = UITowerUIFactory.CreateText("Comparison", recordBox.transform,
                "이전 기록 · 실패 (적 체력 18% 잔여)", 26f,
                TextAlignmentOptions.Center, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.comparisonText.rectTransform,
                new Vector2(880f, 34f), new Vector2(0f, -68f));

            // (2) 기여도
            float contributionTop = top - 380f;

            dialog.contributionCaption = UITowerUIFactory.CreateText("ContribCaption", p,
                "다이스별 피해 기여도", 26f, TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(dialog.contributionCaption.rectTransform,
                new Vector2(880f, 32f), new Vector2(0f, contributionTop));

            for (int i = 0; i < MaxDamageBars; i++)
            {
                UITowerDamageBar bar = UITowerDamageBar.Create(
                    p, new Vector2(0f, contributionTop - 50f - i * 56f), font);

                dialog.damageBars.Add(bar);
            }

            dialog.missingCounterText = UITowerUIFactory.CreateText("Missing", p,
                "부족했던 대응 : 방어 감소", 26f, TextAlignmentOptions.Left,
                new Color(1f, 0.55f, 0.52f, 1f), font);
            UITowerUIFactory.SetRect(dialog.missingCounterText.rectTransform,
                new Vector2(880f, 34f), new Vector2(0f, contributionTop - 50f - MaxDamageBars * 56f - 6f));

            // (3) 보상 · 해금
            float rewardTop = contributionTop - 50f - MaxDamageBars * 56f - 70f;

            Image rewardBox = UITowerUIFactory.CreateImage("RewardBox", p, UITowerUIFactory.CardColor);
            UITowerUIFactory.SetRect(rewardBox.rectTransform, new Vector2(920f, 240f), new Vector2(0f, rewardTop - 120f));

            TMP_Text rewardCaption = UITowerUIFactory.CreateText("Caption", rewardBox.transform, "획득 보상", 24f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(rewardCaption.rectTransform, new Vector2(880f, 30f), new Vector2(0f, 86f));

            dialog.rewardText = UITowerUIFactory.CreateText("Rewards", rewardBox.transform,
                "골드 ×340", 30f, TextAlignmentOptions.Left, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.rewardText.rectTransform, new Vector2(880f, 40f), new Vector2(0f, 44f));

            dialog.unlockCaptionText = UITowerUIFactory.CreateText("UnlockCaption", rewardBox.transform,
                "KingFire 해금 진행도", 26f, TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.unlockCaptionText.rectTransform,
                new Vector2(880f, 34f), new Vector2(0f, -6f));

            dialog.unlockGaugeFill = UITowerUIFactory.CreateGauge("UnlockGauge", rewardBox.transform,
                new Vector2(GaugeWidth, 18f), new Vector2(0f, -46f), UITowerUIFactory.Accent);

            dialog.unlockProgressText = UITowerUIFactory.CreateText("UnlockProgress", rewardBox.transform,
                "30층 클리어 시 해금 · 25 / 30", 24f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(dialog.unlockProgressText.rectTransform,
                new Vector2(880f, 30f), new Vector2(0f, -82f));

            // (4) 다음 층 미리보기
            // 두 줄이 들어간다 — 콘셉트와 다음 층 보상(기획서 7.3). 높이를 한 줄에
            // 맞춰 두면 보상 줄이 상자 밖으로 삐져나온다.
            Image nextBox = UITowerUIFactory.CreateImage("NextBox", p, UITowerUIFactory.CardColor);
            UITowerUIFactory.SetRect(nextBox.rectTransform, new Vector2(920f, 150f), new Vector2(0f, rewardTop - 310f));

            TMP_Text nextCaption = UITowerUIFactory.CreateText("Caption", nextBox.transform, "다음 층 미리보기", 24f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(nextCaption.rectTransform, new Vector2(880f, 28f), new Vector2(0f, 50f));

            dialog.nextFloorText = UITowerUIFactory.CreateText("NextFloor", nextBox.transform,
                "26층 · 고방어 부대", 30f, TextAlignmentOptions.TopLeft, Color.white, font);
            UITowerUIFactory.SetRect(dialog.nextFloorText.rectTransform,
                new Vector2(880f, 88f), new Vector2(0f, -18f));
            dialog.nextFloorText.textWrappingMode = TextWrappingModes.Normal;

            // 하단 버튼
            float bottomY = -PanelHeight * 0.5f + 70f;

            dialog.retryButton = UITowerUIFactory.CreateButton("Retry", p, "편성 변경",
                new Vector2(420f, 96f), new Vector2(-240f, bottomY),
                UITowerUIFactory.CardColor, Color.white, 32f, font);
            dialog.retryLabel = dialog.retryButton.GetComponentInChildren<TMP_Text>();

            dialog.nextButton = UITowerUIFactory.CreateButton("Next", p, "26층 도전",
                new Vector2(420f, 96f), new Vector2(240f, bottomY),
                UITowerUIFactory.Accent, Color.white, 34f, font);
            dialog.nextLabel = dialog.nextButton.GetComponentInChildren<TMP_Text>();

            return dialog;
        }
    }
}
