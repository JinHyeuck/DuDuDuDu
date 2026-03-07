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
        private float _paralyzedUntilTime;
        private Coroutine _defenseDownRoutine;
        private Coroutine _attackRoutine;
        private Wall _attackWall;
        private Coroutine _pullRoutine;
        private float _pendingPullDistance;
        private float _pullUntilTime;
        private Vector2 _pullCenter;

        public void OnSpawn()
        {
            StopAllCoroutines();
            isAttacking = false;

            _baseDefense = defense;
            _defenseDownAmount = 0;
            _paralyzedUntilTime = -1f;
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
            _paralyzedUntilTime = -1f;
            _attackRoutine = null;
            _attackWall = null;
            _pullRoutine = null;
            _pendingPullDistance = 0f;
            _pullUntilTime = -1f;
            RecalculateDefense();

            MonsterManager.Instance?.UnregisterMonster(this, false);
        }

        void Update()
        {
            if (IsParalyzed())
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
            int appliedDamage = Mathf.CeilToInt(dmg * damageMultiplier);

            _hp -= appliedDamage;
            if (_hp <= 0)
            {
                if (CharacterState != CharacterState.Dead)
                {
                    CharacterState = CharacterState.Dead;
                    StopAllCoroutines();
                    UIDiceSummonSystem.Instance?.AddSP(10);
                    EquipmentManager.Instance?.OnMonsterKilled();
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

        public void ApplySlow()
        {
            if (!IsAlive)
                return;

            ApplyMoveSpeed *= 0.8f;
        }

        public void ApplyPoison()
        {
            if (!IsAlive)
                return;

            StartCoroutine(PlayPoison());
        }

        public void ApplyParalysis(float duration)
        {
            if (!IsAlive)
                return;

            float validDuration = Mathf.Max(0.1f, duration);
            _paralyzedUntilTime = Mathf.Max(_paralyzedUntilTime, Time.time + validDuration);
        }

        public void ApplyDefenseDown(float duration, int amount)
        {
            if (!IsAlive)
                return;

            int validAmount = Mathf.Max(0, amount);
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
            while (_hp > 0)
            {
                int intdamage = _hp * 10 / 100;
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

                if (IsParalyzed())
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

        private bool IsParalyzed()
        {
            return Time.time < _paralyzedUntilTime;
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
    }
}
