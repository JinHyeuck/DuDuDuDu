using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using VContainer;
using OJ.Analytics;
using OJ.Battle;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
using OJ.Relic;
using OJ.Tutorial;

namespace OJ.Dice
{
    public class UIDiceSummonSystem : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 단 Awake 시점에는 아직 비어 있다 — 스코프는 씬의 모든 Awake 뒤에 빌드된다.
        [Inject] private IBattleRefs battle;

        /// <summary>
        /// 소환 풀에서 미보유를 빼기 위한 판정. 루트 스코프에 살아 배틀 스코프에서 해석된다.
        /// </summary>
        [Inject] private DiceOwnershipManager ownership;

        [Header("References")]
        public UIBoard board;
        public Button summonButton;
        public TMP_Text spText;

        [Header("SP Settings")]
        [SerializeField] private int summonsPerCostIncrease = 2;

        /// <summary>몇 번 소환하면 비용이 오르는가. 튜토리얼이 SP 를 정확히 맞추는 데 쓴다.</summary>
        public int SummonsPerCostIncrease => summonsPerCostIncrease;

        /// <summary>
        /// 이 판의 SP. <b>값은 <c>RunState</c> 가 들고 여기는 창문이다.</b>
        ///
        /// 예전에는 여기 <c>public int currentSP = 100</c> 이 있었다. 직렬화 필드라
        /// 프리팹에 박힌 100 이 판 시작값처럼 보였지만 실제로는
        /// <see cref="SetStageStartSp"/> 가 매번 덮었고, 되돌리기를 위해 이 컴포넌트가
        /// 자기만의 스냅샷 구조체를 따로 들고 있어야 했다.
        /// 지금은 <c>RunState.WaveSnapshot</c> 이 한꺼번에 덮는다.
        /// </summary>
        public int currentSP => battle != null && battle.BattlePoints != null
            ? battle.BattlePoints.Get(BattlePointType.SummonPoint)
            : 0;

        /// <summary>다음 소환 비용. <see cref="currentSP"/> 와 같은 이유로 여기 값이 없다.</summary>
        public int summonCost => battle != null && battle.Game != null
            ? battle.Game.Run.SummonCost
            : 0;

        [Header("Dice Settings")]
        public List<DiceType> deckTypes = new()
        {
            DiceType.Normal,
            DiceType.Fire,
            DiceType.Ice,
            DiceType.Poison,
            DiceType.Thunder,
            DiceType.Tornado,
            DiceType.Stun,
            DiceType.ArmorBreak,
            DiceType.Wind,
            DiceType.Time
        };

        private void OnDestroy()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(OnSummonButton);

