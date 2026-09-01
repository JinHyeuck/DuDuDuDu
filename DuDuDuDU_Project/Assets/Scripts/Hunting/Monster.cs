using UnityEngine;
using System.Collections;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Relic;
using OJ.Utils;
using VContainer;

namespace OJ.Hunting
{
    public class Monster : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. 이 몬스터는 MonsterSpawner 가 런타임에 찍는 프리팹이라
        // 씬 순회로는 안 잡히고, 생성부의 resolver.Instantiate 가 찍는 그 순간에 주입된다.
        //
        // Awake 에서는 읽지 않는다. 주입이 Awake 보다 먼저냐 나중이냐가 생성부에 달려 있어서다
        // — 지금 생성부(MonsterSpawner 3곳)는 resolver.Instantiate 라 VContainer 가 프리팹을
        // 껐다 찍고 주입한 뒤 켜므로 Awake 가 나중에 돌지만, 평범한 Object.Instantiate 로
        // 바뀌는 순간 순서가 뒤집혀 Awake 에서 null 이 된다. Awake 가 하는 일은 localScale 을
        // 읽어 두는 것뿐이라 애초에 창구에 닿지 않는다.
        //
        // 아래 사용처는 OnSpawn / Update / TakeDamage / 코루틴이라 전부 생성이 끝난 뒤다.
        // OnDisable 도 안전하다 — 풀 예열이 오브젝트를 끄는 것은 resolver.Instantiate 가
        // 돌아온 다음 문장이고, 주입은 그 안에서 이미 끝나 있다.
        // null 이면 그것은 사고이니 새 ?. 를 쓰지 않는다.
        [Inject] private IBattleRefs battle;

        CharacterState CharacterState = CharacterState.None;

        private bool IsAlive => gameObject.activeInHierarchy && isActiveAndEnabled && CharacterState != CharacterState.Dead && _hp > 0;

        public int MonsterID = -1;
        public int _hp = 3;
        public int defense = 0;
        public int attackDamage = 1;
        public float attackInterval = 0.5f;
        public float moveSpeed = 2f;
        public float ApplyMoveSpeed = 2f;
        private bool isAttacking = false;

        public CharacterAnimation characterAnimation;

        private readonly WaitForSeconds poisonDelay = new WaitForSeconds(0.5f);
        private int _baseDefense;
        private int _defenseDownAmount;
        private float _stunnedUntilTime;
        private float _slowUntilTime;
        private float _poisonUntilTime;
        private float _poisonDamageMultiplier = 1f;
        private int _poisonDamageTakenBonusPercent;
        private int _stunDamageTakenBonusPercent;
        private float _stunDamageTakenBonusUntil;
        private int _armorBreakDamageTakenBonusPercent;
        private float _armorBreakDamageTakenBonusUntil;
        private int _thunderDamageTakenBonusPercent;
        private float _thunderDamageTakenBonusUntil;
        private int _windDamageTakenBonusPercent;
        private float _windDamageTakenBonusUntil;
        private int _relicDamageTakenBonusPercent;
        private float _relicDamageTakenBonusUntil;
        private Coroutine _defenseDownRoutine;
        private Coroutine _poisonRoutine;
        private Coroutine _attackRoutine;
        private Wall _attackWall;
        private Coroutine _pullRoutine;
        private float _pendingPullDistance;
        private float _pullUntilTime;
        private Vector2 _pullCenter;
        private Vector3 _defaultLocalScale;

        private void Awake()
        {
            _defaultLocalScale = transform.localScale;
        }

        /// <summary>
        /// 시간 소스. (5.5) 상태이상 만료·중독 틱은 <b>게임 시간</b>이라 배속의 영향을 받아야
        /// 한다 — 배속 2배에서 슬로우가 두 배로 빨리 풀리는 것이 현행 동작이고 의도다.
        /// 실시간(<c>RealTime</c>)으로 바꾸면 그 동작이 조용히 달라진다.
        ///
        /// 프로퍼티로 두는 것은 나중에 <c>RunState</c> 가 시계를 소유하게 될 때 여기만
        /// 갈아 끼우면 되게 하려는 것이다. 지금은 Unity 시간을 그대로 돌려준다.
        /// </summary>
        private static IClock Clock => UnityClock.Instance;

