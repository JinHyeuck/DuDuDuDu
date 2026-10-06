using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 구매 창의 보상 미리보기 한 칸. 누를 수 없는 그림이다 — 수령 칸(<see cref="UISeasonPassSlot"/>)과
    /// 상태·버튼이 없다는 점이 달라 따로 둔다.
    /// </summary>
    public sealed class UISeasonPassRewardCell : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;

        public void Bind(PointRewardEntry reward)
        {
            if (amountText != null)
                amountText.SetText(reward.Amount.ToString());

            if (icon != null)
            {
                Sprite sprite = PointRewardUtility.GetPointIcon(reward.PointType);
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        internal static UISeasonPassRewardCell Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassRewardCell", parent);
            var cell = root.AddComponent<UISeasonPassRewardCell>();

            // 바탕 — Itme_Slot_5(유료 칸 주황) x3, 보이는 135. 오른쪽·아래 여백이 1px 넓어 (1.5,-1.5)
            UISeasonPassUIFactory.Picture(root.transform, "Bg", "ItemSlot/Itme_Slot_5",
                new Vector2(192f, 192f), new Vector2(1.5f, -1.5f));

            Image image = UISeasonPassUIFactory.CreateImage("Icon", root.transform, Color.white);
            UISeasonPassUIFactory.SetRect(image.rectTransform, new Vector2(110f, 110f), new Vector2(0f, 14f));
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            cell.icon = image;

            cell.amountText = UISeasonPassUIFactory.CreateFittedText(
                "Amount", root.transform, "0", 34f, 20f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(cell.amountText.rectTransform, new Vector2(126f, 44f), new Vector2(1f, -34f));

            return cell;
        }
    }
}
