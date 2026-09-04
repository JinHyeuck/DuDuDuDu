using System;

namespace OJ.Core
{
    /// <summary>
    /// 방치 보상의 시간 환산 공식만 모은 순수 함수 모음.
    /// </summary>
    /// <remarks>
    /// 여기 있는 식은 골든 기준선(Tests/Golden/formula_baseline.txt)의 idle.conversion 절이
    /// 값 단위로 고정하고 있다. 그래서 이 파일의 목적은 "좋은 코드"가 아니라 "같은 값"이다.
    /// 항의 순서, 괄호, 중간 변수의 타입(double/int/float)이 전부 계약의 일부다.
    ///
    /// OJ.Core 는 Assembly-CSharp 을 참조할 수 없으므로 파라미터는 전부 기본형이다.
    /// 상수(8시간 / 20분 / 6시간 / 30세트)는 호출부인 IdleRewardManager 가 정본으로 들고 있고,
    /// 여기로 복사해 오지 않는다 — 같은 숫자가 두 곳에 있으면 한쪽만 고쳐지는 사고가 난다.
    /// </remarks>
    public static class IdleRewardFormula
    {
        /// <summary>
        /// 시작 시각(UTC ticks)부터 현재(UTC ticks)까지의 경과 초.
        /// </summary>
        /// <remarks>
        /// 가드 두 개는 원본 IdleRewardManager.GetElapsedSeconds 를 그대로 옮긴 것이다.
        /// startUtcTicks 가 0 이하면 저장값이 없거나 깨진 상태이고, now 가 start 보다 앞서면
        /// 기기 시계가 뒤로 돌아간 상태다. 둘 다 음수 경과를 만들어 보상 계산이 뒤집히므로
        /// 0d 로 눌러 막는다. 가드를 Math.Max 한 방으로 합치면 startUtcTicks <= 0 케이스가
        /// 사라지므로 합치지 마라.
        ///
        /// TimeSpan.FromTicks(...).TotalSeconds 를 (now - start) / 10000000.0 으로 펴지 마라.
        /// TotalSeconds 는 ticks 에 1e-07 을 곱하는 구현이라 1e7 로 나누는 식과 마지막 비트가
        /// 달라질 수 있고, 그 한 비트가 골든 기준선을 깬다.
        /// </remarks>
        public static double ElapsedSeconds(long startUtcTicks, long nowUtcTicks)
        {
            if (startUtcTicks <= 0 || nowUtcTicks <= startUtcTicks)
                return 0d;

            return TimeSpan.FromTicks(nowUtcTicks - startUtcTicks).TotalSeconds;
        }

        /// <summary>
        /// 경과 초에 방치 보상 상한을 적용한다.
        /// </summary>
        /// <remarks>
        /// 원본 GetAutoBattleElapsed 의 Math.Min(AutoBattleMaxSeconds, seconds) 를 그대로 옮겼다.
        /// 인자 순서를 뒤집지 마라 — Math.Min(double, double) 은 두 값이 같을 때 ±0 의 부호를,
        /// 한쪽이 NaN 일 때 반환값을 첫 번째 인자 기준으로 결정한다. 즉 순서가 결과의 일부다.
        /// </remarks>
        public static double CappedElapsedSeconds(double elapsedSeconds, double maxSeconds)
        {
            return Math.Min(maxSeconds, elapsedSeconds);
        }

        /// <summary>
        /// 상한이 적용된 경과 초를 자동 전투 클리어 횟수(소수 포함)로 환산한다.
        /// </summary>
        /// <remarks>
        /// 원본 식은 GetAutoBattleElapsed(utcNow).TotalSeconds / SecondsPerAutoBattleClear 다.
        /// 호출부는 반드시 TimeSpan 을 한 번 거친 초를 넘겨야 한다. TimeSpan.FromSeconds 는
        /// 이 런타임에서 밀리초 단위로 반올림하므로, 상한만 적용한 raw double 을 그대로 넘기면
        /// 서브밀리초 자리가 살아남아 값이 달라진다. 편의를 위해 CappedElapsedSeconds 의 결과를
        /// 바로 여기에 물리고 싶어지겠지만, 그건 다른 계산이다.
        ///
        /// 나눗셈을 (1.0 / secondsPerClear) 곱셈으로 바꾸는 "최적화"도 금지다. 역수는 정확히
        /// 표현되지 않아 마지막 자리가 어긋난다.
        /// </remarks>
        public static double AutoBattleClearCount(double cappedElapsedSeconds, double secondsPerClear)
        {
            return cappedElapsedSeconds / secondsPerClear;
        }

