using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Dice;
using OJ.Pinball;
using OJ.Point;
using OJ.Save;

namespace OJ.EventMinigame
{
    /// <summary>낚시 한 번의 결과. 화면이 그대로 그린다.</summary>
    public sealed class FishingCastResult
    {
        /// <summary>false 면 이벤트가 닫혔거나 배율이 잘못됐거나 낚싯대가 모자라 아무 일도 없었다.</summary>
        public bool Valid;

        public int Tier;
        public int Species;
        public int WeightClass;
        public FishTiming Timing;
        public int Multiplier;

        /// <summary>한 마리 점수(배율 전).</summary>
        public int FishScore;

        /// <summary>이번에 오른 점수(한 마리 점수 × 배율).</summary>
        public long CastScore;

        public long WeightGrams;

        /// <summary>이 어종의 최고 무게를 새로 썼는가.</summary>
        public bool NewRecord;

        /// <summary>이 무게 등급 이상이 나올 확률(‰). "상위 N%" 문구용.</summary>
        public int TopPermille;

        /// <summary>이번에 터진 떡밥 수.</summary>
        public int Baits;

        public int LuckStacks;
        public int RodLevelBefore;
        public int RodLevelAfter;
        public long TotalScore;

        /// <summary>이번에 새로 지급한 보상(마일스톤 + 초과 환산). 비어 있을 수 있다.</summary>
        public List<PointRewardEntry> Rewards = new List<PointRewardEntry>();
    }

    /// <summary>보물찾기 한 칸의 결과.</summary>
    public sealed class TreasureDigOutcome
    {
        /// <summary>false 면 이벤트가 닫혔거나, 이미 판 칸이거나, 곡괭이가 모자라 아무 일도 없었다.</summary>
        public bool Valid;

        public DigResult Dig;

        /// <summary>이 칸을 판 스테이지(클리어하면 다음 스테이지로 넘어가기 전 번호).</summary>
        public int Stage;

        public int PicksSpent;

        public bool StageCleared;

        /// <summary>클리어로 열쇠를 얻었는가.</summary>
        public bool KeyGranted;

        /// <summary>클리어로 상자가 열렸는가(번호, 아니면 0).</summary>
        public int ChestOpened;

        public List<PointRewardEntry> Rewards = new List<PointRewardEntry>();
    }

    /// <summary>래프팅 한 번의 결과.</summary>
    public sealed class RaftingSpinResult
    {
        public bool Valid;
        public int Multiplier;

        /// <summary>실제로 돈 횟수(무료 칸 연쇄 포함).</summary>
        public int Spins;

        /// <summary>돈 칸 번호를 차례로. 마지막만 무료가 아니다(연쇄가 상한에서 끊긴 때는 예외). 룰렛 연출이 쓴다.</summary>
        public List<int> Cells = new List<int>();

        /// <summary>이번에 늘어난 거리(m).</summary>
        public long Distance;

        public long TotalDistance;

        /// <summary>이번에 새로 고르기를 기다리게 된 3택1 줄 번호.</summary>
        public List<int> NewPendingChoices = new List<int>();

        public List<PointRewardEntry> Rewards = new List<PointRewardEntry>();
    }

    /// <summary>
    /// 이벤트 미니게임 3종(낚시·보물찾기·래프팅)의 상태와 진행. 규칙은 전부 static
    /// (<see cref="FishingRules"/>·<see cref="TreasureHuntRules"/>·<see cref="RaftingRules"/>)이고 여기는 상태·지급만 든다.
    ///
    /// <b>재화는 여기서만 쓴다.</b> <see cref="TrySpend"/> 가 <c>PointManager.TrySpend</c> 로 쓰고 곧바로
    /// <c>PromotionManager.NotifyEventCurrencySpent</c> 를 부른다 — 쓴 만큼이 승급 미션 B 진행도다.
    /// <c>OnPointSpent</c> 를 구독하지 않는 이유는 종료 환급도 소모로 잡히기 때문이다.
    ///
    /// <b>회차가 바뀌면 진행도를 비운다</b>(<see cref="EventMinigameRules.OccurrenceId"/>). 고르기를 기다리던 3택1 상자는
    /// 비우기 전에 첫 묶음으로 자동 지급한다. 낚시 도감 기록만 남는다.
    ///
    /// 난수와 시각은 밖에서 받는다(<see cref="Roll"/>, 각 메서드의 <c>utcNow</c>) — 헤드리스 테스트가 결정적으로 돌리려는 것이다.
    /// 세이브는 건드리지 않는다: 플레이 한 번마다 파일을 쓰면 연타가 끊긴다. 값은 <c>SaveService</c> 가 앱 정지·종료에서 쓴다.
    /// </summary>
    [Preserve]
    public sealed class EventMinigameManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static EventMinigameManager Instance { get; internal set; }

