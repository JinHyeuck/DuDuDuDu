using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.Point;
using OJ.SeasonPass;

namespace OJ.Shop
{
    /// <summary>
    /// 특별한 7일의 한 줄 — "출석 n일차 · 접속하기 · 보상 아이콘+이름 · 보상 받기".
    /// 레퍼런스(출석 리턴 패키지)는 2열 그리드인데 <b>1열 7줄</b>로 쌓는다(사용자 지정 2026-10-06).
    ///
    /// 상태별 표기(기획서 ShopPackageDesign 4.3):
    /// 받음 = 줄을 어둡게 누르고 "수령 완료" · 받을 수 있음 = 초록 버튼 ·
    /// 쌓임(미구매) = 회색 버튼 + 자물쇠, <b>보상은 그대로 보인다</b> · 미도래 = "진행 전".
    /// </summary>
    public sealed class UISpecialSevenDaysRow : MonoBehaviour
    {
        /// <summary>줄에 그리는 보상 수. 데이터가 더 많아도 앞의 둘만 그린다.</summary>
        internal const int MaxRewards = 2;

        [SerializeField] private Image outline;
        [SerializeField] private Image card;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject[] rewardRoots = new GameObject[MaxRewards];
        [SerializeField] private Image[] rewardIcons = new Image[MaxRewards];
        [SerializeField] private TMP_Text[] rewardTexts = new TMP_Text[MaxRewards];
        [SerializeField] private Button claimButton;
        [SerializeField] private Image claimImage;
        [SerializeField] private TMP_Text claimText;
        [SerializeField] private GameObject lockMark;
        [SerializeField] private TMP_Text statusText;

        [SerializeField] private Sprite activeButtonSprite;
        [SerializeField] private Sprite lockedButtonSprite;

        private static readonly Color TodayOutline = UISeasonPassUIFactory.Hex(0x7be35a);
        private static readonly Color HighlightOutline = UISeasonPassUIFactory.TitleColor;
        private static readonly Color PlainOutline = Color.black;
        private static readonly Color DoneText = UISeasonPassUIFactory.TitleColor;

        private Action<int> claimAction;
        private int day;
        private bool pulse;

        private void Awake()
        {
            if (claimButton != null)
                claimButton.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (claimButton != null)
                claimButton.onClick.RemoveListener(OnClick);
        }

        private void Update()
        {
            // 오늘 받을 줄은 테두리가 숨 쉰다(4.3 "하이라이트 + 펄스").
            if (!pulse || outline == null)
                return;

            Color c = TodayOutline;
            c.a = 0.55f + 0.45f * Mathf.PingPong(Time.unscaledTime * 1.6f, 1f);
            outline.color = c;
        }

        private void OnClick()
        {
            Action<int> action = claimAction;
            action?.Invoke(day);
        }