        public void OnSpawn()
        {
            StopAllCoroutines();
            isAttacking = false;

            _baseDefense = defense;
            _defenseDownAmount = 0;
            _stunnedUntilTime = -1f;
            _slowUntilTime = -1f;
            _poisonUntilTime = -1f;
            _poisonDamageMultiplier = 1f;
            _poisonDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusUntil = -1f;
            _armorBreakDamageTakenBonusPercent = 0;
            _armorBreakDamageTakenBonusUntil = -1f;
            _thunderDamageTakenBonusPercent = 0;
            _thunderDamageTakenBonusUntil = -1f;
            _windDamageTakenBonusPercent = 0;
            _windDamageTakenBonusUntil = -1f;
            _relicDamageTakenBonusPercent = 0;
            _relicDamageTakenBonusUntil = -1f;
            RecalculateDefense();

            battle.Monsters.RegisterMonster(this);
            ApplyMoveSpeed = moveSpeed;
            characterAnimation.PlayAnimation(CharacterState.Run);
            CharacterState = CharacterState.Run;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isAttacking = false;

            _defenseDownRoutine = null;
            _defenseDownAmount = 0;
            _stunnedUntilTime = -1f;
            _slowUntilTime = -1f;
            _poisonUntilTime = -1f;
            _poisonDamageMultiplier = 1f;
            _poisonDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusUntil = -1f;
            _armorBreakDamageTakenBonusPercent = 0;
            _armorBreakDamageTakenBonusUntil = -1f;
            _thunderDamageTakenBonusPercent = 0;
            _thunderDamageTakenBonusUntil = -1f;
            _windDamageTakenBonusPercent = 0;
            _windDamageTakenBonusUntil = -1f;
            _relicDamageTakenBonusPercent = 0;
            _relicDamageTakenBonusUntil = -1f;
            _attackRoutine = null;
            _attackWall = null;
            _pullRoutine = null;
            _poisonRoutine = null;
            _pendingPullDistance = 0f;
            _pullUntilTime = -1f;
            ApplyMoveSpeed = moveSpeed;
            RecalculateDefense();

            // 여기 ?. 는 새로 넣은 것이 아니라 원래 MonsterManager.Instance 뒤에 붙어 있던
            // 것을 그대로 옮긴 것이다. OnDisable 은 씬을 내릴 때도 도는데 그때는 배틀 스코프가
            // 이미 Clear() 해서 창구가 비어 있을 수 있다 — 파괴 순서가 정해져 있지 않다.
            // 떼면 씬 전환 때마다 터진다.
            battle.Monsters?.UnregisterMonster(this, false);
        }

        void Update()
        {
            UpdateTimedStates();

            if (IsStunned())
                return;

            if (IsBeingPulled())
                return;

            if (!isAttacking)
            {
                transform.Translate(Vector2.down * ApplyMoveSpeed * Time.deltaTime);

                if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
                    battle.Spawner.PoolMonster(this);
            }
        }

