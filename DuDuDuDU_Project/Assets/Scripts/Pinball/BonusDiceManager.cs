using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Point;
using OJ.Rewind;
using OJ.Save;

namespace OJ.Pinball
{
    /// <summary>공 한 발의 재생 정보. 결과는 이미 정해졌고 이것은 "어떻게 굴러가는가"뿐이다.</summary>
    public readonly struct BonusShot
    {
        public readonly int Slot;
        public readonly uint Seed;

        public BonusShot(int slot, uint seed)
        {
            Slot = slot;
            Seed = seed;
        }
    }

    /// <summary>
    /// 궤적을 뽑아 주는 쪽. 보상판(<c>PinballBoard</c>·<c>SeedTable</c>)을 아는 화면이 구현한다.
    ///
    /// <b>매니저가 판을 직접 알지 않게 하려고 끼운 포트다.</b> <c>PinballManager</c> 가
    /// <c>declaredProbability</c> 를 인자로 받는 것과 같은 이유다 — 매니저가 씬 오브젝트를
    /// 모르면 규칙이 어디서든 돈다.
    /// </summary>
    public interface IBonusShotSource
    {
        /// <summary>
        /// <paramref name="ballCount"/> 발의 궤적을 뽑는다.
        /// <paramref name="shots"/> 에는 재생할 순서대로, <paramref name="hitsByTag"/> 에는
        /// <b>그 궤적들이 만들어 낼 특수 핀 적중 수</b>를 태그별로 채운다.
        ///
        /// 여기서 채워진 적중 수가 곧 지급의 근거다 — 재생은 그 뒤로 순수한 연출이다.
        /// </summary>
        /// <returns>한 발도 못 뽑았으면 false. 그 경우 발사 자체가 취소된다.</returns>
        bool TryDraw(int ballCount, List<BonusShot> shots, Dictionary<int, int> hitsByTag);
    }

    /// <summary>
    /// 핀볼 보상 라운드의 실행부. 주사위·족보·특수 핀 카운트·지급·저장을 한 자리에서 한다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> <c>GameContainer</c> 가 만들고 <c>SaveService</c> 가 저장한다.
    ///
    /// <b>이 라운드가 어떻게 굴러가는지.</b> 사이클마다 주사위를 굴려 족보를 만들고,
    /// 족보가 <i>발사되는 공의 개수</i>를 정한다. 공은 특수 핀만 있는 보상판으로 나가고,
    /// 핀마다 정해진 횟수를 채우면 보상이 나온다. 아래 칸에는 경품이 없다 — 떨어지는 공을
    /// 받는 연출용 받이일 뿐이다.
    ///
    /// <b>샷 순간에 전부 확정한다.</b> <see cref="IBonusShotSource"/> 가 시드를 뽑는 순간
    /// 그 공들이 어느 핀을 몇 번 맞을지 이미 정해져 있다(<c>SeedTable.hitMask</c>).
    /// 그래서 카운트 반영·지급·저장을 그 자리에서 끝내고, 재생은 순수한 연출로 남긴다 —
    /// 도중에 앱이 죽거나 화면을 나가도 <b>보상과 진행도가 갈라지지 않는다.</b>
    ///
    /// <b>주사위는 저장하지 않는다.</b> 굴림은 아직 아무것도 지급하지 않은 상태라,
    /// 앱이 죽으면 그 사이클의 주사위만 다시 굴리면 된다. 저장은 재화가 실제로 움직이는
    /// 순간(샷)과 기회가 늘어나는 순간(광고)에만 한다.
    ///
    /// <b><see cref="PinballManager"/> 의 게이지를 절대 건드리지 않는다.</b> 건드리면
    /// 보상 라운드가 또 보상 라운드를 여는 무한 루프가 된다.
    /// </summary>
    [Preserve]
    public sealed class BonusDiceManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static BonusDiceManager Instance { get; internal set; }

        /// <summary>세이브 키의 접두사. <c>tag:1</c> 꼴이 된다.</summary>
        private const string TagKeyPrefix = "tag:";

        /// <summary>특수 핀 태그 → 이번 라운드 누적 적중 수. 0 인 태그는 넣지 않는다.</summary>
        private readonly Dictionary<int, int> pinCounts = new Dictionary<int, int>();

        private readonly Dictionary<int, int> scratchHits = new Dictionary<int, int>();

        private int[] dice = Array.Empty<int>();
        private bool[] locks = Array.Empty<bool>();
        private int rerollsLeft;

