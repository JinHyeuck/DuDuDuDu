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
        public int attackDamage = 1;
        public float attackInterval = 0.5f;
        public float moveSpeed = 2f; // 이동 속도
        public float ApplyMoveSpeed = 2f; // 이동 속도
        private bool isAttacking = false;

        public CharacterAnimation characterAnimation;

        private WaitForSeconds poisonDelay = new WaitForSeconds(0.5f);

        public void OnSpawn()
        {
            StopAllCoroutines();
            isAttacking = false;
            MonsterManager.Instance.RegisterMonster(this);
            ApplyMoveSpeed = moveSpeed;
            characterAnimation.PlayAnimation(CharacterState.Run);
            CharacterState = CharacterState.Run;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isAttacking = false;
            MonsterManager.Instance?.UnregisterMonster(this, false);
        }

        void Update()
        {
            // Wall을 향해 계속 이동 (아직 공격하지 않을 때만)
            if (!isAttacking)
            {
                transform.Translate(Vector2.down * ApplyMoveSpeed * Time.deltaTime);

                if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
                    MonsterSpawner.Instance.PoolMonster(this);
            }
        }

        public void TakeDamage(int dmg)
        {
            if (!gameObject.activeInHierarchy || CharacterState == CharacterState.Dead)
                return;

            _hp -= dmg;
            if (_hp <= 0)
            {
                if (CharacterState != CharacterState.Dead)
                {
                    CharacterState = CharacterState.Dead;
                    StopAllCoroutines();
                    UIDiceSummonSystem.Instance?.AddSP(10);
                    MonsterManager.Instance.UnregisterMonster(this, true);
                    MonsterSpawner.Instance.PoolMonster(this);
                }
            }
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.CompareTag("Wall") && !isAttacking)
            {
                isAttacking = true;
                StartCoroutine(AttackWall(col.GetComponent<Wall>()));
            }
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

        IEnumerator PlayPoison()
        {
            while (_hp > 0)
            {
                int intdamage = _hp * 10 / 100;
                if (intdamage <= 0)
                    intdamage = 1;

                GameObject dtObj = DamageTextPool.Instance.GetDamageText();
                dtObj.transform.position = transform.position; // 몬스터 위치
                dtObj.transform.ResetLocalZ();
                Color typeColor = DiceMetaDataProvider.GetColor(DiceType.Poison);

                dtObj.GetComponent<DamageText>().SetText(intdamage, typeColor);

                TakeDamage(intdamage);

                yield return poisonDelay;
            }
        }

        IEnumerator AttackWall(Wall wall)
        {
            while (wall != null && wall.CurrentHp > 0)
            {
                wall.TakeDamage(attackDamage);

                // 데미지 UI 표시
                GameObject dtObj = DamageTextPool.Instance.GetDamageText();
                dtObj.transform.position = transform.position; // 몬스터 위치
                dtObj.transform.ResetLocalZ();
                dtObj.GetComponent<DamageText>().SetText(attackDamage * -1, Color.red);

                yield return new WaitForSeconds(attackInterval);
            }
            //gameObject.SetActive(false);
        }

        public void SetHp(int hp)
        {
            _hp = hp;
        }
    }

}
