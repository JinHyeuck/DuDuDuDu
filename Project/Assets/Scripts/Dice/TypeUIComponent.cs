using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TypeUIComponent : MonoBehaviour
{
    public Image BGImage;        // 타입 UI 배경
    public Image Icon;
    public TMP_Text TypeLabel;
    public TMP_Text Star;

    private DiceType type;

    public void Init(DiceType t)
    {
        type = t;
        if (TypeLabel != null)
            TypeLabel.text = type.ToString();
    }

    private void Start()
    {
        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if (DiceTypeStarManager.Instance == null) return;

        Color typeColor = DiceTypeResourceManager.Instance.GetColor(type);
        Sprite typeSprite = DiceTypeResourceManager.Instance.GetIcon(type);

        // BG 색상만 변경
        if (BGImage != null)
            BGImage.color = typeColor;

        if (Icon != null)
        {
            if (typeSprite != null) Icon.sprite = typeSprite;
        }

        if (Star != null)
            Star.text = DiceTypeStarManager.Instance.GetTypeStars(type).ToString();
    }
}
