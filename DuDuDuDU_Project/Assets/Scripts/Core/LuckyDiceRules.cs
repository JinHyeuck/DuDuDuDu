namespace OJ.Core
{
    /// <summary>
    /// 행운 주사위의 순수 규칙. 엔진을 쓰지 않는다.
    ///
    /// SP 를 <see cref="CostSp"/> 내고 주사위를 굴려 <b>마석 1~10개</b>를 얻는다. 확률은 낮은 눈이 흔하고 높은 눈이
    /// 드물다 — 기대값은 약 4개라 마석 수급(웨이브당 2개)에 숨통을 틔우되, 큰 눈은 "터졌다" 의 쾌감이 되게 했다.
    ///
    /// <b>표를 한 곳에 둔다.</b> 화면의 확률 툴팁과 실제 굴림이 같은 표를 읽어야 "표기와 다르다" 는 사고가 안 난다.
    /// 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class LuckyDiceRules
    {
        /// <summary>한 번 굴리는 데 드는 SP.</summary>
        public const int CostSp = 100;

        public const int MinStones = 1;
        public const int MaxStones = 10;

        // [마석 개수 - 1] = 퍼센트. 합은 100 이다(테스트가 지킨다).
        private static readonly int[] chance = { 20, 16, 14, 12, 10, 8, 7, 6, 4, 3 };

        /// <summary><paramref name="stones"/> 개가 나올 확률(퍼센트). 범위 밖이면 0.</summary>
        public static int Chance(int stones)
        {
            if (stones < MinStones || stones > MaxStones)
                return 0;

            return chance[stones - 1];
        }

        /// <summary>
        /// 굴린다. <paramref name="roll"/> 은 0 이상 100 미만의 정수 — 난수는 부르는 쪽이 준다
        /// (엔진 난수를 여기서 부르면 테스트에서 분포를 못 잰다).
        /// </summary>
        public static int Roll(int roll)
        {
            int cumulative = 0;
            for (int stones = MinStones; stones <= MaxStones; stones++)
            {
                cumulative += chance[stones - 1];
                if (roll < cumulative)
                    return stones;
            }

            return MaxStones;
        }

        // ── 웨이브 이벤트판(2026-10-27) ──────────────────────────────────
        // SP 를 내고 굴리는 옛 버튼과 따로, 웨이브 사이 이벤트에서 "마석 5~15개" 를 굴린다. 가운데(10)가 흔한 종 모양이다.

        public const int EventMinStones = 5;
        public const int EventMaxStones = 15;

        // [마석 개수 - EventMinStones] = 퍼센트. 합은 100 이다(테스트가 지킨다).
        private static readonly int[] eventChance = { 5, 7, 9, 11, 12, 14, 12, 11, 9, 6, 4 };

        /// <summary>이벤트에서 <paramref name="stones"/> 개가 나올 확률(퍼센트). 범위 밖이면 0.</summary>
        public static int EventChance(int stones)
        {
            if (stones < EventMinStones || stones > EventMaxStones)
                return 0;

            return eventChance[stones - EventMinStones];
        }

        /// <summary>이벤트 굴림. <paramref name="roll"/> 은 0 이상 100 미만의 정수.</summary>
        public static int RollEvent(int roll)
        {
            int cumulative = 0;
            for (int stones = EventMinStones; stones <= EventMaxStones; stones++)
            {
                cumulative += eventChance[stones - EventMinStones];
                if (roll < cumulative)
                    return stones;
            }

            return EventMaxStones;
        }

        /// <summary>기대 마석 개수(100 을 곱한 정수 — 소수점 없이 비교하려는 것이다). 400 이면 평균 4.00개.</summary>
        public static int ExpectedStonesTimes100()
        {
            int sum = 0;
            for (int stones = MinStones; stones <= MaxStones; stones++)
                sum += stones * chance[stones - 1];

            return sum;
        }
    }
}