        /// <summary>진행도·보상·회차가 바뀌어 화면이 다시 그려져야 할 때.</summary>
        public event Action OnChanged;

        /// <summary>회차가 바뀌며 자동 지급한 선택 상자 보상. 앱을 연 사이에 회차가 넘어갔을 때 알림 화면이 구독한다.</summary>
        public event Action<IReadOnlyList<PointRewardEntry>> OnSettled;

        /// <summary>0~1 난수. 테스트가 갈아끼운다.</summary>
        public Func<float> Roll = () => UnityEngine.Random.value;

        /// <summary>핀볼 한 발의 티켓 수. 비어 있으면 <c>PinballManager</c> 를 읽는다(없으면 1).</summary>
        public Func<int> PinballTicketCostSource;

        private readonly PointManager points;
        private readonly PromotionManager promotion;

        private int occurrenceId = int.MinValue;

        // 낚시
        private long fishingScore;
        private int luckStacks;
        private long luckUntilTicks;
        private int fishingClaimed;
        private readonly Dictionary<int, int> fishingRecords = new Dictionary<int, int>();

        // 보물찾기
        private int treasureStage = 1;
        private bool hasBoard;
        private int boardSeed;
        private readonly HashSet<int> revealedCells = new HashSet<int>();
        private TreasureBoard board;
        private bool[] revealed;

        // 래프팅
        private long raftingDistance;
        private int raftingClaimed;
        private readonly List<int> raftingPending = new List<int>();

        public EventMinigameManager(PointManager points, PromotionManager promotion)
        {
            this.points = points;
            this.promotion = promotion;
        }

        // ── 공통 ───────────────────────────────────────────────────────

        public bool IsOpen(EventKind kind, DateTime utcNow)
        {
            return EventCurrencyRules.IsEventOpen(utcNow)
                   && EventMinigameRules.KindOf(EventCurrencyRules.DayIndex(utcNow)) == kind;
        }

        /// <summary>지금 열린 이벤트.</summary>
        public EventKind OpenKind(DateTime utcNow)
        {
            return EventMinigameRules.KindOf(EventCurrencyRules.DayIndex(utcNow));
        }

        /// <summary>
        /// 회차가 바뀌었으면 지난 회차를 정산하고 진행도를 비운다. 앱 시작과 화면을 열 때 부른다 —
        /// 플레이 메서드도 처음에 부르므로 빠뜨려도 틀린 회차로 이어지지는 않는다.
        /// </summary>
        public void Tick(DateTime utcNow)
        {
            int current = EventMinigameRules.OccurrenceId(EventCurrencyRules.DayIndex(utcNow));
            if (current == occurrenceId)
                return;

            List<PointRewardEntry> settled = SettlePending();
            ResetProgress();
            occurrenceId = current;

            OnChanged?.Invoke();
            if (settled.Count > 0)
                OnSettled?.Invoke(settled);
        }

        private List<PointRewardEntry> SettlePending()
        {
            var rewards = new List<PointRewardEntry>();
            for (int i = 0; i < raftingPending.Count; i++)
            {
                int index = raftingPending[i];
                if (index >= 0 && index < EventRewardTables.Rafting.Length)
                    rewards.AddRange(Grant(EventRewardTables.Rafting[index].Default));
            }

            raftingPending.Clear();
            return PointRewardUtility.MergeRewards(rewards);
        }

