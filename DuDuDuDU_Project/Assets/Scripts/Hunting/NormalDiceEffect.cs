namespace OJ
{
    public class NormalDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Normal;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null)
                return;

            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
