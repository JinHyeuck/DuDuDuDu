using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 보상 한 칸. 무료 트랙과 유료 트랙이 <b>같은 칸</b>을 쓴다 — 둘은 색과 자물쇠만 다르고
    /// 생김새가 같다. 칸을 둘로 나누면 여백과 아이콘 크기를 두 벌 관리하게 된다.
    ///
    /// <b>상태가 넷이다.</b> 못 닿음(어두움) · 받을 수 있음(밝음) · 받음(체크) ·
    /// 잠김(자물쇠, 유료를 안 산 경우). 넷을 색 하나로만 가르면 "받을 수 있는데 안 받은"
    /// 칸이 눈에 안 띄어 시즌이 끝나 버린다.
    /// </summary>
    public sealed class UISeasonPassSlot : MonoBehaviour
    {
        internal static readonly Vector2 SlotSize = new Vector2(150f, 150f);

        [SerializeField] private Image background;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private GameObject lockMark;
        [SerializeField] private GameObject doneMark;
        [SerializeField] private Button button;

        private Action claimAction;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            // 지역 변수로 떼어 낸 뒤 부른다. 수령이 목록을 다시 그리면서 이 칸이 재사용되거나
            // 파괴될 수 있는데, 그때 필드를 다시 읽으면 다음 칸의 수령이 불린다.
            Action action = claimAction;
            action?.Invoke();
        }

        /// <summary>칸을 그린다. 보상이 없으면 통째로 숨긴다(스샷의 5레벨 무료 칸처럼).</summary>
        public void Bind(SeasonPassSlotView view, Action onClaim)
        {
            claimAction = onClaim;

            if (!view.HasRewards)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            SeasonPassReward first = view.Rewards[0];

            if (amountText != null)
                amountText.SetText(first.amount.ToString());

            if (rewardIcon != null)
            {
                Sprite icon = PointRewardUtility.GetPointIcon(first.pointType);
                rewardIcon.sprite = icon;

                // 스프라이트가 없으면 칸을 숨긴다. 켜 두면 흰 사각형이 남는데
                // 그것이 "아이콘 없음" 인지 "흰 아이콘" 인지 구분되지 않는다.
                rewardIcon.enabled = icon != null;
            }

            if (background != null)
            {
                background.color =
                    view.Claimed ? UISeasonPassUIFactory.SlotDoneColor :
                    view.Claimable ? UISeasonPassUIFactory.SlotColor :
                    UISeasonPassUIFactory.SlotDimColor;
            }

            // 자물쇠는 <b>유료를 안 산 경우</b>다. 레벨에 못 닿은 것과 다르다 —
            // 전자는 사면 바로 열리고 후자는 더 플레이해야 한다.
            if (lockMark != null)
                lockMark.SetActive(view.Locked);

            if (doneMark != null)
                doneMark.SetActive(view.Claimed);

            if (button != null)
                button.interactable = view.Claimable;
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        internal static UISeasonPassSlot Create(Transform parent, TMP_FontAsset font)
        {
            Image root = UISeasonPassUIFactory.CreateImage(
                "UISeasonPassSlot", parent, UISeasonPassUIFactory.SlotDimColor);
            UISeasonPassUIFactory.SetRect(root.rectTransform, SlotSize, Vector2.zero);

            var slot = root.gameObject.AddComponent<UISeasonPassSlot>();
            slot.background = root;

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            slot.button = button;

            Image icon = UISeasonPassUIFactory.CreateImage("Icon", root.transform, Color.white);
            UISeasonPassUIFactory.SetRect(icon.rectTransform, new Vector2(96f, 96f), new Vector2(0f, 14f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            slot.rewardIcon = icon;

            // 수량은 자릿수가 데이터에 달렸다(10 부터 300000 까지). 고정 크기로 두면
            // 큰 값이 칸 밖으로 흘러 옆 칸 위에 겹쳐 찍힌다.
            slot.amountText = UISeasonPassUIFactory.CreateFittedText(
                "Amount", root.transform, "0", 32f, 18f,
                TextAlignmentOptions.Right, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(
                slot.amountText.rectTransform, new Vector2(132f, 38f), new Vector2(-4f, -54f));

            TMP_Text lockText = UISeasonPassUIFactory.CreateText(
                "Lock", root.transform, "잠김", 26f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(
                lockText.rectTransform, new Vector2(70f, 34f), new Vector2(38f, 54f));
            slot.lockMark = lockText.gameObject;

            TMP_Text doneText = UISeasonPassUIFactory.CreateText(
                "Done", root.transform, "완료", 30f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(
                doneText.rectTransform, SlotSize, Vector2.zero);
            slot.doneMark = doneText.gameObject;

            return slot;
        }
    }
}
