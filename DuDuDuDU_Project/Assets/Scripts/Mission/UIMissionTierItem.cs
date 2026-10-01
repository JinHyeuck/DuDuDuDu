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
        /// <summary>
        /// 칸 하나의 크기. <b>세로가 넉넉해야 한다</b> — 상자·게이지·숫자를 위에서 아래로
        /// 겹치지 않게 놓아야 하는데, 가운데를 게이지가 가로지른다.
        /// </summary>
        internal static readonly Vector2 ItemSize = new Vector2(120f, 180f);

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

            // 상자는 위, 게이지는 가운데(TierRoot 가 그린다), 숫자는 아래다.
            // 셋의 세로 범위가 겹치지 않도록 5px 씩 띄워 뒀다.
            Image box = UIMissionUIFactory.CreateImage("Box", root.transform, UIMissionUIFactory.RowColor);
            UIMissionUIFactory.SetRect(box.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 38f));
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
                item.countText.rectTransform, new Vector2(120f, 40f), new Vector2(0f, -66f));

            // 버튼은 상자 전체를 덮는다. 작은 칸이라 따로 그린 버튼을 얹으면
            // 상자가 가려지고, 누를 곳도 더 작아진다.
            Image hit = UIMissionUIFactory.CreateImage("ClaimButton", root.transform, new Color(1f, 1f, 1f, 0f));
            UIMissionUIFactory.SetRect(hit.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 38f));
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            item.claimButton = button;

            TMP_Text done = UIMissionUIFactory.CreateText(
                "DoneMark", root.transform, "✓", 56f,
                TextAlignmentOptions.Center, UIMissionUIFactory.LightText, font);
            UIMissionUIFactory.SetRect(done.rectTransform, new Vector2(104f, 104f), new Vector2(0f, 38f));
            item.doneMark = done.gameObject;

            return item;
        }
    }
}
