using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;
using OJ.UI;

namespace OJ.Bag
{
    /// <summary>
    /// 가방에서 재화 한 칸을 눌렀을 때 뜨는 작은 설명 창. 이름·그림·보유량·한 줄 설명.
    ///
    /// <b>가방과 별개의 창이다.</b> 가방 안에 끼워 넣으면 백키가 가방째 닫혀 버린다 —
    /// 따로 두면 백키 스택에 가방 위로 쌓여서 이 창만 먼저 닫힌다.
    /// </summary>
    public sealed class UIItemDetailPopup : DialogBase
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text descriptionText;

        protected override void OnLoad()
        {
            UseBackBtn = true;
        }

        public void Open(PointType type)
        {
            Sprite sprite = PointRewardUtility.GetPointIcon(type);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (nameText != null)
                nameText.text = PointRewardUtility.GetPointName(type);

            if (amountText != null)
            {
                int amount = PointManager.Instance != null ? PointManager.Instance.Get(type) : 0;
                amountText.text = "보유한 수량 : " + amount;
            }

            if (descriptionText != null)
                descriptionText.text = PointRewardUtility.GetPointDescription(type);

            Enter();
        }
    }
}
