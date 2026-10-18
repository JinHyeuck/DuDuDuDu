using System;

namespace OJ.Core
{
    /// <summary>
    /// 저금통의 순수 규칙. 엔진을 쓰지 않는다. <b>방식이 둘이다</b>(DEV 치트로 전환 — <c>DevCheatFlags.SavingsTiered</c>).
    ///
    /// <b>선택(기본)</b>: 버튼 둘 — "지금 바로 N SP" / "45초 뒤 M SP". 어느 쪽을 골라도 45초 뒤에 다시 고른다. 비용이 없다.
    /// 즉시 값은 스테이지 SP 총량의 3%~5%, 나중 값은 즉시 값의 ×1.1 이상 ×3 미만이다(<see cref="RollOffer"/>).
    /// 밸런스는 맞추지 않았다 — 나중 쪽이 항상 이득이고 쉬워도 된다는 결정이다.
    ///
    /// <b>3단(이전 방식)</b>: SP 를 맡기면 시간이 지난 뒤 <b>원금보다 더 많이</b> 돌려받는다. 맡길 때 <b>셋 중 하나를 고른다</b> —
    /// 적게 맡고 빨리 받을지, 많이 맡고 오래 기다려 더 크게 받을지다. 고르는 것이 이 시스템의 재미다.
    /// 보유 SP 에 비례해 이자가 붙는 방식으로 하지 않은 이유는 "이자를 아는 유저" 와 모르는 유저의 격차가 벌어져서다.
    ///
    /// 선택지는 <b>맡길수록, 오래 기다릴수록 비율이 좋아진다</b>(원금 대비 +50% → +62% → +80%).
    /// 초당 이익도 0.50 → 0.56 → 0.67 SP 로 오른다 — 큰 선택지가 항상 낫다면 고를 이유가 없으니,
    /// 대신 <b>지금 가진 SP 와 웨이브 압박</b>이 고르는 기준이 된다(큰 것은 SP 를 오래 묶는다).
    /// 한 번에 하나만 맡긴다. 밸런스 값이라 정식 규칙으로 굳으면 SO 로 옮긴다(결정 3번).
    /// </summary>
    public static class SavingsRules
    {
        // ── 선택(기본) ───────────────────────────────────────────

        /// <summary>어느 쪽을 골라도 다음 선택까지 기다리는 시간(초). 게임 시간이다.</summary>
        public const float ChoiceCooldownSeconds = 45f;

        /// <summary>즉시 값의 하한(SP). 총량이 작은 판(튜토리얼)에서 값이 너무 작아지지 않게 한다.</summary>
        public const int MinInstant = 30;

        /// <summary>즉시 값이 스테이지 SP 총량의 몇 천분율인가(30 = 3.0%, 50 = 5.0%). 양 끝을 포함한다.</summary>
        public const int InstantMinPermille = 30;
        public const int InstantMaxPermille = 50;

        /// <summary>나중 값이 즉시 값의 몇 퍼센트인가. <b>110 이상 299 이하</b> = ×1.1 이상 ×3 미만. 양 끝을 포함한다.</summary>
        public const int DelayedMinPercent = 110;
        public const int DelayedMaxPercent = 299;

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
        /// 오퍼를 굴린다. 난수는 호출자가 준다(엔진 난수를 여기서 부르면 분포를 못 잰다) — 0 이상의 정수면 된다.
        /// <paramref name="instant"/> 는 총량의 3%~5%(하한 <see cref="MinInstant"/>), <paramref name="delayed"/> 는
        /// 그 값의 110%~299%(정수 내림)다. 나중 값은 언제나 즉시 값보다 크다.
        /// </summary>
        public static void RollOffer(int stageTotal, int rollA, int rollB, out int instant, out int delayed)
        {
            int permille = InstantMinPermille + Wrap(rollA, InstantMaxPermille - InstantMinPermille + 1);
            long raw = (long)Math.Max(0, stageTotal) * permille / 1000;
            instant = (int)Math.Max(MinInstant, raw);

            int percent = DelayedMinPercent + Wrap(rollB, DelayedMaxPercent - DelayedMinPercent + 1);
            delayed = (int)((long)instant * percent / 100);
        }

        private static int Wrap(int roll, int count)
        {
            int r = roll % count;
            return r < 0 ? r + count : r;
        }

        // ── 3단(이전 방식) ───────────────────────────────────────

        /// <summary>선택지 수.</summary>
        public const int OptionCount = 3;

        // [선택지] = 맡기는 SP / 돌려받는 SP / 걸리는 시간(초, 게임 시간).
        private static readonly int[] deposits = { 30, 40, 50 };
        private static readonly int[] payouts = { 45, 65, 90 };
        private static readonly float[] durations = { 30f, 45f, 60f };

        /// <summary>범위 밖 번호를 가장 가까운 선택지로 맞춘다.</summary>
        public static int ClampOption(int option)
        {
            if (option < 0)
                return 0;

            return option >= OptionCount ? OptionCount - 1 : option;
        }

        /// <summary>맡기는 금액(SP).</summary>
        public static int Deposit(int option)
        {
            return deposits[ClampOption(option)];
        }

        /// <summary>돌려받는 총액(SP). 원금을 포함한다.</summary>
        public static int Payout(int option)
        {
            return payouts[ClampOption(option)];
        }

        /// <summary>순이익(SP) = 돌려받는 총액 - 원금.</summary>
        public static int Gain(int option)
        {
            return Payout(option) - Deposit(option);
        }

        /// <summary>돌려받기까지 걸리는 시간(초).</summary>
        public static float Duration(int option)
        {
            return durations[ClampOption(option)];
        }

        /// <summary>가장 싼 선택지의 금액. "저금통을 쓸 수 있는가" 의 기준이다.</summary>
        public static int CheapestDeposit => deposits[0];
    }
}
