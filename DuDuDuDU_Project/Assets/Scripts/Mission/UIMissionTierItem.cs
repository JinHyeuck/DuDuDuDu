using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.Mission
{
    /// <summary>
    /// 추가 보상 한 칸. 게이지 위에 놓인 보상 칸 하나다 — 칸(아이템 슬롯) 안에 보상 아이콘과 수량,
    /// 칸 밑에 문턱 숫자가 있다.
    ///
    /// <b>상태가 셋이다.</b> 못 연 것(어두움, 숫자 흰색) · 받을 수 있는 것(밝음 + 노란 숫자 + 버튼) ·
    /// 이미 받은 것(눌러 둠 + 완료 표시). 셋을 색 하나로만 가르면 "받을 수 있는데 안 받은" 칸이
    /// 눈에 안 띄어 그대로 자정을 넘긴다.
    /// </summary>
    public sealed class UIMissionTierItem : MonoBehaviour
    {
        /// <summary>
        /// 칸 하나의 크기. 폭은 칸 중심 간격(시안 171.3)이고, 세로는 슬롯(134)과 밑 숫자까지 들어가는 높이다.
        /// <b>폭을 바꾸면 <see cref="UIQuestDialog"/> 의 게이지 폭도 같이 바꿔야 한다</b> — 눈금이 칸 중심에 떨어져야 한다.
        /// </summary>
        internal static readonly Vector2 ItemSize = new Vector2(171.3f, 240f);

        private static readonly Color LockedTint = new Color(0.78f, 0.78f, 0.82f, 1f);
        private static readonly Color ClaimableTint = new Color(1f, 0.93f, 0.62f, 1f);

        [SerializeField] private Image box;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;
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
            {
                countText.SetText(MissionText.TierLabel(tier.requiredClearCount));

                // 문턱을 넘긴 칸 밑 숫자는 노랑(시안 주석 fcf900), 못 넘긴 것은 흰색.
                countText.color = unlocked ? UIMissionUIFactory.ReachedNumberColor : Color.white;
            }

            bool claimable = unlocked && !claimed;

            if (box != null)
            {
                box.color = claimed
                    ? UIMissionUIFactory.DoneTint
                    : (claimable ? ClaimableTint : LockedTint);
            }

            if (claimButton != null)
                claimButton.gameObject.SetActive(claimable);

            if (doneMark != null)
                doneMark.SetActive(claimed);

            bool has = tier.rewards != null && tier.rewards.Count > 0;

            if (rewardIcon != null)
            {
                Sprite icon = has ? PointRewardUtility.GetPointIcon(tier.rewards[0].pointType) : null;
                rewardIcon.sprite = icon;
                rewardIcon.color = claimed ? UIMissionUIFactory.DoneTint : Color.white;
                rewardIcon.enabled = icon != null;
            }

            if (rewardAmountText != null)
                rewardAmountText.SetText(has ? tier.rewards[0].amount.ToString() : string.Empty);
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 좌표는 칸 슬롯 중심 기준, 위가 +. PSD(Art/0PSD/퀘스트.psd)의 첫 칸 — 슬롯 중심 (338, 644),
        // 밑 숫자 중심 y 732, 수량 중심 (368, 676) — 에서 옮겼다.

        internal static UIMissionTierItem Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIMissionUIFactory.CreateRect("UIMissionTierItem", parent);
            var rect = root.GetComponent<RectTransform>();
            UIMissionUIFactory.SetRect(rect, ItemSize, Vector2.zero);

            var item = root.AddComponent<UIMissionTierItem>();

            // 슬롯 — Itme_Slot_0 x3, 보이는 135(시안 134).
            Image box = UIMissionUIFactory.Picture(
                root.transform, "Slot", "ItemSlot/Itme_Slot_0", new Vector2(192f, 192f), Vector2.zero);
            item.box = box;

            Image icon = UIMissionUIFactory.CreateImage("RewardIcon", root.transform, Color.white);
            UIMissionUIFactory.SetRect(icon.rectTransform, new Vector2(96f, 96f), new Vector2(0f, 4f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;
            item.rewardIcon = icon;

            // 수량은 슬롯 오른쪽 아래(시안 35pt). 자릿수가 데이터에 달려 자동 축소를 건다.
            item.rewardAmountText = UIMissionUIFactory.CreateFittedText(
                "RewardAmount", root.transform, "0", 35f, 20f,
                TextAlignmentOptions.Right, Color.white, font);
            UIMissionUIFactory.SetRect(
                item.rewardAmountText.rectTransform, new Vector2(100f, 44f), new Vector2(15f, -32f));

            // 문턱 숫자는 슬롯 밑(시안 35pt).
            item.countText = UIMissionUIFactory.CreateText(
                "Count", root.transform, "0", 35f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(
                item.countText.rectTransform, new Vector2(150f, 48f), new Vector2(-4f, -88f));

            // 버튼은 슬롯 전체를 덮는 투명 판이다. 작은 칸이라 따로 그린 버튼을 얹으면
            // 슬롯이 가려지고 누를 곳도 더 작아진다.
            Image hit = UIMissionUIFactory.CreateImage("ClaimButton", root.transform, new Color(1f, 1f, 1f, 0f));
            UIMissionUIFactory.SetRect(hit.rectTransform, new Vector2(135f, 135f), Vector2.zero);
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            item.claimButton = button;

            TMP_Text done = UIMissionUIFactory.CreateText(
                "DoneMark", root.transform, MissionText.CompleteLabel, 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIMissionUIFactory.SetRect(done.rectTransform, new Vector2(135f, 40f), new Vector2(0f, 34f));
            item.doneMark = done.gameObject;

            return item;
        }
    }
}
