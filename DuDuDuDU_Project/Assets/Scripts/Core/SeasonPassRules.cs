using System;
using System.Globalization;

namespace OJ.Core
{
    /// <summary>
    /// 시즌 패스의 순수 규칙. 목록과 값을 인자로 받는 <c>static</c> 뿐이라
    /// 헤드리스 러너에서 전부 돈다.
    ///
    /// <b>시즌은 달력 한 달이다.</b> 기획서 7.2 는 28일 고정을 적어 두었지만(요일 고정·
    /// 멤버십 만료일 분리) 달력 월로 가기로 정했다. 그래서 시즌 길이가 28~31일로 들쭉날쭉한데,
    /// <b>레벨 요구치를 날짜로 환산하지 않는 이유가 그것이다</b> — 길이가 달라도 "고기를
    /// 얼마나 썼는가" 라는 축은 그대로다.
    /// </summary>
    public static class SeasonPassRules
    {
        /// <summary>
        /// 시즌을 가리키는 키. <c>yyyy-MM</c> 이며 <b>세이브에 그대로 들어간다.</b>
        ///
        /// <b>로컬 날짜다.</b> 일일 미션·상점 리셋과 같은 기준이어야 "오늘" 이 한 가지 뜻이 된다.
        /// 문화권을 안 타게 <see cref="CultureInfo.InvariantCulture"/> 를 명시한다 —
        /// 기기 로캘에 따라 형식이 바뀌면 시즌이 매번 새로 시작하거나 영영 안 바뀐다.
        /// </summary>
        public static string SeasonId(DateTime localNow)
        {
            return localNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        /// <summary>이 시즌이 끝나는 순간(다음 달 1일 0시). 경계는 <b>미만</b>이다.</summary>
        public static DateTime SeasonEnd(DateTime localNow)
        {
            DateTime firstOfThisMonth = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, localNow.Kind);
            return firstOfThisMonth.AddMonths(1);
        }

        /// <summary>
        /// 시즌 종료까지 남은 시간. 화면의 "8일 17시간" 이 이 값이다.
        /// 음수는 0 으로 접는다(기기 시계를 앞으로 돌린 경우).
        /// </summary>
        public static TimeSpan TimeUntilSeasonEnd(DateTime localNow)
        {
            TimeSpan remaining = SeasonEnd(localNow) - localNow;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        /// <summary>
        /// 누적 포인트로 도달한 레벨. <b>1부터 시작한다</b> —
        /// 화면이 "레벨 1 / 0 / 1000" 으로 열리고 1레벨 보상은 그 자리에서 받을 수 있다.
        ///
        /// <paramref name="pointsPerLevel"/> 이 0 이하면 데이터 사고다. 그때는 1 레벨에
        /// 묶어 둔다 — 0 으로 나누는 것을 막는 것이자, 전부 최대 레벨로 열려 보상이
        /// 통째로 나가는 것을 막는 것이다.
        /// </summary>
        public static int LevelFor(int points, int pointsPerLevel, int maxLevel)
        {
            if (maxLevel < 1)
                return 1;

            if (pointsPerLevel <= 0 || points <= 0)
                return 1;

            long level = 1L + points / pointsPerLevel;
            return level >= maxLevel ? maxLevel : (int)level;
        }

        /// <summary>
        /// 지금 레벨 안에서 쌓은 포인트. 화면 게이지의 분자다.
        /// <b>최대 레벨에서는 요구치를 그대로 돌려준다</b>(꽉 찬 게이지) —
        /// 넘긴 만큼을 보여 주면 "더 쌓이는데 안 오른다" 로 읽힌다.
        /// </summary>
        public static int PointsIntoLevel(int points, int pointsPerLevel, int maxLevel)
        {
            if (pointsPerLevel <= 0)
                return 0;

            if (LevelFor(points, pointsPerLevel, maxLevel) >= maxLevel)
                return pointsPerLevel;

            int into = points % pointsPerLevel;
            return into < 0 ? 0 : into;
        }

        /// <summary>게이지 비율(0~1).</summary>
        public static float LevelProgress(int points, int pointsPerLevel, int maxLevel)
        {
            if (pointsPerLevel <= 0)
                return 0f;

            return (float)PointsIntoLevel(points, pointsPerLevel, maxLevel) / pointsPerLevel;
        }

        /// <summary>
        /// 그 레벨에 닿는 데 필요한 누적 포인트. 1 레벨은 0 이다.
        /// 화면이 잠긴 칸에 "얼마나 더" 를 적을 때 쓴다.
        /// </summary>
        public static int PointsRequiredFor(int level, int pointsPerLevel)
        {
            if (level <= 1 || pointsPerLevel <= 0)
                return 0;

            long required = (long)(level - 1) * pointsPerLevel;
            return required >= int.MaxValue ? int.MaxValue : (int)required;
        }

        /// <summary>
        /// 그 레벨의 보상을 받을 수 있는가.
        ///
        /// <b>유료 트랙은 활성화돼 있어야 한다.</b> 그리고 활성화하면 <b>이미 지나간 레벨도
        /// 전부 받을 수 있다</b> — 기획서 7.3 의 소급 지급이다. 늦게 사도 손해가 없어야
        /// 결제 시점이 시즌 내내 열려 있다. 그래서 여기서 "언제 샀는가" 를 보지 않는다.
        /// </summary>
        public static bool CanClaim(int level, int reachedLevel, bool alreadyClaimed, bool premiumTrack, bool premiumUnlocked)
        {
            if (alreadyClaimed || level > reachedLevel)
                return false;

            return !premiumTrack || premiumUnlocked;
        }
    }
}
