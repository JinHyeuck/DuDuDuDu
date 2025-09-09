using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class UIDice : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Image BGImage;          // ���̽� ���
    public Image Icon;
    public TMP_Text StarText;
    public TMP_Text TypeText;
    public Animator animator;

    public DiceType Type { get; private set; }
    public int Star { get; private set; }
    public int SlotIndex { get; private set; }

    private Transform originalParent;
    private Vector3 originalPos;
    private CanvasGroup canvasGroup;
    private Canvas canvas;

    public void Init(DiceType type, int star, int slotIndex)
    {
        Type = type;
        Star = star;
        SlotIndex = slotIndex;
        Refresh();
    }

    private void Awake()
    {
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void Refresh()
    {
        StarText.text = $"��{Star}";
        TypeText.text = Type.ToString();

        Color typeColor = DiceTypeResourceManager.Instance.GetColor(Type);
        Sprite typeSprite = DiceTypeResourceManager.Instance.GetIcon(Type);

        if (BGImage != null)
            BGImage.color = typeColor;

        if (Icon != null)
        {
            if (typeSprite != null) Icon.sprite = typeSprite;
        }

        if (Star >= MergeSystem.MaxStar && animator != null)
            animator.SetBool("MaxStar", true);
        else if (animator != null)
            animator.SetBool("MaxStar", false);
    }

    public void SetStar(int star)
    {
        Star = star;
        Refresh();
    }

    #region Drag Handlers

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent = transform.parent;
        originalPos = transform.localPosition;

        transform.SetParent(canvas.transform, true); // �ֻ��� ĵ������ �̵�
        canvasGroup.blocksRaycasts = false;          // Raycast �����ؼ� �ڱ� �ڽ��� Drop Ÿ�� ���� �ʰ�
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, eventData.position, canvas.worldCamera, out pos);
        transform.position = canvas.transform.TransformPoint(pos);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;

        // ���� �õ�
        GameObject hitObj = eventData.pointerCurrentRaycast.gameObject;
        if (hitObj != null)
        {
            UIDice targetDice = hitObj.GetComponentInParent<UIDice>();
            if (targetDice != null && targetDice != this)
            {
                bool merged = MergeSystem.Instance.TryMerge(this, targetDice);
                if (merged)
                    return; // ���� �����ϸ� �巡�� ���̽� �ı������Ƿ� ����
            }
        }

        // �巡�� ���� �� ���� �ڸ��� ����
        transform.SetParent(originalParent);
        transform.localPosition = originalPos;
    }

    #endregion
}
