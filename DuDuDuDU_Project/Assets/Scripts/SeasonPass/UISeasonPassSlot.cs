using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 보상 한 칸. 무료 트랙과 유료 트랙이 <b>같은 칸</b>을 쓴다 — 둘은 바탕 그림만 다르고
    /// 생김새가 같다. 칸을 둘로 나누면 여백과 아이콘 크기를 두 벌 관리하게 된다.
    ///
    /// <b>바탕 그림이 상태를 말한다</b>(PSD 의 회색·주황·초록 칸).
    /// 받을 수 있음 = 초록 · 유료인데 닿았지만 안 삼 = 주황 · 그 밖(못 닿음, 무료) = 회색.
    /// 받은 칸은 제 바탕을 어둡게 누르고 수량 대신 "완료" 를 적는다. "받을 수 있는데 안 받은" 칸이
    /// 한눈에 튀어야 시즌이 끝나기 전에 받는다.
    /// </summary>
    public sealed class UISeasonPassSlot : MonoBehaviour
    {
        /// <summary>보이는 칸 크기(PSD 180x180). Itme_Slot x4 의 보이는 외곽이다.</summary>
        internal static readonly Vector2 SlotSize = new Vector2(180f, 180f);

        [SerializeField] private Image background;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private GameObject doneMark;
        [SerializeField] private GameObject lockMark;
        [SerializeField] private Button button;

        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite premiumSprite;
        [SerializeField] private Sprite claimableSprite;

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

        /// <summary>칸을 그린다. 보상이 없으면 통째로 숨긴다.</summary>
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

            // 받은 칸은 수량 자리에 "완료" 를 적는다. 둘을 겹치면 어느 쪽도 안 읽힌다.
            if (amountText != null)
            {
                amountText.SetText(first.amount.ToString());
                amountText.gameObject.SetActive(!view.Claimed);
            }

            Color tint = view.Claimed ? UISeasonPassUIFactory.DimTint : Color.white;

            if (rewardIcon != null)
            {
                Sprite icon = PointRewardUtility.GetPointIcon(first.pointType);
                rewardIcon.sprite = icon;
                rewardIcon.color = tint;

                // 스프라이트가 없으면 칸을 숨긴다. 켜 두면 흰 사각형이 남는데
                // 그것이 "아이콘 없음" 인지 "흰 아이콘" 인지 구분되지 않는다.
                rewardIcon.enabled = icon != null;
            }

            if (background != null)
            {
                // 유료 칸이 주황인 것은 "닿았는데 잠김" 일 때다. 못 닿은 유료 칸까지 주황이면
                // 사면 무엇이 바로 열리는지가 안 보인다.
                background.sprite =
                    view.Claimable ? claimableSprite :
                    view.Premium && view.Reached ? premiumSprite :
                    normalSprite;
                background.color = tint;
            }

            if (doneMark != null)
                doneMark.SetActive(view.Claimed);

            // 자물쇠 — 아직 못 받는 칸. 레벨이 안 닿았거나 유료를 안 산 경우다(사용자 피드백 2026-10-06).
            if (lockMark != null)
                lockMark.SetActive(!view.Claimed && (!view.Reached || view.Locked));

            if (button != null)
                button.interactable = view.Claimable;
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        internal static UISeasonPassSlot Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassSlot", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), SlotSize, Vector2.zero);

            var slot = root.AddComponent<UISeasonPassSlot>();
            slot.normalSprite = UISeasonPassUIFactory.LoadSprite("ItemSlot/Itme_Slot_0");
            slot.premiumSprite = UISeasonPassUIFactory.LoadSprite("ItemSlot/Itme_Slot_5");
            slot.claimableSprite = UISeasonPassUIFactory.LoadSprite("ItemSlot/Itme_Slot_1");

            // 바탕 — Itme_Slot x4. 64 안에 보이는 것은 (9,9)~(54,54) 라 Image 는 256 이고
            // 오른쪽·아래 여백이 1px 더 넓어 중심이 (2,-2) 밀린다. 누르는 자리는 보이는 만큼.
            Image background = UISeasonPassUIFactory.Picture(
                root.transform, "Bg", "ItemSlot/Itme_Slot_0", new Vector2(256f, 256f), new Vector2(2f, -2f));
            background.raycastPadding = new Vector4(36f, 40f, 40f, 36f);
            slot.background = background;
            slot.button = UISeasonPassUIFactory.SpriteButton(background);

            // 아이콘 — PSD 다이아 보이는 79x72, 중심이 칸 중심보다 18 위. 64 그림의 x2.5
            Image icon = UISeasonPassUIFactory.CreateImage("Icon", root.transform, Color.white);
            UISeasonPassUIFactory.SetRect(icon.rectTransform, new Vector2(160f, 160f), new Vector2(-0.5f, 18f));
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;
            slot.rewardIcon = icon;

            // 수량 43pt, 글자 중심이 칸 중심보다 44.5 아래. 자릿수가 데이터에 달려(10 ~ 300000)
            // 고정 크기로 두면 큰 값이 칸 밖으로 흐른다.
            slot.amountText = UISeasonPassUIFactory.CreateFittedText(
                "Amount", root.transform, "0", 43f, 24f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(slot.amountText.rectTransform, new Vector2(166f, 56f), new Vector2(1f, -44.5f));

            TMP_Text doneText = UISeasonPassUIFactory.CreateText(
                "Done", root.transform, "완료", 43f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(doneText.rectTransform, new Vector2(166f, 56f), new Vector2(1f, -44.5f));
            slot.doneMark = doneText.gameObject;
            slot.doneMark.SetActive(false);

            // 자물쇠 — Icon_Unlock x2, 칸 오른쪽 위 모서리에 걸친다(레퍼런스 배치).
            // <b>파일 이름이 뒤바뀌어 있다</b>: Icon_Unlock 이 잠긴 자물쇠, Icon_lock 이 열린 자물쇠다.
            slot.lockMark = UISeasonPassUIFactory.Picture(
                root.transform, "Lock", "Upgrade/Icon_Unlock", new Vector2(64f, 64f), new Vector2(72f, 72f)).gameObject;
            slot.lockMark.SetActive(false);

            return slot;
        }
    }
}
