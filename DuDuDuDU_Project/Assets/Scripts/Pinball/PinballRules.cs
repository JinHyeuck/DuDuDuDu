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
        /// 특수 핀 게이지를 1 올린다. 임계치에 닿으면 보상이 나가고 게이지는 0으로 돌아간다.
        ///
        /// <paramref name="requiredHits"/> 가 0 이하면 <b>게이지를 올리지도, 보상을 주지도 않는다.</b>
        /// 임계치가 0 이면 "매번 지급"이 되어 보상 설정을 빠뜨린 태그가 무한 지급으로 새는데,
        /// 그건 설정 실수를 사고로 키우는 쪽이다.
        /// </summary>
        /// <returns>반영 후의 게이지 값.</returns>
        public static int AdvanceGauge(int current, int requiredHits, out bool granted)
        {
            granted = false;

            int safe = current < 0 ? 0 : current;
            if (requiredHits <= 0)
                return safe;

            int next = safe + 1;
            if (next < requiredHits)
                return next;

            granted = true;
            return 0;
        }
    }
}
