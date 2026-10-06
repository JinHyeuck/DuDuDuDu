using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;
using OJ.UI;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 시즌 패스 프리미엄 구매 창. 패스 창의 "프리미엄 활성화" 가 띄운다.
    ///
    /// <b>파는 것을 숫자로 보여 준다.</b> 혜택 문구는 지어내지 않고 실제 규칙에서 나온 것만 적는다 —
    /// 유료 트랙 레벨 수, 소급 지급(<c>SeasonPassRules</c>), 남은 기간. 아래 보상 칸은 이번 시즌
    /// 유료 트랙 보상을 재화별로 합친 것이다. 시즌표가 바뀌면 화면이 따라 바뀐다.
    ///
    /// <b>결제는 아직 없다.</b> 구매 버튼이 바로 활성화한다 — <see cref="OnClickBuy"/> 하나가 그 자리이고,
    /// 상품이 생기면 결제 관문을 지난 뒤에 <c>TryUnlockPremium</c> 을 부르도록 그 메서드만 고친다.
    /// </summary>
    public sealed class UISeasonPassPremiumDialog : DialogBase
    {
        /// <summary>보상 칸 최대 개수(4 x 2). 넘치면 앞쪽(골드 먼저, 그다음 재화 순)만 보인다.</summary>
        private const int MaxRewardCells = 8;

        [SerializeField] private TMP_Text seasonNameText;
        [SerializeField] private TMP_Text levelBenefitText;
        [SerializeField] private TMP_Text remainingText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button buyButton;
        [SerializeField] private RectTransform rewardGrid;
        [SerializeField] private UISeasonPassRewardCell rewardTemplate;

        private readonly List<UISeasonPassRewardCell> cells = new List<UISeasonPassRewardCell>();

        protected override void OnLoad()
        {
            UseBackBtn = true;

            if (rewardTemplate != null)
                rewardTemplate.gameObject.SetActive(false);

            if (buyButton != null)
                buyButton.onClick.AddListener(OnClickBuy);
        }

        protected override void OnUnload()
        {
            if (buyButton != null)
                buyButton.onClick.RemoveListener(OnClickBuy);
        }

        protected override void OnEnter()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass == null)
            {
                Debug.LogError("[시즌패스] SeasonPassManager 가 없다. 구매 창을 그리지 못한다.");
                return;
            }

            SeasonPassSeason season = pass.Season;

            if (seasonNameText != null)
                seasonNameText.SetText(season.displayName + " 프리미엄");

            if (levelBenefitText != null)
                levelBenefitText.SetText("유료 트랙 " + CountPremiumLevels(season) + "레벨 보상 해금");

            if (remainingText != null)
                remainingText.SetText("시즌 종료까지 " + SeasonPassText.Remaining(pass.TimeUntilSeasonEnd));

            if (priceText != null)
            {
                int price = SeasonPassDatabaseProvider.Database.PremiumPriceWon;
                priceText.SetText(price.ToString("N0", CultureInfo.InvariantCulture) + "원");
            }

            RefreshRewards(season);
        }

        private static int CountPremiumLevels(SeasonPassSeason season)
        {
            int count = 0;
            for (int level = 1; level <= season.MaxLevel; level++)
            {
                SeasonPassLevel data = season.GetLevel(level);
                if (data != null && data.premiumRewards != null && data.premiumRewards.Count > 0)
                    count++;
            }

            return count;
        }

        private void RefreshRewards(SeasonPassSeason season)
        {
            var all = new List<PointRewardEntry>();
            for (int level = 1; level <= season.MaxLevel; level++)
            {
                SeasonPassLevel data = season.GetLevel(level);
                if (data != null)
                    all.AddRange(SeasonPassReward.ToPointRewards(data.premiumRewards));
            }

            List<PointRewardEntry> merged = PointRewardUtility.MergeRewards(all);
            int shown = Mathf.Min(merged.Count, MaxRewardCells);

            while (cells.Count < shown && rewardTemplate != null && rewardGrid != null)
            {
                UISeasonPassRewardCell cell = Instantiate(rewardTemplate, rewardGrid);
                cells.Add(cell);
            }

            for (int i = 0; i < cells.Count; i++)
            {
                bool active = i < shown;
                cells[i].gameObject.SetActive(active);
                if (active)
                    cells[i].Bind(merged[i]);
            }
        }

        /// <summary>
        /// 유료 트랙을 연다.
        ///
        /// <b>지금은 결제 없이 열린다.</b> 이 프로젝트에 IAP 가 0 줄이라
        /// <c>ShopPurchaseManager.TryBuyCashProduct</c> 도 "결제된 셈 치고" 지급하는 상태다.
        /// 상품이 생기면 그 관문을 지난 뒤에 여기를 부르도록 바꾼다 — <b>고칠 곳은 이 메서드 하나다.</b>
        /// </summary>
        private void OnClickBuy()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass == null)
                return;

            if (!pass.PremiumUnlocked)
            {
                Debug.LogWarning("[시즌패스] 결제 없이 유료 트랙을 연다(IAP 미연동). 출시 전 실제 결제로 교체할 것.");
                pass.TryUnlockPremium();
            }

            Exit();
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 이 창은 PSD 가 없다. 패스 창과 같은 그림·색으로 레퍼런스(전체 화면 구매 창) 구성을 따랐다.
        // 좌표는 1080x1920 화면 픽셀(좌상단 원점). 측정·결정: Docs/SeasonPassArtPort.md

        /// <summary>창 하나를 조립한다. 에디터 굽기 경로에서만 부른다.</summary>
        public static UISeasonPassPremiumDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISeasonPassPremiumDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISeasonPassPremiumDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Popup);

            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            Transform p = view.transform;

            // 딤 — 뒤의 패스 창이 눌리지 않게 클릭을 먹는다. 닫기는 뒤로 버튼·Esc 로만 한다
            // (사려고 들어온 창이 빈 곳 한 번에 닫히면 손해다).
            Image backdrop = UISeasonPassUIFactory.CreateImage("Backdrop", p, UISeasonPassUIFactory.Backdrop);
            UISeasonPassUIFactory.Stretch(backdrop.rectTransform);

            UISeasonPassUIFactory.Picture(p, "Hero", "Pass/FromPsd/FromPsd_PassHero",
                new Vector2(1080f, 481f), UISeasonPassUIFactory.Pos(540f, 320f));

            BakeTitle(p, font);
            BakeCard(dialog, p, font);
            BakeButtons(dialog, p, font);

            return dialog;
        }

        private static void BakeTitle(Transform p, TMP_FontAsset font)
        {
            // 띠 — 패스 창 프리미엄 버튼과 같은 그림 x3, 보이는 880x102 (y 549~651)
            Image banner = UISeasonPassUIFactory.Sliced(p, "TitleBanner", "Pass/Pass_premium_Btn", 3f,
                new Vector2(952f, 384f), UISeasonPassUIFactory.Pos(540f, 600f), Color.white);
            banner.raycastTarget = false;

            UISeasonPassUIFactory.Picture(banner.transform, "Crown", "Pass/Pass_Crown",
                new Vector2(96f, 96f), new Vector2(-340f, 1f));

            TMP_Text title = UISeasonPassUIFactory.CreateText(
                "Title", banner.transform, "시즌 패스 프리미엄", 55f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(640f, 80f), new Vector2(20f, 2f));
        }

        private static void BakeCard(UISeasonPassPremiumDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 판 — SmallBox x4 짙은 보라, 보이는 900x760 (90,690). 여백 6/7px x4.
            // 밝은 Ui_Popup_Bg 로는 금색 제목·혜택 띠가 바탕에 묻혔다.
            UISeasonPassUIFactory.Sliced(p, "Card", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(952f, 812f), UISeasonPassUIFactory.Pos(542f, 1072f), UISeasonPassUIFactory.CardColor);

            dialog.seasonNameText = UISeasonPassUIFactory.CreateFittedText(
                "SeasonName", p, "시즌 1 프리미엄", 45f, 30f, TextAlignmentOptions.Center, UISeasonPassUIFactory.TitleColor, font);
            UISeasonPassUIFactory.SetRect(dialog.seasonNameText.rectTransform, new Vector2(820f, 60f), UISeasonPassUIFactory.Pos(540f, 748f));

            dialog.levelBenefitText = BenefitRow(p, "LevelBenefit", "Pass/Pass_Crown", "유료 트랙 30레벨 보상 해금", 845f, font);
            BenefitRow(p, "RetroBenefit", "Pass/Pass_LevelNumber", "지난 레벨 보상도 바로 수령", 945f, font);
            dialog.remainingText = BenefitRow(p, "RemainingBenefit", "Pass/Pass_Clork", "시즌 종료까지 0일 0시간", 1045f, font);

            TMP_Text heading = UISeasonPassUIFactory.CreateText(
                "RewardHeading", p, "활성화하면 받는 보상", 40f, TextAlignmentOptions.Center, UISeasonPassUIFactory.PremiumActiveText, font);
            UISeasonPassUIFactory.SetRect(heading.rectTransform, new Vector2(820f, 56f), UISeasonPassUIFactory.Pos(540f, 1132f));

            // 보상 칸 — 4열 x 2줄, 칸 간격 165 x 140 (y 1170~1450)
            GameObject grid = UISeasonPassUIFactory.CreateRect("RewardGrid", p);
            UISeasonPassUIFactory.SetRect(grid.GetComponent<RectTransform>(), new Vector2(660f, 280f), UISeasonPassUIFactory.Pos(540f, 1310f));
            var layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(165f, 140f);
            layout.spacing = Vector2.zero;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 4;
            // 가운데 정렬 — 보상 종류가 4개 이하면 한 줄이라 아래가 비어 보이지 않게 칸 영역 한가운데에 둔다.
            layout.childAlignment = TextAnchor.MiddleCenter;
            dialog.rewardGrid = grid.GetComponent<RectTransform>();

            dialog.rewardTemplate = UISeasonPassRewardCell.Create(grid.transform, font);
        }

        /// <summary>혜택 한 줄 — SmallBox x4 어두운 금색, 보이는 820x86. 반환값은 글자.</summary>
        private static TMP_Text BenefitRow(Transform p, string name, string iconPath, string text, float y, TMP_FontAsset font)
        {
            Image bar = UISeasonPassUIFactory.Sliced(p, name, "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(872f, 138f), UISeasonPassUIFactory.Pos(542f, y + 2f), UISeasonPassUIFactory.BenefitBarColor);
            bar.raycastTarget = false;

            UISeasonPassUIFactory.Picture(bar.transform, "Icon", iconPath, new Vector2(80f, 80f), new Vector2(-350f, 0f));

            TMP_Text label = UISeasonPassUIFactory.CreateFittedText(
                "Label", bar.transform, text, 38f, 26f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(660f, 56f), new Vector2(40f, 0f));
            return label;
        }

        private static void BakeButtons(UISeasonPassPremiumDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 구매 — Big_Btn_Green x4, 보이는 520x150 (280,1515). 여백 좌우 4 · 위 6 · 아래 2 px x4
            Image buy = UISeasonPassUIFactory.Sliced(p, "BuyButton", "Ingame/Big_Btn_Green", 4f,
                new Vector2(552f, 182f), UISeasonPassUIFactory.Pos(540f, 1582f), Color.white);
            dialog.buyButton = UISeasonPassUIFactory.SpriteButton(buy);

            dialog.priceText = UISeasonPassUIFactory.CreateFittedText(
                "Price", buy.transform, "9,900원", 60f, 36f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.priceText.rectTransform, new Vector2(460f, 80f), new Vector2(0f, 8f));

            // 뒤로 — 패스 창과 같은 자리·그림
            Image back = UISeasonPassUIFactory.Sliced(p, "PremiumBackButton", "Ingame/Btn_Gray", 5f,
                new Vector2(204f, 209f), UISeasonPassUIFactory.Pos(87f, 1813.5f), Color.white);
            dialog.AddExitButton(UISeasonPassUIFactory.SpriteButton(back));
            UISeasonPassUIFactory.Picture(back.transform, "Icon", "Main/Icon_Back",
                new Vector2(128f, 128f), new Vector2(2f, -1.5f));
        }
    }
}