        private void ResetProgress()
        {
            fishingScore = 0;
            luckStacks = 0;
            luckUntilTicks = 0;
            fishingClaimed = 0;

            treasureStage = 1;
            hasBoard = false;
            boardSeed = 0;
            revealedCells.Clear();
            board = null;
            revealed = null;

            raftingDistance = 0;
            raftingClaimed = 0;
            raftingPending.Clear();
        }

        private bool TrySpend(EventKind kind, int amount, DateTime utcNow)
        {
            if (points == null || amount <= 0)
                return false;

            Tick(utcNow);
            if (!IsOpen(kind, utcNow))
                return false;

            PointType currency = EventMinigameRules.CurrencyOf(kind);
            if (!points.TrySpend(currency, amount, false))
                return false;

            if (promotion != null)
                promotion.NotifyEventCurrencySpent(currency, amount, utcNow);

            return true;
        }

        private int TicketCost()
        {
            if (PinballTicketCostSource != null)
                return PinballTicketCostSource();

            return PinballManager.Instance != null ? PinballManager.Instance.TicketCost : 1;
        }

        /// <summary>묶음을 지급하고 지급 목록을 돌려준다.</summary>
        private List<PointRewardEntry> Grant(EventBundle bundle)
        {
            List<PointRewardEntry> rewards = bundle.ToRewards(TicketCost());
            for (int i = 0; i < rewards.Count; i++)
                points.Add(rewards[i].PointType, rewards[i].Amount, false);

            return rewards;
        }

        private void GrantOverflow(EventKind kind, long before, long after, List<PointRewardEntry> sink)
        {
            long shots = EventRewardTables.OverflowShots(kind, after) - EventRewardTables.OverflowShots(kind, before);
            if (shots <= 0)
                return;

            long tickets = shots * TicketCost();
            int amount = tickets > int.MaxValue ? int.MaxValue : (int)tickets;
            points.Add(PointType.PinballTicket, amount, false);
            sink.Add(new PointRewardEntry(PointType.PinballTicket, amount));
        }

        private int RollInt()
        {
            return (int)(Roll() * 2147483647f);
        }

        // ── 낚시 ───────────────────────────────────────────────────────

        public long FishingScore
        {
            get { return fishingScore; }
        }

        public int FishingRodLevel
        {
            get { return FishingRules.RodLevelFromScore(fishingScore); }
        }

        public int FishingClaimedCount
        {
            get { return fishingClaimed; }
        }

        /// <summary>지금 켜져 있는 행운 단계. 시한이 지났으면 0.</summary>
        public int GetFishingLuck(DateTime utcNow)
        {
            return luckUntilTicks > utcNow.Ticks ? luckStacks : 0;
        }

        public TimeSpan FishingLuckRemaining(DateTime utcNow)
        {
            return luckUntilTicks > utcNow.Ticks ? TimeSpan.FromTicks(luckUntilTicks - utcNow.Ticks) : TimeSpan.Zero;
        }

        /// <summary>어종의 최고 무게(g). 아직 못 낚았으면 0.</summary>
        public int GetFishingRecord(int speciesId)
        {
            return fishingRecords.TryGetValue(speciesId, out int grams) ? grams : 0;
        }

