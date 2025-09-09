using UnityEngine;
using System.Collections;

public class Monster : MonoBehaviour
{
    public int _hp = 3;
    public int attackDamage = 1;
    public float attackInterval = 0.5f;
    public float moveSpeed = 2f; // �̵� �ӵ�
    private bool isAttacking = false;

    private void OnEnable()
    {
        MonsterManager.Instance.RegisterMonster(this);
    }

    private void OnDisable()
    {
        MonsterManager.Instance.UnregisterMonster(this);
        UIDiceSummonSystem.Instance?.AddSP(10);
    }

    void Update()
    {
        // Wall�� ���� ��� �̵� (���� �������� ���� ����)
        if (!isAttacking)
        {
            transform.Translate(Vector2.down * moveSpeed * Time.deltaTime);
        }
    }

    public void TakeDamage(int dmg)
    {
        _hp -= dmg;
        if (_hp <= 0)
            gameObject.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Wall") && !isAttacking)
        {
            isAttacking = true;
            StartCoroutine(AttackWall(col.GetComponent<Wall>()));
        }
    }

    IEnumerator AttackWall(Wall wall)
    {
        while (wall != null && wall.hp > 0)
        {
            wall.TakeDamage(attackDamage);

            // ������ UI ǥ��
            GameObject dtObj = DamageTextPool.Instance.GetDamageText();
            dtObj.transform.position = transform.position; // ���� ��ġ
            dtObj.GetComponent<DamageText>().SetText(attackDamage);

            yield return new WaitForSeconds(attackInterval);
        }
        gameObject.SetActive(false);
    }

    public void SetHp(int hp)
    {
        _hp = hp;
    }
}
