using System.Collections.Generic;

namespace OJ.Pinball
{
    /// <summary>
    /// 보상 라운드에서 주사위 5개가 만든 족보. <b>값의 크기가 그대로 순위다</b> —
    /// 뒤로 갈수록 공이 많이 나간다. <see cref="BonusDiceRules.DefaultBallTable"/> 가
    /// 이 순서에 의존하므로 <b>중간에 끼워 넣지 말 것.</b>
    /// </summary>
    public enum DiceHand
    {
        /// <summary>아무것도 아님. <b>그래도 공은 나간다</b> — 꽝이 있으면 축하가 아니게 된다.</summary>
        None = 0,
        OnePair = 1,
        TwoPair = 2,
        Triple = 3,
        /// <summary>4연속.</summary>
        SmallStraight = 4,
        FullHouse = 5,
        /// <summary>5연속.</summary>
        LargeStraight = 6,
        FourOfAKind = 7,
        FiveOfAKind = 8,
    }

    /// <summary>
    /// 보상 라운드의 판정 규칙. <b>UnityEngine 타입도, ScriptableObject 도, 싱글톤도 쓰지 않는다.</b>
    /// <see cref="PinballRules"/> 와 같은 규약이고, 이유도 같다 — 헤드리스 EditMode 러너에서
    /// SO 생성과 <c>GameContainer</c> 가 돌지 않기 때문이다.
    ///
    /// <b>카운트 누적에 <see cref="PinballRules.AdvanceGauge"/> 를 쓰지 않는다.</b>
    /// 그쪽은 임계치를 넘으면 <i>나머지를 남기고 되감아</i> 여러 번 지급한다 — 판을 넘어
    /// 계속 도는 핀볼 게이지에는 맞지만, 보상 라운드의 핀은 <b>한 라운드에 한 번만</b>
    /// 채워져야 한다. 되감기면 같은 핀이 무한히 보상을 뱉는다.
    /// </summary>
    public static class BonusDiceRules
    {
        /// <summary>주사위 눈의 최댓값. 족보가 d6 을 전제한다(스트레이트 판정).</summary>
        public const int MaxPip = 6;

        /// <summary>
        /// 족보별 기본 공 개수. 인덱스가 <see cref="DiceHand"/> 값이다.
        /// 실제 값은 <c>BonusDiceDatabase</c> 가 덮으며, 이것은 표가 비었을 때의 바닥이다.
        /// </summary>
        public static readonly int[] DefaultBallTable = { 3, 5, 8, 12, 15, 20, 25, 35, 60 };

        /// <summary>
        /// 주사위 눈 목록에서 족보를 판정한다.
        ///
        /// <b>큰 족보부터 본다.</b> 한 손패가 여러 족보를 동시에 만족할 수 있어서다 —
        /// <c>1,1,2,3,4</c> 는 원페어이면서 4연속이고, <c>1,2,3,4,6</c> 은 노페어이면서 4연속이다.
        /// 위에서부터 내려오면 항상 가장 좋은 것이 잡힌다.
        ///
        /// <b>1~6 밖의 눈은 없는 것으로 친다.</b> 굴림이 깨졌을 때 엉뚱한 족보가 나오는 것보다
        /// 최하위로 떨어지는 편이 안전하다 — 이 컨텐츠는 최하위도 보상이 있다.
        /// </summary>
        public static DiceHand EvaluateHand(IReadOnlyList<int> dice)
        {
            if (dice == null || dice.Count == 0)
                return DiceHand.None;

            // counts[0] 은 안 쓴다. 눈 값을 그대로 인덱스로 쓰려는 것이다.
            var counts = new int[MaxPip + 1];
            for (int i = 0; i < dice.Count; i++)
            {
                int pip = dice[i];
                if (pip >= 1 && pip <= MaxPip)
                    counts[pip]++;
            }

            int maxOfAKind = 0;
            int pairs = 0;
            bool hasTriple = false;

            for (int pip = 1; pip <= MaxPip; pip++)
            {
                int n = counts[pip];
                if (n > maxOfAKind) maxOfAKind = n;
                if (n == 2) pairs++;
                if (n == 3) hasTriple = true;
            }

            if (maxOfAKind >= 5) return DiceHand.FiveOfAKind;
            if (maxOfAKind == 4) return DiceHand.FourOfAKind;
            if (HasRun(counts, 5)) return DiceHand.LargeStraight;
            if (hasTriple && pairs >= 1) return DiceHand.FullHouse;
            if (HasRun(counts, 4)) return DiceHand.SmallStraight;
            if (maxOfAKind == 3) return DiceHand.Triple;
            if (pairs >= 2) return DiceHand.TwoPair;
            if (pairs == 1) return DiceHand.OnePair;

            return DiceHand.None;
        }

