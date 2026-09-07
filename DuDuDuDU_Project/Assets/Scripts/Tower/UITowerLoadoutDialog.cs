using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.SceneFlow;
using OJ.UI;

namespace OJ.Tower
{
    /// <summary>
    /// 다이스 편성 화면. 기획서 5.3 — <b>이 콘텐츠의 핵심 화면</b>이다.
    ///
    /// <b>세로 배치가 사고 순서다.</b> 적 정보(1) → 현재 답(2) → 후보 전체(3).
    /// 유저는 위에서 아래로 한 번 훑으며 판단을 끝낸다. 적 정보를 별도 화면으로 빼지
    /// 않은 것도 같은 이유다 — 편성 중에 전제를 다시 확인하러 되돌아가는 동선을 없앤다.
    ///
    /// <b>후보 30칸이 스크롤 없이 한 화면에 들어온다.</b> 그래야 "무엇을 못 쓰는가(잠금)"
    /// 와 "무엇을 더 얻어야 하는가" 가 한눈에 전달된다. 그리드를 접거나 탭으로 나누면
    /// 그 효과가 사라진다(기획서 5.3 레이아웃 근거).
    /// </summary>
    public class UITowerLoadoutDialog : DialogBase
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text slotCountText;

        [Header("(1) 적 정보")]
        [SerializeField] private TMP_Text enemyHeadlineText;
        [SerializeField] private TMP_Text enemyDetailText;
        [SerializeField] private TMP_Text counterTagText;

        [Header("(2) 선택 슬롯 바")]
        [SerializeField] private List<UITowerSlotView> slotViews = new List<UITowerSlotView>();

        [Header("(3) 후보 그리드")]
        [SerializeField] private List<UITowerDiceCell> cells = new List<UITowerDiceCell>();
        [SerializeField] private List<TMP_Text> rowCapacityTexts = new List<TMP_Text>();

        [Header("(4) 편의 기능")]
        [SerializeField] private Button loadRecentButton;
        [SerializeField] private Button clearAllButton;
        [SerializeField] private TMP_Text newCountText;

        [Header("하단")]
        [SerializeField] private Button recommendButton;
        [SerializeField] private Button startButton;

