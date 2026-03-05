using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace OJ
{
    public class Wall : MonoBehaviour
    {
        public int TotalHp = 10;

        public int CurrentHp = 10;

        public TMP_Text CurrentHp_Text;
        public RectTransform wallHp_RectTrans;
        public float wallHp_Width = 1025.0f;


        public void SetInit(int hp)
        {
            TotalHp = hp;
            CurrentHp = hp;

            if (CurrentHp_Text != null)
                CurrentHp_Text.SetText("{0}", hp);
        }

        public void TakeDamage(int dmg)
        {
            CurrentHp -= dmg;
            if (CurrentHp < 0)
                CurrentHp = 0;

            if (CurrentHp_Text != null)
                CurrentHp_Text.SetText("{0}", CurrentHp);

            float ratio = (float)CurrentHp / (float)TotalHp;
            if (ratio < 0)
                ratio = 0;

            Vector2 vector2 = wallHp_RectTrans.sizeDelta;
            vector2.x = wallHp_Width * ratio;
            wallHp_RectTrans.sizeDelta = vector2;

            if (CurrentHp <= 0)
            {
                GameManager.Instance.GameOver();
                Destroy(gameObject);
            }
        }

        public void Heal(int value)
        {
            if (value <= 0 || CurrentHp <= 0)
                return;

            CurrentHp += value;
            if (CurrentHp > TotalHp)
                CurrentHp = TotalHp;

            if (CurrentHp_Text != null)
                CurrentHp_Text.SetText("{0}", CurrentHp);

            float ratio = TotalHp > 0 ? (float)CurrentHp / TotalHp : 0f;
            ratio = Mathf.Clamp01(ratio);

            Vector2 vector2 = wallHp_RectTrans.sizeDelta;
            vector2.x = wallHp_Width * ratio;
            wallHp_RectTrans.sizeDelta = vector2;
        }
    }

}
