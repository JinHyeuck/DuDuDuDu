namespace OJ
{
    public class PoisonDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Poison;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            target.ApplyPoison();
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
