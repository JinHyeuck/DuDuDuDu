using OJ.Core;

namespace OJ.Mission
{
    /// <summary>
    /// 카운터의 <b>하위 키</b>를 만드는 곳. 한 행동을 종류별로 갈라 세야 할 때 쓴다
    /// (업적의 "일반/레어/신화 다이스 레벨업", "레어 등급 보석 합성").
    ///
    /// <b>문자열을 호출부에 흩어 놓지 않는다.</b> 세이브 키이자 에셋에 적히는 값이라,
    /// 오타 하나가 "카운트가 안 오르는 업적" 으로만 드러난다 — 컴파일도 콘솔도 조용하다.
    /// </summary>
    public static class MissionSubKeys
    {
        /// <summary>기본 5종(일반·불·얼음·번개·독).</summary>
        public const string DiceNormal = "Normal";

        /// <summary>특수 5종(토네이도·스턴·방깎·바람·시간). 강화 재화가 레어석이다.</summary>
        public const string DiceRare = "Rare";

        /// <summary>킹 5종. 강화 재화가 신화석이다.</summary>
        public const string DiceMythic = "Mythic";

        /// <summary>
        /// 다이스의 등급 하위 키.
        ///
        /// <b>정본은 강화 재화다.</b> <c>PointManager.ToScrollType</c> 이 킹→신화석,
        /// 특수→레어석, 기본→속성 스크롤로 이미 완전히 매핑하고 있고, 여기는 그 가름을
        /// 번호 구간으로 다시 적은 것이다. 둘이 어긋나면
        /// <c>MissionDataTests.DiceGradeMatchesUpgradeCurrency</c> 가 떨어진다 —
        /// 표를 둘로 둔 값을 그 테스트가 치른다.
        ///
        /// <b>번호 구간으로 적은 이유.</b> <c>ToScrollType</c> 은 모르는 다이스에 예외를
        /// 던지는데, 카운트를 올리다 예외가 나면 <b>레벨업 자체가 실패한 것처럼 보인다.</b>
        /// 미션이 게임 플레이를 막는 것은 어떤 경우에도 옳지 않다.
        /// </summary>
        public static string ForDice(DiceType diceType)
        {
            if (diceType >= DiceType.KingNormal)
                return DiceMythic;

            if (diceType >= DiceType.Tornado)
                return DiceRare;

            return DiceNormal;
        }

        /// <summary>
        /// 보석 등급 하위 키. <b>합성의 재료 등급</b>을 적는다 — 결과가 아니다.
        /// 레어 4개를 합쳐 에픽 1개가 나오면 <c>GemMerge:Rare</c> 가 오른다.
        /// </summary>
        public static string ForRarity(Rarity rarity)
        {
            return rarity.ToString();
        }

        /// <summary>다이스 레벨업 카운터 키. 하위 키까지 붙은 쪽이다.</summary>
        public static string DiceLevelUpKey(DiceType diceType)
        {
            return MissionRules.CounterKey(MissionAction.DiceLevelUp, ForDice(diceType));
        }
    }
}
