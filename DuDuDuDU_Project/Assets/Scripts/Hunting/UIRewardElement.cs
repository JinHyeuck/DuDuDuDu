using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Hunting
{
    /// <summary>
    /// 보상 한 칸. 재화든 다이스든 <b>이 칸 하나로 그린다.</b>
    ///
    /// <b>왜 다이스용 칸을 따로 안 만드나.</b> 이 프리팹
    /// (<c>Assets/Prefab/Hunting/UIRewardElement.prefab</c>)은 이미 결과창·스테이지 보상·
    /// 별 보상·스테이지 결과 <b>네 화면이 함께 참조</b>한다. 여기에 능력을 더하면 네 곳이
    /// 한 번에 따라오지만, 새 칸을 만들면 네 화면에 각각 배선을 심어야 하고
    /// 그중 하나를 빠뜨리면 <b>그 화면에서만 다이스가 안 보이는</b> 상태가 된다.
    /// </summary>
    public class UIRewardElement : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;

        /// <summary>
        /// 아이콘 위에 겹치는 작은 아이콘. <b>이미 가진 다이스가 재화로 돌아온 경우</b>에만 켜진다.
        ///
        /// 환급 재화만 덩그러니 보여 주면 "무엇 때문에 받은 재화인지" 를 알 수 없다.
        /// 그래서 <b>다이스를 딤 처리해 밑에 깔고</b> 그 위에 재화 아이콘을 얹는다 —
        /// "이 다이스 대신 이걸 받았다" 가 그림 하나로 읽힌다.
        /// </summary>
        [SerializeField] private Image overlayIcon;

        /// <summary>이미 가진 다이스를 그릴 때의 색. 흑백에 가깝게 눌러 "이건 못 받았다" 를 말한다.</summary>
        private static readonly Color DimmedColor = new Color(0.42f, 0.42f, 0.46f, 1f);

        public void Construct(Image icon, TMP_Text amount)
        {
            iconImage = icon;
            amountText = amount;
        }

        public void Bind(Sprite iconSprite, int amount)
        {
            Bind(iconSprite, amount, "{0:#,##0}");
        }

        /// <summary>재화 한 칸. 다이스로 쓰였던 칸이 재사용될 수 있어 <b>상태를 되돌린다.</b></summary>
        public void Bind(Sprite iconSprite, int amount, string amountFormat)
        {
            ResetVisual();

            if (iconImage != null)
                iconImage.sprite = iconSprite;

            if (amountText != null)
            {
                amountText.gameObject.SetActive(true);

                if (string.IsNullOrEmpty(amountFormat))
                    amountText.SetText("{0:#,##0}", amount);
                else
                    amountText.SetText(amountFormat, amount);
            }
        }

        /// <summary>
        /// 처음 얻은 다이스. 개수는 언제나 하나라 숫자를 적지 않는다 —
        /// "x1" 은 알려 주는 것이 없고 아이콘만 좁힌다.
        /// </summary>
        public void BindDice(Sprite diceSprite)
        {
            ResetVisual();

            if (iconImage != null)
                iconImage.sprite = diceSprite;

            if (amountText != null)
                amountText.gameObject.SetActive(false);
        }

        /// <summary>
        /// 이미 가진 다이스라 재화로 돌아온 경우.
        /// 딤 처리한 다이스 + 그 위의 재화 아이콘 + 아래의 수량.
        /// </summary>
        public void BindDice(Sprite diceSprite, Sprite refundSprite, int refundAmount)
        {
            ResetVisual();

            if (iconImage != null)
            {
                iconImage.sprite = diceSprite;
                iconImage.color = DimmedColor;
            }

            if (overlayIcon != null)
            {
                overlayIcon.sprite = refundSprite;
                overlayIcon.gameObject.SetActive(true);
            }

            if (amountText != null)
            {
                amountText.gameObject.SetActive(true);
                amountText.SetText("x{0:#,##0}", refundAmount);
            }
        }

        /// <summary>
        /// 칸을 기본 상태로 되돌린다.
        ///
        /// <b>없으면 조용히 틀린다.</b> 이 칸들은 목록에서 <c>Instantiate</c> 후 재사용되므로,
        /// 앞 번에 딤 처리된 칸이 다음 번 재화 보상에 그대로 쓰이면 <b>골드가 회색으로</b> 뜬다.
        /// </summary>
        private void ResetVisual()
        {
            if (iconImage != null)
                iconImage.color = Color.white;

            if (overlayIcon != null)
                overlayIcon.gameObject.SetActive(false);
        }
    }
}
