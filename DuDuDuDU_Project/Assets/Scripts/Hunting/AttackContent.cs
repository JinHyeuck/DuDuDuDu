using System.Collections;
using System.Collections.Generic;
using OJ.Core;
using OJ.DI;
using UnityEngine;
using OJ.Dice;
using OJ.Equipment;
using OJ.Relic;
using OJ.Utils;
using VContainer;

namespace OJ.Hunting
{
    public class AttackContent : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. 이 컴포넌트는 BattleScene 에서만 사니 여기서는 null 이 아니다.
        // 이 컴포넌트는 <b>씬에 놓여 있으므로</b> 스코프의 sceneLoaded 순회로 채워진다 —
        // 즉 자기 Awake 뒤다. Start 부터 쓸 것. (런타임에 Instantiate 되는 것은 반대다.)
        [Inject] private IBattleRefs battle;

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

        // 8.6: 다이스 효과 15개가 창구를 생성자로 받게 되면서(DiceEffectBase 에 무인자 생성자가
        // 없다) 효과 생성을 Awake 에 둘 수 없다. 배틀 스코프는 씬의 모든 Awake 뒤에 빌드되므로
        // Awake 시점의 battle 은 아직 null 이고, 거기서 만들면 효과 15개 전부가 null 창구를
        // 들고 태어나 첫 공격에서 터진다. 스코프 빌드는 모든 Start 앞이라 여기서는 채워져 있다.
        private void Start()
        {
            InitializeDiceEffects();
        }

        private void InitializeDiceEffects()
        {
            _diceEffects.Clear();

            // 효과 15개는 컨테이너가 만들지 않는 순수 C# 이라 [Inject] 를 쓸 수 없다.
            // 그래서 만드는 쪽인 여기가 창구를 생성자로 넘겨준다.
            RegisterDiceEffect(new NormalDiceEffect(battle));
            RegisterDiceEffect(new FireDiceEffect(battle));
            RegisterDiceEffect(new IceDiceEffect(battle));
            RegisterDiceEffect(new ThunderDiceEffect(battle));
            RegisterDiceEffect(new PoisonDiceEffect(battle));
            RegisterDiceEffect(new KingNormalDiceEffect(battle));
            RegisterDiceEffect(new KingFireDiceEffect(battle));
            RegisterDiceEffect(new KingIceDiceEffect(battle));
            RegisterDiceEffect(new KingThunderDiceEffect(battle));
            RegisterDiceEffect(new KingPoisonDiceEffect(battle));
            RegisterDiceEffect(new TornadoDiceEffect(battle));
            RegisterDiceEffect(new StunDiceEffect(battle));
            RegisterDiceEffect(new ArmorBreakDiceEffect(battle));
            RegisterDiceEffect(new WindDiceEffect(battle));
            RegisterDiceEffect(new TimeDiceEffect(battle));
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

            GameObject dtObj = battle.DamageTexts.GetDamageText();
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

            // 배수 3단(크리 → 일반 lv12 더블 → 유물)의 산술은 OJ.Core 의 CriticalFormula 로 내려갔다.
            // 여기 남은 것은 순수 함수가 될 수 없는 것뿐이다 — 난수, 싱글톤 조회, DiceType 판정,
            // 그리고 ConsumeAttackDamageMultiplier(읽기가 아니라 쓰기다).
            //
            // <b>아래 네 줄의 차례를 바꾸지 마라.</b> 크리 난수 → 크리배수 조회 → 더블 난수 →
            // 유물 소모 순서가 원본(141~147줄)과 같아야 한다. 값이 아니라 난수열과 유물
            // 1회성 효과가 여기에 걸려 있다. 단축평가(&&)도 그대로다 — 크리 확률이 0 이면
            // 난수를 뽑지 않고, 일반 다이스가 아니거나 lv<12 면 역시 뽑지 않는다.
            float critChance = DiceMetaDataProvider.GetGlobalCriticalChancePercent();
            bool criticalHit = CriticalFormula.IsCriticalChanceActive(critChance)
                               && CriticalFormula.RollHitsCritical(Random.value, critChance);

            // 크리가 안 떴으면 배수를 조회하지 않는다(원본도 if 안에서만 불렀다).
            // 이때 넘기는 1f 는 ApplyCritical 이 criticalHit=false 라 쓰지 않는 자리채움이다.
            float criticalDamageMultiplier = criticalHit
                ? DiceMetaDataProvider.GetGlobalCriticalDamageMultiplier()
                : 1f;

            bool doubleHit = attackType == DiceType.Normal
                             && CriticalFormula.IsDoubleHitLevel(diceLevel)
                             && CriticalFormula.RollHitsDoubleHit(Random.value);

            // 매니저 유무를 bool 로 따로 넘긴다. 3단은 곱만 하는 것이 아니라 하한 1 을 같이
            // 걸기 때문에 "유물 없음"과 "배수 1f"가 damage=0 에서 갈린다.
            bool relicMultiplierApplies = RelicManager.Instance != null;
            float relicDamageMultiplier = relicMultiplierApplies
                ? RelicManager.Instance.ConsumeAttackDamageMultiplier()
                : 1f;

            damage = CriticalFormula.ApplyCritical(
                damage, criticalHit, criticalDamageMultiplier, doubleHit, relicMultiplierApplies, relicDamageMultiplier);

            // 아래는 데미지가 아니라 SP 다. 임계 9 와 0.2f 를 CriticalFormula 로 끌어오지 않은 것은
            // 값이 우연히 겹칠 뿐 다른 기능이기 때문이다 — 합치면 한쪽 조정이 양쪽을 움직인다.
            // 8.3b: 기존 ?. 는 지웠다. 소환 시스템은 같은 씬에 상주하므로 여기서 null 이면
            // 그것은 사고다 — 조용히 SP 를 삼키는 대신 터져야 한다. 난수를 뽑는 조건식은
            // 그대로라 SP 판정의 난수열은 바뀌지 않는다.
            if (attackType == DiceType.Normal && diceLevel >= 9 && Random.value <= 0.2f)
                battle.Summon.AddSP(5);

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
            if (RelicManager.Instance != null)
                count += RelicManager.Instance.GetThunderExtraTargetCount(diceType);
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

            // 8.3b: 매니저 자체의 null 검사는 지웠다(같은 씬에 상주하므로 없을 수 없다).
            // 반면 activeMonsters 는 매니저의 내부 상태라 창구가 보증하는 것이 아니어서 남긴다.
            if (battle.Monsters.activeMonsters == null)
                return results;

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            Vector2 side = new Vector2(-dir.y, dir.x);
            float minForward = -halfLength;
            float maxForward = halfLength;
            float width = Mathf.Max(0.01f, halfWidth);

            for (int i = 0; i < battle.Monsters.activeMonsters.Count; i++)
            {
                Monster monster = battle.Monsters.activeMonsters[i];
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
