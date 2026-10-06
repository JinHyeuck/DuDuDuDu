using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Point;
using OJ.Save;

namespace OJ.SeasonPass
{
    /// <summary>패스 한 칸의 현재 상태. 화면이 그대로 그린다.</summary>
    public readonly struct SeasonPassSlotView
    {
        public SeasonPassSlotView(
            int level, bool premium, IReadOnlyList<SeasonPassReward> rewards,
            bool reached, bool claimed, bool locked)
        {
            Level = level;
            Premium = premium;
            Rewards = rewards;
            Reached = reached;
            Claimed = claimed;
            Locked = locked;
        }

        public int Level { get; }

        /// <summary>유료 트랙 칸인가.</summary>
        public bool Premium { get; }

        /// <summary>비어 있으면 그 칸은 그리지 않는다.</summary>
        public IReadOnlyList<SeasonPassReward> Rewards { get; }

        /// <summary>레벨에 닿았는가.</summary>
        public bool Reached { get; }

        public bool Claimed { get; }

        /// <summary>유료 트랙인데 아직 안 샀다. 자물쇠를 그리는 조건이다.</summary>
        public bool Locked { get; }

        public bool HasRewards => Rewards != null && Rewards.Count > 0;

        public bool Claimable => HasRewards && Reached && !Claimed && !Locked;
    }

    /// <summary>
    /// 시즌 패스의 실행부. 시즌 교체·포인트 적립·수령이 한 자리에 있다.
    ///
    /// <b>포인트는 "쓴 고기" 다.</b> <c>PointManager.OnPointSpent</c> 를 구독해서 세며,
    /// 호출부에 손을 뻗지 않는다 — 지금 고기를 쓰는 곳은 소탕과 전투 입장 둘이지만
    /// 세는 쪽이 호출부마다 붙으면 새 소모처가 생길 때 빠뜨리는 쪽이 기본값이 된다.
    ///
    /// <b>지급은 세지 않는다.</b> <c>OnPointChanged</c> 가 아니라 <c>OnPointSpent</c> 인
    /// 이유가 그것이다. 방치 보상으로 받은 고기가 패스를 올리면 "쓴 만큼" 이 아니게 된다.
    ///
    /// <b>시즌이 바뀌면 통째로 비운다.</b> 포인트도 수령 기록도 유료 활성화도.
    /// 활성화가 넘어오면 한 번 사고 영원히 쓰는 상품이 된다.
    /// </summary>
    [Preserve]
    public sealed class SeasonPassManager : ISaveStateOwner, IDisposable
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static SeasonPassManager Instance { get; internal set; }

        /// <summary>포인트·수령·시즌 교체로 화면이 바뀌어야 할 때.</summary>
        public event Action OnChanged;

        private readonly SeasonPassSave state = new SeasonPassSave();
        private readonly PointManager points;

        public SeasonPassManager(PointManager points)
        {
            this.points = points;

            if (this.points != null)
                this.points.OnPointSpent += OnPointSpent;
        }

        public void Dispose()
        {
            if (points != null)
                points.OnPointSpent -= OnPointSpent;
        }

        // ── 적립 ───────────────────────────────────────────────────────

        private void OnPointSpent(PointType pointType, int amount)
        {
            // 고기만 센다. 패스의 축이 "고기를 얼마나 썼는가" 하나다.
            if (pointType != PointType.Stamina || amount <= 0)
                return;

            EnsureSeason();

            long sum = (long)state.Points + amount;
            state.Points = sum >= int.MaxValue ? int.MaxValue : (int)sum;

            OnChanged?.Invoke();
        }

        // ── 조회 ───────────────────────────────────────────────────────

        /// <summary>이번 시즌의 표. 그 달의 표가 없으면 기본 시즌이다(절대 null 이 아니다).</summary>
        public SeasonPassSeason Season
        {
            get
            {
                EnsureSeason();
                return SeasonPassDatabaseProvider.Database.GetSeason(state.SeasonId);
            }
        }

        public int PointsPerLevel => SeasonPassDatabaseProvider.Database.PointsPerLevel;

        public int Points
        {
            get
            {
                EnsureSeason();
                return state.Points;
            }
        }

        public bool PremiumUnlocked
        {
            get
            {
                EnsureSeason();
                return state.PremiumUnlocked;
            }
        }

        public int MaxLevel => Season.MaxLevel;

        /// <summary>지금 레벨(1부터).</summary>
        public int Level => SeasonPassRules.LevelFor(Points, PointsPerLevel, MaxLevel);