        /// <summary>
        /// 한 번 던진다. 낚싯대를 <paramref name="multiplier"/> 개 쓰고, 한 마리를 낚아 점수가 그 배율이 된다.
        /// 타이밍 판정은 화면이 정해서 넘긴다.
        /// </summary>
        public FishingCastResult Cast(int multiplier, FishTiming timing, DateTime utcNow)
        {
            var result = new FishingCastResult { Multiplier = multiplier, Timing = timing };

            if (Array.IndexOf(FishingRules.Multipliers, multiplier) < 0)
                return result;

            if (!TrySpend(EventKind.Fishing, multiplier, utcNow))
                return result;

            int rodBefore = FishingRodLevel;
            int luck = GetFishingLuck(utcNow);
            int effective = FishingRules.EffectiveLevel(rodBefore, luck);

            int tier = FishingRules.DrawTier(effective, Roll());
            int species = FishingRules.DrawSpecies(Roll());
            int weightClass = FishingRules.DrawWeightClass(Roll());
            int fishScore = FishingRules.FishScore(tier, weightClass, timing);
            long cast = FishingRules.CastScore(fishScore, multiplier);
            long grams = FishingRules.WeightGrams(tier, species, weightClass, Roll());

            int baits = FishingRules.BaitSuccesses(multiplier, Roll);
            if (baits > 0)
            {
                luckStacks = FishingRules.AddLuck(luck, baits);
                luckUntilTicks = utcNow.Ticks + TimeSpan.FromSeconds(FishingRules.LuckSeconds).Ticks;
            }

            int speciesId = FishingRules.SpeciesId(tier, species);
            int best = GetFishingRecord(speciesId);
            int gramsInt = grams > int.MaxValue ? int.MaxValue : (int)grams;
            bool newRecord = gramsInt > best;
            if (newRecord)
                fishingRecords[speciesId] = gramsInt;

            long before = fishingScore;
            fishingScore += cast;

            // 마일스톤: 새로 넘은 줄을 앞에서부터 전부 준다(한 번에 여러 줄을 넘을 수 있다).
            var rewards = new List<PointRewardEntry>();
            EventMilestone[] table = EventRewardTables.Fishing;
            while (fishingClaimed < table.Length && fishingScore >= table[fishingClaimed].Threshold)
            {
                rewards.AddRange(Grant(table[fishingClaimed].Default));
                fishingClaimed++;
            }

            GrantOverflow(EventKind.Fishing, before, fishingScore, rewards);

            result.Valid = true;
            result.Tier = tier;
            result.Species = species;
            result.WeightClass = weightClass;
            result.FishScore = fishScore;
            result.CastScore = cast;
            result.WeightGrams = grams;
            result.NewRecord = newRecord;
            result.TopPermille = FishingRules.TopPermille(weightClass);
            result.Baits = baits;
            result.LuckStacks = GetFishingLuck(utcNow);
            result.RodLevelBefore = rodBefore;
            result.RodLevelAfter = FishingRodLevel;
            result.TotalScore = fishingScore;
            result.Rewards = PointRewardUtility.MergeRewards(rewards);

            OnChanged?.Invoke();
            return result;
        }

        // ── 보물찾기 ───────────────────────────────────────────────────

        /// <summary>지금 스테이지(1부터).</summary>
        public int TreasureStage
        {
            get { return treasureStage; }
        }

        /// <summary>지금까지 얻은 열쇠 수(3스테이지를 깰 때마다 1개).</summary>
        public int TreasureKeys
        {
            get { return (treasureStage - 1) / TreasureHuntRules.StagesPerKey; }
        }

        /// <summary>지금 판. 없으면 만든다. <paramref name="cells"/> 는 판 크기의 "이미 판 칸" 표(복사본이 아니라 안쪽 상태다 — 고치지 말 것).</summary>
        public void GetTreasureBoard(DateTime utcNow, out TreasureBoard currentBoard, out bool[] cells)
        {
            Tick(utcNow);
            EnsureBoard();
            currentBoard = board;
            cells = revealed;
        }

        private void EnsureBoard()
        {
            if (!hasBoard)
            {
                hasBoard = true;
                boardSeed = RollInt();
                revealedCells.Clear();
                board = null;
            }

            if (board != null)
                return;

            board = TreasureHuntRules.Generate(TreasureHuntRules.TierOfStage(treasureStage), boardSeed);
            revealed = new bool[board.Width * board.Height];
            foreach (int cell in revealedCells)
            {
                if (cell >= 0 && cell < revealed.Length)
                    revealed[cell] = true;
            }
        }

