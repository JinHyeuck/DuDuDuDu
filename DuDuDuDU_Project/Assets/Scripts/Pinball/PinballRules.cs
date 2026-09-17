using System.Collections.Generic;

namespace OJ.Pinball
{
    /// <summary>
    /// 핀볼의 판정 규칙. <b>UnityEngine 타입도, ScriptableObject 도, 싱글톤도 쓰지 않는다.</b>
    ///
    /// 헤드리스 EditMode 러너에서는 SO 생성과 <c>GameContainer</c> 가 돌지 않는다.
    /// 규칙이 <see cref="PinballManager"/> 안에 있으면 테스트가 닿지 못하고,
    /// 그러면 확률표가 어긋나도 플레이로만 드러난다 — 그것도 아주 많이 돌려 봐야 드러난다.
    /// 그래서 <b>판정은 전부 여기 static 으로 있고, 매니저는 이것을 부르기만 한다.</b>
    /// </summary>
    public static class PinballRules
    {
        /// <summary>
        /// 누적 가중치로 착지할 슬롯을 뽑는다.
        /// <paramref name="weights"/> 에는 <c>PinballBoard.declaredProbability</c> 를 그대로 넘긴다.
        ///
        /// <b>난수원을 인자로 받는 이유.</b> 안에서 <c>Random.value</c> 를 부르면 테스트가
        /// 경계값을 짚지 못하고 분포를 세는 것밖에 못 한다. 호출부가 굴린 값을 받는다.
        /// <paramref name="roll"/> 은 0~1 이고, 1.0 은 마지막 칸으로 친다
        /// (<c>UnityEngine.Random.value</c> 가 1.0 을 포함하므로 여기서 가둬야 한다).
        ///
        /// <b>합이 1이 아니어도 동작한다.</b> 합으로 정규화한다. 합이 1에서 벗어난 것 자체는
        /// <c>DeclaredProbability_SumsToOne</c> 이 CI 에서 잡으므로, 런타임은 지급을 멈추는
        /// 대신 비율을 지키는 쪽을 택한다. 0 이하인 칸은 뽑히지 않는다.
        /// </summary>
        /// <returns>슬롯 인덱스. 표가 비었거나 양수 가중치가 하나도 없으면 -1.</returns>
        public static int DrawSlot(IReadOnlyList<float> weights, float roll)
        {
            if (weights == null || weights.Count == 0)
                return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                float weight = weights[i];
                if (weight > 0f)
                    total += weight;
            }

            if (total <= 0f)
                return -1;

            // 1.0 을 그대로 두면 target == total 이 되어 어느 칸에도 안 걸린다.
            float clamped = roll;
            if (clamped < 0f)
                clamped = 0f;
            if (clamped >= 1f)
                clamped = 0.99999994f;   // float 로 표현 가능한 1 바로 아래

            float target = clamped * total;
            float accumulated = 0f;

            for (int i = 0; i < weights.Count; i++)
            {
                float weight = weights[i];
                if (weight <= 0f)
                    continue;

                accumulated += weight;
                if (target < accumulated)
                    return i;
            }

            // 누적 오차로 여기 닿을 수 있다. 마지막 양수 칸으로 떨어뜨린다 —
            // -1 을 돌려주면 티켓을 쓴 판이 아무 일도 없이 끝난다.
            for (int i = weights.Count - 1; i >= 0; i--)
            {
                if (weights[i] > 0f)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 특수 핀 게이지를 <paramref name="step"/> 만큼 올린다. 임계치를 넘은 만큼 보상이
        /// 나가고, 넘고 남은 나머지가 게이지에 남는다.
        ///
        /// <b>왜 한 번에 여러 번 지급될 수 있나.</b> 배율이 걸리면 한 번 맞는 것이
        /// <paramref name="step"/> 회분으로 계산된다. x100 에서 임계치가 10 이면 한 번
        /// 맞는 것으로 10회를 달성한다 — 그것을 1회로 깎으면 배율을 올릴수록 특수 핀이
        /// 손해가 되어, 배율이 <b>불이익</b>이 된다.
        ///
        /// <paramref name="requiredHits"/> 나 <paramref name="step"/> 이 0 이하면
        /// <b>게이지를 올리지도, 보상을 주지도 않는다.</b> 임계치가 0 이면 "매번 지급"이 되어
        /// 보상 설정을 빠뜨린 태그가 무한 지급으로 새는데, 그건 설정 실수를 사고로 키우는 쪽이다.
        /// </summary>
        /// <param name="grantCount">이번에 보상을 몇 번 지급해야 하는가. 0 이면 안 준다.</param>
        /// <returns>반영 후의 게이지 값. 항상 0 이상 <paramref name="requiredHits"/> 미만.</returns>
        public static int AdvanceGauge(int current, int requiredHits, int step, out int grantCount)
        {
            grantCount = 0;

            int safe = current < 0 ? 0 : current;
            if (requiredHits <= 0 || step <= 0)
                return safe;

            // long 으로 올린다. 배율 100 에 게이지가 이미 높으면 int 를 넘을 수 있다.
            long total = (long)safe + step;

            long times = total / requiredHits;
            grantCount = times > int.MaxValue ? int.MaxValue : (int)times;

            return (int)(total % requiredHits);
        }

        /// <summary>
        /// 배율을 곱한 수량. <b>넘치면 int 최대값에서 멈춘다</b> —
        /// 넘겨서 음수가 되면 <c>PointManager.Add</c> 가 조용히 무시해 지급이 통째로 사라진다.
        /// </summary>
        public static int ScaleAmount(int amount, int multiplier)
        {
            if (amount <= 0 || multiplier <= 0)
                return 0;

            long scaled = (long)amount * multiplier;
            return scaled > int.MaxValue ? int.MaxValue : (int)scaled;
        }

        /// <summary>
        /// 그 배율을 쓸 수 있는가. <b>보유량이 기준이지 소모량이 아니다</b> —
        /// 배율은 클릭 수를 줄이는 장치라, 충분히 쌓아 둔 사람에게만 열어 시행 횟수가
        /// 한꺼번에 녹아 없어지지 않게 한다.
        /// </summary>
        public static bool IsMultiplierUnlocked(int ticketCount, int requiredTickets)
        {
            return ticketCount >= requiredTickets;
        }
    }
}