        /// <summary><paramref name="length"/> 개가 연속으로 있는 구간이 있는가.</summary>
        private static bool HasRun(int[] counts, int length)
        {
            int run = 0;
            for (int pip = 1; pip <= MaxPip; pip++)
            {
                run = counts[pip] > 0 ? run + 1 : 0;
                if (run >= length)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 그 족보로 나가는 공의 개수.
        ///
        /// <paramref name="table"/> 이 비었거나 그 족보 자리가 없으면
        /// <see cref="DefaultBallTable"/> 로 내려간다. <b>0 을 돌려주지 않는다</b> —
        /// 표를 빠뜨린 것이 "공이 한 발도 안 나가는 라운드"로 드러나면,
        /// 설정 실수가 그대로 유저의 꽝이 된다.
        /// </summary>
        public static int BallCountFor(DiceHand hand, IReadOnlyList<int> table)
        {
            int index = (int)hand;
            if (index < 0)
                index = 0;

            if (table != null && index < table.Count && table[index] > 0)
                return table[index];

            return index < DefaultBallTable.Length ? DefaultBallTable[index] : DefaultBallTable[0];
        }

        // ── 특수 핀 카운트 ─────────────────────────────────────

        /// <summary>
        /// 특수 핀 카운트를 <paramref name="step"/> 만큼 올린다.
        /// 임계치에 <b>처음 닿는 순간에만</b> 보상이 나가고, 그 뒤로는 아무리 맞아도
        /// 다시 나가지 않는다. 그래서 반환값은 <paramref name="required"/> 에서 멈춘다.
        ///
        /// <b>카운트를 임계치에 가두는 이유.</b> "채웠는가"가
        /// <c>count &gt;= required</c> 하나로 판정된다 — 따로 「수령함」 집합을 두면
        /// 그 둘이 어긋나는 날이 오고, 세이브에도 한 줄이 더 늘어난다.
        ///
        /// <paramref name="required"/> 가 0 이하면 <b>올리지도, 주지도 않는다</b> —
        /// 보상이 안 걸린 태그라는 뜻이다. <see cref="PinballRules.AdvanceGauge"/> 와 같은 판단이다.
        /// </summary>
        /// <param name="justCompleted">이번 호출로 처음 채워졌는가. 보상은 이것이 true 일 때만 나간다.</param>
        /// <returns>반영 후의 카운트. 항상 0 이상 <paramref name="required"/> 이하.</returns>
        public static int AdvanceCount(int current, int required, int step, out bool justCompleted)
        {
            justCompleted = false;

            int safe = current < 0 ? 0 : current;
            if (required <= 0 || step <= 0)
                return safe;

            if (safe >= required)
                return required;        // 이미 채웠다. 더 올리지도, 다시 주지도 않는다

            // long 으로 올린다. 공이 한꺼번에 많이 나가는 구조라 step 이 클 수 있다.
            long total = (long)safe + step;
            if (total >= required)
            {
                justCompleted = true;
                return required;
            }

            return (int)total;
        }

        /// <summary>
        /// 그 핀이 다 채워졌는가. <paramref name="required"/> 가 0 이하면
        /// 보상이 안 걸린 태그라 「채울 것이 없다」 — 광고 버튼을 띄우는 조건에서도 빠진다.
        /// </summary>
        public static bool IsComplete(int count, int required)
        {
            return required > 0 && count >= required;
        }

        // ── 락 ─────────────────────────────────────────────

        /// <summary>
        /// 이번 굴림에서 실제로 다시 굴려야 하는 주사위의 수.
        /// <paramref name="locks"/> 가 짧거나 null 이면 그 자리는 잠기지 않은 것으로 본다.
        /// </summary>
        public static int UnlockedCount(int diceCount, IReadOnlyList<bool> locks)
        {
            if (diceCount <= 0)
                return 0;

            if (locks == null)
                return diceCount;

            int n = 0;
            for (int i = 0; i < diceCount; i++)
            {
                if (i >= locks.Count || !locks[i])
                    n++;
            }
            return n;
        }
    }
}
