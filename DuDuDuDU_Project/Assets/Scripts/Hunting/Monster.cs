using UnityEngine;
using System.Collections;

namespace OJ
{
    public class Monster : MonoBehaviour
    {
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

            MonsterManager.Instance.RegisterMonster(this);
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

            MonsterManager.Instance?.UnregisterMonster(this, false);
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
                    MonsterSpawner.Instance.PoolMonster(this);
            }
        }

        public int TakeDamage(int dmg)
        {
            if (!gameObject.activeInHierarchy || CharacterState == CharacterState.Dead)
                return 0;

            if (dmg <= 0)
                return 0;

            float armor = defense;
            float damageMultiplier = armor >= 0f
                ? 100f / (100f + armor)
                : 2f - (100f / (100f - armor));
            CleanupExpiredDamageBonuses();
            int stateBonusPercent = 0;
            if (IsSlowed() && DiceMetaDataProvider.HasKingIceDamageBonus())
                stateBonusPercent += 15;
            if (IsPoisoned() && DiceMetaDataProvider.HasKingPoisonDamageBonus())
                stateBonusPercent += 15;
            if (IsSlowed() && RelicManager.Instance != null)
                stateBonusPercent += RelicManager.Instance.GetSlowDamageTakenBonusPercent();

            float incomingDamageMultiplier = 1f + (_poisonDamageTakenBonusPercent + _stunDamageTakenBonusPercent + _armorBreakDamageTakenBonusPercent + _thunderDamageTakenBonusPercent + _windDamageTakenBonusPercent + _relicDamageTakenBonusPercent + stateBonusPercent) * 0.01f;
            int appliedDamage = Mathf.CeilToInt(dmg * damageMultiplier * incomingDamageMultiplier);

            _hp -= appliedDamage;
            if (_hp <= 0)
            {
                if (CharacterState != CharacterState.Dead)
                {
                    bool wasPoisoned = IsPoisoned();
                    Vector3 deathPosition = transform.position;
                    CharacterState = CharacterState.Dead;
                    StopAllCoroutines();
                    EquipmentManager.Instance?.OnMonsterKilled();
                    RelicManager.Instance?.OnMonsterKilled(this, wasPoisoned, deathPosition);
                    MonsterManager.Instance.UnregisterMonster(this, true);
                    MonsterSpawner.Instance.PoolMonster(this);
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

            _slowUntilTime = Mathf.Max(_slowUntilTime, Time.time + Mathf.Max(0.1f, duration));
            ApplyMoveSpeed = Mathf.Max(moveSpeed * 0.2f, ApplyMoveSpeed * Mathf.Clamp(multiplier, 0.1f, 1f));
        }

        public void ApplyPoison(float duration = 4f, float damageMultiplier = 1f)
        {
            if (!IsAlive)
                return;

            _poisonUntilTime = Mathf.Max(_poisonUntilTime, Time.time + Mathf.Max(0.1f, duration));
            _poisonDamageMultiplier = Mathf.Max(_poisonDamageMultiplier, Mathf.Max(0.1f, damageMultiplier));

            if (_poisonRoutine == null)
                _poisonRoutine = StartCoroutine(PlayPoison());
        }

        public void ApplyStun(float duration)
        {
            if (!IsAlive)
                return;

            float validDuration = Mathf.Max(0.1f, duration);
            _stunnedUntilTime = Mathf.Max(_stunnedUntilTime, Time.time + validDuration);
        }

        public void ApplyPoisonDamageTakenBonus(int percent)
        {
            _poisonDamageTakenBonusPercent = Mathf.Max(_poisonDamageTakenBonusPercent, Mathf.Max(0, percent));
            _poisonUntilTime = Mathf.Max(_poisonUntilTime, Time.time + 4f);
        }

        public void ApplyStunDamageTakenBonus(int percent, float duration)
        {
            _stunDamageTakenBonusPercent = Mathf.Max(_stunDamageTakenBonusPercent, Mathf.Max(0, percent));
            _stunDamageTakenBonusUntil = Mathf.Max(_stunDamageTakenBonusUntil, Time.time + Mathf.Max(0.1f, duration));
        }

        public void ApplyArmorBreakDamageTakenBonus(int percent, float duration)
        {
            _armorBreakDamageTakenBonusPercent = Mathf.Max(_armorBreakDamageTakenBonusPercent, Mathf.Max(0, percent));
            _armorBreakDamageTakenBonusUntil = Mathf.Max(_armorBreakDamageTakenBonusUntil, Time.time + Mathf.Max(0.1f, duration));
        }

        public void ApplyThunderDamageTakenBonus(int percent, float duration)
        {
            _thunderDamageTakenBonusPercent = Mathf.Max(_thunderDamageTakenBonusPercent, Mathf.Max(0, percent));
            _thunderDamageTakenBonusUntil = Mathf.Max(_thunderDamageTakenBonusUntil, Time.time + Mathf.Max(0.1f, duration));
        }

        public void ApplyWindDamageTakenBonus(int percent, float duration)
        {
            _windDamageTakenBonusPercent = Mathf.Max(_windDamageTakenBonusPercent, Mathf.Max(0, percent));
            _windDamageTakenBonusUntil = Mathf.Max(_windDamageTakenBonusUntil, Time.time + Mathf.Max(0.1f, duration));
        }

        public void ApplyRelicDamageTakenBonus(int percent, float duration)
        {
            _relicDamageTakenBonusPercent = Mathf.Max(_relicDamageTakenBonusPercent, Mathf.Max(0, percent));
            _relicDamageTakenBonusUntil = Mathf.Max(_relicDamageTakenBonusUntil, Time.time + Mathf.Max(0.1f, duration));
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
            _pullUntilTime = Mathf.Max(_pullUntilTime, Time.time + Mathf.Max(0.01f, duration));

            if (_pullRoutine == null)
                _pullRoutine = StartCoroutine(CoSmoothPull());
        }

        IEnumerator PlayPoison()
        {
            while (_hp > 0 && Time.time < _poisonUntilTime)
            {
                int intdamage = Mathf.CeilToInt((_hp * 0.1f) * _poisonDamageMultiplier);
                if (intdamage <= 0)
                    intdamage = 1;

                GameObject dtObj = DamageTextPool.Instance.GetDamageText();
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

                GameObject dtObj = DamageTextPool.Instance.GetDamageText();
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
                float remainTime = Mathf.Max(0.01f, _pullUntilTime - Time.time);
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
            return Time.time < _stunnedUntilTime;
        }

        public bool IsSlowed()
        {
            return Time.time < _slowUntilTime;
        }

        public bool IsPoisoned()
        {
            return Time.time < _poisonUntilTime;
        }

        private void CleanupExpiredDamageBonuses()
        {
            if (Time.time >= _poisonUntilTime)
                _poisonDamageTakenBonusPercent = 0;

            if (Time.time >= _stunDamageTakenBonusUntil)
            {
                _stunDamageTakenBonusPercent = 0;
                _stunDamageTakenBonusUntil = -1f;
            }

            if (Time.time >= _armorBreakDamageTakenBonusUntil)
            {
                _armorBreakDamageTakenBonusPercent = 0;
                _armorBreakDamageTakenBonusUntil = -1f;
            }

            if (Time.time >= _thunderDamageTakenBonusUntil)
            {
                _thunderDamageTakenBonusPercent = 0;
                _thunderDamageTakenBonusUntil = -1f;
            }

            if (Time.time >= _windDamageTakenBonusUntil)
            {
                _windDamageTakenBonusPercent = 0;
                _windDamageTakenBonusUntil = -1f;
            }

            if (Time.time >= _relicDamageTakenBonusUntil)
            {
                _relicDamageTakenBonusPercent = 0;
                _relicDamageTakenBonusUntil = -1f;
            }
        }

        private bool IsBeingPulled()
        {
            return _pullRoutine != null && Time.time < _pullUntilTime;
        }

        private void UpdateTimedStates()
        {
            if (Time.time >= _slowUntilTime && ApplyMoveSpeed != moveSpeed)
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
