namespace OJ.Hunting
{
    public static class AttackContentExtensions
    {
        public static void PlayHit(this AttackContent attackContent, Monster rootTarget, DiceType diceType, int shotDicePip)
        {
            attackContent?.PlayHit(rootTarget, diceType, shotDicePip);
        }
    }
}
