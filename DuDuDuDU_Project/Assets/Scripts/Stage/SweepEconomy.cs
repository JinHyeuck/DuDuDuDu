namespace OJ.Stage
{
    /// <summary>
    /// 고기 루프의 회수율 <b>r</b> 을 계산한다. (<c>Docs/CurrencyPolicy.md</c> 4장)
    ///
    /// <code>
    /// 고기 ──Sweep──> 티켓 ──Pinball_Base──> 고기
    /// </code>
    ///
    /// <b>r 은 어디에도 적혀 있지 않은 파생값이다.</b> 네 수치의 곱이고, 넷은 서로 다른
    /// 시점에 서로 다른 이유로 만져진다 — 티켓을 후하게 준 날, 핀볼 고기를 조금 올린 날,
    /// 소탕 비용을 낮춘 날. 아무도 r 을 건드린 적이 없는데 r 이 움직인다.
    ///
    /// 그리고 총 스태미나는 <c>공급 x 1/(1-r)</c> 로 수렴하는데, 이 승수는 위로 갈수록
    /// 급격하다 — 0.3 → 0.6 은 "두 배 버프"로 보이지만 승수는 1.43 → 2.5 다.
    /// <b>r 이 유일한 방어선이고 2차 방어선이 없다</b>(정책 4.2). 그래서 <c>SweepEconomyTests</c> 가
    /// 이 계산을 잠근다. 멤버십이 없는 유저에게는 하루 소탕 한도가 생겼지만(2026-10-06,
    /// <c>ShopDatabase.Membership.freeDailySweepLimit</c> = 72 = <see cref="DailySweepClears"/>)
    /// <b>고기 멤버십이 그 한도를 풀어 주므로</b> 멤버에게는 여전히 r 하나뿐이다.
    ///
    /// <b>왜 값을 여기 베껴 두는가.</b> 핀볼 칸 보상은 SO 에셋이 정본인데 헤드리스 러너에서는
    /// <c>ScriptableObject</c> 가 뜨지 않는다. 그래서 <b>선언값</b>을 코드에 두어 테스트가
    /// 두드리게 하고, 에셋과의 어긋남은 <c>PinballRewardDatabase.Validate</c> 가 잡는다.
    /// 에셋을 고치면 여기도 같이 고칠 것 — 안 고치면 Validate 가 말한다.
    /// </summary>
    public static class SweepEconomy
    {
        /// <summary>방치 자동전투의 하루 클리어 수. 시간당 1회 x 24시간 상한.</summary>
        public const int DailyIdleClears = 24;

        /// <summary>
        /// 소탕의 하루 클리어 수. 고기 유입 360(2시간마다 30 x 12) / 1회 5개.
        /// <see cref="OJ.IdleReward"/> 의 <c>MeatPerSet</c> 과 짝이다.
        /// </summary>
        public const int DailySweepClears = 72;

        /// <summary>핀볼 1발에 드는 티켓. <c>PinballRewardDatabase.ticketCost</c> 의 선언값.</summary>
        public const int DeclaredTicketCost = 1;

        /// <summary>
        /// 핀볼 판 5칸의 확률. <c>PinballBoard.asset</c> 의 선언 확률이고
        /// <c>PinballRules.DrawSlot</c> 이 이 표로 칸을 뽑는다.
        /// </summary>
        public static readonly float[] DeclaredSlotProbability = { 0.2475f, 0.2475f, 0.01f, 0.2475f, 0.2475f };

        /// <summary>
        /// 칸별 고기. <c>PinballRewardDatabase.asset</c> 의 선언값이다.
        ///
        /// <b>가운데가 잭팟이다.</b> 그 칸의 확률이 1% 뿐이라 25개를 넣어도 기댓값이
        /// 0.25 밖에 안 오른다 — 정책 5.1 의 "평소 조금 + 가끔 왕창"이 여기서 성립한다.
        ///
        /// <b>확률을 깎는 것이 핵심이었다.</b> 다섯 칸이 고른 확률(0.16~0.22)이면 잭팟을
        /// 만들 수가 없다. 발당 최소 1개씩만 줘도 96발이면 96개인데 회수율 상한
        /// (r = 0.35, 하루 126개)이 거의 차서 남는 몫이 하루 30개뿐이고, 그것을
        /// 다시 나누면 한 번에 몇 개가 안 된다.
        ///
        /// 바닥값은 0 으로 두지 않았다(정책 5.1) — 잭팟이 안 터져도 조금씩은 쌓여야 한다.
        /// </summary>
        public static readonly int[] DeclaredSlotStamina = { 1, 1, 25, 1, 1 };

        /// <summary>잭팟 칸의 인덱스. 확률이 가장 낮고 고기가 가장 많은 칸이다.</summary>
        public const int JackpotSlotIndex = 2;

        /// <summary>
        /// 테스트가 막는 상한. 목표는 0.3 이고 0.35 를 넘으면 실패다.
        /// 여유 0.05 는 반올림 때문이지 조정 여지가 아니다.
        /// </summary>
        public const float MaxRecoveryRate = 0.35f;

        /// <summary>핀볼 1발이 돌려주는 고기의 기댓값.</summary>
        public static float StaminaPerShot(float[] slotProbability, int[] slotStamina)
        {
            if (slotProbability == null || slotStamina == null)
                return 0f;

            int count = slotProbability.Length < slotStamina.Length
                ? slotProbability.Length
                : slotStamina.Length;

            float sum = 0f;
            for (int i = 0; i < count; i++)
                sum += slotProbability[i] * slotStamina[i];

            return sum;
        }

        /// <summary>선언값으로 계산한 발당 고기.</summary>
        public static float StaminaPerShot()
        {
            return StaminaPerShot(DeclaredSlotProbability, DeclaredSlotStamina);
        }

        /// <summary>하루에 쏘는 핀볼 발수. 클리어마다 티켓이 나오고 티켓으로 쏜다.</summary>
        public static int DailyShots(int idleClears, int sweepClears, int ticketPerClear, int ticketCost)
        {
            if (ticketCost < 1)
                return 0;

            int tickets = (idleClears + sweepClears) * ticketPerClear;
            return tickets / ticketCost;
        }

        /// <summary>하루에 소탕으로 태우는 고기.</summary>
        public static int DailyStaminaSpent(int sweepClears, int staminaCostPerSweep)
        {
            return sweepClears * staminaCostPerSweep;
        }

        /// <summary>
        /// 회수율 <b>r</b> = 핀볼이 돌려주는 고기 / 소탕이 태우는 고기.
        /// 총 스태미나는 <c>공급 x 1/(1-r)</c> 로 수렴한다.
        /// </summary>
        public static float RecoveryRate(int dailyShots, float staminaPerShot, int dailyStaminaSpent)
        {
            if (dailyStaminaSpent <= 0)
                return 0f;

            return dailyShots * staminaPerShot / dailyStaminaSpent;
        }

        /// <summary>지금 선언값 전체로 계산한 r.</summary>
        public static float RecoveryRate()
        {
            int shots = DailyShots(
                DailyIdleClears,
                DailySweepClears,
                StageRewardCalculator.PinballTicketPerClear,
                DeclaredTicketCost);

            int spent = DailyStaminaSpent(DailySweepClears, SweepRules.StaminaCostPerSweep);
            return RecoveryRate(shots, StaminaPerShot(), spent);
        }

        /// <summary>경제 승수 <c>1/(1-r)</c>. r 이 1 이상이면 발산이므로 0 을 돌려준다.</summary>
        public static float Multiplier(float recoveryRate)
        {
            if (recoveryRate < 0f || recoveryRate >= 1f)
                return 0f;

            return 1f / (1f - recoveryRate);
        }

        // ── 상위 재화 (Pinball_Bonus) ──────────────────────────────────

        /// <summary>
        /// 하루에 열리는 보너스 라운드 수. <b>가정값이다.</b>
        ///
        /// 보너스는 센터핀(tag 3) 게이지 25히트로 열리는데, 한 발이 그 핀을 몇 번 맞히는지는
        /// 판의 물리에 달려 있어 코드에서 셀 수가 없다. 하루 96발에 두 번 열린다고 보고
        /// 지급량을 잡았다 — <b>실측하면 이 값부터 고칠 것</b>이고, 고치면 아래 일일 페이스가
        /// 통째로 따라 움직인다.
        /// </summary>
        public const int AssumedBonusRoundsPerDay = 2;

        /// <summary>보너스 라운드 <b>1회 완주</b>가 주는 무료젬. 젬 핀 100 + 젬 대박 핀 50.</summary>
        public const int BonusFreeGemPerRound = 150;

        /// <summary>1회 완주 레어석. 레어석 핀.</summary>
        public const int BonusRareStonePerRound = 25;

        /// <summary>1회 완주 신화석. 신화석 핀.</summary>
        public const int BonusMythicStonePerRound = 20;

        /// <summary>1회 완주 유물권. 유물권 핀.</summary>
        public const int BonusRelicTicketPerRound = 2;

        /// <summary>
        /// 상위 재화의 일일 페이스. <b>이것이 게임 전체의 성장 속도다</b> —
        /// 티어 4~6 재화의 반복 수급처가 Pinball_Bonus 하나뿐이기 때문이다(정책 3.2).
        /// </summary>
        public static int DailyFromBonus(int perRound)
        {
            return perRound * AssumedBonusRoundsPerDay;
        }
    }
}
