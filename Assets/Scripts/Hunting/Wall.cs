using UnityEngine;

public class Wall : MonoBehaviour
{
    public int hp = 10;

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            GameManager.Instance.GameOver();
            Destroy(gameObject);
        }
    }
}
