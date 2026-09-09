using System.Collections.Generic;

namespace OJ.Shop
{
    /// <summary>
    /// 뽑기 판정. <b>확률을 정하는 식만</b> 여기 있고 난수와 지급은 매니저가 한다.
    ///
    /// <b>왜 분리했나.</b> 헤드리스 테스트는 <c>UnityEngine.Random</c> 도 매니저도 못 만든다.
    /// 난수를 인자(<c>roll01</c>)로 받으면 "가중치 [3,2,1] 에 0.9 를 넣으면 마지막이 나온다"를
    /// 값만으로 잠글 수 있다 — 확률은 <b>눈으로 확인이 안 되는 종류</b>라 이게 특히 중요하다.
    /// </summary>
    public static class ShopDraw
    {
        /// <summary>
        /// 가중치 목록에서 하나를 고른다. <paramref name="roll01"/> 은 [0,1) 이다.
        ///
        /// 가중치가 비었거나 합이 0 이면 <b>균등</b>으로 본다 — 데이터가 비었을 때
        /// 0번만 계속 나오면 "확률이 이상하다"가 아니라 "이 상자는 원래 이것만 준다"로 읽힌다.
        /// </summary>
        public static int PickWeightedIndex(IReadOnlyList<int> weights, float roll01, int count)
        {
            if (count <= 0)
                return -1;

            if (roll01 < 0f)
                roll01 = 0f;
            else if (roll01 >= 1f)
                roll01 = 0.9999999f;

            int total = 0;
            if (weights != null)
            {
                for (int i = 0; i < count && i < weights.Count; i++)
                {
                    if (weights[i] > 0)
                        total += weights[i];
                }
            }

            // 균등 폴백.
            if (total <= 0)
                return (int)(roll01 * count);

            // roll 을 가중치 합에 얹고 앞에서부터 깎는다. 부동소수 누적을 피하려고
            // 정수 경계로 비교한다 — 0.1 을 열 번 더하면 1 이 안 되는 그 문제다.
            int target = (int)(roll01 * total);
            int walked = 0;

            for (int i = 0; i < count; i++)
            {
                int weight = weights != null && i < weights.Count ? weights[i] : 0;
                if (weight <= 0)
                    continue;

                walked += weight;
                if (target < walked)
                    return i;
            }

            // 여기 오면 부동소수 끝자락이다. 마지막 유효 칸을 준다.
            for (int i = count - 1; i >= 0; i--)
            {
                int weight = weights != null && i < weights.Count ? weights[i] : 0;
                if (weight > 0)
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// 등급 구간의 칸 수. <c>min..max</c> 양끝을 포함한다.
        /// 뒤집혀 들어와도(<c>max &lt; min</c>) 1 이상을 준다 — 데이터 실수로 뽑기가
        /// <b>아무것도 안 주는</b> 상태가 되지 않게 한다.
        /// </summary>
        public static int RarityStepCount(Rarity min, Rarity max)
        {
            int count = (int)max - (int)min + 1;
            return count < 1 ? 1 : count;
        }
    }
}