        public void Bind(SpecialSevenDaysCellView view, Action<int> onClaim)
        {
            day = view.Day;
            claimAction = onClaim;

            SpecialSevenDaysCellState state = view.State;
            bool claimed = state == SpecialSevenDaysCellState.Claimed;
            bool reached = state != SpecialSevenDaysCellState.Upcoming;

            if (titleText != null)
                titleText.SetText("출석 " + view.Day + "일차");

            // 출석 여부. 안 샀어도 그날이 왔으면 1/1 이다 — "출석은 쌓인다" 가 이 숫자로 보인다.
            if (progressText != null)
                progressText.SetText(reached ? "1/1" : "0/1");

            for (int i = 0; i < MaxRewards; i++)
            {
                bool has = view.Rewards != null && i < view.Rewards.Count && view.Rewards[i].amount > 0;
                if (rewardRoots[i] != null)
                    rewardRoots[i].SetActive(has);
                if (!has)
                    continue;

                ShopDatabase.Reward reward = view.Rewards[i];
                if (rewardIcons[i] != null)
                {
                    Sprite sprite = PointRewardUtility.GetPointIcon(reward.pointType);
                    rewardIcons[i].sprite = sprite;
                    rewardIcons[i].enabled = sprite != null;
                }

                if (rewardTexts[i] != null)
                    rewardTexts[i].SetText(PointRewardUtility.GetPointName(reward.pointType) + " " +
                                           reward.amount.ToString("N0", CultureInfo.InvariantCulture));
            }

            // 버튼은 받을 수 있음·쌓임에만 있다. 쌓임은 눌리지 않는 회색 + 자물쇠 — 무엇이 기다리는지는 보이되.
            bool showButton = state == SpecialSevenDaysCellState.Claimable || state == SpecialSevenDaysCellState.Accrued;
            if (claimButton != null)
            {
                claimButton.gameObject.SetActive(showButton);
                claimButton.interactable = state == SpecialSevenDaysCellState.Claimable;
            }

            if (claimImage != null)
                claimImage.sprite = state == SpecialSevenDaysCellState.Claimable ? activeButtonSprite : lockedButtonSprite;

            if (lockMark != null)
                lockMark.SetActive(state == SpecialSevenDaysCellState.Accrued);

            if (statusText != null)
            {
                statusText.gameObject.SetActive(!showButton);
                statusText.SetText(claimed ? "수령 완료" : "진행 전");
                statusText.color = claimed ? DoneText : UISeasonPassUIFactory.MutedText;
            }

            if (card != null)
                card.color = claimed ? UISeasonPassUIFactory.CardColor * UISeasonPassUIFactory.DimTint : UISeasonPassUIFactory.CardColor;

            pulse = view.IsToday && state == SpecialSevenDaysCellState.Claimable;
            if (outline != null)
                outline.color = pulse || view.IsToday ? TodayOutline : view.Highlighted ? HighlightOutline : PlainOutline;
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 좌표는 줄 판의 중심 기준. 보이는 줄은 1000x156.

        internal const float RowWidth = 1000f;
        internal const float RowHeight = 156f;

        internal static UISpecialSevenDaysRow Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISpecialSevenDaysRow", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(RowWidth, RowHeight), Vector2.zero);
            var row = root.AddComponent<UISpecialSevenDaysRow>();
            Transform t = root.transform;

            row.activeButtonSprite = UISeasonPassUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            row.lockedButtonSprite = UISeasonPassUIFactory.LoadSprite("Ingame/Btn_Gray");

            // 판 — SmallBox x4. 테두리(오늘·강조)용 판을 8px 크게 뒤에 깐다. 여백 6/7px x4 → +52.
            const float edge = 6f;
            row.outline = UISeasonPassUIFactory.Sliced(t, "Outline", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(RowWidth + edge * 2f + 52f, RowHeight + edge * 2f + 52f), new Vector2(2f, -2f), PlainOutline);
            row.card = UISeasonPassUIFactory.Sliced(t, "Card", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(RowWidth + 52f, RowHeight + 52f), new Vector2(2f, -2f), UISeasonPassUIFactory.CardColor);

            // 제목 줄 — "출석 n일차" + 진행 배지
            row.titleText = UISeasonPassUIFactory.CreateText("Title", t, "출석 1일차", 40f,
                TextAlignmentOptions.Left, Color.white, font);
            UISeasonPassUIFactory.SetRect(row.titleText.rectTransform, new Vector2(240f, 52f), new Vector2(-340f, 46f));

            Image badge = UISeasonPassUIFactory.Sliced(t, "ProgressBadge", "Upgrade/Ui_Popup_SmallBox", 2f,
                new Vector2(84f, 48f), new Vector2(-245f, 46f), UISeasonPassUIFactory.Hex(0x3a1414));
            badge.raycastTarget = false;
            row.progressText = UISeasonPassUIFactory.CreateText("Text", badge.transform, "0/1", 26f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.Hex(0xff6a4a), font);
            UISeasonPassUIFactory.SetRect(row.progressText.rectTransform, new Vector2(80f, 40f), new Vector2(0f, 1f));

            TMP_Text sub = UISeasonPassUIFactory.CreateText("Sub", t, "접속하기", 26f,
                TextAlignmentOptions.Left, UISeasonPassUIFactory.MutedText, font);
            UISeasonPassUIFactory.SetRect(sub.rectTransform, new Vector2(240f, 36f), new Vector2(-340f, 8f));

            // 보상 — 아이콘 칸 + 이름·수량. 둘째 칸은 x +290.
            for (int i = 0; i < MaxRewards; i++)
            {
                GameObject item = UISeasonPassUIFactory.CreateRect("Reward" + i, t);
                UISeasonPassUIFactory.SetRect(item.GetComponent<RectTransform>(), new Vector2(280f, 72f),
                    new Vector2(-320f + i * 290f, -38f));
                row.rewardRoots[i] = item;

                // Itme_Slot_0 x1.6 — 보이는 72
                UISeasonPassUIFactory.Picture(item.transform, "Slot", "ItemSlot/Itme_Slot_0",
                    new Vector2(102.4f, 102.4f), new Vector2(-103.2f, -0.8f));

                Image icon = UISeasonPassUIFactory.CreateImage("Icon", item.transform, Color.white);
                UISeasonPassUIFactory.SetRect(icon.rectTransform, new Vector2(60f, 60f), new Vector2(-104f, 0f));
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.enabled = false;
                row.rewardIcons[i] = icon;

                TMP_Text label = UISeasonPassUIFactory.CreateFittedText("Label", item.transform, "유료젬 300", 30f, 20f,
                    TextAlignmentOptions.Left, UISeasonPassUIFactory.Hex(0x8fd0ff), font);
                UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(196f, 48f), new Vector2(40f, 0f));
                row.rewardTexts[i] = label;
            }

            // 받기 버튼 — 보이는 220x96. 여백 x4 → +32.
            row.claimImage = UISeasonPassUIFactory.Sliced(t, "ClaimButton", "Ingame/Big_Btn_Green", 4f,
                new Vector2(252f, 128f), new Vector2(370f, 0f), Color.white);
            row.claimButton = UISeasonPassUIFactory.SpriteButton(row.claimImage);
            row.claimText = UISeasonPassUIFactory.CreateFittedText("Label", row.claimImage.transform, "보상 받기", 34f, 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(row.claimText.rectTransform, new Vector2(200f, 52f), new Vector2(0f, 6f));

            // 자물쇠 — 버튼 오른쪽 위 모서리. Icon_Unlock 이 잠긴 그림이다(파일 이름이 뒤바뀌어 있다).
            row.lockMark = UISeasonPassUIFactory.Picture(row.claimImage.transform, "Lock", "Upgrade/Icon_Unlock",
                new Vector2(56f, 56f), new Vector2(104f, 44f)).gameObject;
            row.lockMark.SetActive(false);

            row.statusText = UISeasonPassUIFactory.CreateText("Status", t, "진행 전", 34f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.MutedText, font);
            UISeasonPassUIFactory.SetRect(row.statusText.rectTransform, new Vector2(220f, 52f), new Vector2(370f, 0f));
            row.statusText.gameObject.SetActive(false);

            return row;
        }
    }
}
