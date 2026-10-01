using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Mission
{
    /// <summary>
    /// 추가 보상 한 칸. 게이지 위에 놓인 선물 상자 하나다.
    ///
    /// <b>상태가 셋이다.</b> 못 연 것(어두움) · 받을 수 있는 것(밝음 + 버튼) ·
    /// 이미 받은 것(완료 표시). 셋을 색 하나로만 가르면 "받을 수 있는데 안 받은" 칸이
    /// 눈에 안 띄어 그대로 자정을 넘긴다.
    /// </summary>
    public sealed class UIMissionTierItem : MonoBehaviour
    {
        internal static readonly Vector2 ItemSize = new Vector2(120f, 150f);

        [SerializeField] private Image box;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button claimButton;
        [SerializeField] private GameObject doneMark;

        private Action claimAction;

        private void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClickClaim);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(OnClickClaim);
        }

        private void OnClickClaim()
        {
            // UIMissionRow 와 같은 이유로 지역 변수에 떼어 낸 뒤 부른다.
            Action action = claimAction;
            action?.Invoke();
        }

        public void Bind(DailyMissionTier tier, bool unlocked, bool claimed, Action onClaim)
        {
            claimAction = onClaim;

            if (countText != null)
                countText.SetText(MissionText.TierLabel(tier.requiredClearCount));

            bool claimable = unlocked && !claimed;

            if (box != null)
            {
                box.color = claimed
                    ? UIMissionUIFactory.DisabledColor
                    : (unlocked ? UIMissionUIFactory.ClaimColor : UIMissionUIFactory.RowColor);
            }

            if (claimButton != null)
                claimButton.gameObject.SetActive(claimable);

            if (doneMark != null)
                doneMark.SetActive(claimed);

            if (rewardIcon != null)
            {
                bool has = tier.rewards != null && tier.rewards.Count > 0;
                Sprite icon = has ? PointRewardUtility.GetPointIcon(tier.rewards[0].pointType) : null;
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
            }
        }

        internal static UIMissionTierItem Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UIMissionTierItem", parent);
            var rect = root.GetComponent<RectTransform>();
            UIMissionUIFactory.SetRect(rect, ItemSize, Vector2.zero);

            var item = root.AddComponent<UIMissionTierItem>();

            Image box = UIMissionUIFactory.CreateImage("Box", root.transform, UIMissionUIFactory.RowColor);
            UIMissionUIFactory.SetRect(box.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 20f));
            box.raycastTarget = false;
            item.box = box;

            Image icon = UIMissionUIFactory.CreateImage("RewardIcon", box.transform, Color.white);
            UIMissionUIFactory.SetRect(icon.rectTransform, new Vector2(72f, 72f), Vector2.zero);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            item.rewardIcon = icon;

            item.countText = UIMissionUIFactory.CreateText(
                "Count", root.transform, "0", 34f,
                TextAlignmentOptions.Center, UIMissionUIFactory.DarkText, font);
            UIMissionUIFactory.SetRect(
                item.countText.rectTransform, new Vector2(120f, 40f), new Vector2(0f, -52f));

            // 버튼은 상자 전체를 덮는다. 작은 칸이라 따로 그린 버튼을 얹으면
            // 상자가 가려지고, 누를 곳도 더 작아진다.
            Image hit = UIMissionUIFactory.CreateImage("ClaimButton", root.transform, new Color(1f, 1f, 1f, 0f));
            UIMissionUIFactory.SetRect(hit.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 20f));
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            item.claimButton = button;

            TMP_Text done = UIMissionUIFactory.CreateText(
                "DoneMark", root.transform, "✓", 56f,
                TextAlignmentOptions.Center, UIMissionUIFactory.LightText, font);
            UIMissionUIFactory.SetRect(done.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 20f));
            item.doneMark = done.gameObject;

            return item;
        }
    }
}
