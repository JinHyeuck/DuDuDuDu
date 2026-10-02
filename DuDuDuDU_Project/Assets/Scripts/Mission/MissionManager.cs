using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.Point;
using OJ.Save;
using OJ.Shop;

namespace OJ.Mission
{
    /// <summary>일일 미션 한 줄의 현재 상태. 화면이 그대로 그린다.</summary>
    public readonly struct DailyMissionView
    {
        public DailyMissionView(DailyMissionDefinition definition, int count, bool claimed)
        {
            Definition = definition;
            Count = count;
            Claimed = claimed;
        }

        public DailyMissionDefinition Definition { get; }

        /// <summary>오늘 쌓인 횟수. 요구치를 넘을 수 있다.</summary>
        public int Count { get; }

        public bool Claimed { get; }

        public bool Cleared => MissionRules.IsCleared(Count, Definition.requiredCount);

        /// <summary>받기 버튼이 눌리는 상태인가.</summary>
        public bool Claimable => Cleared && !Claimed;
    }

    /// <summary>업적 한 계열의 현재 상태. <b>단계 하나만</b> 들고 있다.</summary>
    public readonly struct AchievementView
    {
        public AchievementView(AchievementDefinition definition, int count, bool claimed, bool seriesComplete)
        {
            Definition = definition;
            Count = count;
            Claimed = claimed;
            SeriesComplete = seriesComplete;
        }

        public AchievementDefinition Definition { get; }

        /// <summary>누적 횟수. 단계를 넘어 계속 쌓인다 — 화면의 (10/100) 이 이 값이다.</summary>
        public int Count { get; }

        public bool Claimed { get; }

        /// <summary>마지막 단계까지 전부 받았다. 화면은 "완료" 로 표시한다.</summary>
        public bool SeriesComplete { get; }

        public bool Cleared => MissionRules.IsCleared(Count, Definition.requiredCount);

        public bool Claimable => Cleared && !Claimed;
    }

    /// <summary>
    /// 일일 미션·업적의 실행부. <b>세는 것·리셋하는 것·주는 것</b>이 한 자리에 있다.
    ///
    /// <b>왜 호출부가 아니라 여기인가.</b> 세는 지점이 13곳이고 앞으로 는다. 각자
    /// "오늘 날짜인지 확인하고 → 올리고 → 저장한다" 를 하면 그 순서가 13벌이 되고,
    /// 그중 하나가 리셋 확인을 빠뜨리면 <b>날짜가 바뀌는 순간에만</b> 어긋나는 카운터가
    /// 하나 생긴다 — 개발 중에 가장 안 밟히는 경로다.
    ///
    /// <b><see cref="Notify"/> 는 저장하지 않는다.</b> 적 처치는 한 웨이브에 수백 번
    /// 들어오는데 그때마다 세이브 파일을 쓰면 전투가 끊긴다. 값은 메모리에만 올라가고
    /// 파일은 <b>보상을 받을 때</b>와 <c>SaveOnApplicationLifecycle</c> 이 앱 정지·종료에서
    /// 쓸 때 굳는다(AGENTS "dirty 플래그 1개 + 주기 flush + pause/quit flush").
    /// 그래서 앱이 강제 종료되면 마지막 몇 분의 카운트는 날아갈 수 있다 — 받은 보상이
    /// 날아가는 것보다 낫고, 수령은 즉시 저장되므로 그쪽은 안전하다.
    ///
    /// <b>리셋 기준은 <c>ShopPurchaseManager</c> 와 같은 함수를 쓴다.</b> 둘이 다른 시각에
    /// 리셋되면 화면에 띄우는 "남은 시간" 이 둘 중 하나와 어긋난다.
    /// </summary>
    [Preserve]
    public sealed class MissionManager : ISaveStateOwner
    {
        /// <summary>과도기 다리. 대입은 <c>GameContainer</c> 에서만 한다.</summary>
        public static MissionManager Instance { get; internal set; }

        /// <summary>카운트·수령·리셋으로 화면이 바뀌어야 할 때. 퀘스트 창이 통째로 다시 그린다.</summary>
        public event Action OnChanged;

