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
    /// 특별한 7일의 한 줄 — "일차 숫자 · 출석 n일차 · 0/1 · 보상 칸 둘 · 버튼".
    /// 시안 <c>Art/Layout/7DaysEvent_Layout.png</c> · <c>Art/0PSD/7일출석.psd</c> (2026-10-27).
    ///
    /// 줄 판 색이 보상 등급을 말한다 — 강조할 날(1·3일차)은 보라, 마지막 날은 금색, 나머지는 흰색
    /// (시안 주석 "더 좋은 건 보라색 / 평범한 건 흰색").
    ///
    /// 상태별 표기(기획서 ShopPackageDesign 4.3):
    /// 받을 수 있음 = 노란 "보상 받기" · 쌓임(미구매) = 어두운 판 "보상 받기" + 자물쇠, <b>보상은 그대로 보인다</b> ·
    /// 미도래 = 어두운 판 "진행 전" + 자물쇠 · 받음 = 어두운 판 금색 "수령 완료".
    /// 받은 줄의 판은 어둡게 누르지 않는다 — 보라 판을 누르면 진행 숫자(412650)가 판과 같은 색이 되어 사라진다.
    /// </summary>
    public sealed class UISpecialSevenDaysRow : MonoBehaviour
    {
        /// <summary>줄에 그리는 보상 수. 데이터가 더 많아도 앞의 둘만 그린다.</summary>
        internal const int MaxRewards = 2;

        [SerializeField] private Image plate;
        [SerializeField] private Image numberBox;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject[] rewardRoots = new GameObject[MaxRewards];
        [SerializeField] private Image[] rewardIcons = new Image[MaxRewards];
        [SerializeField] private TMP_Text[] rewardTexts = new TMP_Text[MaxRewards];
        [SerializeField] private Button claimButton;
        [SerializeField] private GameObject statusRoot;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject lockMark;
        [SerializeField] private GameObject starMark;

        [SerializeField] private Sprite normalPlate;
        [SerializeField] private Sprite rarePlate;
        [SerializeField] private Sprite specialPlate;
        [SerializeField] private Sprite normalNumber;
        [SerializeField] private Sprite rareNumber;
        [SerializeField] private Sprite specialNumber;
        [SerializeField] private Material plainTextMaterial;

        private static readonly Color ProgressColor = UISeasonPassUIFactory.Hex(0x412650);
        private static readonly Color StatusColor = UISeasonPassUIFactory.Hex(0xe3e3e3);
        private static readonly Color DoneText = UISeasonPassUIFactory.TitleColor;

        /// <summary>어두운 상태 판 — SmallBox 검정 알파 30%(PSD 불투명도 77).</summary>
        private static readonly Color StatusPlateColor = new Color(0f, 0f, 0f, 77f / 255f);

        private Action<int> claimAction;
        private int day;

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

            bool special = view.Day == SpecialSevenDaysRules.DayCount;
            bool rare = !special && view.Highlighted;

            if (plate != null)
                plate.sprite = special ? specialPlate : rare ? rarePlate : normalPlate;

            if (numberBox != null)
                numberBox.sprite = special ? specialNumber : rare ? rareNumber : normalNumber;

            if (numberText != null)
                numberText.SetText(view.Day.ToString(CultureInfo.InvariantCulture));

            if (starMark != null)
                starMark.SetActive(special);

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
                    rewardTexts[i].SetText(reward.amount.ToString("N0", CultureInfo.InvariantCulture));
            }

            // 노란 버튼은 받을 수 있을 때만. 나머지는 어두운 판에 상태 글자 — 쌓임·미도래는 자물쇠까지.
            bool claimable = state == SpecialSevenDaysCellState.Claimable;
            if (claimButton != null)
                claimButton.gameObject.SetActive(claimable);

            if (statusRoot != null)
                statusRoot.SetActive(!claimable);

            bool locked = state == SpecialSevenDaysCellState.Accrued || state == SpecialSevenDaysCellState.Upcoming;
            if (lockMark != null)
                lockMark.SetActive(locked);

            if (statusText != null)
            {
                statusText.SetText(claimed ? "수령 완료" : state == SpecialSevenDaysCellState.Accrued ? "보상 받기" : "진행 전");
                statusText.color = claimed ? DoneText : StatusColor;

                // "수령 완료" 금색은 흰 줄의 회색 판 위에서 안 읽혀 외곽선을 붙인다. 나머지는 시안대로 외곽선 없음.
                if (plainTextMaterial != null)
                    statusText.fontSharedMaterial = claimed ? statusText.font.material : plainTextMaterial;

                // 자물쇠가 있으면 그 왼쪽에 오른쪽 정렬로 붙이고(글자 길이가 달라도 간격 16 유지), 없으면 판 가운데로.
                statusText.alignment = locked ? TextAlignmentOptions.Right : TextAlignmentOptions.Center;
                Vector2 p = statusText.rectTransform.anchoredPosition;
                p.x = locked ? StatusTextLockedX : StatusTextCenter;
                statusText.rectTransform.anchoredPosition = p;
            }
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 좌표는 줄 판(보이는 925x141)의 중심 기준, 위가 +. PSD 레이어 bbox 에서 옮겼다.
        // 기록: Docs/SpecialSevenDaysUI.md

        internal const float RowWidth = 925f;
        internal const float RowHeight = 141f;

        /// <summary>
        /// 상태 글자 칸(폭 <see cref="StatusTextWidth"/>)의 중심 x — 오른쪽 끝이 자물쇠 왼쪽 끝(46)에서 16 떨어진 30.
        /// PSD 는 "진행 전" 오른쪽 끝 906 → 자물쇠 922 로 16 간격이다.
        /// </summary>
        private const float StatusTextWidth = 150f;
        private const float StatusTextLockedX = 30f - StatusTextWidth * 0.5f;

        /// <summary>자물쇠가 없을 때 — 보이는 판의 가운데. 그림 여백이 오른쪽·아래가 1px 넓어 그림 중심보다 2 왼쪽이다.</summary>
        private const float StatusTextCenter = -2f;

        internal static UISpecialSevenDaysRow Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISpecialSevenDaysRow", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(RowWidth, RowHeight), Vector2.zero);
            var row = root.AddComponent<UISpecialSevenDaysRow>();
            Transform t = root.transform;

            row.normalPlate = UISeasonPassUIFactory.LoadSprite("7DaysEvent/7DaysEvent_Btn_Normal");
            row.rarePlate = UISeasonPassUIFactory.LoadSprite("7DaysEvent/7DaysEvent_Btn_Rare");
            row.specialPlate = UISeasonPassUIFactory.LoadSprite("7DaysEvent/7DaysEvent_Btn_Special");
            row.normalNumber = UISeasonPassUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Number_Normal");
            row.rareNumber = UISeasonPassUIFactory.LoadSprite("7DaysEvent/7DaysEvent_Popup_Number_Rare");
            row.specialNumber = UISeasonPassUIFactory.LoadSprite("7DaysEvent/7DaysEvent_Popup_Number_Special");

            // 판 — 7DaysEvent_Btn x4. 여백 없는 그림이라 보이는 크기 그대로.
            row.plate = UISeasonPassUIFactory.Sliced(t, "Plate", "7DaysEvent/7DaysEvent_Btn_Normal", 4f,
                new Vector2(RowWidth, RowHeight), Vector2.zero, Color.white);

            // 일차 숫자 판 — x3, 보이는 88x104. 여백 3px x3 → +18. 판의 아래 그림자만큼 위로 7.5.
            row.numberBox = UISeasonPassUIFactory.Sliced(t, "NumberBox", "InfinityMode/Infinity_Popup_Number_Normal", 3f,
                new Vector2(106f, 122f), new Vector2(-400.5f, 7.5f), Color.white);
            row.plainTextMaterial = plainMaterial;
            row.numberText = UISeasonPassUIFactory.CreateText("Number", row.numberBox.transform, "1", 65f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(row.numberText.rectTransform, new Vector2(88f, 90f), new Vector2(0f, 1.5f));

            // 마지막 날 별 — Icon_Star x4, 숫자 판 왼쪽 위 모서리
            row.starMark = UISeasonPassUIFactory.Picture(t, "Star", "Upgrade/Icon_Star",
                new Vector2(64f, 64f), new Vector2(-444.5f, 56.5f)).gameObject;
            row.starMark.SetActive(false);

            // 제목·진행 — 왼쪽 끝 x 205(판 기준 -341.5)
            row.titleText = UISeasonPassUIFactory.CreateText("Title", t, "출석 1일차", 35f,
                TextAlignmentOptions.Left, Color.white, font);
            UISeasonPassUIFactory.SetRect(row.titleText.rectTransform, new Vector2(300f, 50f), new Vector2(-191.5f, 27f));

            row.progressText = UISeasonPassUIFactory.CreateText("Progress", t, "0/1", 35f,
                TextAlignmentOptions.Left, ProgressColor, font);
            SetPlain(row.progressText, plainMaterial);
            UISeasonPassUIFactory.SetRect(row.progressText.rectTransform, new Vector2(300f, 50f), new Vector2(-191.5f, -18.5f));

            // 보상 — 슬롯 x2(보이는 90), 아이콘 x1.5. 수량은 슬롯 아래 테두리에 걸친다(시안 주석).
            // 첫 칸은 보라(Itme_Slot_3), 둘째 칸은 파랑(Itme_Slot_2) — 시안 그대로.
            string[] slots = { "ItemSlot/Itme_Slot_3", "ItemSlot/Itme_Slot_2" };
            for (int i = 0; i < MaxRewards; i++)
            {
                GameObject item = UISeasonPassUIFactory.CreateRect("Reward" + i, t);
                UISeasonPassUIFactory.SetRect(item.GetComponent<RectTransform>(), new Vector2(90f, 90f),
                    new Vector2(38.5f + i * 109f, 10.5f));
                row.rewardRoots[i] = item;

                UISeasonPassUIFactory.Picture(item.transform, "Slot", slots[i], new Vector2(128f, 128f), Vector2.zero);

                Image icon = UISeasonPassUIFactory.CreateImage("Icon", item.transform, Color.white);
                UISeasonPassUIFactory.SetRect(icon.rectTransform, new Vector2(96f, 96f), new Vector2(0f, 1.5f));
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.enabled = false;
                row.rewardIcons[i] = icon;

                TMP_Text label = UISeasonPassUIFactory.CreateFittedText("Amount", item.transform, "300", 30f, 20f,
                    TextAlignmentOptions.Center, Color.white, font);
                UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(108f, 40f), new Vector2(-1f, -41.5f));
                row.rewardTexts[i] = label;
            }

            // 받기 버튼 — Big_Btn_Yellow x4, 보이는 227x100. 여백(위 6·아래 2·좌우 4) x4 → +32, 그림이 8 아래로 치우쳐 위로 8.
            Image claim = UISeasonPassUIFactory.Sliced(t, "ClaimButton", "Ingame/Big_Btn_Yellow", 4f,
                new Vector2(259f, 132f), new Vector2(327f, 14.5f), Color.white);
            row.claimButton = UISeasonPassUIFactory.SpriteButton(claim);
            TMP_Text claimText = UISeasonPassUIFactory.CreateFittedText("Label", claim.transform, "보상 받기", 35f, 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(claimText.rectTransform, new Vector2(200f, 50f), new Vector2(-1.5f, -3f));

            // 상태 판 — SmallBox x4 검정 30%, 보이는 221x76. 여백(좌·위 6, 우·아래 7) x4 → +52.
            Image status = UISeasonPassUIFactory.Sliced(t, "StatusPlate", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(273f, 128f), new Vector2(329f, 4f), StatusPlateColor);
            status.raycastTarget = false;
            row.statusRoot = status.gameObject;

            row.statusText = UISeasonPassUIFactory.CreateFittedText("Label", status.transform, "진행 전", 35f, 24f,
                TextAlignmentOptions.Right, StatusColor, font);
            SetPlain(row.statusText, plainMaterial);
            UISeasonPassUIFactory.SetRect(row.statusText.rectTransform, new Vector2(StatusTextWidth, 50f), new Vector2(StatusTextLockedX, 3f));

            // 자물쇠 — Icon_Unlock x1.5(보이는 35x42, PSD 실측). 이 파일이 잠긴 그림이다(이름이 뒤바뀌어 있다).
            row.lockMark = UISeasonPassUIFactory.Picture(status.transform, "Lock", "Upgrade/Icon_Unlock",
                new Vector2(48f, 48f), new Vector2(63.5f, 2f)).gameObject;

            return row;
        }

        /// <summary>진행 숫자·상태 글자는 시안에서 외곽선이 없다(글자 둘레에 검정 픽셀 0).</summary>
        private static void SetPlain(TMP_Text text, Material plainMaterial)
        {
            if (plainMaterial != null)
                text.fontSharedMaterial = plainMaterial;
        }
    }
}
