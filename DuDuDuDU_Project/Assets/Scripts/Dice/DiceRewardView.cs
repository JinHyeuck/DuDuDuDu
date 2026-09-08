using OJ.Point;

namespace OJ.Dice
{
    /// <summary>
    /// 보상 화면이 다이스 하나를 그리는 데 필요한 것 전부.
    ///
    /// <b>왜 <c>DiceType</c> 하나로 안 되나.</b> 같은 다이스라도 <b>처음 얻은 것</b>과
    /// <b>이미 갖고 있어 재화로 돌아온 것</b>은 그림이 다르다. 그런데 지급이 끝난 뒤에는
    /// <c>IsOwned</c> 가 두 경우 모두 참이라 화면이 스스로 구별할 수 없다 —
    /// 그 사실을 아는 것은 지급한 매니저뿐이므로 여기 담아서 넘긴다.
    ///
    /// <b>환급액도 같이 담는다.</b> 화면이 <c>GetPrice</c> 를 다시 부르면 그 사이에 표가
    /// 바뀌었을 때 지급한 값과 보여 준 값이 갈라진다. 준 것을 그대로 들고 다니는 편이 맞다.
    /// </summary>
    public struct DiceRewardView
    {
        public DiceType DiceType;

        /// <summary>이번에 <b>처음</b> 얻었는가. 거짓이면 아래 환급이 대신 들어왔다.</summary>
        public bool WasNew;

        public PointType RefundCurrency;
        public int RefundAmount;

        public static DiceRewardView NewlyOwned(DiceType diceType)
        {
            return new DiceRewardView { DiceType = diceType, WasNew = true };
        }

        public static DiceRewardView Refunded(DiceType diceType, PointType currency, int amount)
        {
            return new DiceRewardView
            {
                DiceType = diceType,
                WasNew = false,
                RefundCurrency = currency,
                RefundAmount = amount,
            };
        }
    }
}