        /// <summary>
        /// 받을 것이 하나라도 생기거나 사라졌을 때. 로비의 빨간 점이 이것만 본다.
        ///
        /// <b><see cref="OnChanged"/> 와 가른 이유.</b> 전투 중 적 처치마다
        /// <see cref="OnChanged"/> 가 뜨는데, 로비 버튼이 그것을 구독하면 1초에 수십 번
        /// 다시 그린다. 빨간 점이 알아야 하는 것은 <b>개수가 아니라 0 인지 아닌지</b>뿐이다.
        /// </summary>
        public event Action OnClaimableChanged;

        private readonly MissionSave state = new MissionSave();
        private readonly PointManager points;

        /// <summary>마지막으로 알린 "받을 것이 있나". <see cref="OnClaimableChanged"/> 를 걸러 내는 기준이다.</summary>
        private bool lastHadClaimable;

        public MissionManager(PointManager points)
        {
            this.points = points;
        }

        // ── 세기 ───────────────────────────────────────────────────────

        /// <summary>
        /// 행동 하나를 센다. 하위 키가 없는 쪽이다.
        ///
        /// <b>절대 던지지 않는다.</b> 이 호출은 레벨업·처치 같은 <b>게임 플레이 한가운데</b>에
        /// 끼어 있어서, 여기서 예외가 나면 그 플레이가 실패한 것처럼 보인다.
        /// 미션이 게임을 막는 것은 어떤 경우에도 옳지 않다.
        /// </summary>
        public void Notify(MissionAction action, int amount = 1)
        {
            Notify(action, null, amount);
        }

        /// <summary>
        /// 행동 하나를 센다. <paramref name="subKey"/> 가 있으면
        /// <b>하위 키가 붙은 카운터와 안 붙은 카운터가 함께</b> 오른다 —
        /// 일일 미션은 "다이스 레벨업" 을 보고 업적은 "신화 다이스 레벨업" 을 보는데,
        /// 호출부가 그 둘을 알 필요는 없다.
        /// </summary>
        public void Notify(MissionAction action, string subKey, int amount = 1)
        {
            if (amount <= 0)
                return;

            EnsureToday();

            AddCount(MissionRules.CounterKey(action), amount);

            if (!string.IsNullOrEmpty(subKey))
                AddCount(MissionRules.CounterKey(action, subKey), amount);

            OnChanged?.Invoke();

            // <b>구독자가 있을 때만 계산한다.</b> HasClaimable 은 목록 둘을 통째로 훑고
            // 리스트를 새로 만든다. 적 처치는 한 웨이브에 수백 번 들어오는 경로라 그것을
            // 매번 돌리면 전투가 끊긴다. 빨간 점은 로비에만 있으므로 전투 중에는
            // 구독자가 0 이고, 이 한 줄이 그 비용을 통째로 없앤다.
            if (OnClaimableChanged != null)
                RaiseClaimableIfChanged();
        }

        private void AddCount(string key, int amount)
        {
            state.DailyCounts.TryGetValue(key, out int daily);
            state.DailyCounts[key] = SafeAdd(daily, amount);

            state.TotalCounts.TryGetValue(key, out int total);
            state.TotalCounts[key] = SafeAdd(total, amount);
        }

        /// <summary>
        /// 넘치지 않게 더한다. 누적 카운터는 영영 늘기만 하므로, 치트나 고배율이
        /// 겹치면 언젠가 <see cref="int.MaxValue"/> 에 닿는다. 넘치면 <b>음수가 되어
        /// 달성한 업적이 미달성으로 되돌아간다</b> — 그 사고를 여기서 막는다.
        /// </summary>
        private static int SafeAdd(int current, int amount)
        {
            long sum = (long)current + amount;
            return sum >= int.MaxValue ? int.MaxValue : (int)sum;
        }

        // ── 조회 ───────────────────────────────────────────────────────

        /// <summary>오늘 이 카운터가 몇인가.</summary>
        public int GetDailyCount(string counterKey)
        {
            EnsureToday();
            return state.DailyCounts.TryGetValue(counterKey, out int count) ? count : 0;
        }

