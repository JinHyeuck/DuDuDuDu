using UnityEngine;
using TMPro;

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
