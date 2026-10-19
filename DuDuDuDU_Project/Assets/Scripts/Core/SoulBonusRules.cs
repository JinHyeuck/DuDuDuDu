using System;

namespace OJ.Core
{
    /// <summary>
    /// 영혼 보너스의 순수 규칙. 엔진을 쓰지 않는다.
    ///
    /// 적을 처치하면 영혼이 저금통에 쌓이고, <b>목표 마릿수</b>에 닿으면 보상 하나를 고른다 — <b>SP</b> 또는 <b>마석</b>.
    /// 목표는 첫 <see cref="FirstGoal"/> 마리에서 시작해 <b>받을 때마다 <see cref="GoalStep"/> 마리씩 늘어난다</b>(10 → 15 → 20 …).
    ///
    /// SP 값은 스테이지 SP 총량(시작 + 웨이브 클리어 × 웨이브 수)의 3%~5% 이다(저금통 시절 "지금 바로" 값을 그대로 가져왔다).
    /// 마석은 <see cref="MinStones"/>~<see cref="MaxStones"/> 개를 균등하게 굴린다. 난수는 호출자가 준다
    /// (엔진 난수를 여기서 부르면 분포를 못 잰다). 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class SoulBonusRules
    {
        /// <summary>첫 목표 마릿수.</summary>
        public const int FirstGoal = 10;

        /// <summary>보상을 받을 때마다 다음 목표가 늘어나는 마릿수.</summary>
        public const int GoalStep = 5;

        /// <summary>SP 보상의 하한. 총량이 작은 판(튜토리얼)에서 값이 너무 작아지지 않게 한다.</summary>
        public const int MinSp = 30;

        /// <summary>SP 보상이 스테이지 SP 총량의 몇 천분율인가(30 = 3.0%, 50 = 5.0%). 양 끝을 포함한다.</summary>
        public const int SpMinPermille = 30;
        public const int SpMaxPermille = 50;

        /// <summary>마석 보상 개수의 범위. 양 끝을 포함한다.</summary>
        public const int MinStones = 4;
        public const int MaxStones = 13;

        /// <summary>
        /// <paramref name="goalIndex"/> 번째(0부터) 목표 마릿수. 10 · 15 · 20 · 25 …
        /// 음수는 첫 목표로 본다. 아주 큰 값에서도 넘치지 않는다.
        /// </summary>
        public static int GoalFor(int goalIndex)
        {
            if (goalIndex < 0)
                goalIndex = 0;

            long goal = (long)FirstGoal + (long)GoalStep * goalIndex;
            return goal > int.MaxValue ? int.MaxValue : (int)goal;
        }

        /// <summary>
        /// 스테이지 SP 총량 = 시작 SP + 웨이브 클리어 SP × 웨이브 수. 현상금 보상은 넣지 않는다(판마다 다르다).
        /// 1~10스테이지는 모두 100 + 115 × 15 = 1,825 이다.
        /// </summary>
        public static int StageSpTotal(int initialSp, int waveClearSp, int totalWaves)
        {
            long total = (long)Math.Max(0, initialSp) + (long)Math.Max(0, waveClearSp) * Math.Max(0, totalWaves);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>
        /// SP 보상. 총량의 3%~5%(정수 내림)에 <paramref name="curvePercent"/>(웨이브 곡선 배율, 기본 100)을 곱하고 하한 <see cref="MinSp"/> 을 건다.
        /// 곡선을 곱하는 이유 — 초반에 큰 보상이 한 번에 들어오면 웨이브 SP 곡선(<c>WaveSpCurve</c>)으로 줄여 둔 초반 소환 횟수가 다시 부푼다.
        /// <paramref name="roll"/> 은 0 이상의 정수.
        /// </summary>
        public static int RollSp(int stageTotal, int roll, int curvePercent = 100)
        {
            int permille = SpMinPermille + Wrap(roll, SpMaxPermille - SpMinPermille + 1);
            long raw = (long)Math.Max(0, stageTotal) * permille / 1000;
            raw = raw * Math.Max(0, curvePercent) / 100;
            return (int)Math.Max(MinSp, Math.Min(raw, int.MaxValue));
        }

        /// <summary>마석 보상. <see cref="MinStones"/>~<see cref="MaxStones"/> 개. <paramref name="roll"/> 은 0 이상의 정수.</summary>
        public static int RollStones(int roll)
        {
            return MinStones + Wrap(roll, MaxStones - MinStones + 1);
        }

        private static int Wrap(int roll, int count)
        {
            int r = roll % count;
            return r < 0 ? r + count : r;
        }
    }
}
