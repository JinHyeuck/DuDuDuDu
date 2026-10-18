namespace OJ.Dice
{
    /// <summary>
    /// 카드 상점이 보는 보드. 순수 로직(<see cref="CardShop"/>)과 실제 보드·보유 판정 사이의 다리다.
    /// </summary>
    public sealed class BoardShopContext : IShopContext
    {
        private readonly UIBoard board;
        private readonly DiceOwnershipManager ownership;

        public BoardShopContext(UIBoard board, DiceOwnershipManager ownership)
        {
            this.board = board;
            this.ownership = ownership;
        }

        public int CountOnBoard(DiceType type, int star)
        {
            return board != null ? board.CountDice(type, star) : 0;
        }

        public bool IsOwned(DiceType type)
        {
            // 보유 판정이 없으면(전투 밖) 규칙대로 처음부터 보유인 것만 인정한다.
            return ownership != null ? ownership.IsOwned(type) : DiceEvolution.IsOwnedFromStart(type);
        }
    }
}