        /// <summary>
        /// 칸 하나를 판다. 곡괭이를 <see cref="TreasureHuntRules.DigCost"/> 개 쓰고, 보물을 다 찾으면 스테이지 보상이 나가며
        /// 다음 스테이지 판이 열린다. 이미 판 칸이나 범위 밖이면 곡괭이를 쓰지 않는다.
        /// </summary>
        public TreasureDigOutcome Dig(int x, int y, DateTime utcNow)
        {
            var outcome = new TreasureDigOutcome { Stage = treasureStage };

            Tick(utcNow);
            if (!IsOpen(EventKind.TreasureHunt, utcNow))
                return outcome;

            EnsureBoard();
            outcome.Stage = treasureStage;

            if (x < 0 || y < 0 || x >= board.Width || y >= board.Height || revealed[y * board.Width + x])
                return outcome;

            int cost = TreasureHuntRules.DigCost(treasureStage);
            if (!TrySpend(EventKind.TreasureHunt, cost, utcNow))
                return outcome;

            DigResult dig = TreasureHuntRules.Dig(board, revealed, x, y);
            revealedCells.Add(y * board.Width + x);

            outcome.Valid = true;
            outcome.Dig = dig;
            outcome.PicksSpent = cost;

            if (dig.StageCleared)
            {
                int clearedStage = treasureStage;
                var rewards = new List<PointRewardEntry>();
                rewards.AddRange(Grant(TreasureHuntRules.StageReward(clearedStage)));

                int chest = TreasureHuntRules.ChestIndexAt(clearedStage);
                if (chest > 0)
                    rewards.AddRange(Grant(TreasureHuntRules.ChestReward(chest)));

                outcome.StageCleared = true;
                outcome.KeyGranted = TreasureHuntRules.GrantsKey(clearedStage);
                outcome.ChestOpened = chest;
                outcome.Rewards = PointRewardUtility.MergeRewards(rewards);

                // 다음 스테이지: 판은 처음 열 때 만든다.
                treasureStage++;
                hasBoard = false;
                revealedCells.Clear();
                board = null;
                revealed = null;
            }

            OnChanged?.Invoke();
            return outcome;
        }

        // ── 래프팅 ─────────────────────────────────────────────────────

        public long RaftingDistance
        {
            get { return raftingDistance; }
        }

        public int RaftingClaimedCount
        {
            get { return raftingClaimed; }
        }

        /// <summary>고르기를 기다리는 3택1 줄 번호(복사본).</summary>
        public List<int> GetRaftingPending()
        {
            return new List<int>(raftingPending);
        }

        /// <summary>보트 <paramref name="multiplier"/> 개로 룰렛을 돌린다. 무료 칸이 나오면 보트 없이 이어 돈다.</summary>
        public RaftingSpinResult Spin(int multiplier, DateTime utcNow)
        {
            var result = new RaftingSpinResult { Multiplier = multiplier };

            if (Array.IndexOf(RaftingRules.Multipliers, multiplier) < 0)
                return result;

            if (!TrySpend(EventKind.Rafting, multiplier, utcNow))
                return result;

            int spins;
            long distance = RaftingRules.SpinChain(multiplier, Roll, result.Cells, out spins);

            long before = raftingDistance;
            raftingDistance += distance;

            var rewards = new List<PointRewardEntry>();
            EventMilestone[] table = EventRewardTables.Rafting;
            while (raftingClaimed < table.Length && raftingDistance >= table[raftingClaimed].Threshold)
            {
                if (table[raftingClaimed].IsChoice)
                {
                    raftingPending.Add(raftingClaimed);
                    result.NewPendingChoices.Add(raftingClaimed);
                }
                else
                {
                    rewards.AddRange(Grant(table[raftingClaimed].Default));
                }

                raftingClaimed++;
            }

            GrantOverflow(EventKind.Rafting, before, raftingDistance, rewards);

            result.Valid = true;
            result.Spins = spins;
            result.Distance = distance;
            result.TotalDistance = raftingDistance;
            result.Rewards = PointRewardUtility.MergeRewards(rewards);

            OnChanged?.Invoke();
            return result;
        }

