using UnityEngine;
using TMPro;

namespace OJ.Hunting
{
    public class DamageText : MonoBehaviour
    {
        public float moveSpeed = 1f;
        public float duration = 0.8f;
        private TextMeshProUGUI textMesh;
        private float timer = 0f;

        private void Awake()
        {
            textMesh = GetComponent<TextMeshProUGUI>();
        }

        public void SetText(int damage)
        {
            textMesh.text = damage.ToString();
            textMesh.color = Color.white;
            timer = 0f;
            gameObject.SetActive(true);
        }

        public void SetText(int damage, Color color)
        {
            textMesh.text = damage.ToString();
            textMesh.color = color;
            timer = 0f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
            timer += Time.deltaTime;
            if (timer >= duration)
                gameObject.SetActive(false);
        }
    }

}