        /// <summary>누적으로 이 카운터가 몇인가. 리셋되지 않는다.</summary>
        public int GetTotalCount(string counterKey)
        {
            return state.TotalCounts.TryGetValue(counterKey, out int count) ? count : 0;
        }

        /// <summary>다음 리셋까지 남은 시간. 상점 헤더와 같은 기준이다.</summary>
        public TimeSpan TimeUntilReset => ShopPurchaseManager.TimeUntilReset(DateTime.Now);

        /// <summary>
        /// 화면에 그릴 오늘의 목록. <b>받기 가능 → 진행 중 → 완료</b> 순이다.
        ///
        /// <b>정렬을 화면이 아니라 여기서 한다.</b> 목록을 읽는 곳이 셋이고
        /// (목록 그리기·달성 수 세기·빨간 점) 화면에서 정렬하면 셋이 다른 순서를 보게 된다.
        /// 세는 쪽은 순서를 신경 쓰지 않으므로 여기서 한 번 정하는 편이 싸다.
        /// </summary>
        public List<DailyMissionView> GetDailyMissions()
        {
            EnsureToday();

            List<DailyMissionDefinition> definitions =
                DailyMissionDatabaseProvider.Database.GetActiveMissions();

            var views = new List<DailyMissionView>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                DailyMissionDefinition definition = definitions[i];
                views.Add(new DailyMissionView(
                    definition,
                    GetDailyCount(definition.CounterKey),
                    state.ClaimedDailyIds.Contains(definition.id)));
            }

            return MissionRules.OrderByClaimState(views, v => v.Claimable, v => v.Claimed);
        }

