using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{

    public class AttackContent : MonoBehaviour
    {
        public static AttackContent Instance;

        private List<Monster> monsterLists = new List<Monster>();

        private Dictionary<int, Collider2D[]> _recvColliderPools = new Dictionary<int, Collider2D[]>();
        private List<Monster> _skillHitReceivers = new List<Monster>();

        private List<Monster> thunderOper = new List<Monster>();

        [Header("lv5EffectMergyCheat")]
        public List<DiceType> cheatDiceTypes = new List<DiceType>();

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
        //------------------------------------------------------------------------------------
        private void HitMonster(Monster target, DiceType diceType, int damage)
        { // 맞는 처리는 여기서
            // 데미지 UI 표시
            target.TakeDamage(damage);

            GameObject dtObj = DamageTextPool.Instance.GetDamageText();
            dtObj.transform.position = target.transform.position; // 몬스터 위치
            dtObj.transform.ResetLocalZ();
            Color typeColor = StaticResource.Instance.DiceTypeResourceManager.GetColor(diceType);

            dtObj.GetComponent<DamageText>().SetText(damage, typeColor);
        }
        //------------------------------------------------------------------------------------
        List<Monster> hitmonsters = new List<Monster>();
        public void PlayHit(Monster rootTarget, List<DiceType> diceTypes)
        {
            List<DiceType> order = null;
            if (cheatDiceTypes.Count > 0)
                order = cheatDiceTypes;
            else
                order = diceTypes;
            
            order.Sort(Sort);

            hitmonsters.Clear();

            hitmonsters.Add(rootTarget);

            for (int dtype = 0; dtype < order.Count; ++dtype)
            {
                DiceType diceType = order[dtype];


                if (diceType == DiceType.Thunder)
                {
                    Dictionary<Monster, List<Monster>> sunderTarget = GetNPerTarget_NoGlobalDup(MonsterManager.Instance.activeMonsters, hitmonsters, 2);

                    foreach (var pair in sunderTarget)
                    {
                        {
                            BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(diceType);
                            bulletEffect.transform.position = pair.Key.transform.position;
                            bulletEffect.PlayEffect();
                        }

                        if (pair.Value.Count > 0)
                        {
                            for (int i = 0; i < pair.Value.Count; ++i)
                            {
                                BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(diceType, EffectID.C1);
                                bulletEffect.PlayLineEffect(pair.Key.transform.position, pair.Value[i].transform.position);

                                BulletEffect bulletEffectS = BulletEffectPool.Instance.GetBullet(diceType);
                                bulletEffectS.transform.position = pair.Value[i].transform.position;
                                bulletEffectS.PlayEffect();

                                hitmonsters.Add(pair.Value[i]);
                            }
                        }

                        
                    }
                }
                else if (diceType == DiceType.Fire)
                {
                    List<Monster> firetargets = new List<Monster>();

                    for (int monstertarget = 0; monstertarget < hitmonsters.Count; ++monstertarget)
                    {
                        Monster target = hitmonsters[monstertarget];

                        List<Monster> monsters = GetRedHitTarget(target.transform.position,
                            IFFType.IFF_Friend,
                            1, 10, target);

                        BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(diceType);
                        bulletEffect.transform.position = target.transform.position;
                        bulletEffect.PlayEffect();

                        for (int hitmon = 0; hitmon < monsters.Count; ++hitmon)
                        {
                            firetargets.Add(monsters[hitmon]);
                        }
                    }

                    hitmonsters.AddRange(firetargets);
                }
                
            }

            for (int dtype = 0; dtype < order.Count; ++dtype)
            {
                DiceType diceType = order[dtype];

                //int damage = DiceTypeStarManager.Instance.GetTypeStars(diceType);

                int damage = DiceTypeStarManager.Instance.GetTypeStars(diceType);

                if (cheatDiceDamage > 0)
                    damage = cheatDiceDamage;


                for (int i = 0; i < hitmonsters.Count; ++i)
                {
                    Monster target = hitmonsters[i];
                    if (target == null || target.gameObject.activeInHierarchy == false)
                        continue;

                    if (diceType == DiceType.Normal)
                        HitMonster(target, diceType, damage * 2);
                    else
                        HitMonster(target, diceType, damage);

                    if (diceType == DiceType.Poison)
                    {
                        if (target.gameObject.activeInHierarchy == false)
                            continue;

                        target.ApplyPoison();
                        BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(diceType);
                        bulletEffect.transform.position = target.transform.position;
                        bulletEffect.PlayEffect();
                    }
                    else if (diceType == DiceType.Normal)
                    {
                        BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(diceType);
                        bulletEffect.transform.position = target.transform.position;
                        bulletEffect.PlayEffect();
                    }
                    else if (diceType == DiceType.Ice)
                    {
                        if (target.gameObject.activeInHierarchy == false)
                            continue;

                        target.ApplySlow();
                        BulletEffect bulletEffect = BulletEffectPool.Instance.GetBullet(diceType);
                        bulletEffect.transform.position = target.transform.position;
                        bulletEffect.PlayEffect();
                    }
                }
            }
        }
        //------------------------------------------------------------------------------------
        private int Sort(DiceType x, DiceType y)
        {
            if (x == DiceType.Thunder && y != DiceType.Thunder)
                return -1;
            else if (x != DiceType.Thunder && y == DiceType.Thunder)
                return 1;
            else if (x != DiceType.Thunder && y != DiceType.Thunder)
            {
                if (x == DiceType.Fire && y != DiceType.Fire)
                    return -1;
                else if (x != DiceType.Fire && y == DiceType.Fire)
                    return 1;
            }

            return 0;
        }
        //------------------------------------------------------------------------------------
        public IEnumerator HitColorEffect(Monster target, List<DiceType> order)
        {
            List<Monster> targets = new List<Monster>();
            targets.Add(target);

            for (int i = 0; i < order.Count; ++i)
            {
                DiceType elementType = order[i];

                if (elementType == DiceType.Thunder)
                { 

                }
            }

            yield return null;
        }
        //------------------------------------------------------------------------------------
        public int GetThunderTargetCount()
        {
            return 2;
        }
        //------------------------------------------------------------------------------------
        public void ShowTrail(Transform start, Transform end)
        { 

        }
        //------------------------------------------------------------------------------------
        /// </summary>
        public Dictionary<T, List<T>> GetNPerTarget_NoGlobalDup<T>(
            List<T> allMonsters,
            List<T> targetMonsters,
            int pickPerTarget)
        {
            var result = new Dictionary<T, List<T>>();
            var globalUsed = new HashSet<T>(); // 전체 중복 방지
            var allTargetsSet = new HashSet<T>(targetMonsters); // 타겟 몬스터 전체

            foreach (var target in targetMonsters)
            {
                var filtered = new List<T>();

                foreach (var monster in allMonsters)
                {
                    // 자기 자신 + 다른 타겟 몬스터 + 이미 선택된 몬스터 제외
                    if (allTargetsSet.Contains(monster)) continue;
                    if (globalUsed.Contains(monster)) continue;

                    filtered.Add(monster);
                }

                // Fisher-Yates 셔플
                Shuffle(filtered);

                int countToPick = Mathf.Min(pickPerTarget, filtered.Count);
                var picked = filtered.GetRange(0, countToPick);

                foreach (var p in picked)
                    globalUsed.Add(p);

                result[target] = picked;
            }

            return result;
        }

        /// <summary>
        /// Fisher-Yates Shuffle
        /// </summary>
        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        //------------------------------------------------------------------------------------
        public IFFType GetEnemyIFFType(IFFType iFFType)
        {
            if (iFFType == IFFType.IFF_Foe)
                return IFFType.IFF_Friend;

            return IFFType.IFF_Foe;
        }
        //------------------------------------------------------------------------------------
        public List<Monster> GetRedHitTarget(Vector2 pos, IFFType ActorType, float range, int targetCount, Monster fixSkillHitReceiver)
        {
            int searchLayer = 0;

            searchLayer = LayerMask.NameToLayer(GetEnemyIFFType(ActorType).ToString());

            searchLayer = 1 << searchLayer;

            Collider2D[] colliders = null;

            int colliderCount = 0;

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
                    colliders = _recvColliderPools[targetCount];

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

            bool needAddRecver = true;

            if (fixSkillHitReceiver == null)
            {
                needAddRecver = false;
            }

            for (int i = 0; i < colliderCount; i++)
            {
                if (colliders[i] == null)
                    continue;

                Monster skillHitReceiver = colliders[i].gameObject.GetComponent<Monster>();

                _skillHitReceivers.Add(skillHitReceiver);

                if (needAddRecver == true)
                {
                    if (skillHitReceiver == fixSkillHitReceiver)
                        needAddRecver = false;
                }
            }

            if (needAddRecver == true)
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
        //------------------------------------------------------------------------------------
        public Vector3 drawGizmoPos;
        public float drawGizmoRadius;
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            //Use the same vars you use to draw your Overlap SPhere to draw your Wire Sphere.
            Gizmos.DrawWireSphere(drawGizmoPos, drawGizmoRadius);
        }
    }
}