        /// <summary>지금 레벨 안에서 쌓은 포인트. 게이지의 분자다.</summary>
        public int PointsIntoLevel => SeasonPassRules.PointsIntoLevel(Points, PointsPerLevel, MaxLevel);

        /// <summary>게이지 비율(0~1).</summary>
        public float LevelProgress => SeasonPassRules.LevelProgress(Points, PointsPerLevel, MaxLevel);

        /// <summary>시즌 종료까지 남은 시간.</summary>
        public TimeSpan TimeUntilSeasonEnd => SeasonPassRules.TimeUntilSeasonEnd(DateTime.Now);

        /// <summary>화면에 그릴 칸 목록. 레벨 오름차순으로 무료·유료가 번갈아 들어간다.</summary>
        public List<SeasonPassSlotView> GetSlots()
        {
            EnsureSeason();

            SeasonPassSeason season = Season;
            int reached = Level;

            var slots = new List<SeasonPassSlotView>(season.MaxLevel * 2);

            for (int level = 1; level <= season.MaxLevel; level++)
            {
                SeasonPassLevel data = season.GetLevel(level);
                if (data == null)
                    continue;

                slots.Add(new SeasonPassSlotView(
                    level, false, data.freeRewards,
                    level <= reached, state.ClaimedFreeLevels.Contains(level), false));

                slots.Add(new SeasonPassSlotView(
                    level, true, data.premiumRewards,
                    level <= reached, state.ClaimedPremiumLevels.Contains(level),
                    !state.PremiumUnlocked));
            }

            return slots;
        }

