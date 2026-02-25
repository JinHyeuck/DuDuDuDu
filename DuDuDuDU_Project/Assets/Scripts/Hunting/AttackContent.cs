using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class AttackContent : MonoBehaviour
    {
        public static AttackContent Instance;

        private Dictionary<int, Collider2D[]> _recvColliderPools = new Dictionary<int, Collider2D[]>();
        private List<Monster> _skillHitReceivers = new List<Monster>();
        private List<Monster> hitmonsters = new List<Monster>();

        [Header("Cheat")]
        public DiceType cheatDiceType = DiceType.Max;
        public int cheatDiceDamage = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void HitMonster(Monster target, DiceType diceType, int damage)
        {
            target.TakeDamage(damage);

            GameObject dtObj = DamageTextPool.Instance.GetDamageText();
            dtObj.transform.position = target.transform.position;
            dtObj.transform.ResetLocalZ();
            Color typeColor = DiceMetaDataProvider.GetColor(diceType);
            dtObj.GetComponent<DamageText>().SetText(damage, typeColor);
        }

        public void PlayHit(Monster rootTarget, DiceType diceType, int shotDicePip)
        {
            if (rootTarget == null)
                return;

            DiceType attackType = cheatDiceType != DiceType.Max ? cheatDiceType : diceType;
            attackType = DiceMetaDataProvider.GetBaseElementType(attackType);

            hitmonsters.Clear();
            hitmonsters.Add(rootTarget);

            if (attackType == DiceType.Thunder)
            {
                Dictionary<Monster, List<Monster>> sunderTarget = GetNPerTarget_NoGlobalDup(
                    MonsterManager.Instance.activeMonsters,
                    hitmonsters,
                    2);

                foreach (var pair in sunderTarget)
                {
                    BulletEffect originEffect = BulletEffectPool.Instance.GetBullet(attackType);
                    if (originEffect != null)
                    {
                        originEffect.transform.position = pair.Key.transform.position;
                        originEffect.PlayEffect();
                    }

                    for (int i = 0; i < pair.Value.Count; ++i)
                    {
                        Monster chained = pair.Value[i];

                        BulletEffect chain = BulletEffectPool.Instance.GetBullet(attackType, EffectID.C1);
                        if (chain != null)
                            chain.PlayLineEffect(pair.Key.transform.position, chained.transform.position);

                        BulletEffect impact = BulletEffectPool.Instance.GetBullet(attackType);
                        if (impact != null)
                        {
                            impact.transform.position = chained.transform.position;
                            impact.PlayEffect();
                        }

                        hitmonsters.Add(chained);
                    }
                }
            }
            else if (attackType == DiceType.Fire)
            {
                List<Monster> firetargets = new List<Monster>();

                for (int i = 0; i < hitmonsters.Count; ++i)
                {
                    Monster target = hitmonsters[i];
                    if (target == null)
                        continue;

                    List<Monster> monsters = GetRedHitTarget(
                        target.transform.position,
                        IFFType.IFF_Friend,
                        1,
                        10,
                        target);

                    BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(attackType);
                    if (bulletEffect != null)
                    {
                        bulletEffect.transform.position = target.transform.position;
                        bulletEffect.PlayEffect();
                    }

                    for (int hitIdx = 0; hitIdx < monsters.Count; ++hitIdx)
                        firetargets.Add(monsters[hitIdx]);
                }

                hitmonsters.AddRange(firetargets);
            }

            int myDicePip = Mathf.Max(1, shotDicePip);
            int diceLevel = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(attackType) : 1;
            int damage = DiceMetaDataProvider.CalculateDamage(attackType, myDicePip, diceLevel);
            if (cheatDiceDamage > 0)
                damage = cheatDiceDamage;

            for (int i = 0; i < hitmonsters.Count; ++i)
            {
                Monster target = hitmonsters[i];
                if (target == null || target.gameObject.activeInHierarchy == false)
                    continue;

                HitMonster(target, attackType, damage);

                if (attackType == DiceType.Poison)
                {
                    if (target.gameObject.activeInHierarchy == false)
                        continue;

                    target.ApplyPoison();
                    BulletEffect effect = BulletEffectPool.Instance.GetBullet(attackType);
                    if (effect != null)
                    {
                        effect.transform.position = target.transform.position;
                        effect.PlayEffect();
                    }
                }
                else if (attackType == DiceType.Normal)
                {
                    BulletEffect effect = BulletEffectPool.Instance.GetBullet(attackType);
                    if (effect != null)
                    {
                        effect.transform.position = target.transform.position;
                        effect.PlayEffect();
                    }
                }
                else if (attackType == DiceType.Ice)
                {
                    if (target.gameObject.activeInHierarchy == false)
                        continue;

                    target.ApplySlow();
                    BulletEffect effect = BulletEffectPool.Instance.GetBullet(attackType);
                    if (effect != null)
                    {
                        effect.transform.position = target.transform.position;
                        effect.PlayEffect();
                    }
                }
            }
        }

        public IEnumerator HitColorEffect(Monster target, DiceType elementType)
        {
            yield return null;
        }

        public int GetThunderTargetCount()
        {
            return 2;
        }

        public void ShowTrail(Transform start, Transform end)
        {
        }

        public Dictionary<T, List<T>> GetNPerTarget_NoGlobalDup<T>(
            List<T> allMonsters,
            List<T> targetMonsters,
            int pickPerTarget)
        {
            var result = new Dictionary<T, List<T>>();
            var globalUsed = new HashSet<T>();
            var allTargetsSet = new HashSet<T>(targetMonsters);

            foreach (var target in targetMonsters)
            {
                var filtered = new List<T>();

                foreach (var monster in allMonsters)
                {
                    if (allTargetsSet.Contains(monster)) continue;
                    if (globalUsed.Contains(monster)) continue;

                    filtered.Add(monster);
                }

                Shuffle(filtered);

                int countToPick = Mathf.Min(pickPerTarget, filtered.Count);
                var picked = filtered.GetRange(0, countToPick);

                foreach (var p in picked)
                    globalUsed.Add(p);

                result[target] = picked;
            }

            return result;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public IFFType GetEnemyIFFType(IFFType iFFType)
        {
            if (iFFType == IFFType.IFF_Foe)
                return IFFType.IFF_Friend;

            return IFFType.IFF_Foe;
        }

        public List<Monster> GetRedHitTarget(Vector2 pos, IFFType ActorType, float range, int targetCount, Monster fixSkillHitReceiver)
        {
            int searchLayer = LayerMask.NameToLayer(GetEnemyIFFType(ActorType).ToString());
            searchLayer = 1 << searchLayer;

            Collider2D[] colliders;
            int colliderCount;

            if (targetCount < 0)
            {
                colliders = Physics2D.OverlapCircleAll(pos, range, searchLayer);
                colliderCount = colliders.Length;
            }
            else
            {
                if (_recvColliderPools.ContainsKey(targetCount) == false)
                {
                    colliders = new Collider2D[targetCount];
                    _recvColliderPools.Add(targetCount, colliders);
                }
                else
                {
                    colliders = _recvColliderPools[targetCount];
                }

                ContactFilter2D filter = new ContactFilter2D();
                filter.SetLayerMask(searchLayer);
                filter.useTriggers = false;
                colliderCount = Physics2D.OverlapCircle(pos, range, filter, colliders);
            }

            if (ActorType == IFFType.IFF_Friend)
            {
                drawGizmoPos = pos;
                drawGizmoRadius = range;
            }

            _skillHitReceivers.Clear();

            bool needAddRecver = fixSkillHitReceiver != null;

            for (int i = 0; i < colliderCount; i++)
            {
                if (colliders[i] == null)
                    continue;

                Monster skillHitReceiver = colliders[i].gameObject.GetComponent<Monster>();
                _skillHitReceivers.Add(skillHitReceiver);

                if (needAddRecver && skillHitReceiver == fixSkillHitReceiver)
                    needAddRecver = false;
            }

            if (needAddRecver)
            {
                if (targetCount < 0)
                    _skillHitReceivers.Add(fixSkillHitReceiver);
                else
                {
                    if (_skillHitReceivers.Count == 0)
                        _skillHitReceivers.Add(fixSkillHitReceiver);
                    else if (_skillHitReceivers.Count < targetCount)
                        _skillHitReceivers.Add(fixSkillHitReceiver);
                    else
                        _skillHitReceivers[_skillHitReceivers.Count - 1] = fixSkillHitReceiver;
                }
            }

            return _skillHitReceivers;
        }

        public Vector3 drawGizmoPos;
        public float drawGizmoRadius;
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(drawGizmoPos, drawGizmoRadius);
        }
    }
}
