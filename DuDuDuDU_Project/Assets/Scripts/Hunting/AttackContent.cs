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
        private readonly Dictionary<DiceType, DiceEffectBase> _diceEffects = new Dictionary<DiceType, DiceEffectBase>();
        private int _currentDamage;
        private int _currentDiceLevel;
        private int _currentShotDicePip;
        private DiceType _currentAttackType = DiceType.Max;
        private Monster _currentRootTarget;

        [Header("Tornado Tuning")]
        [SerializeField, Min(0.05f)] private float tornadoPullDuration = 0.5f;

        public float TornadoPullDuration => tornadoPullDuration;
        public int CurrentDamage => _currentDamage;
        public int CurrentDiceLevel => _currentDiceLevel;
        public int CurrentShotDicePip => _currentShotDicePip;
        public DiceType CurrentAttackType => _currentAttackType;
        public Monster CurrentRootTarget => _currentRootTarget;
        private bool _hasWindRangeGizmo;
        private Vector3 _windRangeGizmoCenter;
        private Vector3 _windRangeGizmoSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            InitializeDiceEffects();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void InitializeDiceEffects()
        {
            _diceEffects.Clear();

            RegisterDiceEffect(new NormalDiceEffect());
            RegisterDiceEffect(new FireDiceEffect());
            RegisterDiceEffect(new IceDiceEffect());
            RegisterDiceEffect(new ThunderDiceEffect());
            RegisterDiceEffect(new PoisonDiceEffect());
            RegisterDiceEffect(new KingNormalDiceEffect());
            RegisterDiceEffect(new KingFireDiceEffect());
            RegisterDiceEffect(new KingIceDiceEffect());
            RegisterDiceEffect(new KingThunderDiceEffect());
            RegisterDiceEffect(new KingPoisonDiceEffect());
            RegisterDiceEffect(new TornadoDiceEffect());
            RegisterDiceEffect(new StunDiceEffect());
            RegisterDiceEffect(new ArmorBreakDiceEffect());
            RegisterDiceEffect(new WindDiceEffect());
            RegisterDiceEffect(new TimeDiceEffect());
        }

        private void RegisterDiceEffect(DiceEffectBase diceEffect)
        {
            if (diceEffect == null)
                return;

            _diceEffects[diceEffect.DiceType] = diceEffect;
        }

        private DiceEffectBase GetDiceEffect(DiceType diceType)
        {
            if (_diceEffects.TryGetValue(diceType, out DiceEffectBase diceEffect))
                return diceEffect;

            DiceType baseType = DiceMetaDataProvider.GetBaseElementType(diceType);
            if (_diceEffects.TryGetValue(baseType, out DiceEffectBase baseEffect))
                return baseEffect;

            return _diceEffects.TryGetValue(DiceType.Normal, out DiceEffectBase normalEffect) ? normalEffect : null;
        }

        public bool TryCastNoTarget(DiceType diceType, int shotDicePip)
        {
            DiceEffectBase diceEffect = GetDiceEffect(diceType);
            if (diceEffect == null)
                return false;

            return diceEffect.TryCastWithoutTarget(this, shotDicePip);
        }

        public void SetWindRangeGizmo(float minX, float maxX, float centerY, float bandHalfHeight)
        {
            _hasWindRangeGizmo = true;
            _windRangeGizmoCenter = new Vector3((minX + maxX) * 0.5f, centerY, 0f);
            _windRangeGizmoSize = new Vector3(
                Mathf.Max(0.1f, maxX - minX),
                Mathf.Max(0.1f, bandHalfHeight * 2f),
                0f);
        }

        public void HitMonster(Monster target, DiceType diceType, int damage)
        {
            int appliedDamage = target.TakeDamage(damage);
            if (appliedDamage <= 0)
                return;

            GameObject dtObj = DamageTextPool.Instance.GetDamageText();
            dtObj.transform.position = target.transform.position;
            dtObj.transform.ResetLocalZ();
            Color typeColor = DiceMetaDataProvider.GetColor(diceType);
            dtObj.GetComponent<DamageText>().SetText(appliedDamage, typeColor);
        }

        public void PlayHit(Monster rootTarget, DiceType diceType, int shotDicePip)
        {
            if (rootTarget == null)
                return;

            DiceType attackType = diceType;
            DiceEffectBase diceEffect = GetDiceEffect(attackType);

            hitmonsters.Clear();
            hitmonsters.Add(rootTarget);
            diceEffect?.BuildTargets(this, rootTarget, hitmonsters);

            int myDicePip = Mathf.Max(1, shotDicePip);
            int diceLevel = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(attackType) : 1;
            int damage = DiceMetaDataProvider.CalculateDamage(attackType, myDicePip, diceLevel);
            float critChance = DiceMetaDataProvider.GetGlobalCriticalChancePercent();
            if (critChance > 0f && Random.value * 100f <= critChance)
                damage = Mathf.RoundToInt(damage * DiceMetaDataProvider.GetGlobalCriticalDamageMultiplier());
            if (attackType == DiceType.Normal && diceLevel >= 12 && Random.value <= 0.2f)
                damage *= 2;

            if (attackType == DiceType.Normal && diceLevel >= 9 && Random.value <= 0.2f)
                UIDiceSummonSystem.Instance?.AddSP(5);

            _currentDamage = damage;
            _currentDiceLevel = diceLevel;
            _currentShotDicePip = myDicePip;
            _currentAttackType = attackType;
            _currentRootTarget = rootTarget;

            if (attackType == DiceType.KingNormal && diceEffect is KingNormalDiceEffect kingNormalEffect)
            {
                PlayKingNormalMultiHit(rootTarget, attackType, damage, kingNormalEffect);
            }
            else
            {
                for (int i = 0; i < hitmonsters.Count; ++i)
                {
                    Monster target = hitmonsters[i];
                    if (target == null || target.gameObject.activeInHierarchy == false)
                        continue;

                    if (diceEffect == null || diceEffect.ShouldApplyDamage)
                        HitMonster(target, attackType, damage);

                    diceEffect?.ApplyOnHit(this, target);
                }
            }

            _currentDamage = 0;
            _currentDiceLevel = 1;
            _currentShotDicePip = 1;
            _currentAttackType = DiceType.Max;
            _currentRootTarget = null;
        }

        private void PlayKingNormalMultiHit(Monster rootTarget, DiceType attackType, int totalDamage, KingNormalDiceEffect diceEffect)
        {
            int firstHitDamage = Mathf.Max(1, Mathf.RoundToInt(totalDamage * 0.7f));
            int followUpDamage = Mathf.Max(1, Mathf.RoundToInt(totalDamage * 0.1f));
            Vector3 areaCenter = rootTarget.transform.position;
            List<Monster> fixedTargets = new List<Monster>(hitmonsters);

            diceEffect.PlayImpactEffect(areaCenter);
            ApplyKingNormalHit(attackType, firstHitDamage, diceEffect, fixedTargets);

            if (KingNormalDiceEffect.TotalHitCount > 1)
                StartCoroutine(PlayKingNormalFollowUpHits(attackType, followUpDamage, diceEffect, fixedTargets));
        }

        private IEnumerator PlayKingNormalFollowUpHits(DiceType attackType, int damage, KingNormalDiceEffect diceEffect, List<Monster> fixedTargets)
        {
            WaitForSeconds delay = new WaitForSeconds(KingNormalDiceEffect.MultiHitInterval);

            for (int i = 1; i < KingNormalDiceEffect.TotalHitCount; i++)
            {
                yield return delay;
                ApplyKingNormalHit(attackType, damage, diceEffect, fixedTargets);
            }
        }

        private void ApplyKingNormalHit(DiceType attackType, int damage, KingNormalDiceEffect diceEffect, List<Monster> fixedTargets)
        {
            if (diceEffect == null || fixedTargets == null)
                return;

            for (int i = 0; i < fixedTargets.Count; i++)
            {
                Monster target = fixedTargets[i];
                if (target == null || target.gameObject.activeInHierarchy == false)
                    continue;

                HitMonster(target, attackType, damage);
            }
        }

        public IEnumerator HitColorEffect(Monster target, DiceType elementType)
        {
            yield return null;
        }

        public int GetThunderTargetCount(DiceType diceType)
        {
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;
            int count = DiceMetaDataProvider.GetThunderTargetCount(level);
            if (EquipmentManager.Instance != null)
                count += EquipmentManager.Instance.GetThunderChainExtraCount(DiceType.Thunder);
            return Mathf.Max(0, count);
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

        public List<Monster> GetMonstersInOrientedBox(
            Vector2 origin,
            Vector2 direction,
            float halfLength,
            float halfWidth,
            int maxTargets,
            Monster forceInclude = null)
        {
            List<Monster> results = new List<Monster>();

            if (MonsterManager.Instance == null || MonsterManager.Instance.activeMonsters == null)
                return results;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float minForward = -halfLength;
            float maxForward = halfLength;
            float width = Mathf.Max(0.01f, halfWidth);

            for (int i = 0; i < MonsterManager.Instance.activeMonsters.Count; i++)
            {
                Monster monster = MonsterManager.Instance.activeMonsters[i];
                if (monster == null || monster.gameObject.activeInHierarchy == false)
                    continue;

                Vector2 delta = (Vector2)monster.transform.position - origin;
                float forward = Vector2.Dot(delta, dir);
                if (forward < minForward || forward > maxForward)
                    continue;

                float lateral = Mathf.Abs(Vector2.Dot(delta, side));
                if (lateral > width)
                    continue;

                results.Add(monster);
                if (maxTargets > 0 && results.Count >= maxTargets)
                    break;
            }

            if (forceInclude != null
                && forceInclude.gameObject.activeInHierarchy
                && results.Contains(forceInclude) == false)
            {
                if (maxTargets <= 0 || results.Count < maxTargets)
                {
                    results.Add(forceInclude);
                }
                else if (results.Count > 0)
                {
                    results[results.Count - 1] = forceInclude;
                }
            }

            return results;
        }

        public Vector3 drawGizmoPos;
        public float drawGizmoRadius;
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(drawGizmoPos, drawGizmoRadius);

            if (_hasWindRangeGizmo)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(_windRangeGizmoCenter, _windRangeGizmoSize);

                float halfWidth = _windRangeGizmoSize.x * 0.5f;
                Gizmos.DrawLine(
                    new Vector3(_windRangeGizmoCenter.x - halfWidth, _windRangeGizmoCenter.y, 0f),
                    new Vector3(_windRangeGizmoCenter.x + halfWidth, _windRangeGizmoCenter.y, 0f));
            }
        }
    }
}