        /// <summary>보상 배율. 1 이상. 센터핀 게이지를 한 번 더 채울 때마다 오른다.</summary>
        private int rewardMultiplier = 1;
        private int remainingCycles;
        private bool inProgress;

        /// <summary>
        /// 로드 직후에는 true. 주사위를 저장하지 않으므로 이어하기는 새로 굴려야 하는데,
        /// <b>로드 시점에 수치표를 건드리면 안 된다</b> — <c>StaticResource</c> 가 아직 안 서 있어
        /// Provider 가 거짓 LogError 를 찍는다. 그래서 첫 조회까지 미룬다.
        /// </summary>
        private bool cycleDirty;

        /// <summary>주사위·리롤·사이클·카운트 중 무엇이든 바뀌었을 때.</summary>
        public event Action OnChanged;

        /// <summary>라운드가 끝났을 때. 화면이 결과를 띄우고 핀볼판으로 되돌아간다.</summary>
        public event Action OnRoundEnded;

        private static BonusDiceDatabase Database => BonusDiceDatabaseProvider.GetDatabase();

        // ──────────────────────────────────────────────── 조회

        public bool InProgress => inProgress;
        public int RemainingCycles => remainingCycles;

        /// <summary>이 라운드의 보상이 몇 배인가. 화면이 안내 문구에 그대로 쓴다.</summary>
        public int RewardMultiplier => rewardMultiplier;

        public int RerollsLeft
        {
            get { EnsureCycle(); return rerollsLeft; }
        }

        public IReadOnlyList<int> Dice
        {
            get { EnsureCycle(); return dice; }
        }

        public bool IsLocked(int index)
        {
            EnsureCycle();
            return index >= 0 && index < locks.Length && locks[index];
        }

        /// <summary>지금 주사위가 만든 족보.</summary>
        public DiceHand CurrentHand
        {
            get { EnsureCycle(); return BonusDiceRules.EvaluateHand(dice); }
        }

        /// <summary>지금 쏘면 나갈 공의 개수.</summary>
        public int CurrentBallCount => BonusDiceRules.BallCountFor(CurrentHand, Database.ballTable);

        /// <summary>경품표에 걸린 특수 핀 전부. 화면이 진행도를 나열하는 데 쓴다.</summary>
        public IReadOnlyList<BonusPinReward> Pins => Database.pinRewards;

        public int GetPinCount(int tag)
        {
            return pinCounts.TryGetValue(tag, out int value) ? value : 0;
        }

        public bool IsPinComplete(int tag)
        {
            BonusPinReward pin = Database.GetPin(tag);
            return pin != null && BonusDiceRules.IsComplete(GetPinCount(tag), pin.requiredHits);
        }

        /// <summary>
        /// 아직 못 채운 핀이 남았는가. <b>광고 버튼을 띄우는 조건이자 라운드가 끝나는 조건</b>이다 —
        /// 전부 채우면 더 줄 것이 없으므로 "무한 재도전"의 상한이 여기서 자연스럽게 걸린다.
        /// </summary>
        public bool HasUnclaimedPins
        {
            get
            {
                IReadOnlyList<BonusPinReward> pins = Database.pinRewards;
                if (pins == null)
                    return false;

                for (int i = 0; i < pins.Count; i++)
                {
                    BonusPinReward pin = pins[i];
                    if (pin == null || pin.requiredHits <= 0)
                        continue;   // 보상이 안 걸린 핀은 "채울 것"에 들지 않는다

                    if (!BonusDiceRules.IsComplete(GetPinCount(pin.tag), pin.requiredHits))
                        return true;
                }
                return false;
            }
        }

        public bool CanRoll
        {
            get
            {
                EnsureCycle();
                return inProgress
                    && remainingCycles > 0
                    && rerollsLeft > 0
                    && BonusDiceRules.UnlockedCount(dice.Length, locks) > 0;
            }
        }

        public bool CanShoot => inProgress && remainingCycles > 0;

        /// <summary>
        /// 광고로 기회를 더 받을 수 있는가. 사이클을 다 쓰고도 못 채운 핀이 남았을 때만이다.
        ///
        /// <b>광고가 없으면 false 다.</b> 규약상 <c>IsAvailable</c> 이 false 면 화면은 버튼을
        /// 아예 그리지 않는다 — 눌리지 않는 버튼은 "광고가 안 나온다"는 고장으로 읽힌다.
        /// </summary>
        public bool CanWatchAd
        {
            get
            {
                if (!inProgress || remainingCycles > 0 || !HasUnclaimedPins)
                    return false;

                IRewardedAdService ads = OJ.DI.GameContainer.RewardedAds;
                return ads != null && ads.IsAvailable;
            }
        }

