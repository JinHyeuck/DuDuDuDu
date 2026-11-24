using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Bag
{
    /// <summary>
    /// 가방 한 칸. 재화 그림과 보유 수량.
    ///
    /// 그림이 없는 재화(예: 시너지 티켓)는 <b>이름 글자로 대신한다</b> — 빈 칸으로 두면 무엇인지
    /// 알 길이 없고, 그림이 들어오는 순간 저절로 그림으로 바뀐다.
    /// </summary>
    public sealed class UIItemSlot : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text nameText;

        private PointType type;
        private Action<PointType> clicked;

        public PointType Type => type;

        public void Bind(PointType pointType, int amount, Action<PointType> onClick)
        {
            type = pointType;
            clicked = onClick;

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }

            Sprite sprite = PointRewardUtility.GetPointIcon(pointType);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = PointRewardUtility.GetPointName(pointType);
                nameText.gameObject.SetActive(sprite == null);
            }

            SetAmount(amount);
        }

        public void SetAmount(int amount)
        {
            if (countText != null)
                countText.text = "X" + amount;
        }

        private void OnClick()
        {
            clicked?.Invoke(type);
        }
    }
}
