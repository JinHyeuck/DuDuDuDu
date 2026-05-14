using System.Collections.Generic;

namespace OJ
{
    public class KingPoisonDiceEffect : DiceEffectBase
    {
        public override DiceType DiceType => DiceType.KingPoison;

        public override void ApplyOnHit(AttackContent attackContent, Monster target)
        {
            if (target == null)
                return;

            var impactPosition = target.transform.position;
            PlayEffectAt(DiceType, impactPosition);

            if (target.gameObject.activeInHierarchy == false)
                return;

            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType) : 1;
            target.ApplyPoison(DiceMetaDataProvider.GetPoisonDuration(DiceType), 1f);
            target.ApplySlow(DiceMetaDataProvider.GetSlowDuration(DiceType, level));
            if (level >= 9 && UnityEngine.Random.value <= 0.3f && attackContent != null)
            {
                List<Monster> nearby = attackContent.GetRedHitTarget(
                    impactPosition,
                    IFFType.IFF_Friend,
                    1.1f,
                    1,
                    null);

                if (nearby.Count > 0 && nearby[0] != null && nearby[0] != target)
                    nearby[0].ApplyPoison(DiceMetaDataProvider.GetPoisonDuration(DiceType), 1f);
            }
        }
    }
}