        /// <summary>광고제거권으로 남은 보상을 즉시 받을 수 있는가.</summary>
        public bool CanClaimAll
        {
            get
            {
                if (!inProgress || !HasUnclaimedPins)
                    return false;

                EntitlementManager entitlements = EntitlementManager.Instance;
                return entitlements != null && entitlements.AdFree;
            }
        }

        // ──────────────────────────────────────────────── 라운드

        /// <summary>
        /// 라운드를 연다. 핀볼 게이지가 다 찼을 때 부른다.
        /// 이미 열려 있으면 아무것도 하지 않는다 — 이어하기 중에 덮어써서 진행도를 날리지 않기 위해서다.
        /// </summary>
        public bool StartRound()
        {
            if (inProgress)
                return false;

            inProgress = true;
            remainingCycles = Mathf.Max(1, Database.cyclesPerRound);
            rewardMultiplier = 1;
            pinCounts.Clear();
            ResetCycle();

            Save();
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 보상 배율을 한 단계 올린다. 센터핀 게이지가 <b>또</b> 찼을 때 부른다.
        ///
        /// <b>라운드를 하나 더 열지 않는 이유.</b> 같은 판을 두 번 하게 만들면 칭찬이 아니라
        /// 숙제가 된다. 대신 한 판의 보상을 곱해 "크게 쏜 만큼 크게 받는다"로 갚는다.
        ///
        /// <b>즉시 저장한다.</b> 배율은 이미 맞힌 것에 대한 권리라, 굳히기 전에 앱이 죽으면
        /// 센터핀을 두 번 채운 보람이 사라진다.
        /// </summary>
        public bool AddRewardMultiplier()
        {
            if (!inProgress)
                return false;

            rewardMultiplier++;
            Save();

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>잠기지 않은 주사위를 다시 굴린다. <b>저장하지 않는다.</b></summary>
        public bool Roll()
        {
            if (!CanRoll)
                return false;

            for (int i = 0; i < dice.Length; i++)
            {
                if (!locks[i])
                    dice[i] = UnityEngine.Random.Range(1, BonusDiceRules.MaxPip + 1);
            }

            rerollsLeft--;
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>주사위 하나를 고정하거나 푼다. <b>저장하지 않는다.</b></summary>
        public void SetLock(int index, bool locked)
        {
            EnsureCycle();
            if (index < 0 || index >= locks.Length || locks[index] == locked)
                return;

            locks[index] = locked;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 지금 족보로 공을 쏜다. <b>여기서 결과·지급·저장이 전부 끝난다.</b>
        ///
        /// 궤적을 한 발도 못 뽑으면 <b>사이클을 소모하지 않고</b> 돌아간다 —
        /// 판이나 시드 테이블이 어긋난 상태에서 유저의 기회만 사라지면 안 된다.
        /// </summary>
        /// <param name="shots">재생할 궤적이 여기 채워진다. 호출부가 그대로 재생에 넘긴다.</param>
        /// <returns>이번 샷으로 지급한 보상. 실패하면 null 이고 아무것도 건드리지 않는다.</returns>
        public IReadOnlyList<PointRewardEntry> TryShoot(
            IBonusShotSource source, List<BonusShot> shots, out DiceHand hand, out int ballCount)
        {
            hand = DiceHand.None;
            ballCount = 0;

            if (!CanShoot || source == null || shots == null)
                return null;

            hand = CurrentHand;
            ballCount = CurrentBallCount;

            shots.Clear();
            scratchHits.Clear();

            if (!source.TryDraw(ballCount, shots, scratchHits) || shots.Count == 0)
            {
                Debug.LogError(
                    "[보상라운드] 궤적을 한 발도 뽑지 못해 발사를 취소한다. 사이클은 그대로 남는다. " +
                    "보상판의 SeedTable 이 판과 맞는지(IsStale) 확인할 것.");
                return null;
            }

            var rewards = new List<PointRewardEntry>();
            foreach (KeyValuePair<int, int> pair in scratchHits)
                ApplyHits(pair.Key, pair.Value, rewards);

            remainingCycles--;
            GrantWithoutSave(rewards);

            // 다 채웠으면 더 줄 것이 없다. 사이클이 남았어도 여기서 끝낸다.
            bool finished = !HasUnclaimedPins;
            if (finished)
                EndRoundInternal();
            else
                ResetCycle();

            Save();     // 지급과 카운트와 남은 사이클이 한 번의 쓰기에 같이 굳는다

            OnChanged?.Invoke();
            if (finished)
                OnRoundEnded?.Invoke();

            return rewards;
        }

        /// <summary>
        /// 광고를 끝까지 본 뒤. 사이클을 하나 더 주고 <b>그 자리에서 저장한다</b> —
        /// 굳히기 전에 앱이 죽으면 "광고는 봤는데 기회가 없다"가 된다.
        /// </summary>
        public bool GrantAdRetry()
        {
            if (!inProgress || !HasUnclaimedPins)
                return false;

            remainingCycles++;
            ResetCycle();
            Save();

            OnChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 광고제거권으로 남은 보상을 한 번에 받는다.
        ///
        /// <b>이 금액은 "광고를 무한히 봤을 때의 종착점"과 반드시 같아야 한다.</b>
        /// 못 채운 핀을 전부 채운 것으로 치고 그 보상을 그대로 주는 것이 그 뜻이다 —
        /// 여기서 더 주면 광고제거권이 페이투윈이 되고, 덜 주면 산 사람이 손해를 본다.
        /// </summary>
        /// <returns>지급한 보상. 받을 것이 없으면 null.</returns>
        public IReadOnlyList<PointRewardEntry> ClaimAllRemaining()
        {
            if (!inProgress)
                return null;

            IReadOnlyList<BonusPinReward> pins = Database.pinRewards;
            if (pins == null)
                return null;

            var rewards = new List<PointRewardEntry>();
            for (int i = 0; i < pins.Count; i++)
            {
                BonusPinReward pin = pins[i];
                if (pin == null || pin.requiredHits <= 0)
                    continue;

                if (BonusDiceRules.IsComplete(GetPinCount(pin.tag), pin.requiredHits))
                    continue;

                SetPinCount(pin.tag, pin.requiredHits);
                AppendRewards(pin, rewards);
            }

            if (rewards.Count == 0)
                return null;

            GrantWithoutSave(rewards);
            EndRoundInternal();
            Save();

            OnChanged?.Invoke();
            OnRoundEnded?.Invoke();
            return rewards;
        }

        /// <summary>
        /// 라운드를 그냥 닫는다. 남은 보상은 포기된다 —
        /// 화면이 "정말 나가겠는가"를 물은 뒤에만 부를 것.
        /// </summary>
        public void EndRound()
        {
            if (!inProgress)
                return;

            EndRoundInternal();
            Save();

            OnChanged?.Invoke();
            OnRoundEnded?.Invoke();
        }

        // ──────────────────────────────────────────────── 내부

        private void ApplyHits(int tag, int hits, List<PointRewardEntry> into)
        {
            if (hits <= 0)
                return;

            BonusPinReward pin = Database.GetPin(tag);
            if (pin == null)
            {
                // 판에는 있는데 표에 없는 태그다. 세어 봐야 쓸 데가 없으므로 올리지 않는다 —
                // 나중에 보상을 붙이면 그때부터 0 에서 시작한다.
                return;
            }

            int next = BonusDiceRules.AdvanceCount(
                GetPinCount(tag), pin.requiredHits, hits, out bool justCompleted);

            SetPinCount(tag, next);

            if (justCompleted)
                AppendRewards(pin, into);
        }

        /// <summary>
        /// 그 핀의 보상을 <see cref="RewardMultiplier"/> 만큼 곱해 담는다.
        ///
        /// <c>PinballRules.ScaleAmount</c> 를 쓰는 이유는 넘침 때문이다 — 넘겨서 음수가 되면
        /// <c>PointManager.Add</c> 가 조용히 무시해 <b>지급이 통째로 사라진다.</b>
        /// </summary>
        private void AppendRewards(BonusPinReward pin, List<PointRewardEntry> into)
        {
            if (pin.rewards == null)
                return;

            int scale = Mathf.Max(1, rewardMultiplier);
            for (int i = 0; i < pin.rewards.Count; i++)
            {
                PinballReward reward = pin.rewards[i];
                into.Add(new PointRewardEntry(
                    reward.pointType, PinballRules.ScaleAmount(reward.amount, scale)));
            }
        }

        /// <summary>
        /// 저장 없이 지급한다. <c>PointRewardUtility.GrantRewards</c> 는 내부에서
        /// <c>SaveAll</c> 을 부르므로 여기서는 쓸 수 없다 — 카운트와 지급이 한 번의 쓰기에
        /// 같이 굳어야 한다.
        /// </summary>
        private static void GrantWithoutSave(IReadOnlyList<PointRewardEntry> rewards)
        {
            PointManager points = PointManager.Instance;
            if (points == null || rewards == null)
                return;

            for (int i = 0; i < rewards.Count; i++)
                points.Add(rewards[i].PointType, rewards[i].Amount, false);
        }

        private void SetPinCount(int tag, int value)
        {
            if (value <= 0)
                pinCounts.Remove(tag);
            else
                pinCounts[tag] = value;
        }

        private void EndRoundInternal()
        {
            inProgress = false;
            remainingCycles = 0;
            rerollsLeft = 0;
            cycleDirty = false;
            dice = Array.Empty<int>();
            locks = Array.Empty<bool>();

            // <b>pinCounts 는 여기서 지우지 않는다.</b> 라운드는 마지막 핀이 채워지는 순간
            // <see cref="TryShoot"/> 안에서 끝나는데, 그때 공은 아직 하나도 안 굴렀다.
            // 여기서 지우면 화면이 게이지를 채워 보여 줄 근거를 잃어 <b>숫자가 0 에 멈춘다</b> —
            // 에러도 안 난다. 정리는 다음 <see cref="StartRound"/> 가 한다.
        }

        /// <summary>사이클 하나를 새로 연다. 주사위를 한 번 굴려 두고 리롤 횟수를 채운다.</summary>
        private void ResetCycle()
        {
            BonusDiceDatabase db = Database;
            int count = Mathf.Max(1, db.diceCount);

            if (dice.Length != count)
            {
                dice = new int[count];
                locks = new bool[count];
            }

            for (int i = 0; i < count; i++)
            {
                dice[i] = UnityEngine.Random.Range(1, BonusDiceRules.MaxPip + 1);
                locks[i] = false;
            }

            // 사이클 시작의 이 한 번은 리롤로 세지 않는다.
            rerollsLeft = Mathf.Max(0, db.rerollsPerCycle);
            cycleDirty = false;
        }

        private void EnsureCycle()
        {
            if (!cycleDirty)
                return;

            cycleDirty = false;
            if (inProgress)
                ResetCycle();
        }

        private static void Save()
        {
            // ?. 가 필수다. 생성자 시점에는 SaveService 가 아직 null 이다.
            OJ.DI.GameContainer.SaveService?.SaveAll();
        }

        // ──────────────────────────────────────────────── 세이브

        public void WriteTo(SaveState state)
        {
            if (state == null)
                return;

            state.BonusDice.InProgress = inProgress;
            state.BonusDice.RemainingCycles = remainingCycles;
            state.BonusDice.RewardMultiplier = Mathf.Max(1, rewardMultiplier);
            state.BonusDice.PinCounts.Clear();

            // 끝난 라운드의 카운트는 파일에 남기지 않는다. 화면이 마지막 연출에 쓰려고
            // 메모리에 들고 있을 뿐이고, 저장까지 하면 다음 판에 남은 진행도로 오해된다.
            if (!inProgress)
                return;

            foreach (KeyValuePair<int, int> pair in pinCounts)
            {
                if (pair.Value <= 0)
                    continue;

                state.BonusDice.PinCounts[TagKeyPrefix + pair.Key] = pair.Value;
            }
        }

        public void ReadFrom(SaveState state)
        {
            if (state == null)
                return;

            pinCounts.Clear();
            inProgress = state.BonusDice.InProgress;
            remainingCycles = state.BonusDice.RemainingCycles;
            rewardMultiplier = Mathf.Max(1, state.BonusDice.RewardMultiplier);

            foreach (KeyValuePair<string, int> pair in state.BonusDice.PinCounts)
            {
                if (!TryParseTagKey(pair.Key, out int tag))
                {
                    Debug.LogWarning("[보상라운드] 모르는 카운트 키라 버린다: " + pair.Key);
                    continue;
                }

                if (pair.Value > 0)
                    pinCounts[tag] = pair.Value;
            }

            // 주사위는 저장하지 않았다. 이어하기면 새로 굴려야 하는데, 지금은 StaticResource 가
            // 아직 안 서 있을 수 있어 수치표를 건드리지 않고 첫 조회까지 미룬다.
            dice = Array.Empty<int>();
            locks = Array.Empty<bool>();
            rerollsLeft = 0;
            cycleDirty = inProgress;

            // 여기서 OnChanged 를 쏘지 않는다. 로드는 아직 다른 매니저가 자기 몫을
            // 읽기 전이고, 구독자가 그 상태를 보면 절반만 로드된 게임을 그리게 된다.
        }

        private static bool TryParseTagKey(string key, out int tag)
        {
            tag = 0;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(TagKeyPrefix, StringComparison.Ordinal))
                return false;

            return int.TryParse(key.Substring(TagKeyPrefix.Length), out tag);
        }
    }
}
