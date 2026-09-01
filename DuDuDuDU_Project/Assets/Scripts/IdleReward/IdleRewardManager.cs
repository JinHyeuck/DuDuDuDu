using System;
using System.Collections.Generic;
using OJ.Core;
using UnityEngine.Scripting;
using OJ.DI;
using OJ.Point;
using OJ.Save;
using OJ.Stage;

namespace OJ.IdleReward
{
    /// <summary>
    /// 방치 보상 타이머. (MIGRATION_BASELINE 8.3a)
    ///
    /// <c>StageProgressManager</c> 와 <c>PointManager</c> 를 생성자로 받는다.
    ///
    /// <b>생명주기 저장 훅이 없다.</b> 다른 매니저와 달리 <c>OnApplicationPause</c> 를 갖고
    /// 있지 않았다 — 값이 바뀌는 순간(리셋·수령)마다 즉시 저장하기 때문이다. 7.5 로 저장
    /// 매체가 통합 파일로 바뀐 뒤에도 그 성질은 그대로 유지한다. 즉시 저장을 버리고 앱이
    /// 멈출 때의 일괄 저장에만 기대면, 수령 직후 OS 가 프로세스를 죽였을 때 그 거래가
    /// 통째로 사라진다 — 모바일에서 그건 예외 상황이 아니라 일상이다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class IdleRewardManager : ISaveStateOwner
    {
        public const double AutoBattleMaxSeconds = 8d * 60d * 60d;
        public const double SecondsPerAutoBattleClear = 20d * 60d;
        public const double MeatSetIntervalSeconds = 6d * 60d * 60d;
        public const int MeatPerSet = 30;
        public const int MaxMeatSetCount = 30;

        /// <summary>
        /// 과도기 다리. <b>대입은 <see cref="GameContainer"/> 에서만 한다.</b>
        /// 호출부를 전부 주입으로 옮기면 사라진다.
        /// </summary>
        public static IdleRewardManager Instance { get; internal set; }

        public event Action OnChanged;

        private long autoBattleStartUtcTicks;

        /// <summary>
        /// 세이브 대조(F10)용 접근자. 이 값은 화면에 "몇 분 경과"로만 나오는데, 대조는
        /// <b>저장되는 원본</b>과 비교해야 의미가 있다. 분 단위로 반올림된 값을 비교하면
        /// tick 이 어긋나도 통과해 버린다.
        /// </summary>
        public long AutoBattleStartUtcTicksForDiagnostics => autoBattleStartUtcTicks;
        private long meatFestivalStartUtcTicks;

        private readonly StageProgressManager stageProgress;
        private readonly PointManager points;

        public IdleRewardManager(StageProgressManager stageProgress, PointManager points)
        {
            this.stageProgress = stageProgress;
            this.points = points;

            // 타이머 기준 시각은 로드와 무관하게 생성자에서 무조건 세운다.
            //
            // 7.5 전에는 이 초기화가 PlayerPrefs 로드 경로(LoadOrInitialize) 안에 있었다.
            // 그 경로를 지우면서 초기화까지 같이 지웠다면 <b>세이브 파일이 없는 첫 실행에서
            // 두 tick 이 0 으로 남는다</b> — SaveService.TryLoadAll 은 파일이 없으면 owners
            // 루프 전에 돌아가므로 ReadFrom 이 아예 불리지 않기 때문이다. 그리고
            // IdleRewardFormula.ElapsedSeconds 는 start <= 0 을 경과 0d 로 눌러 버려서,
            // 그 상태는 예외도 로그도 없이 "자동전투·고기축제 게이지가 영원히 0" 으로만 드러난다.
            //
            // DateTime.UtcNow 를 한 번만 읽어 두 tick 에 같은 값을 넣는 것은 예전 그대로다.
            // 이 값이 '앱을 켠 시각'의 기준이라 호출 시점이 바뀌면 경과 시간이 달라지고,
            // 두 번 나눠 읽으면 두 타이머의 기준점이 서로 어긋난다.
            DateTime utcNow = DateTime.UtcNow;
            autoBattleStartUtcTicks = utcNow.Ticks;
            meatFestivalStartUtcTicks = utcNow.Ticks;

            // 여기서 Save() 를 부르지 않는다. 옛 LoadOrInitialize 는 끝에서 PlayerPrefs 에
            // 즉시 되썼지만, 지금 시점에는 GameContainer.SaveService 가 아직 null 이라
            // (컨테이너가 매니저를 다 만든 뒤에 SaveService 를 해석한다) 어차피 무시된다.
            // 첫 파일은 첫 거래나 앱 일시정지 때 만들어진다.
        }

        public TimeSpan GetAutoBattleElapsed()
        {
            return GetAutoBattleElapsed(DateTime.UtcNow);
        }

        public TimeSpan GetAutoBattleElapsed(DateTime utcNow)
        {
            double seconds = GetElapsedSeconds(autoBattleStartUtcTicks, utcNow);
            return TimeSpan.FromSeconds(IdleRewardFormula.CappedElapsedSeconds(seconds, AutoBattleMaxSeconds));
        }

        public float GetAutoBattleProgress01()
        {
            // TimeSpan 을 한 번 거친 초를 그대로 넘긴다. 원본이 GetAutoBattleElapsed().TotalSeconds 를
            // 나눴기 때문에, 여기서 CappedElapsedSeconds 를 직접 부르면 밀리초 반올림이 빠져 값이 달라진다.
            return IdleRewardFormula.Progress01(GetAutoBattleElapsed().TotalSeconds, AutoBattleMaxSeconds);
        }

        public int GetAutoBattleStageIndex()
        {
            return stageProgress.GetLastClearedStageIndex();
        }

        public List<PointRewardEntry> GetAutoBattleRewards()
        {
            return GetAutoBattleRewards(DateTime.UtcNow);
        }

        public List<PointRewardEntry> GetAutoBattleRewards(DateTime utcNow)
        {
            int stageIndex = GetAutoBattleStageIndex();
            if (stageIndex < 1)
                return new List<PointRewardEntry>();

            // GetAutoBattleProgress01 과 같은 이유로 GetAutoBattleElapsed(...).TotalSeconds 를 넘긴다.
            double clearCount = IdleRewardFormula.AutoBattleClearCount(GetAutoBattleElapsed(utcNow).TotalSeconds, SecondsPerAutoBattleClear);
            return StageRewardCalculator.BuildAutoBattleRewards(stageIndex, clearCount, BuildRewardSeed(stageIndex));
        }

        public bool CanClaimAutoBattle()
        {
            return GetAutoBattleRewards().Count > 0;
        }

        public bool TryClaimAutoBattle(out List<PointRewardEntry> rewards, out int stageIndex)
        {
            DateTime utcNow = DateTime.UtcNow;
            stageIndex = GetAutoBattleStageIndex();
            rewards = GetAutoBattleRewards(utcNow);
            if (stageIndex < 1 || rewards.Count == 0)
                return false;

            PointRewardUtility.GrantRewards(rewards);
            autoBattleStartUtcTicks = utcNow.Ticks;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public int GetStoredMeatSetCount()
        {
            return GetStoredMeatSetCount(DateTime.UtcNow);
        }

        public int GetStoredMeatSetCount(DateTime utcNow)
        {
            // 고기는 자동 전투와 달리 경과 시간에 상한을 걸지 않는다. 세트 수 쪽에서 잘린다.
            double elapsedSeconds = GetElapsedSeconds(meatFestivalStartUtcTicks, utcNow);
            return IdleRewardFormula.StoredMeatSetCount(elapsedSeconds, MeatSetIntervalSeconds, MaxMeatSetCount);
        }

        public TimeSpan GetTimeUntilNextMeatSet()
        {
            DateTime utcNow = DateTime.UtcNow;
            int storedSetCount = GetStoredMeatSetCount(utcNow);
            if (storedSetCount >= MaxMeatSetCount)
                return TimeSpan.Zero;

            double elapsedSeconds = GetElapsedSeconds(meatFestivalStartUtcTicks, utcNow);
            return TimeSpan.FromSeconds(IdleRewardFormula.SecondsUntilNextMeatSet(elapsedSeconds, MeatSetIntervalSeconds));
        }

        public bool TryClaimMeat(out int meatAmount, out int setCount)
        {
            DateTime utcNow = DateTime.UtcNow;
            setCount = GetStoredMeatSetCount(utcNow);
            meatAmount = setCount * MeatPerSet;
            if (setCount <= 0)
                return false;

            points.Add(PointType.Stamina, meatAmount);

            if (setCount >= MaxMeatSetCount)
            {
                meatFestivalStartUtcTicks = utcNow.Ticks;
            }
            else
            {
                long consumedTicks = TimeSpan.FromSeconds(MeatSetIntervalSeconds * setCount).Ticks;
                meatFestivalStartUtcTicks = Math.Min(utcNow.Ticks, meatFestivalStartUtcTicks + consumedTicks);
            }

            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void ResetTimersForDebug()
        {
            DateTime utcNow = DateTime.UtcNow;
            autoBattleStartUtcTicks = utcNow.Ticks;
            meatFestivalStartUtcTicks = utcNow.Ticks;
            Save();
            OnChanged?.Invoke();
        }

        /// <summary>이 매니저가 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.</summary>
        public void WriteTo(OJ.Core.SaveState state)
        {
            // 옛 PlayerPrefs 두 키와 같은 집합이다(7.5 에서 그 키를 지웠고, 이제 여기가 유일한
            // 저장 경로다). 두 타이머 다 조건 없이 나간다 — 레벨이나 개수와 달리 tick 에는
            // "0 이면 안 가진 것"이라는 해석이 없어서 걸러 낼 기준 자체가 없다.
            state.Idle.AutoBattleStartUtcTicks = autoBattleStartUtcTicks;
            state.Idle.MeatFestivalStartUtcTicks = meatFestivalStartUtcTicks;
        }

        /// <summary>영구 상태를 <paramref name="state"/> 에서 읽어 온다.</summary>
        public void ReadFrom(OJ.Core.SaveState state)
        {
            // "0 이하면 fallback(=현재 시각)" 규칙. 파일에 0 이 들어 있을 수 있고(필드가 빠진
            // 세이브, 손으로 고친 파일), IdleRewardFormula.ElapsedSeconds 는 start <= 0 을
            // 경과 0d 로 눌러 버린다. 그대로 두면 타이머가 영원히 안 차는 상태로 굳는다.
            // 생성자가 이미 현재 시각을 넣어 뒀지만 여기서 다시 막아야 한다 — ReadFrom 은
            // 생성자가 세운 값을 무조건 덮기 때문이다.
            long fallbackUtcTicks = DateTime.UtcNow.Ticks;
            autoBattleStartUtcTicks = state.Idle.AutoBattleStartUtcTicks > 0
                ? state.Idle.AutoBattleStartUtcTicks
                : fallbackUtcTicks;
            meatFestivalStartUtcTicks = state.Idle.MeatFestivalStartUtcTicks > 0
                ? state.Idle.MeatFestivalStartUtcTicks
                : fallbackUtcTicks;

            // 옛 LoadOrInitialize 의 "저장된 시각이 미래면 현재로 당긴다" 보정은 7.5 에서도
            // 가져오지 않았다. 그 보정은 8.7 시점에 이미 죽어 있었기 때문이다 — 생성자가
            // PlayerPrefs 값을 당겨 놔도 그 직후 ReadFrom 이 파일 값으로 통째로 덮었다.
            // 지금 새로 넣는 것은 저장 매체 교체가 아니라 동작 추가다.
            //
            // 미래 tick 이 들어와도 보상이 새지는 않는다. ElapsedSeconds 가 now <= start 를
            // 경과 0d 로 막는다. 대신 그 시각이 될 때까지 게이지가 안 찬다 — 기기 시계를
            // 앞으로 돌렸다 되돌린 경우가 여기 해당한다. 되살릴 값어치가 있다고 판단되면
            // 생성자가 읽어 둔 부팅 시각을 필드로 기억해 그것으로 당겨라. 여기서 UtcNow 를
            // 새로 읽으면 기준점이 생성자 경로와 어긋난다. 어느 쪽이든 로드 직후 상태가
            // 파일과 달라지므로 SaveService.VerifyRoundTrip 이 "ReadFrom 이 빠뜨렸다"고
            // 찍는다는 것까지 같이 봐야 한다(바로 위 0 이하 fallback 도 같은 성질이다).

            // 여기서 Save() 를 부르지 않는다. ReadFrom 은 SaveService.TryLoadAll 의 owners
            // 루프 안에서 불리는데, 그 안에서 SaveAll 을 부르면 <b>아직 ReadFrom 이 돌지 않은
            // 뒤쪽 매니저까지 Capture 에 끌려 들어가 생성자 기본값 상태로 파일에 굳는다.</b>
            // 지금은 이 매니저가 마지막 소유자라 우연히 안전하지만, 등록 순서는 계약이 아니다.
            // (메모리 상태는 result.State 에서 계속 읽으므로 멀쩡하고 파일만 깨진다 —
            //  그래서 그 자리에서는 아무 증상도 안 보이고 다음 실행에 드러난다.)
        }

        /// <summary>
        /// 타이머가 바뀐 자리에서 즉시 저장한다.
        /// </summary>
        /// <remarks>
        /// 7.5: PlayerPrefs 대신 통합 세이브를 쓴다. <b>호출 지점(수령·리셋)을 그대로 두는 것이
        /// 중요하다</b> — 여기서 즉시 저장하지 않으면 앱이 백그라운드로 갈 때까지 진행도가
        /// 메모리에만 남고, 모바일에서 OS 가 프로세스를 죽이는 것은 일상이다. 저장 매체만
        /// 바꾸는 것이지 저장 시점을 미루는 것이 아니다.
        ///
        /// <c>?.</c> 가 필요하다. 매니저 생성자가 도는 시점에는 <c>SaveService</c> 가 아직
        /// 없다(컨테이너가 매니저를 전부 만든 뒤에 해석한다). 생성자에서 간접적으로 여기까지
        /// 오는 경로가 생기면 조용히 건너뛰는 것이 맞다 — 그때는 저장할 내용도 아직 없다.
        ///
        /// 파일 전체를 쓰지만 매니저별로 나눠 쓸 방법은 없다. 통합 세이브에서 저장 단위는
        /// 매니저가 아니라 파일이다(SaveService 주석 참고).
        /// </remarks>
        private void Save() => GameContainer.SaveService?.SaveAll();

        // DateTime 을 받는 얇은 어댑터로만 남긴다. 계산 본체는 OJ.Core 로 옮겼고,
        // 호출부가 넘기는 것이 utcNow.Ticks 라는 사실(로컬 시각이 아니라 UTC)은 여기서만 보장된다.
        private static double GetElapsedSeconds(long startUtcTicks, DateTime utcNow)
        {
            return IdleRewardFormula.ElapsedSeconds(startUtcTicks, utcNow.Ticks);
        }

        private int BuildRewardSeed(int stageIndex)
        {
            unchecked
            {
                return ((int)autoBattleStartUtcTicks * 397) ^ (int)(autoBattleStartUtcTicks >> 32) ^ stageIndex;
            }
        }
    }
}