        /// <summary>고르기를 기다리던 3택1 상자에서 하나를 받는다. 받은 지급 목록을 돌려주고, 실패하면 null.</summary>
        public List<PointRewardEntry> TryClaimChoice(int milestoneIndex, int option, DateTime utcNow)
        {
            Tick(utcNow);

            int slot = raftingPending.IndexOf(milestoneIndex);
            if (slot < 0 || milestoneIndex >= EventRewardTables.Rafting.Length)
                return null;

            EventMilestone milestone = EventRewardTables.Rafting[milestoneIndex];
            if (option < 0 || option >= milestone.Options.Length)
                return null;

            raftingPending.RemoveAt(slot);
            List<PointRewardEntry> rewards = Grant(milestone.Options[option]);

            OnChanged?.Invoke();
            return rewards;
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState saveState)
        {
            EventMinigameSave target = saveState.EventMinigames;

            target.OccurrenceId = occurrenceId;

            target.FishingScore = fishingScore;
            target.FishingLuckStacks = luckStacks;
            target.FishingLuckUntilUtcTicks = luckUntilTicks;
            target.FishingClaimed = fishingClaimed;
            target.FishingRecords.Clear();
            foreach (KeyValuePair<int, int> pair in fishingRecords)
            {
                if (pair.Value > 0)
                    target.FishingRecords[pair.Key.ToString()] = pair.Value;
            }

            target.TreasureStage = treasureStage;
            target.TreasureHasBoard = hasBoard;
            target.TreasureBoardSeed = boardSeed;
            target.TreasureRevealed.Clear();
            if (hasBoard)
            {
                target.TreasureRevealed.AddRange(revealedCells);
                target.TreasureRevealed.Sort();
            }

            target.RaftingDistance = raftingDistance;
            target.RaftingClaimed = raftingClaimed;
            target.RaftingPending.Clear();
            target.RaftingPending.AddRange(raftingPending);
        }

        public void ReadFrom(SaveState saveState)
        {
            EventMinigameSave source = saveState.EventMinigames;

            ResetProgress();
            fishingRecords.Clear();
            occurrenceId = source.OccurrenceId;

            fishingScore = source.FishingScore < 0 ? 0 : source.FishingScore;
            luckStacks = source.FishingLuckStacks < 0 ? 0 : (source.FishingLuckStacks > FishingRules.MaxLuckStacks ? FishingRules.MaxLuckStacks : source.FishingLuckStacks);
            luckUntilTicks = source.FishingLuckUntilUtcTicks < 0 ? 0 : source.FishingLuckUntilUtcTicks;

            int fishingRows = EventRewardTables.Fishing.Length;
            fishingClaimed = source.FishingClaimed < 0 ? 0 : (source.FishingClaimed > fishingRows ? fishingRows : source.FishingClaimed);

            foreach (KeyValuePair<string, int> pair in source.FishingRecords)
            {
                // 모르는 어종 id 와 0 이하 무게는 손상이다 — 그대로 들이면 다음 저장에서 굳는다.
                if (!int.TryParse(pair.Key, out int id) || !IsKnownSpecies(id) || pair.Value <= 0)
                    continue;

                fishingRecords[id] = pair.Value;
            }

            treasureStage = source.TreasureStage < 1 ? 1 : source.TreasureStage;
            hasBoard = source.TreasureHasBoard;
            boardSeed = source.TreasureBoardSeed;
            if (hasBoard)
            {
                // 판은 시드와 단계로 다시 만든다. 칸 번호가 판 밖이면 그 칸은 버린다.
                TreasureTier tier = TreasureHuntRules.TierData(treasureStage);
                int cells = tier.Width * tier.Height;
                foreach (int cell in source.TreasureRevealed)
                {
                    if (cell >= 0 && cell < cells)
                        revealedCells.Add(cell);
                }
            }

            raftingDistance = source.RaftingDistance < 0 ? 0 : source.RaftingDistance;
            int raftingRows = EventRewardTables.Rafting.Length;
            raftingClaimed = source.RaftingClaimed < 0 ? 0 : (source.RaftingClaimed > raftingRows ? raftingRows : source.RaftingClaimed);

            foreach (int index in source.RaftingPending)
            {
                // 이미 처리한 줄 안에 있고 3택1 인 것만 기다림으로 인정한다.
                if (index >= 0 && index < raftingClaimed && EventRewardTables.Rafting[index].IsChoice && !raftingPending.Contains(index))
                    raftingPending.Add(index);
            }
        }

        private static bool IsKnownSpecies(int id)
        {
            int tier = id / 10;
            int species = id % 10;
            return id >= 0 && tier < FishingRules.TierCount && species < FishingRules.SpeciesPerTier;
        }
    }
}