        /// <summary>오늘 달성한 미션 수. 수령 여부와 무관하다.</summary>
        public int DailyClearedCount
        {
            get
            {
                List<DailyMissionView> views = GetDailyMissions();
                int cleared = 0;
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i].Cleared)
                        cleared++;
                }

                return cleared;
            }
        }

        /// <summary>추가 보상 문턱을 이미 받았는가.</summary>
        public bool IsTierClaimed(int requiredClearCount)
        {
            EnsureToday();
            return state.ClaimedDailyTiers.Contains(requiredClearCount);
        }

        /// <summary>
        /// 화면에 그릴 업적 목록. <b>계열마다 한 줄</b>이고, 그 한 줄은 아직 받지 않은
        /// 가장 낮은 단계다(전부 받았으면 마지막 단계 + 완료 표시).
        /// </summary>
        public List<AchievementView> GetAchievements()
        {
            IReadOnlyList<AchievementSeries> series = AchievementDatabaseProvider.Database.Series;
            var views = new List<AchievementView>(series.Count);

            var claimedFlags = new List<bool>();

            for (int i = 0; i < series.Count; i++)
            {
                List<AchievementDefinition> tiers = series[i].Tiers;
                if (tiers.Count == 0)
                    continue;

                claimedFlags.Clear();
                for (int t = 0; t < tiers.Count; t++)
                    claimedFlags.Add(state.ClaimedAchievementIds.Contains(tiers[t].id));

                int visible = MissionRules.VisibleTierIndex(tiers.Count, claimedFlags);
                if (visible < 0)
                    continue;

                bool complete = MissionRules.IsSeriesComplete(tiers.Count, claimedFlags);

                views.Add(new AchievementView(
                    tiers[visible],
                    GetTotalCount(series[i].CounterKey),
                    claimedFlags[visible],
                    complete));
            }

            // 일일과 같은 순서 규칙이다. 업적의 "완료" 는 계열을 끝까지 받은 것이고,
            // 그건 다시 볼 일이 없으므로 맨 아래가 맞다.
            return MissionRules.OrderByClaimState(views, v => v.Claimable, v => v.SeriesComplete);
        }

        /// <summary>
        /// 어딘가에 받을 것이 있는가. 로비 버튼의 빨간 점이 쓴다.
        ///
        /// <b>목록을 전부 훑는다.</b> 캐시하지 않는 이유는 틀렸을 때의 증상 때문이다 —
        /// 캐시가 어긋나면 "빨간 점이 안 사라진다"(또는 안 뜬다)가 되는데, 그건 유저가
        /// 보기에 고장이고 우리가 보기엔 재현이 안 된다.
        /// </summary>
        public bool HasClaimable()
        {
            List<DailyMissionView> dailies = GetDailyMissions();
            int cleared = 0;

            for (int i = 0; i < dailies.Count; i++)
            {
                if (dailies[i].Claimable)
                    return true;

                if (dailies[i].Cleared)
                    cleared++;
            }

            IReadOnlyList<DailyMissionTier> tiers = DailyMissionDatabaseProvider.Database.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                DailyMissionTier tier = tiers[i];
                if (tier != null && cleared >= tier.requiredClearCount &&
                    !state.ClaimedDailyTiers.Contains(tier.requiredClearCount))
                {
                    return true;
                }
            }

            List<AchievementView> achievements = GetAchievements();
            for (int i = 0; i < achievements.Count; i++)
            {
                if (achievements[i].Claimable)
                    return true;
            }

            return false;
        }

        // ── 수령 ───────────────────────────────────────────────────────

        /// <summary>일일 미션 보상을 받는다. 받을 수 없는 상태면 false 이고 아무것도 바뀌지 않는다.</summary>
        public bool TryClaimDaily(string missionId)
        {
            EnsureToday();

            if (string.IsNullOrEmpty(missionId))
                return false;

            List<DailyMissionView> views = GetDailyMissions();
            for (int i = 0; i < views.Count; i++)
            {
                DailyMissionView view = views[i];
                if (view.Definition.id != missionId)
                    continue;

                if (!view.Claimable)
                    return false;

                state.ClaimedDailyIds.Add(missionId);
                GrantAndSave(view.Definition.rewards);
                return true;
            }

            return false;
        }

        /// <summary>추가 보상을 받는다. <paramref name="requiredClearCount"/> 는 문턱 값 그대로다.</summary>
        public bool TryClaimDailyTier(int requiredClearCount)
        {
            EnsureToday();

            if (state.ClaimedDailyTiers.Contains(requiredClearCount))
                return false;

            IReadOnlyList<DailyMissionTier> tiers = DailyMissionDatabaseProvider.Database.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                DailyMissionTier tier = tiers[i];
                if (tier == null || tier.requiredClearCount != requiredClearCount)
                    continue;

                if (DailyClearedCount < tier.requiredClearCount)
                    return false;

                state.ClaimedDailyTiers.Add(requiredClearCount);
                GrantAndSave(tier.rewards);
                return true;
            }

            return false;
        }

        /// <summary>업적 보상을 받는다. 받으면 그 계열은 다음 단계로 넘어간다.</summary>
        public bool TryClaimAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return false;

            if (state.ClaimedAchievementIds.Contains(achievementId))
                return false;

            IReadOnlyList<AchievementDefinition> all = AchievementDatabaseProvider.Database.Achievements;
            for (int i = 0; i < all.Count; i++)
            {
                AchievementDefinition definition = all[i];
                if (definition == null || definition.id != achievementId)
                    continue;

                if (!MissionRules.IsCleared(GetTotalCount(definition.CounterKey), definition.requiredCount))
                    return false;

                state.ClaimedAchievementIds.Add(achievementId);
                GrantAndSave(definition.rewards);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 지급하고 굳힌다. <b>순서가 중요하다</b> — 수령 기록을 먼저 남기고 지급해야,
        /// 지급 도중에 앱이 죽어도 두 번 받히지 않는다. 호출부가 기록을 먼저 넣고 들어온다.
        /// </summary>
        private void GrantAndSave(IReadOnlyList<MissionReward> rewards)
        {
            List<PointRewardEntry> entries = MissionReward.ToPointRewards(rewards);
            if (entries.Count > 0)
                PointRewardUtility.GrantRewards(entries);

            OJ.DI.GameContainer.SaveService?.SaveAll();

            OnChanged?.Invoke();
            RaiseClaimableIfChanged();
        }

        // ── 리셋 ───────────────────────────────────────────────────────

        /// <summary>
        /// 앱이 켜졌을 때 한 번 부른다. 세이브를 읽은 <b>뒤</b>여야 한다 —
        /// 먼저 부르면 로드가 그 위를 덮어 로그인 미션이 사라진다.
        /// </summary>
        public void NotifyAppStart()
        {
            EnsureToday();
        }

        /// <summary>
        /// 날짜가 바뀌었으면 오늘치를 버리고 <b>로그인을 1 올린다</b>.
        ///
        /// 로그인을 여기서 올리는 것이 요점이다 — "새 날의 첫 접근" 이라는 사건이
        /// 이 자리에만 있다. 바깥에서 따로 올리면 하루에 두 번 오르는 경로가 생긴다.
        ///
        /// <b>UTC 가 아니라 로컬 날짜다.</b> 유저가 "오늘" 이라고 부르는 것이 로컬 날짜이고,
        /// 상점의 일일 리셋과 같은 기준이어야 화면의 남은 시간이 둘 다에 맞는다.
        /// </summary>
        private void EnsureToday()
        {
            string today = ShopPurchaseManager.FormatDate(DateTime.Now);
            if (state.DailyResetDate == today)
                return;

            state.DailyResetDate = today;
            state.DailyCounts.Clear();
            state.ClaimedDailyIds.Clear();
            state.ClaimedDailyTiers.Clear();

            // 로그인 카운트는 일일분과 누적분 둘 다 올라간다. AddCount 를 쓰면
            // EnsureToday 가 다시 불려 무한 재귀가 되므로 여기서 직접 더한다.
            string loginKey = MissionRules.CounterKey(MissionAction.Login);
            state.DailyCounts[loginKey] = 1;
            state.TotalCounts.TryGetValue(loginKey, out int totalLogin);
            state.TotalCounts[loginKey] = SafeAdd(totalLogin, 1);

            OnChanged?.Invoke();
        }

        private void RaiseClaimableIfChanged()
        {
            bool has = HasClaimable();
            if (has == lastHadClaimable)
                return;

            lastHadClaimable = has;
            OnClaimableChanged?.Invoke();
        }

        // ── 저장 ───────────────────────────────────────────────────────

        public void WriteTo(SaveState saveState)
        {
            MissionSave target = saveState.Missions;

            target.DailyResetDate = state.DailyResetDate;

            target.DailyCounts.Clear();
            foreach (KeyValuePair<string, int> pair in state.DailyCounts)
                target.DailyCounts[pair.Key] = pair.Value;

            target.TotalCounts.Clear();
            foreach (KeyValuePair<string, int> pair in state.TotalCounts)
                target.TotalCounts[pair.Key] = pair.Value;

            target.ClaimedDailyIds.Clear();
            target.ClaimedDailyIds.AddRange(state.ClaimedDailyIds);

            target.ClaimedDailyTiers.Clear();
            target.ClaimedDailyTiers.AddRange(state.ClaimedDailyTiers);

            target.ClaimedAchievementIds.Clear();
            target.ClaimedAchievementIds.AddRange(state.ClaimedAchievementIds);
        }

        public void ReadFrom(SaveState saveState)
        {
            MissionSave source = saveState.Missions;

            state.DailyResetDate = source.DailyResetDate ?? string.Empty;

            state.DailyCounts.Clear();
            foreach (KeyValuePair<string, int> pair in source.DailyCounts)
            {
                // 음수는 손상이다. 그대로 들이면 다음 저장에서 그 값이 굳고,
                // 업적 진행도가 영영 뒤로 가 있다.
                if (pair.Value > 0)
                    state.DailyCounts[pair.Key] = pair.Value;
            }

            state.TotalCounts.Clear();
            foreach (KeyValuePair<string, int> pair in source.TotalCounts)
            {
                if (pair.Value > 0)
                    state.TotalCounts[pair.Key] = pair.Value;
            }

            state.ClaimedDailyIds.Clear();
            state.ClaimedDailyIds.AddRange(source.ClaimedDailyIds);

            state.ClaimedDailyTiers.Clear();
            state.ClaimedDailyTiers.AddRange(source.ClaimedDailyTiers);

            state.ClaimedAchievementIds.Clear();
            state.ClaimedAchievementIds.AddRange(source.ClaimedAchievementIds);
        }
    }
}