        public int TakeDamage(int dmg)
        {
            if (!gameObject.activeInHierarchy || CharacterState == CharacterState.Dead)
                return 0;

            if (dmg <= 0)
                return 0;

            // 산술은 전부 OJ.Core.IncomingDamageFormula 로 내려갔다. 여기 남은 일은 "엔진과 싱글톤에서
            // 값을 긁어모아 넘기고, 그 결과로 상태를 바꾸는 것"뿐이다.
            //
            // 원본은 방어력 감쇄(armor/damageMultiplier)를 CleanupExpiredDamageBonuses() **앞에서**
            // 계산했다. 한 함수로 합치면서 그 순서가 뒤집혔는데 안전하다 — cleanup 이 쓰는 것은
            // 6개 percent 필드와 5개 until 필드뿐이고 defense / _baseDefense / _defenseDownAmount 는
            // 한 글자도 건드리지 않는다. defense 를 바꾸는 것은 RecalculateDefense 하나이고 그것을
            // 부르는 곳은 OnSpawn / OnDisable / ApplyDefenseDown / CoApplyDefenseDown /
            // SetCombatStats 인데 cleanup 은 그중 무엇도 부르지 않는다.
            CleanupExpiredDamageBonuses();

            // 단축평가를 푸는 대신 판정을 여기 남긴다. DiceMetaDataProvider.HasKing*DamageBonus() 는
            // DiceTypeStarManager / DiceLevelManager 의 MonoSingleton getter 를 타는데, 그 getter 는
            // 읽기가 아니라 FindObjectOfType + 생성 시도다(Utils/Singleton.cs). RelicManager 쪽도
            // Database 프로퍼티가 지연 로드를 유발한다. 즉 "미리 평가해서 bool 로 넘기기"는
            // 값은 같아도 관측 가능한 변화라 하지 않는다.
            //
            // IsSlowed() 는 원본에서 두 번 불렸다. 같은 프레임의 Time.time 은 고정이고 그 사이
            // _slowUntilTime 을 건드리는 것이 없어 값이 같으므로 한 번만 부른다.
            bool isSlowed = IsSlowed();
            int stateBonusPercent = IncomingDamageFormula.StateBonusPercent(
                isSlowed && DiceMetaDataProvider.HasKingIceDamageBonus(),
                IsPoisoned() && DiceMetaDataProvider.HasKingPoisonDamageBonus(),
                isSlowed && RelicManager.Instance != null ? RelicManager.Instance.GetSlowDamageTakenBonusPercent() : 0);

            int totalBonusPercent = IncomingDamageFormula.TotalBonusPercent(
                _poisonDamageTakenBonusPercent,
                _stunDamageTakenBonusPercent,
                _armorBreakDamageTakenBonusPercent,
                _thunderDamageTakenBonusPercent,
                _windDamageTakenBonusPercent,
                _relicDamageTakenBonusPercent,
                stateBonusPercent);
            int appliedDamage = IncomingDamageFormula.AppliedDamage(dmg, defense, totalBonusPercent);

            _hp -= appliedDamage;
            if (_hp <= 0)
            {
                if (CharacterState != CharacterState.Dead)
                {
                    bool wasPoisoned = IsPoisoned();
                    Vector3 deathPosition = transform.position;
                    CharacterState = CharacterState.Dead;
                    StopAllCoroutines();
                    // 벽을 여기서 넘긴다. 예전에는 EquipmentManager 가 안에서
                    // GameManager.Instance.wall 을 직접 붙잡았는데, 영구 메타 서비스가
                    // 전투 씬 오브젝트를 이름으로 찾는 거꾸로 된 방향이었다. (8.3a)
                    // GameManager 를 창구로 바꾸면서도 != null 검사는 남긴다. 지우면
                    // "벽 없이 죽인 경우"의 동작이 달라지고, 이 트랜치는 참조를 얻는
                    // 경로만 바꾸는 자리다.
                    EquipmentManager.Instance?.OnMonsterKilled(
                        battle.Game != null ? battle.Game.wall : null);
                    RelicManager.Instance?.OnMonsterKilled(this, wasPoisoned, deathPosition);
                    battle.Monsters.UnregisterMonster(this, true);
                    battle.Spawner.PoolMonster(this);
                }
            }

            return appliedDamage;
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.CompareTag("Wall") && !isAttacking)
            {
                StartAttack(col.GetComponent<Wall>());
            }
        }

        void OnTriggerExit2D(Collider2D col)
        {
            if (!isAttacking || col == null || !col.CompareTag("Wall"))
                return;

            Wall wall = col.GetComponent<Wall>();
            if (wall == null || wall == _attackWall)
                StopAttack();
        }

        public void ApplySlow(float duration = 2f, float multiplier = 0.8f)
        {
            if (!IsAlive)
                return;

            _slowUntilTime = Mathf.Max(_slowUntilTime, Clock.GameTime + Mathf.Max(0.1f, duration));
            ApplyMoveSpeed = Mathf.Max(moveSpeed * 0.2f, ApplyMoveSpeed * Mathf.Clamp(multiplier, 0.1f, 1f));
        }

        public void ApplyPoison(float duration = 4f, float damageMultiplier = 1f)
        {
            if (!IsAlive)
                return;

            _poisonUntilTime = Mathf.Max(_poisonUntilTime, Clock.GameTime + Mathf.Max(0.1f, duration));
            _poisonDamageMultiplier = Mathf.Max(_poisonDamageMultiplier, Mathf.Max(0.1f, damageMultiplier));

            if (_poisonRoutine == null)
                _poisonRoutine = StartCoroutine(PlayPoison());
        }

        public void ApplyStun(float duration)
        {
            if (!IsAlive)
                return;

            float validDuration = Mathf.Max(0.1f, duration);
            _stunnedUntilTime = Mathf.Max(_stunnedUntilTime, Clock.GameTime + validDuration);
        }

        public void ApplyPoisonDamageTakenBonus(int percent)
        {
            _poisonDamageTakenBonusPercent = Mathf.Max(_poisonDamageTakenBonusPercent, Mathf.Max(0, percent));
            _poisonUntilTime = Mathf.Max(_poisonUntilTime, Clock.GameTime + 4f);
        }

        public void ApplyStunDamageTakenBonus(int percent, float duration)
        {
            _stunDamageTakenBonusPercent = Mathf.Max(_stunDamageTakenBonusPercent, Mathf.Max(0, percent));
            _stunDamageTakenBonusUntil = Mathf.Max(_stunDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyArmorBreakDamageTakenBonus(int percent, float duration)
        {
            _armorBreakDamageTakenBonusPercent = Mathf.Max(_armorBreakDamageTakenBonusPercent, Mathf.Max(0, percent));
            _armorBreakDamageTakenBonusUntil = Mathf.Max(_armorBreakDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyThunderDamageTakenBonus(int percent, float duration)
        {
            _thunderDamageTakenBonusPercent = Mathf.Max(_thunderDamageTakenBonusPercent, Mathf.Max(0, percent));
            _thunderDamageTakenBonusUntil = Mathf.Max(_thunderDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyWindDamageTakenBonus(int percent, float duration)
        {
            _windDamageTakenBonusPercent = Mathf.Max(_windDamageTakenBonusPercent, Mathf.Max(0, percent));
            _windDamageTakenBonusUntil = Mathf.Max(_windDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyRelicDamageTakenBonus(int percent, float duration)
        {
            _relicDamageTakenBonusPercent = Mathf.Max(_relicDamageTakenBonusPercent, Mathf.Max(0, percent));
            _relicDamageTakenBonusUntil = Mathf.Max(_relicDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyDefenseDown(float duration, int percent)
        {
            if (!IsAlive)
                return;

            int validPercent = Mathf.Max(0, percent);
            int validAmount = Mathf.RoundToInt(_baseDefense * (validPercent * 0.01f));
            if (validAmount <= 0)
                return;

            _defenseDownAmount = Mathf.Max(_defenseDownAmount, validAmount);
            RecalculateDefense();

            if (_defenseDownRoutine != null)
                StopCoroutine(_defenseDownRoutine);

            _defenseDownRoutine = StartCoroutine(CoApplyDefenseDown(Mathf.Max(0.1f, duration)));
        }

        public void PushBy(Vector2 direction, float distance)
        {
            if (!IsAlive)
                return;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.position += (Vector3)(direction.normalized * Mathf.Max(0f, distance));
        }

        public void PullTowards(Vector2 center, float distance)
        {
            if (!IsAlive)
                return;

            Vector2 direction = center - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.position += (Vector3)(direction.normalized * Mathf.Max(0f, distance));
        }

        public void AddSmoothPull(Vector2 center, float distance, float duration)
        {
            if (!IsAlive)
                return;

            float addDistance = Mathf.Max(0f, distance);
            if (addDistance <= 0.0001f)
                return;

            _pullCenter = center;
            _pendingPullDistance += addDistance;
            _pullUntilTime = Mathf.Max(_pullUntilTime, Clock.GameTime + Mathf.Max(0.01f, duration));

            if (_pullRoutine == null)
                _pullRoutine = StartCoroutine(CoSmoothPull());
        }

        IEnumerator PlayPoison()
        {
            while (_hp > 0 && Clock.GameTime < _poisonUntilTime)
            {
                int intdamage = Mathf.CeilToInt((_hp * 0.1f) * _poisonDamageMultiplier);
                if (intdamage <= 0)
                    intdamage = 1;

                GameObject dtObj = battle.DamageTexts.GetDamageText();
                dtObj.transform.position = transform.position;
                dtObj.transform.ResetLocalZ();
                Color typeColor = DiceMetaDataProvider.GetColor(DiceType.Poison);

                int appliedDamage = TakeDamage(intdamage);
                if (appliedDamage > 0)
                    dtObj.GetComponent<DamageText>().SetText(appliedDamage, typeColor);
                else
                    dtObj.SetActive(false);

                yield return poisonDelay;
            }

            _poisonDamageMultiplier = 1f;
            _poisonDamageTakenBonusPercent = 0;
            _poisonRoutine = null;
        }

        IEnumerator AttackWall(Wall wall)
        {
            while (wall != null && wall.CurrentHp > 0)
            {
                if (!IsTouchingWall(wall))
                {
                    StopAttack();
                    yield break;
                }

                if (IsStunned())
                {
                    yield return null;
                    continue;
                }

                wall.TakeDamage(attackDamage);

                GameObject dtObj = battle.DamageTexts.GetDamageText();
                dtObj.transform.position = transform.position;
                dtObj.transform.ResetLocalZ();
                dtObj.GetComponent<DamageText>().SetText(attackDamage * -1, Color.red);

                yield return new WaitForSeconds(attackInterval);
            }

            StopAttack();
        }

        IEnumerator CoApplyDefenseDown(float duration)
        {
            yield return new WaitForSeconds(duration);

            _defenseDownAmount = 0;
            RecalculateDefense();
            _defenseDownRoutine = null;
        }

        private void RecalculateDefense()
        {
            defense = _baseDefense - _defenseDownAmount;
        }

        private IEnumerator CoSmoothPull()
        {
            while (IsAlive && _pendingPullDistance > 0.0001f)
            {
                float remainTime = Mathf.Max(0.01f, _pullUntilTime - Clock.GameTime);
                float step = _pendingPullDistance * (Time.deltaTime / remainTime);
                step = Mathf.Min(step, _pendingPullDistance);
                if (step > 0f)
                    PullTowards(_pullCenter, step);

                _pendingPullDistance -= step;
                yield return null;
            }

            _pendingPullDistance = 0f;
            _pullUntilTime = -1f;
            _pullRoutine = null;
        }

        private bool IsStunned()
        {
            return Clock.GameTime < _stunnedUntilTime;
        }

        public bool IsSlowed()
        {
            return IncomingDamageFormula.IsStateActive(Clock.GameTime, _slowUntilTime);
        }

        public bool IsPoisoned()
        {
            return IncomingDamageFormula.IsStateActive(Clock.GameTime, _poisonUntilTime);
        }

        /// <summary>
        /// 만료된 상태 피해증가를 0 으로 되돌린다. <b>판정만</b> OJ.Core 로 내려갔다 —
        /// 필드 write 는 여기 남는다(순수 함수가 될 수 없는 부분이다).
        ///
        /// 원본은 Time.time 을 6번 읽었다. 같은 프레임 안에서 Time.time 은 고정값이고 이 메서드는
        /// 중간에 yield 하지 않으므로 한 번 읽어 쓰는 것과 결과가 같다.
        ///
        /// poison 만 전용 until 필드가 없고 IsPoisoned() 가 쓰는 _poisonUntilTime 을 공유한다.
        /// 그래서 (a) poison 만 until 을 -1f 로 되돌리지 않고, (b) ApplyPoisonDamageTakenBonus 가
        /// 중독 지속시간 자체를 Time.time + 4f 로 연장하는 부작용을 갖는다. 나머지 5종에는 없다.
        /// 의도인지 사고인지 코드로는 판정할 수 없어 <b>그대로 보존한다.</b>
        /// </summary>
        private void CleanupExpiredDamageBonuses()
        {
            float now = Clock.GameTime;

            if (IncomingDamageFormula.IsBonusExpired(now, _poisonUntilTime))
                _poisonDamageTakenBonusPercent = 0;

            if (IncomingDamageFormula.IsBonusExpired(now, _stunDamageTakenBonusUntil))
            {
                _stunDamageTakenBonusPercent = 0;
                _stunDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _armorBreakDamageTakenBonusUntil))
            {
                _armorBreakDamageTakenBonusPercent = 0;
                _armorBreakDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _thunderDamageTakenBonusUntil))
            {
                _thunderDamageTakenBonusPercent = 0;
                _thunderDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _windDamageTakenBonusUntil))
            {
                _windDamageTakenBonusPercent = 0;
                _windDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _relicDamageTakenBonusUntil))
            {
                _relicDamageTakenBonusPercent = 0;
                _relicDamageTakenBonusUntil = -1f;
            }
        }

        private bool IsBeingPulled()
        {
            return _pullRoutine != null && Clock.GameTime < _pullUntilTime;
        }

        private void UpdateTimedStates()
        {
            if (Clock.GameTime >= _slowUntilTime && ApplyMoveSpeed != moveSpeed)
                ApplyMoveSpeed = moveSpeed;
        }

        private void StartAttack(Wall wall)
        {
            if (wall == null || isAttacking)
                return;

            isAttacking = true;
            _attackWall = wall;
            _attackRoutine = StartCoroutine(AttackWall(wall));
        }

        private void StopAttack()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            _attackWall = null;
            isAttacking = false;
        }

        private bool IsTouchingWall(Wall wall)
        {
            if (wall == null)
                return false;

            Collider2D myCollider = GetComponent<Collider2D>();
            Collider2D wallCollider = wall.GetComponent<Collider2D>();
            if (myCollider != null && wallCollider != null)
                return myCollider.IsTouching(wallCollider);

            // Fallback when colliders are missing.
            return Vector2.Distance(transform.position, wall.transform.position) <= 1.0f;
        }

        public void SetHp(int hp)
        {
            _hp = hp;
        }

        public void SetCombatStats(int hp, int baseDefenseValue, float scaleMultiplier = 1f)
        {
            _hp = Mathf.Max(1, hp);
            _baseDefense = Mathf.Max(0, baseDefenseValue);
            _defenseDownAmount = 0;
            RecalculateDefense();

            if (_defaultLocalScale == Vector3.zero)
                _defaultLocalScale = Vector3.one;

            transform.localScale = _defaultLocalScale * Mathf.Max(0.1f, scaleMultiplier);
        }
    }
}
