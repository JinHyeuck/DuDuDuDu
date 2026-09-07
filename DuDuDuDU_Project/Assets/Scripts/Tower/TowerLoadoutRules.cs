using System.Collections.Generic;
using OJ.Core;
using OJ.Dice;

namespace OJ.Tower
{
    /// <summary>
    /// 편성 슬롯의 계층. 기획서 4.1 의 "분류" 칸이다.
    ///
    /// <c>DiceEvolution.DiceTier</c> 와 값이 겹치지만 <b>같은 것이 아니다.</b>
    /// 저쪽은 "진화 단계"(Base → Special → King)이고 이쪽은 "편성 칸"이다.
    /// 지금은 1:1 로 맞지만 앞으로 갈릴 수 있고(예: 신화 슬롯이 둘로 늘거나
    /// 특수 다이스 중 일부만 신화 취급), 그때 저쪽을 고치면 진화 규칙까지 바뀐다.
    /// 이름을 나눠 두는 것이 그 사고를 막는다.
    /// </summary>
    public enum TowerSlotTier
    {
        Base = 0,
        Special = 1,
        Mythic = 2,
    }

    /// <summary>
    /// 편성 규칙. 기획서 4장 전체가 이 파일이다.
    ///
    /// <b>슬롯 수는 <see cref="TowerFormula"/> 에 있고 판정은 여기 있다.</b> 숫자는
    /// 엔진 없이 테스트되어야 하고(Core), 판정은 <c>DiceType</c> 을 봐야 해서
    /// Core 에 둘 수 없다(asmdef 경계). 그 경계가 그대로 이 두 파일의 경계다.
    /// </summary>
    public static class TowerLoadoutRules
    {
        /// <summary>
        /// 이 다이스가 들어가는 칸.
        ///
        /// <c>DiceEvolution.GetTier</c> 와 같은 판정(숫자 구간)을 쓴다. 에셋의
        /// <c>isMythic</c>·<c>summonable</c> 플래그를 보지 않는 이유도 같다 —
        /// 에셋이 비면 조용히 답이 바뀌는데, 편성 규칙은 그래선 안 된다.
        /// </summary>
        public static TowerSlotTier TierOf(DiceType diceType)
        {
            switch (DiceEvolution.GetTier(diceType))
            {
                case DiceTier.King:
                    return TowerSlotTier.Mythic;
                case DiceTier.Special:
                    return TowerSlotTier.Special;
                default:
                    return TowerSlotTier.Base;
            }
        }

        /// <summary>
        /// 기본 다이스 5종. 기획서 4.1 의 1~4성 칸 후보이고, <b>언제나 보유 상태</b>다.
        /// <c>DiceEvolution.BaseTypes</c> 를 그대로 쓴다 — 목록을 두 벌 두면
        /// 다이스를 추가할 때 한쪽만 고치게 된다.
        /// </summary>
        public static IReadOnlyList<DiceType> BaseTypes => DiceEvolution.BaseTypes;

        /// <summary>스페셜 5종.</summary>
        public static IReadOnlyList<DiceType> SpecialTypes => DiceEvolution.SpecialTypes;

        /// <summary>신화 5종.</summary>
        public static IReadOnlyList<DiceType> MythicTypes => DiceEvolution.KingTypes;

        /// <summary>
        /// 이 계층이 고를 수 있는 최대 개수.
        ///
        /// 기본은 <b>성급마다 1개</b>라 계층 전체로는 4개다. 여기서 4 를 돌려주지만
        /// 실제 제한은 <see cref="TowerLoadout"/> 가 성급 단위로 건다 —
        /// 4성 두 개를 넣고 3성을 비우는 편성은 규칙 위반이다(기획서 4.1).
        /// </summary>
        public static int CapacityOf(TowerSlotTier tier)
        {
            switch (tier)
            {
                case TowerSlotTier.Mythic:
                    return TowerFormula.MythicSlotCount;
                case TowerSlotTier.Special:
                    return TowerFormula.SpecialSlotCount;
                default:
                    return TowerFormula.BaseSlotCount;
            }
        }

        /// <summary>
        /// 스페셜·신화는 성급이 없다. 편성에 들어갈 때 성급을 무엇으로 볼 것인가.
        ///
        /// 1 로 고정한다. <c>DiceMetaDataProvider.ShowStarUI</c> 가 false 이고
        /// 실제 전투에서도 성급 1 로 동작하는 것이 본편의 현행 동작이라, 여기서 다르게
        /// 두면 같은 다이스가 콘텐츠에 따라 세기가 달라진다.
        /// </summary>
        public const int NonBaseStar = 1;

        /// <summary>
        /// 이 다이스에 쓸 성급을 정한다. 기본이면 넘겨받은 성급을, 아니면 1 을 준다.
        /// </summary>
        public static int NormalizeStar(DiceType diceType, int star)
        {
            if (TierOf(diceType) != TowerSlotTier.Base)
                return NonBaseStar;

            return OJMath.Clamp(star, 1, TowerFormula.MaxBaseStar);
        }
    }
}