        [Header("토스트")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private TMP_Text toastText;

        private readonly TowerLoadout loadout = new TowerLoadout();
        private TowerFloorPlan plan;
        private Coroutine toastRoutine;

        /// <summary>
        /// 이번에 새로 열린 다이스. NEW 배지의 근거다(기획서 5.4).
        /// <b>창을 닫으면 사라진다</b> — 획득 직후 한 번 눈에 띄면 제 일을 다 한 것이고,
        /// 영구히 남기면 배지가 배경이 된다.
        /// </summary>
        private readonly HashSet<DiceType> newlyUnlocked = new HashSet<DiceType>();

        protected override void OnLoad()
        {
            if (loadRecentButton != null)
                loadRecentButton.onClick.AddListener(OnClickLoadRecent);

            if (clearAllButton != null)
                clearAllButton.onClick.AddListener(OnClickClearAll);

            if (recommendButton != null)
                recommendButton.onClick.AddListener(OnClickRecommend);

            if (startButton != null)
                startButton.onClick.AddListener(OnClickStart);

            HideToast();
        }

        protected override void OnDestroy()
        {
            if (loadRecentButton != null)
                loadRecentButton.onClick.RemoveListener(OnClickLoadRecent);

            if (clearAllButton != null)
                clearAllButton.onClick.RemoveListener(OnClickClearAll);

            if (recommendButton != null)
                recommendButton.onClick.RemoveListener(OnClickRecommend);

            if (startButton != null)
                startButton.onClick.RemoveListener(OnClickStart);

            base.OnDestroy();
        }

        /// <summary>
        /// 이 층의 편성 화면을 연다. 층 선택 화면과 결과 화면이 부른다.
        ///
        /// <b>직전 편성을 기본값으로 깐다.</b> 기획서 5.5 "최근 편성 저장 — 직전 출전
        /// 편성을 자동 저장한다". 매번 빈 판에서 시작하면 재도전이 일곱 번의 탭이 된다.
        /// </summary>
        public void Open(int floor)
        {
            TowerProgressManager progress = TowerProgressManager.Instance;

            // <b>도전 가능한 층으로 눌러 준다.</b> 부르는 곳이 셋이라(층 카드 · 하단
            // 버튼 · 결과 화면에서 넘어온 예약) 한 곳만 어긋나도 이미 깬 층의 편성
            // 화면이 열리고, 거기서 "전투 시작" 을 누르면 반복 플레이가 성립한다.
            // 막는 자리를 화면 셋에 흩지 않고 여기 하나로 모은다.
            if (progress != null && !progress.IsFloorChallengeable(floor))
                floor = progress.HighestUnlockedFloor;

            plan = TowerDatabaseProvider.GetPlan(floor);

            if (progress != null)
            {
                // 방금 열린 다이스가 있으면 배지를 붙인다. 기획서 1.1 이 이 콘텐츠의
                // 문제 정의로 든 것이 "새로 얻은 다이스를 바로 써보기 어렵다" 이고,
                // 그 답이 "획득 즉시 편성" 이다 — 배지가 그 동선의 시작점이다.
                MarkNewlyUnlocked(progress.ConsumeNewUnlock());

                TowerLoadout recent = progress.CloneLastLoadout();
                loadout.CopyFrom(recent);

                // 편성이 비어 있으면(첫 도전) 추천으로 채운다. 빈 슬롯 일곱 개를 보여
                // 주는 것은 "직접 골라라" 가 아니라 "무엇부터 해야 하는지 모르겠다" 가 된다.
                if (loadout.Count == 0)
                    loadout.CopyFrom(progress.BuildRecommendedLoadout(plan));
            }

            Enter();
        }

        protected override void OnEnter()
        {
            transform.SetAsLastSibling();
            HideToast();
            Refresh();
        }

        protected override void OnExit()
        {
            newlyUnlocked.Clear();
        }

        /// <summary>새로 해금된 다이스를 알려 준다. 결과 화면이 이 창을 열기 전에 부른다.</summary>
        public void MarkNewlyUnlocked(DiceType diceType)
        {
            if (diceType != DiceType.Max)
                newlyUnlocked.Add(diceType);
        }

        // ── 그리기 ──────────────────────────────────────────────────────

        private void Refresh()
        {
            if (plan == null)
                return;

            TowerProgressManager progress = TowerProgressManager.Instance;

            if (titleText != null)
                titleText.SetText(plan.Floor + "층 · 다이스 편성");

            if (slotCountText != null)
                slotCountText.SetText(loadout.Count + " / " + TowerFormula.TotalSlotCount);

            RefreshEnemyInfo();
            RefreshSlotBar();
            RefreshGrid(progress);

            if (newCountText != null)
            {
                newCountText.gameObject.SetActive(newlyUnlocked.Count > 0);
                newCountText.SetText("NEW {0}", newlyUnlocked.Count);
            }
        }

        private void RefreshEnemyInfo()
        {
            if (enemyHeadlineText != null)
                enemyHeadlineText.SetText(plan.BuildEnemySummary());

            if (enemyDetailText != null)
            {
                var sb = new StringBuilder();
                sb.Append("체력 ").Append(ShortNumberFormat.Format(plan.MonsterHp));
                sb.Append(" · 방어력 ").Append(plan.MonsterDefense);

                if (plan.ShieldHitCharges > 0)
                    sb.Append(" · 보호막 ").Append(plan.ShieldHitCharges).Append("타");

                if (plan.RegenPercentPerSecond > 0f)
                    sb.Append(" · 초당 회복 ").Append(plan.RegenPercentPerSecond.ToString("0.#")).Append('%');

                // 분열이 있으면 <b>총 처치 수</b>를 같이 적는다. 위 헤드라인은 "분열형 적 ×8"
                // 인데 전투 게이지는 24 를 목표로 잡으므로(자식까지 세니까), 여기서 잇지
                // 않으면 두 화면이 서로 다른 말을 하는 것처럼 보인다.
                if (plan.SplitChildCount > 0)
                {
                    sb.Append(" · 분열 ").Append(plan.SplitChildCount).Append("마리");
                    sb.Append(" (총 ").Append(plan.KillTarget).Append("마리 처치)");
                }

                enemyDetailText.SetText(sb.ToString());
            }

            if (counterTagText != null)
            {
                List<string> tags = plan.BuildCounterTags();
                counterTagText.SetText(string.Join("   ", tags));
            }
        }

        private void RefreshSlotBar()
        {
            List<TowerLoadoutEntry> entries = loadout.ToOrderedList();

            for (int i = 0; i < slotViews.Count; i++)
            {
                UITowerSlotView view = slotViews[i];
                if (view == null)
                    continue;

                if (i < entries.Count)
                    view.BindFilled(entries[i]);
                else
                    view.BindEmpty();
            }
        }

        private void RefreshGrid(TowerProgressManager progress)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                UITowerDiceCell cell = cells[i];
                if (cell == null)
                    continue;

                DiceType diceType = cell.DiceType;
                int star = cell.Star;

                bool unlocked = progress == null || progress.IsDiceUnlocked(diceType);
                bool selected = loadout.Contains(diceType, star);
                bool tierFull = loadout.IsTierFull(diceType, star);

                cell.Bind(
                    diceType, star, unlocked, selected, tierFull,
                    newlyUnlocked.Contains(diceType),
                    OnCellTapped, OnCellHeld);
            }