        /// <summary>
        /// 쌓인 고기 세트 수. 상한에 걸리면 그 이상은 쌓이지 않는다.
        /// </summary>
        /// <remarks>
        /// 여기 들어오는 경과 초는 상한이 걸리지 않은 값이다 — 고기는 자동 전투와 달리
        /// 8시간 상한이 아니라 세트 수 상한(maxSetCount)으로 막힌다. 원본이 그렇게 동작하고,
        /// 골든 기준선의 idle.meatSets[100000] = 4 가 그 증거다(상한을 걸었다면 1이 나온다).
        ///
        /// Math.Floor 를 (int) 캐스팅만으로 대체하지 마라. 캐스팅은 0 방향 절삭이라 음수 입력에서
        /// 결과가 갈린다. 지금은 뒤의 Clamp 가 결과적으로 같은 값을 만들어 주지만, 그건 우연히
        /// 같아지는 것이지 같은 식이 아니다.
        ///
        /// OJMath.Clamp 는 UnityEngine 의 Mathf.Clamp 를 그대로 옮긴 것이라 경계 동작이 같다.
        /// (11.1 에서 OJ.Core 의 엔진 참조를 끊었다. 이 주석은 그 전에 쓰인 것이다.)
        /// </remarks>
        public static int StoredMeatSetCount(double elapsedSeconds, double intervalSeconds, int maxSetCount)
        {
            int setCount = (int)Math.Floor(elapsedSeconds / intervalSeconds);
            return OJMath.Clamp(setCount, 0, maxSetCount);
        }

        /// <summary>
        /// 다음 고기 세트가 채워질 때까지 남은 초.
        /// </summary>
        /// <remarks>
        /// 원본 GetTimeUntilNextMeatSet 의 중간 변수 두 개를 이름까지 그대로 유지했다.
        /// intervalSeconds - (elapsedSeconds % intervalSeconds) 한 줄로 합치지 마라. 값은 같아도
        /// 원본과 1:1로 대조할 수 없게 되고, 이 파일의 유일한 방어선이 그 대조다.
        ///
        /// 마지막 Math.Max(0d, ...) 는 원본이 남긴 방어선이다. 정상 경로에서는 남은 시간이
        /// (0, interval] 이라 음수가 나오지 않지만, 지우면 비정상 입력에서 동작이 달라지므로 둔다.
        ///
        /// 세트 상한 도달 시 0을 돌려주는 분기는 여기가 아니라 호출부에 있다. 상한 판정에는
        /// StoredMeatSetCount 가 필요한데 그건 이 함수의 책임이 아니고, 옮기는 순간
        /// 원본과 호출 순서가 달라진다.
        /// </remarks>
        public static double SecondsUntilNextMeatSet(double elapsedSeconds, double intervalSeconds)
        {
            double secondsIntoInterval = elapsedSeconds % intervalSeconds;
            double remainingSeconds = intervalSeconds - secondsIntoInterval;
            return Math.Max(0d, remainingSeconds);
        }

        /// <summary>
        /// 방치 보상 게이지 진행도 0~1.
        /// </summary>
        /// <remarks>
        /// 원본: OJMath.Clamp01((float)(GetAutoBattleElapsed().TotalSeconds / AutoBattleMaxSeconds)).
        /// double 로 나눈 뒤 → float 으로 내리고 → Clamp01 하는 순서를 지켜라.
        /// 인자를 float 으로 먼저 내려서 나누거나, double 상태에서 먼저 clamp 하면
        /// 반올림이 일어나는 지점이 달라져 마지막 자리가 어긋난다.
        ///
        /// OJMath.Clamp01 을 System.Math.Clamp 로 바꾸지 마라. float/double 정밀도가 다르다.
        /// </remarks>
        public static float Progress01(double cappedElapsedSeconds, double maxSeconds)
        {
            return OJMath.Clamp01((float)(cappedElapsedSeconds / maxSeconds));
        }
    }
}