        /// <summary>받을 것이 하나라도 있는가. 로비 버튼의 빨간 점이 쓴다.</summary>
        public bool HasClaimable()
        {
            List<SeasonPassSlotView> slots = GetSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Claimable)
                    return true;
            }

            return false;
        }

        // ── 수령 ───────────────────────────────────────────────────────

        public bool TryClaim(int level, bool premium)
        {
            return TryClaim(level, premium, out _);
        }

        /// <summary>한 칸을 받는다. <paramref name="granted"/> 는 실제로 지급한 것 — 획득 팝업이 그린다.</summary>
        public bool TryClaim(int level, bool premium, out List<PointRewardEntry> granted)
        {
            granted = null;
            EnsureSeason();

            SeasonPassLevel data = Season.GetLevel(level);
            if (data == null)
                return false;

            List<SeasonPassReward> rewards = premium ? data.premiumRewards : data.freeRewards;
            if (rewards == null || rewards.Count == 0)
                return false;

            List<int> claimed = premium ? state.ClaimedPremiumLevels : state.ClaimedFreeLevels;

            if (!SeasonPassRules.CanClaim(
                    level, Level, claimed.Contains(level), premium, state.PremiumUnlocked))
            {
                return false;
            }

            claimed.Add(level);
            granted = SeasonPassReward.ToPointRewards(rewards);
            Grant(granted);
            Save();
            return true;
        }

        /// <summary>
        /// 받을 수 있는 것을 전부 받는다. 화면의 "일괄 수령" 이다.
        ///
        /// <b>한 번만 저장한다.</b> 칸마다 저장하면 30레벨짜리 패스를 한꺼번에 받을 때
        /// 파일을 수십 번 쓴다.
        /// </summary>
        public int ClaimAll()
        {
            return ClaimAll(out _);
        }

        /// <summary>전부 받는다. <paramref name="merged"/> 는 재화별로 합친 지급 내역 — 획득 팝업 하나로 보여 준다.</summary>
        public int ClaimAll(out List<PointRewardEntry> merged)
        {
            merged = null;
            EnsureSeason();

            var granted = new List<PointRewardEntry>();
            List<SeasonPassSlotView> slots = GetSlots();
            int count = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                SeasonPassSlotView slot = slots[i];
                if (!slot.Claimable)
                    continue;

                List<int> claimed = slot.Premium ? state.ClaimedPremiumLevels : state.ClaimedFreeLevels;
                claimed.Add(slot.Level);
                granted.AddRange(SeasonPassReward.ToPointRewards(slot.Rewards));
                count++;
            }

            if (count == 0)
                return 0;

            merged = PointRewardUtility.MergeRewards(granted);
            Grant(merged);
            Save();
            return count;
        }

        /// <summary>
        /// 유료 트랙을 연다. <b>이미 지나간 레벨도 그 즉시 받을 수 있게 된다</b> —
        /// 기획서 7.3 의 소급 지급이다. 여기서 바로 지급하지 않고 <i>잠금만</i> 푸는 것은,
        /// 받는 순간을 유저가 보게 해야 산 값어치가 드러나기 때문이다(일괄 수령이 있다).
        ///
        /// <b>결제는 아직 없다.</b> 호출부가 <c>ShopPurchaseManager.TryBuyCashProduct</c> 를
        /// 지나온 뒤에 부른다 — 그쪽이 현금의 유일한 관문이다.
        /// </summary>
        public bool TryUnlockPremium()
        {
            EnsureSeason();

            if (state.PremiumUnlocked)
                return false;

            state.PremiumUnlocked = true;
            Save();
            return true;
        }

        private void Grant(List<PointRewardEntry> rewards)
        {
            if (rewards != null && rewards.Count > 0)
                PointRewardUtility.GrantRewards(rewards);
        }

        private void Save()
        {
            OJ.DI.GameContainer.SaveService?.SaveAll();
            OnChanged?.Invoke();
        }

        // ── 시즌 교체 ──────────────────────────────────────────────────

        /// <summary>
        /// 앱이 켜졌을 때 한 번 부른다. 세이브를 읽은 <b>뒤</b>여야 한다 —
        /// 먼저 부르면 로드가 그 결과를 덮어 시즌이 안 바뀐다.
        /// </summary>
        public void NotifyAppStart()
        {
            EnsureSeason();
        }

        /// <summary>
        /// 달이 바뀌었으면 이번 시즌 기록을 통째로 버린다.
        ///
        /// <b>미수령 보상은 사라진다.</b> 시즌이 바뀌면 보상표 자체가 달라지므로 이월할
        /// 자리가 없다 — 지난 시즌의 7레벨 보상을 이번 시즌 7레벨에 줄 수는 없다.
        /// 그래서 화면이 남은 시간을 크게 띄운다.
        /// </summary>
        private void EnsureSeason()
        {
            string current = SeasonPassRules.SeasonId(DateTime.Now);
            if (state.SeasonId == current)
                return;

            state.SeasonId = current;
            state.Points = 0;
            state.PremiumUnlocked = false;
            state.ClaimedFreeLevels.Clear();
            state.ClaimedPremiumLevels.Clear();

            OnChanged?.Invoke();
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState saveState)
        {
            SeasonPassSave target = saveState.SeasonPass;

            target.SeasonId = state.SeasonId;
            target.Points = state.Points;
            target.PremiumUnlocked = state.PremiumUnlocked;

            target.ClaimedFreeLevels.Clear();
            target.ClaimedFreeLevels.AddRange(state.ClaimedFreeLevels);

            target.ClaimedPremiumLevels.Clear();
            target.ClaimedPremiumLevels.AddRange(state.ClaimedPremiumLevels);
        }

        public void ReadFrom(SaveState saveState)
        {
            SeasonPassSave source = saveState.SeasonPass;

            state.SeasonId = source.SeasonId ?? string.Empty;

            // 음수는 손상이다. 그대로 들이면 다음 저장에서 그 값이 굳는다.
            state.Points = source.Points > 0 ? source.Points : 0;
            state.PremiumUnlocked = source.PremiumUnlocked;

            state.ClaimedFreeLevels.Clear();
            state.ClaimedFreeLevels.AddRange(source.ClaimedFreeLevels);

            state.ClaimedPremiumLevels.Clear();
            state.ClaimedPremiumLevels.AddRange(source.ClaimedPremiumLevels);
        }

#if UNITY_EDITOR || DEV_DEFINE
        /// <summary>개발용. 포인트를 그대로 넣는다(고기를 쓰지 않고 레벨을 올려 본다).</summary>
        public void DevAddPoints(int amount)
        {
            if (amount <= 0)
                return;

            EnsureSeason();
            state.Points = (int)Math.Min(int.MaxValue, (long)state.Points + amount);
            Save();
        }

        /// <summary>개발용. 이번 시즌 기록을 비운다(시즌이 바뀐 것을 흉내 낸다).</summary>
        public void DevResetSeason()
        {
            state.SeasonId = string.Empty;
            EnsureSeason();
            Save();
        }

        /// <summary>개발용. 유료 트랙을 결제 없이 연다/닫는다.</summary>
        public void DevSetPremium(bool unlocked)
        {
            EnsureSeason();
            state.PremiumUnlocked = unlocked;
            Save();
        }
#endif
    }
}
