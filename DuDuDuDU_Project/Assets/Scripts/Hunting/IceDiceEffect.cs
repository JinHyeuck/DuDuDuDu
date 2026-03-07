namespace OJ
{
    public class IceDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.Ice;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null || target.gameObject.activeInHierarchy == false)
                return;

            target.ApplySlow();
            PlayEffectAt(DiceType, target.transform.position);
        }
    }
}