            RefreshRowCapacity();
        }

        /// <summary>
        /// 줄 왼쪽의 (n / n) 표기. 기획서 5.3 "줄 좌측의 선택 한도 표기가 규칙을 학습시킨다".
        /// 순서는 그리드와 같다 — 신화 · 스페셜 · 4성 · 3성 · 2성 · 1성.
        /// </summary>
        private void RefreshRowCapacity()
        {
            if (rowCapacityTexts.Count < 6)
                return;

            SetRowCapacity(0, "신화", loadout.Mythics.Count, TowerFormula.MythicSlotCount);
            SetRowCapacity(1, "스페셜", loadout.Specials.Count, TowerFormula.SpecialSlotCount);

            for (int star = TowerFormula.MaxBaseStar; star >= 1; star--)
            {
                int row = 2 + (TowerFormula.MaxBaseStar - star);
                int used = loadout.GetBaseAt(star) != DiceType.Max ? 1 : 0;
                SetRowCapacity(row, star + "성", used, 1);
            }
        }

        private void SetRowCapacity(int row, string name, int used, int capacity)
        {
            TMP_Text text = rowCapacityTexts[row];
            if (text != null)
                text.SetText(name + "\n" + used + " / " + capacity);
        }

        // ── 입력 ────────────────────────────────────────────────────────

        private void OnCellTapped(DiceType diceType, int star)
        {
            TowerProgressManager progress = TowerProgressManager.Instance;

            if (progress != null && !progress.IsDiceUnlocked(diceType))
            {
                // 기획서 5.4 "미보유 — 탭 → 획득처·해금 조건 안내". 잠긴 칸의 탭이
                // 아무 일도 안 하면 그 칸은 화면의 얼룩이 되고, 수집 목표를 노출한다는
                // 이 화면의 목적(8.3)이 반만 이뤄진다.
                int unlockFloor = TowerDatabaseProvider.Database.GetUnlockFloorOf(diceType);
                ShowToast(unlockFloor > 0
                    ? TowerDiceText.NameOf(diceType) + " 은(는) " + unlockFloor + "층을 클리어하면 열려요"
                    : TowerDiceText.NameOf(diceType) + " 은(는) 아직 얻을 수 없어요");
                return;
            }

            if (loadout.IsTierFull(diceType, star))
            {
                // 기획서 5.4 "한도 도달 — 탭 불가 (안내 토스트)".
                ShowToast(BuildTierFullMessage(diceType));
                return;
            }

            if (!loadout.Toggle(diceType, star))
                return;

            newlyUnlocked.Remove(diceType);
            Refresh();
        }

        private static string BuildTierFullMessage(DiceType diceType)
        {
            switch (TowerLoadoutRules.TierOf(diceType))
            {
                case TowerSlotTier.Mythic:
                    return "신화는 " + TowerFormula.MythicSlotCount + "개까지 넣을 수 있어요";
                case TowerSlotTier.Special:
                    return "스페셜은 " + TowerFormula.SpecialSlotCount + "개까지 넣을 수 있어요";
                default:
                    return "성급마다 하나씩만 넣을 수 있어요";
            }
        }

        /// <summary>
        /// 길게 누르기 → 상세. 기획서 5.5 각주.
        ///
        /// 전투 상세 패널(<c>UIBattleDiceDetailPanel</c>)은 보드 위의 <b>실제 다이스</b>를
        /// 받는 구조라 여기서 재사용할 수 없다. 지금은 요약을 토스트로 대신한다 —
        /// 전용 상세창은 아트가 붙을 때 함께 만드는 편이 낫고, 그때까지 "길게 누르면
        /// 아무 일도 안 난다" 로 두면 규약만 있고 동작이 없는 상태가 된다.
        /// </summary>
        private void OnCellHeld(DiceType diceType, int star)
        {
            ShowToast(BuildDiceSummary(diceType, star), 2.4f);
        }

        private static string BuildDiceSummary(DiceType diceType, int star)
        {
            // 강화 레벨은 본편과 공유한다. 탑은 <b>지금 내 성장 수준</b>을 재는 콘텐츠라
            // (기획서 1.2 목표 02) 레벨을 따로 두면 잴 대상이 사라진다.
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;

            var sb = new StringBuilder();
            sb.Append(TowerDiceText.NameWithStar(diceType, star)).Append("  ");
            sb.Append("피해 ").Append(ShortNumberFormat.Format(
                DiceMetaDataProvider.CalculateDamage(diceType, star, level)));

            // battle 은 로비에서 IsActive 가 false 다. DiceTraitText 가 그 경우를 알고
            // 장비 보너스 없이 계산하므로 그대로 넘긴다.
            string trait = DiceTraitText.Short(diceType, level, GameContainer.Battle);
            if (!string.IsNullOrEmpty(trait))
                sb.Append("  ·  ").Append(trait);

            return sb.ToString();
        }

        private void OnClickLoadRecent()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return;

            loadout.CopyFrom(progress.CloneLastLoadout());
            Refresh();
            ShowToast("최근 편성을 불러왔어요");
        }

        private void OnClickClearAll()
        {
            loadout.Clear();
            Refresh();
        }

        private void OnClickRecommend()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null || plan == null)
                return;

            loadout.CopyFrom(progress.BuildRecommendedLoadout(plan));
            Refresh();
            ShowToast("이 층에 맞춰 채웠어요. 바꿔도 좋아요");
        }

        /// <summary>
        /// 출전한다.
        ///
        /// <b>빈 슬롯이 있어도 막지 않는다.</b> 기획서 5.5 — "빈 슬롯이 있어도 전투 시작
        /// 버튼은 항상 활성화한다. 빈 슬롯 안내는 최초 출전 시 1회만 노출". 못 가진
        /// 다이스 자리를 비워 두는 것이 이 콘텐츠의 규칙이므로(4.2), 그것을 경고로
        /// 반복하면 <b>규칙을 실수처럼 보이게</b> 만든다.
        ///
        /// 편성이 <b>통째로 비었을 때</b>만 막는다. 그건 규칙이 아니라 사고다 —
        /// 다이스 0개로는 아무것도 못 한다.
        /// </summary>
        private void OnClickStart()
        {
            if (plan == null)
                return;

            if (loadout.Count == 0)
            {
                ShowToast("다이스를 하나 이상 골라 주세요");
                return;
            }

            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return;

            if (loadout.HasEmptySlot && !progress.EmptySlotNoticeShown)
            {
                progress.MarkEmptySlotNoticeShown();
                ShowToast("빈 슬롯은 그대로 출전해요. 다이스를 더 모으면 채워져요", 2.4f);
            }

            progress.RequestRun(plan.Floor, loadout);
            Exit();
            SceneFlowManager.LoadBattle();
        }

        // ── 토스트 ──────────────────────────────────────────────────────

        private void ShowToast(string message, float seconds = 1.6f)
        {
            if (toastRoot == null || toastText == null)
                return;

            toastText.SetText(message);
            toastRoot.SetActive(true);

            if (toastRoutine != null)
                StopCoroutine(toastRoutine);

            toastRoutine = StartCoroutine(CoHideToast(seconds));
        }

        private IEnumerator CoHideToast(float seconds)
        {
            // 실제 시간으로 잰다. 이 창은 로비에서 뜨므로 timeScale 이 1 이지만,
            // 전투에서 열리게 되는 날 배속에 따라 안내가 사라지는 속도가 달라진다.
            yield return new WaitForSecondsRealtime(seconds);
            HideToast();
            toastRoutine = null;
        }

        private void HideToast()
        {
            if (toastRoot != null)
                toastRoot.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 에디터 굽기 전용. 런타임에 부르지 않는다.
        // ──────────────────────────────────────────────────────────────

        private const float PanelWidth = 1000f;
        private const float PanelHeight = 1700f;
        private const float SlotSize = 108f;
        private const float SlotGap = 12f;
        private const float CellSize = 128f;
        private const float CellGap = 14f;
        private const int GridColumns = 5;
        private const int GridRows = 6;

        /// <summary>에디터 굽기 전용.</summary>
        public static UITowerLoadoutDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UITowerUIFactory.CreateRect("UITowerLoadoutDialog", parent);
            UITowerUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UITowerUIFactory.CreateRect("DialogView", root.transform);
            UITowerUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UITowerLoadoutDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

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
            dialog.titleText = UITowerUIFactory.CreateText("Title", p, "26층 · 다이스 편성", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.titleText.rectTransform,
                new Vector2(600f, 56f), new Vector2(-170f, top - 54f));

            dialog.slotCountText = UITowerUIFactory.CreateText("SlotCount", p, "2 / 7", 34f,
                TextAlignmentOptions.Right, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.slotCountText.rectTransform,
                new Vector2(240f, 56f), new Vector2(310f, top - 54f));

            var closeButton = UITowerUIFactory.CreateButton("Close", p, "X",
                new Vector2(60f, 60f), new Vector2(PanelWidth * 0.5f - 46f, top - 52f),
                UITowerUIFactory.DangerColor, Color.white, 32f, font);
            dialog.AddExitButton(closeButton);

            // (1) 적 정보
            Image enemyBox = UITowerUIFactory.CreateImage("EnemyInfo", p,
                new Color(0.20f, 0.12f, 0.16f, 1f));
            UITowerUIFactory.SetRect(enemyBox.rectTransform, new Vector2(940f, 200f), new Vector2(0f, top - 200f));

            TMP_Text enemyCaption = UITowerUIFactory.CreateText("Caption", enemyBox.transform, "이 층의 적", 24f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(enemyCaption.rectTransform, new Vector2(900f, 30f), new Vector2(0f, 68f));

            dialog.enemyHeadlineText = UITowerUIFactory.CreateText("Headline", enemyBox.transform,
                "고방어 부대 ×1 · 받는 피해를 크게 감소", 32f, TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(dialog.enemyHeadlineText.rectTransform,
                new Vector2(900f, 44f), new Vector2(0f, 26f));
            dialog.enemyHeadlineText.textWrappingMode = TextWrappingModes.Normal;

            dialog.enemyDetailText = UITowerUIFactory.CreateText("Detail", enemyBox.transform,
                "체력 0 · 방어력 0", 26f, TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(dialog.enemyDetailText.rectTransform,
                new Vector2(900f, 34f), new Vector2(0f, -18f));

            dialog.counterTagText = UITowerUIFactory.CreateText("Tags", enemyBox.transform,
                "방어 감소 · 단일 고화력", 26f, TextAlignmentOptions.Left, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(dialog.counterTagText.rectTransform,
                new Vector2(900f, 34f), new Vector2(0f, -60f));

            // (2) 선택 슬롯 바 — 7칸
            Image slotBox = UITowerUIFactory.CreateImage("SlotBar", p, UITowerUIFactory.CardColor);
            UITowerUIFactory.SetRect(slotBox.rectTransform, new Vector2(940f, 176f), new Vector2(0f, top - 400f));

            TMP_Text slotCaption = UITowerUIFactory.CreateText("Caption", slotBox.transform, "선택한 다이스", 24f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(slotCaption.rectTransform, new Vector2(900f, 28f), new Vector2(0f, 62f));

            float slotTotalWidth = TowerFormula.TotalSlotCount * SlotSize + (TowerFormula.TotalSlotCount - 1) * SlotGap;
            float slotStartX = -slotTotalWidth * 0.5f + SlotSize * 0.5f;

            for (int i = 0; i < TowerFormula.TotalSlotCount; i++)
            {
                UITowerSlotView slot = UITowerSlotView.Create(
                    slotBox.transform,
                    new Vector2(SlotSize, SlotSize),
                    new Vector2(slotStartX + i * (SlotSize + SlotGap), -14f),
                    font);

                dialog.slotViews.Add(slot);
            }

            // (3) 후보 그리드 5 × 6
            float gridTop = top - 520f;
            float gridWidth = GridColumns * CellSize + (GridColumns - 1) * CellGap;
            float gridStartX = -gridWidth * 0.5f + CellSize * 0.5f + 60f;

            for (int row = 0; row < GridRows; row++)
            {
                float y = gridTop - row * (CellSize + CellGap) - CellSize * 0.5f;

                TMP_Text capacity = UITowerUIFactory.CreateText("Row" + row + "Capacity", p, "0 / 0", 22f,
                    TextAlignmentOptions.Center, UITowerUIFactory.MutedText, font);
                UITowerUIFactory.SetRect(capacity.rectTransform,
                    new Vector2(120f, 70f), new Vector2(-PanelWidth * 0.5f + 74f, y));
                dialog.rowCapacityTexts.Add(capacity);

                for (int column = 0; column < GridColumns; column++)
                {
                    DiceType diceType = GridDiceAt(row, column);
                    int star = GridStarAt(row);

                    UITowerDiceCell cell = UITowerDiceCell.Create(
                        p,
                        new Vector2(CellSize, CellSize),
                        new Vector2(gridStartX + column * (CellSize + CellGap), y),
                        font);

                    // 굽는 시점에 어떤 다이스인지 확정한다. 런타임에 배정하면
                    // 그리드 순서가 코드 두 곳(굽기·갱신)에 걸치게 된다.
                    cell.BakeAssign(diceType, star);

                    dialog.cells.Add(cell);
                }
            }

            // (4) 편의 기능 바
            float utilityY = gridTop - GridRows * (CellSize + CellGap) - 46f;

            dialog.loadRecentButton = UITowerUIFactory.CreateButton("LoadRecent", p, "최근 편성 불러오기",
                new Vector2(360f, 72f), new Vector2(-280f, utilityY),
                UITowerUIFactory.CardColor, Color.white, 26f, font);

            dialog.clearAllButton = UITowerUIFactory.CreateButton("ClearAll", p, "전체 해제",
                new Vector2(240f, 72f), new Vector2(20f, utilityY),
                UITowerUIFactory.CardColor, Color.white, 26f, font);

            dialog.newCountText = UITowerUIFactory.CreateText("NewCount", p, "NEW 2", 26f,
                TextAlignmentOptions.Center, new Color(0.95f, 0.45f, 0.5f, 1f), font);
            UITowerUIFactory.SetRect(dialog.newCountText.rectTransform,
                new Vector2(200f, 72f), new Vector2(290f, utilityY));

            // 하단 버튼
            float bottomY = -PanelHeight * 0.5f + 70f;

            dialog.recommendButton = UITowerUIFactory.CreateButton("Recommend", p, "추천 편성",
                new Vector2(420f, 96f), new Vector2(-250f, bottomY),
                UITowerUIFactory.CardColor, Color.white, 32f, font);

            dialog.startButton = UITowerUIFactory.CreateButton("Start", p, "전투 시작",
                new Vector2(440f, 96f), new Vector2(250f, bottomY),
                UITowerUIFactory.Accent, Color.white, 36f, font);

            // 토스트
            GameObject toast = UITowerUIFactory.CreateRect("Toast", p);
            UITowerUIFactory.SetRect(toast.GetComponent<RectTransform>(),
                new Vector2(860f, 84f), new Vector2(0f, bottomY + 120f));
            dialog.toastRoot = toast;

            Image toastBg = UITowerUIFactory.CreateImage("Bg", toast.transform, new Color(0.05f, 0.06f, 0.12f, 0.95f));
            UITowerUIFactory.Stretch(toastBg.rectTransform);
            toastBg.raycastTarget = false;

            dialog.toastText = UITowerUIFactory.CreateText("Text", toast.transform, string.Empty, 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(dialog.toastText.rectTransform, new Vector2(840f, 84f), Vector2.zero);
            dialog.toastText.textWrappingMode = TextWrappingModes.Normal;

            toast.SetActive(false);

            return dialog;
        }

        /// <summary>
        /// 그리드 (줄, 칸) 에 놓일 다이스. 기획서 5.3 목업의 배치 그대로다 —
        /// 0줄 신화, 1줄 스페셜, 2~5줄이 4성부터 1성까지의 기본 다이스다.
        /// </summary>
        private static DiceType GridDiceAt(int row, int column)
        {
            switch (row)
            {
                case 0:
                    return TowerLoadoutRules.MythicTypes[column];
                case 1:
                    return TowerLoadoutRules.SpecialTypes[column];
                default:
                    return TowerLoadoutRules.BaseTypes[column];
            }
        }

        /// <summary>2줄이 4성, 5줄이 1성이다. 신화·스페셜 줄은 성급이 없어 1 이다.</summary>
        private static int GridStarAt(int row)
        {
            if (row < 2)
                return TowerLoadoutRules.NonBaseStar;

            return TowerFormula.MaxBaseStar - (row - 2);
        }
    }
}
