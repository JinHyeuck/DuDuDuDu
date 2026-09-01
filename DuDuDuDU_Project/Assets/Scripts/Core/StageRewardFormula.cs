
namespace OJ.Core
{
    // StageRewardCalculator 에서 난수가 섞이지 않은 결정적 계산만 떼어낸 것이다.
    //
    // 이 함수들의 출력은 Tests/Golden/formula_baseline.txt 에 골든 기준선으로 박제돼 있고,
    // 한 값이라도 달라지면 검증이 실패한다. 그래서 아래 표현식은 "읽기 좋게" 정리하는 것조차
    // 금지다. 구체적으로 다음이 전부 금지다.
    //   - 연산 순서·괄호 변경 (a * b + c 를 c + a * b 로 바꾸는 것 포함)
    //   - 중간 변수의 타입 승격 (float 를 double 로 올리는 것)
    //   - Mathf 를 System.Math 로 교체
    // 마지막 항목이 특히 위험하다. Math 는 인자를 double 로 올려 계산하는데, 이 식들은
    // 마지막에 FloorToInt(내림)로 정수를 만들기 때문에 정밀도가 조금만 달라져도 결과가
    // 통째로 1 만큼 어긋난다. AccumulatedGuaranteedGold 의 주석에 실제 사례가 있다.
    //
    // 파라미터·반환이 전부 기본형인 이유: OJ.Core 는 Assembly-CSharp 을 참조할 수 없어
    // PointType / StageClearGrade 같은 enum 을 쓸 수 없다. enum 매핑은 호출부의 몫이다.
    public static class StageRewardFormula
    {
        // 10 스테이지마다 골드 보너스 +5.
        // 정수 나눗셈의 버림이 계단식 증가를 만드는 핵심이라 float 로 바꾸면 안 된다.
        // OJMath.Max 로 하한을 1 로 올린 뒤에 -1 하는 순서도 그대로다. 0 이하 stageIndex 가
        // 들어와도 보너스가 음수로 내려가지 않게 막는 장치다.
        public static int StageBonus(int stageIndex)
        {
            return ((OJMath.Max(1, stageIndex) - 1) / 10) * 5;
        }

        // 노멀 클리어 확정 골드. 150 은 원본 상수를 값 그대로 옮긴 것이다.
        public static int GuaranteedNormalGold(int stageIndex)
        {
            return 150 + StageBonus(stageIndex);
        }

        // 웨이브 진행률에 비례해 확정 골드를 잘라 준다.
        //
        // ratio 를 반드시 float 로 유지해야 한다. double 로 올리면 나눗셈이 안 떨어지는
        // 구간에서 결과가 어긋난다. 예를 들어 stageIndex=1, cleared=1, total=3 이면
        //   float  : 1f / 3   = 0.33333334f (1/3 보다 살짝 큼) → 150 * ratio = 50.0000015 → 50
        //   double : 1.0 / 3.0 = 0.3333333333… (1/3 보다 살짝 작음) → 49.99999999… → 49
        // 내림이라 이 1 의 차이가 그대로 보상 금액 차이가 된다.
        //
        // 캐스팅 위치((float)safeClearedWaves / safeTotalWaves — 분자에만)와
        // 곱셈 방향(GuaranteedNormalGold(...) * ratio)도 원본 그대로다.
        public static int AccumulatedGuaranteedGold(int stageIndex, int clearedWaves, int totalWaves)
        {
            int safeTotalWaves = OJMath.Max(1, totalWaves);
            int safeClearedWaves = OJMath.Clamp(clearedWaves, 0, safeTotalWaves);
            float ratio = (float)safeClearedWaves / safeTotalWaves;
            return OJMath.FloorToInt(GuaranteedNormalGold(stageIndex) * ratio);
        }

        // 남은 벽 체력 비율로 클리어 등급 티어를 판정한다. 0=Minimum, 1=Half, 2=Perfect.
        //
        // 주의: 여기서 돌려주는 0/1/2 는 StageClearGrade 의 값(None=0, Minimum=1, Half=2,
        // Perfect=3)과 다르다. 캐스팅 한 번으로 매핑하면 등급이 조용히 한 칸씩 밀리므로
        // 호출부는 반드시 명시적으로 매핑해야 한다. StageClearGrade 가 Assembly-CSharp 에
        // 있어 여기서 참조할 수 없기 때문에 생긴 제약이다.
        //
        // 0.999f 는 부동소수 오차로 1.0f 에 못 닿는 만점을 구제하려는 임계값이고,
        // 0.5f 는 "이상"이라 정확히 절반(50/100)이면 Half 로 떨어진다. 둘 다 기준선이
        // 경계값까지 고정하고 있으니 >= 를 > 로 바꾸지 마라.
        public static int ClearGradeTier(int currentWallHp, int totalWallHp)
        {
            if (totalWallHp <= 0)
                return 0;

            float ratio = OJMath.Clamp01((float)currentWallHp / totalWallHp);
            if (ratio >= 0.999f)
                return 2;
            if (ratio >= 0.5f)
                return 1;
            return 0;
        }

        // ScaleRewards 와 AddScaledReward 가 공통으로 쓰던 배율 적용식.
        //
        // amount 는 int, multiplier 는 float 이라 곱셈이 float 로 승격된다. double 로
        // 올리면 AccumulatedGuaranteedGold 와 같은 이유로 경계에서 1 이 어긋난다.
        // 반올림이 아니라 내림인 것도 원본 동작이므로 RoundToInt 로 바꾸지 마라.
        // Clamp01 이 안에 있어 1 을 넘는 배율은 잘리고, 음수 배율은 0 이 된다.
        public static int ScaleAmount(int amount, float multiplier)
        {
            return OJMath.FloorToInt(amount * OJMath.Clamp01(multiplier));
        }
    }
}