            if (battle != null && battle.BattlePoints != null)
                battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
        }

        private void Start()
        {
            summonButton.onClick.AddListener(OnSummonButton);

            // SP 가 이 컴포넌트 밖(RunState)에 살게 되면서, 숫자가 바뀌는 것을 여기서
            // 항상 볼 수는 없게 됐다 — 현상금 지급과 되돌리기가 창구를 거쳐 고친다.
            // 그래서 구독한다. 안 걸면 되돌린 직후에 옛 숫자가 그대로 남는다.
            if (battle != null && battle.BattlePoints != null)
            {
                battle.BattlePoints.OnBattlePointChanged -= OnBattlePointChanged;
                battle.BattlePoints.OnBattlePointChanged += OnBattlePointChanged;
            }

            UpdateSPUI();
        }

        private void OnBattlePointChanged(BattlePointType battlePointType, int value)
        {
            if (battlePointType == BattlePointType.SummonPoint)
                UpdateSPUI();
        }

        public void SetStageStartSp(int startSp, int startSummonCost = 10)
        {
            if (battle == null || battle.Game == null)
                return;

            battle.Game.Run.SummonCost = Mathf.Max(1, startSummonCost);
            battle.Game.Run.SummonsSinceLastCostIncrease = 0;

            // 비용을 먼저 세우고 SP 를 창구로 넣는다. 창구가 이벤트를 내고 그것이
            // UpdateSPUI 를 부르는데, 비용이 아직 옛 값이면 한 프레임 동안
            // "새 SP / 옛 비용" 이 뜬다.
            battle.BattlePoints?.Set(BattlePointType.SummonPoint, Mathf.Max(0, startSp));

            UpdateSPUI();
        }

        /// <summary>
        /// SP 표시를 지금 상태로 맞춘다. <b>되돌리기가 <c>RunState</c> 를 직접 고친 뒤에도
        /// 부른다</b> — 그 경로는 창구를 거치지 않아 이벤트가 나지 않는다.
        /// </summary>
        public void UpdateSPUI()
        {
            if (spText == null)
                return;

            int sp = currentSP;
            int cost = summonCost;

            int free = battle != null && battle.Game != null ? battle.Game.Run.FreeSummonCharges : 0;
            if (free > 0)
            {
                // 공짜가 있으면 그것이 먼저 나간다 — 눈에 띄게 알린다.
                spText.text = free > 1 ? $"무료 x{free}" : "무료!";
                spText.color = new Color(0.45f, 1f, 0.55f, 1f);
                return;
            }

            spText.text = $"{sp} / {cost}";
            spText.color = sp >= cost ? Color.white : Color.red;
        }

        // SummonSnapshot · CaptureSnapshot · RestoreSnapshot 이 여기 있었다.
        // SP · 소환 비용 · 비용 상승 카운터가 RunState 로 가면서 RunState.WaveSnapshot 이
        // 셋을 한꺼번에 덮는다. 그쪽이 나은 이유는 <b>빠뜨림이 테스트에 잡힌다</b>는 것이다 —
        // RunStateSnapshotTests 가 리플렉션으로 전 필드를 훑으므로, 필드를 늘리고
        // 스냅샷에 넣는 것을 잊으면 필드 이름을 대고 실패한다. 손으로 쓴 구조체는
        // 늘어난 필드를 모른다.

        public void AddSP(int addsp)
        {
            if (addsp <= 0)
                return;

            battle.BattlePoints?.Add(BattlePointType.SummonPoint, addsp);
        }

        private void OnSummonButton()
        {
            // 무한의 탑에서는 소환이 없다(기획서 3.2 금지 항목). 버튼 자체는
            // GameManager.HideTowerForbiddenUI 가 이미 치웠지만, 규칙은 여기 있어야 한다 —
            // 화면을 끄는 것과 동작을 막는 것은 다른 일이고, 끄는 쪽만 고치면
            // 다른 경로로 들어온 호출이 조용히 통과한다.
            if (battle.Tower.IsActive)
                return;

            if (battle.Game.IsBoardLockedByWave)
                return;

            // 무료 소환권(자리를 사면 생긴다)이 있으면 SP 도 필요 없다.
            bool useFree = battle.Game.Run.FreeSummonCharges > 0;
            if (!useFree && currentSP < summonCost)
            {
                Debug.Log("SP 부족!");
                return;
            }

            // 튜토리얼 대본이 남아 있으면 그 칸·그 종류로 놓는다. 머지 단계의 딤드 구멍이
            // 칸 좌표를 미리 알아야 해서 랜덤에 맡길 수 없다. 대본이 없으면 평소대로다.
            bool scripted = battle.Tutorial.TryTakeScriptedSummon(out TutorialRunManager.ScriptedSummon script);

            int slotIndex = scripted && board.GetDice(script.SlotIndex) == null
                ? script.SlotIndex
                : GetRandomEmptySlot();
            if (slotIndex == -1)
            {
                Debug.Log("보드가 꽉 찼음!");
                return;
            }

            List<DiceType> summonable = new List<DiceType>();
            for (int i = 0; i < deckTypes.Count; i++)
            {
                if (!DiceMetaDataProvider.IsSummonable(deckTypes[i]))
                    continue;

                // 보유하지 않은 다이스는 소환도 안 된다. 지금 deckTypes 는 기본 5종뿐이라
                // 걸러지는 것이 없지만, 그건 <b>씬 데이터</b>라 언제든 특수가 들어갈 수 있다.
                if (ownership != null && !ownership.IsOwned(deckTypes[i]))
                    continue;

                summonable.Add(deckTypes[i]);
            }

            if (summonable.Count == 0)
                return;

            int spentCost = useFree ? 0 : summonCost;
            if (useFree)
                battle.Game.Run.FreeSummonCharges--;
            else
                battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, spentCost);

            // 튜토리얼 판에서는 유물이 소환에 끼지 않는다. 시작 SP 가 "정확히 여섯 번" 으로
            // 계산돼 있어서(TutorialStage.Build) 비용 상승을 건너뛰거나 쌍둥이가 나오면
            // "SP 를 다 쓰면 다음" 이 어긋난다.
            bool relicsApply = RelicManager.Instance != null && !battle.Tutorial.IsActive;

            // 공짜 소환은 비용도 올리지 않는다(자리를 산 값에 이미 들어 있다).
            bool skipCostIncrease = useFree || (relicsApply && RelicManager.Instance.ShouldSkipSummonCostIncrease());
            if (!skipCostIncrease)
            {
                RunState run = battle.Game.Run;
                run.SummonsSinceLastCostIncrease++;
                if (run.SummonsSinceLastCostIncrease >= Mathf.Max(1, summonsPerCostIncrease))
                {
                    run.SummonCost++;
                    run.SummonsSinceLastCostIncrease = 0;
                }
            }
            UpdateSPUI();

            DiceType type = scripted ? script.Type : summonable[Random.Range(0, summonable.Count)];
            // 성급: 확률업 레벨이 굴린 값과 유물이 정한 값 중 큰 쪽. 유물 효과를 덮어쓰지 않는다.
            int rolledStar = SummonStarRules.RollStar(ProbLevel, Random.Range(0, 100));
            int relicStar = relicsApply ? RelicManager.Instance.RollSummonStar() : 1;
            int star = Mathf.Max(rolledStar, relicStar);
            battle.DiceStars.OnDiceSpawn(type, star);
            board.SpawnDice(type, star, slotIndex);
            if (relicsApply)
                RelicManager.Instance.TrySpawnTwinDice(type);
            // GameManager 의 null 검사를 지운다. 소환 버튼은 BattleScene 에서만 눌리고
            // 그 안에서 battle.Game 이 null 이면 그것은 사고다 — 0 웨이브로 조용히
            // 기록해서 덮으면 안 된다. RelicManager·RunHistoryManager 는 루트 서비스라
            // 로비에서도 살아 있어야 하므로 ?. 를 그대로 둔다.
            RunHistoryManager.Instance?.RecordSummon(type, star, battle.Game.CurrentWaveIndex, spentCost, currentSP);
        }

        // ── 확률업 ────────────────────────────────────────────────────

        /// <summary>지금 확률업 레벨. 값은 <c>RunState</c> 가 들고 여기는 창문이다.</summary>
        public int ProbLevel => battle != null && battle.Game != null
            ? SummonStarRules.ClampLevel(battle.Game.Run.SummonProbLevel)
            : SummonStarRules.MinLevel;

        /// <summary>한 레벨 올리는 비용. 최대 레벨이면 -1.</summary>
        public int NextProbUpgradeCost => SummonStarRules.UpgradeCost(ProbLevel);

        /// <summary>
        /// SP 를 내고 확률업 레벨을 올린다. 최대면 거절. <b>재화는 검증한 뒤에만 낸다</b> —
        /// <c>TrySpend</c> 가 모자라면 아무것도 깎지 않는다.
        /// </summary>
        public bool TryUpgradeProbability()
        {
            if (battle.Tower.IsActive)
                return false;

            int cost = NextProbUpgradeCost;
            if (cost < 0)
                return false;

            if (!battle.BattlePoints.TrySpend(BattlePointType.SummonPoint, cost))
                return false;

            RunState run = battle.Game.Run;
            run.SummonProbLevel = SummonStarRules.ClampLevel(run.SummonProbLevel + 1);
            return true;
        }

        /// <summary>
        /// 확률업 레벨을 <b>무료로</b> 한 단계 올린다(웨이브 이벤트). 이미 최대면 false. SP 는 건드리지 않는다.
        /// </summary>
        public bool GrantProbLevel()
        {
            if (battle.Tower.IsActive || NextProbUpgradeCost < 0)
                return false;

            RunState run = battle.Game.Run;
            run.SummonProbLevel = SummonStarRules.ClampLevel(run.SummonProbLevel + 1);
            return true;
        }

        private int GetRandomEmptySlot()
        {
            List<int> emptySlots = new();
            int total = board.rows * board.cols;

            for (int i = 0; i < total; i++)
            {
                if (board.GetDice(i) == null && board.IsSlotUnlocked(i))
                    emptySlots.Add(i);
            }

            if (emptySlots.Count == 0) return -1;
            return emptySlots[Random.Range(0, emptySlots.Count)];
        }
    }
}
