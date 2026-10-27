using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Save;
using OJ.SeasonPass;
using OJ.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 멤버십 창 — 고기 멤버십(30일)과 광고제거권(영구) 카드 두 장. (기획서 ShopPackageDesign 5장)
    ///
    /// <b>혜택 줄 수를 일부러 줄였다.</b> 광고제거권 1줄, 고기 멤버십 2줄(5.2-1·2). 광고제거권에
    /// 혜택을 붙이는 순간 "싸고 정직한 첫 결제" 라는 역할이 사라진다(BMDesign 4장).
    ///
    /// <b>산 뒤의 모습</b>: 카드는 <b>내리지 않는다</b>(사용자 피드백 2026-10-06 — 기획서 5.2-3 의
    /// "카드를 내린다" 를 뒤집음. 산 것이 화면에서 사라지면 산 기록이 없어진 것처럼 읽힌다).
    /// 광고제거권은 가격 버튼 자리에 "보유 중", 고기 멤버십은 카드 아래 "잔여 n일" + 버튼 자리 "이용 중" 을 띄우고
    /// 만료 3일 전부터 다시 가격 버튼을 연다 — 그때 사면 만료일 뒤로 붙는다.
    ///
    /// <b>결제는 아직 없다.</b> 두 버튼 모두 <see cref="ShopPurchaseManager.TryBuyCashProduct"/> 를 지난다 —
    /// 현금의 유일한 관문이고, SDK 를 붙이면 그 한 곳만 고친다.
    /// </summary>
    public sealed class UIMembershipDialog : DialogBase
    {
        private const string MeatMembershipProductId = "meat_membership";
        private const string AdRemovalProductId = "ad_removal";

        /// <summary>
        /// 두 상품은 재화를 주지 않는다 — 권리만 준다. <c>TryBuyCashProduct</c> 는 null 을 "잘못된 상품" 으로
        /// 보므로 빈 목록을 넘긴다.
        /// </summary>
        private static readonly ShopDatabase.Reward[] NoContents = new ShopDatabase.Reward[0];

        [SerializeField] private Button meatBuyButton;
        [SerializeField] private TMP_Text meatPriceText;
        [SerializeField] private TMP_Text meatDaysText;
        [SerializeField] private GameObject meatActiveMark;

        [SerializeField] private Button adBuyButton;
        [SerializeField] private GameObject adOwnedMark;
        [SerializeField] private TMP_Text adPriceText;

        protected override void OnLoad()
        {
            UseBackBtn = true;

            if (meatBuyButton != null)
                meatBuyButton.onClick.AddListener(OnClickBuyMeatMembership);

            if (adBuyButton != null)
                adBuyButton.onClick.AddListener(OnClickBuyAdRemoval);
        }

        protected override void OnUnload()
        {
            if (meatBuyButton != null)
                meatBuyButton.onClick.RemoveListener(OnClickBuyMeatMembership);

            if (adBuyButton != null)
                adBuyButton.onClick.RemoveListener(OnClickBuyAdRemoval);
        }

        protected override void OnEnter()
        {
            if (EntitlementManager.Instance != null)
                EntitlementManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        protected override void OnExit()
        {
            if (EntitlementManager.Instance != null)
                EntitlementManager.Instance.OnChanged -= Refresh;
        }

        private static ShopDatabase.MembershipOffers Offers => ShopDatabaseProvider.Database.Membership;

        private void Refresh()
        {
            EntitlementManager entitlements = EntitlementManager.Instance;
            if (entitlements == null)
            {
                // 컨테이너가 서기 전에 열렸다. 조용히 넘어가지 않는다 — 배선 사고이지 "안 샀음" 이 아니다.
                Debug.LogError("[멤버십] EntitlementManager 가 없다. 창을 그리지 못한다.");
                return;
            }

            ShopDatabase.MembershipOffers offers = Offers;

            // ── 고기 멤버십 ──
            bool active = entitlements.MeatMembershipActive;
            bool canBuy = entitlements.CanRebuyMeatMembership(offers.rebuyWindowDays);

            // 카드 아래 기간 — 사기 전엔 상품 기간, 산 뒤엔 남은 날.
            if (meatDaysText != null)
                meatDaysText.SetText(active
                    ? "잔여 " + entitlements.MeatMembershipRemainingDays + "일"
                    : offers.meatMembershipDays + "일");

            if (meatActiveMark != null)
                meatActiveMark.SetActive(active && !canBuy);

            // 3일 전까지는 버튼을 숨기고 남은 기간만 보인다. 그 전에 사게 두면 "언제 사도 손해 없음" 이
            // 맞더라도 화면이 계속 결제를 권하는 꼴이 된다(기획서 5.2-4).
            if (meatBuyButton != null)
                meatBuyButton.gameObject.SetActive(canBuy);

            if (meatPriceText != null)
                meatPriceText.SetText(Won(offers.meatMembershipPriceWon));

            // ── 광고제거권 ── 산 뒤에도 카드는 남기고 가격 버튼만 "보유 중" 으로 바꾼다.
            if (adBuyButton != null)
                adBuyButton.gameObject.SetActive(!entitlements.AdFree);

            if (adOwnedMark != null)
                adOwnedMark.SetActive(entitlements.AdFree);

            if (adPriceText != null)
                adPriceText.SetText(Won(offers.adRemovalPriceWon));
        }

        private static string Won(int price)
        {
            return price.ToString("N0", CultureInfo.InvariantCulture) + "원";
        }

        private void OnClickBuyMeatMembership()
        {
            EntitlementManager entitlements = EntitlementManager.Instance;
            ShopPurchaseManager shop = ShopPurchaseManager.Instance;
            if (entitlements == null || shop == null)
                return;

            ShopDatabase.MembershipOffers offers = Offers;
            if (!entitlements.CanRebuyMeatMembership(offers.rebuyWindowDays))
                return;

            if (shop.TryBuyCashProduct(MeatMembershipProductId, offers.meatMembershipPriceWon, NoContents) != ShopPurchaseResult.Success)
                return;

            entitlements.GrantMeatMembership(offers.meatMembershipDays);
        }

        private void OnClickBuyAdRemoval()
        {
            EntitlementManager entitlements = EntitlementManager.Instance;
            ShopPurchaseManager shop = ShopPurchaseManager.Instance;
            if (entitlements == null || shop == null || entitlements.AdFree)
                return;

            if (shop.TryBuyCashProduct(AdRemovalProductId, Offers.adRemovalPriceWon, NoContents) != ShopPurchaseResult.Success)
                return;

            entitlements.AdFree = true;
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 시안 Art/Layout/MemberShip_Layout.png · Art/0PSD/멤버십.psd (2026-10-27).
        // 좌표는 1080x1920 PSD 픽셀(좌상단 원점) 그대로. 기록: Docs/MembershipUI.md
        //
        // 긴 화면(22:9 등)은 위 묶음(배너·제목·띠)을 위에, 뒤로 버튼을 아래에 붙이고 상품 상자 둘은 가운데 둔다.

        private static readonly Color BackgroundColor = UISeasonPassUIFactory.Hex(0x1e1828);
        private static readonly Color TitleColor = UISeasonPassUIFactory.Hex(0xf6ff00);
        private static readonly Color SubPointColor = UISeasonPassUIFactory.Hex(0xfeb514);
        private static readonly Color BenefitPointColor = UISeasonPassUIFactory.Hex(0x2aff00);
        private static readonly Color DaysColor = UISeasonPassUIFactory.Hex(0xff8484);

        private static readonly Color MeatStripColor = UISeasonPassUIFactory.Hex(0xfe9e14);
        private static readonly Color MeatHeaderColor = UISeasonPassUIFactory.Hex(0xe13d3e);
        private static readonly Color AdStripColor = UISeasonPassUIFactory.Hex(0x7134be);
        private static readonly Color AdHeaderColor = UISeasonPassUIFactory.Hex(0xfd53f7);
        private static readonly Color AdGlowColor = UISeasonPassUIFactory.Hex(0xff8efb);

        /// <summary>혜택 줄 판 — SmallBox 검정 알파 30%(PSD 불투명도 77).</summary>
        private static readonly Color BenefitRowColor = new Color(0f, 0f, 0f, 77f / 255f);

        /// <summary>카드 기울기(시안 주석 "10도"). 오른쪽이 내려가므로 z 는 음수.</summary>
        private const float CardTilt = -10f;

        /// <summary>창 하나를 조립한다. 에디터 굽기 경로에서만 부른다. 시안의 글자는 전부 검정 외곽선이다.</summary>
        public static UIMembershipDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UIMembershipDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIMembershipDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Page);

            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            // 바탕 — 화면 전체. 클릭을 먹어 뒤의 로비가 안 눌리게 한다.
            Image background = UISeasonPassUIFactory.CreateImage("Background", view.transform, BackgroundColor);
            UISeasonPassUIFactory.Stretch(background.rectTransform);

            Transform middle = UISeasonPassUIFactory.AnchoredGroup("Middle", view.transform, 0.5f);
            Transform top = UISeasonPassUIFactory.AnchoredGroup("Top", view.transform, 1f);
            Transform bottom = UISeasonPassUIFactory.AnchoredGroup("Bottom", view.transform, 0f);

            BakeTop(top, font);
            BakeMeatCard(dialog, middle, font);
            BakeAdCard(dialog, middle, font);

            // 뒤로 — Btn_Gray x5, 보이는 (14,1741) 164x169. 아이콘 Icon_Back x4. 특별한 7일 창과 같은 자리.
            Image back = UISeasonPassUIFactory.Sliced(bottom, "MembershipBackButton", "Ingame/Btn_Gray", 5f,
                new Vector2(204f, 209f), UISeasonPassUIFactory.Pos(96f, 1815.5f), Color.white);
            dialog.AddExitButton(UISeasonPassUIFactory.SpriteButton(back));
            UISeasonPassUIFactory.Picture(back.transform, "Icon", "Main/Icon_Back",
                new Vector2(128f, 128f), new Vector2(2f, -1.5f));

            return dialog;
        }

        /// <summary>배너·"멤버십" 제목·부제 + "특별 멤버십" 띠. 띠는 특별한 7일 창과 같다(시안 주석 "멤버십이랑 같음").</summary>
        private static void BakeTop(Transform p, TMP_FontAsset font)
        {
            // 배너 — 시안 주석은 MemberShop_Baneer x2 인데 그러면 왼쪽 88px 이 빈다. PSD 의 넓은 원본(레이어 2130, 1:1)을
            // 화면 폭으로 잘라 넣었다. 겹치는 곳은 Baneer x2 와 픽셀 대조 오차 6.8 로 같은 그림이다.
            UISeasonPassUIFactory.Picture(p, "Banner", "MemberShip/MemberShip_Banner_Wide",
                new Vector2(1080f, 489f), UISeasonPassUIFactory.Pos(540f, 239.5f));

            UISeasonPassUIFactory.Solid(p, "BandOutline", Color.black, 0f, 478f, 1080f, 570f);
            UISeasonPassUIFactory.Solid(p, "BandLine", UISeasonPassUIFactory.Hex(0xfecf0d), 0f, 483f, 1080f, 565f);
            UISeasonPassUIFactory.Solid(p, "Band", UISeasonPassUIFactory.Hex(0xa94800), 0f, 488f, 1080f, 560f);

            // 띠 위 제목 판 — Pass_premium_Btn x3, 보이는 701x102. 여백(좌우 12, 위아래 47) x3.
            Image header = UISeasonPassUIFactory.Sliced(p, "HeaderPlate", "Pass/Pass_premium_Btn", 3f,
                new Vector2(773f, 384f), UISeasonPassUIFactory.Pos(545.5f, 483f), Color.white);
            header.raycastTarget = false;
            TMP_Text headerText = UISeasonPassUIFactory.CreateText("Label", header.transform, "특별 멤버십", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(headerText.rectTransform, new Vector2(600f, 60f), new Vector2(0f, 1f));

            TMP_Text title = UISeasonPassUIFactory.CreateText("Title", p, "멤버십", 95f,
                TextAlignmentOptions.Left, TitleColor, font);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(500f, 110f), UISeasonPassUIFactory.Pos(35f + 250f, 86.5f));

            TMP_Text sub = UISeasonPassUIFactory.CreateText("Headline", p,
                "매일 받는 " + UISeasonPassUIFactory.Colored("특별 혜택!", SubPointColor), 50f,
                TextAlignmentOptions.Left, Color.white, font);
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            UISeasonPassUIFactory.SetRect(sub.rectTransform, new Vector2(600f, 64f), UISeasonPassUIFactory.Pos(41f + 300f, 169.5f));
        }

        private static void BakeMeatCard(UIMembershipDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 상자 — 보이는 (30,582)~(1048,1141). 상단 제목 줄은 왼쪽에서 오른쪽으로 사라지는 주황.
            Box(p, "MeatBox", "MemberShip/MemberShip_Meet", 582f, 1141f);
            Strip(p, "MeatTitleStrip", MeatStripColor, 908f, 597f);
            Name(p, "고기 멤버십", 637.5f, font);

            Header(p, "MeatHeader", "고기 멤버십 혜택", 727f, MeatHeaderColor, font);
            BenefitRow(p, "EntryBenefit", "Gem/Gem_GamePlay", new Vector2(108f, 853f), new Vector2(128f, 128f),
                "스테이지 입장 " + UISeasonPassUIFactory.Colored("고기 면제", BenefitPointColor), 817f, 173f, font);
            BenefitRow(p, "SweepBenefit", "Main/Icon_Battle", new Vector2(108f, 947f), new Vector2(128f, 128f),
                "소탕 횟수 " + UISeasonPassUIFactory.Colored("무제한", BenefitPointColor), 910f, 179f, font);

            // 카드 — 흰 원판 x6.5 뒤에 고기 카드 x5 를 10도 기울이고, 그 위에 고기 x4(안 기울인다).
            // 배율·각도는 PSD 레이어와 그림을 돌려 맞춘 실측(카드 오차 4.9, 고기 0.8).
            UISeasonPassUIFactory.Picture(p, "CardGlow", "MemberShip/MemberShip_Card_Bg",
                new Vector2(832f, 832f), UISeasonPassUIFactory.Pos(792.75f, 861.75f));
            Tilted(UISeasonPassUIFactory.Picture(p, "Card", "MemberShip/MemberShip_Meet_Card",
                new Vector2(640f, 640f), UISeasonPassUIFactory.Pos(795.5f, 849f)), CardTilt);
            UISeasonPassUIFactory.Picture(p, "CardIcon", "Gem/Gem_GamePlay",
                new Vector2(256f, 256f), UISeasonPassUIFactory.Pos(794f, 844.5f));

            // 기간 — 카드 아래 테두리에 비스듬히(PSD 회전 6.6도). 산 뒤에는 남은 날.
            dialog.meatDaysText = UISeasonPassUIFactory.CreateText("Days", p, "30일", 45f,
                TextAlignmentOptions.Center, DaysColor, font);
            UISeasonPassUIFactory.SetRect(dialog.meatDaysText.rectTransform, new Vector2(240f, 60f), UISeasonPassUIFactory.Pos(756.5f, 972.5f));
            Tilted(dialog.meatDaysText, -6.6f);

            // 가격 — Big_Btn_Yellow x3, 보이는 (667,1014) 323x100. 여백 x3 → +24, 그림이 6 아래로 치우쳐 위로 6.
            dialog.meatBuyButton = PriceButton(p, "MeatBuyButton", 828.5f, 1064f, "5,000원", font, out dialog.meatPriceText);

            // 이용 중 — 재구매 창(만료 3일 전)이 열리기 전까지 가격 버튼 자리에.
            dialog.meatActiveMark = StateText(p, "Active", "이용 중", 828.5f, 1064f, font);
        }

        private static void BakeAdCard(UIMembershipDialog dialog, Transform p, TMP_FontAsset font)
        {
            Box(p, "AdBox", "MemberShip/MemberShip_AD", 1163f, 1722f);
            Strip(p, "AdTitleStrip", AdStripColor, 806f, 1178f);
            Name(p, "광고 제거 카드", 1217.5f, font);

            // 혜택은 한 줄뿐이다(기획서 5.2-1). 여기에 줄을 더하지 말 것.
            Header(p, "AdHeader", "광고 제거 카드 혜택", 1308f, AdHeaderColor, font);
            BenefitRow(p, "AdBenefit", "Shop/Icon_AD", new Vector2(124f, 1426f), new Vector2(128f, 128f),
                "영구 광고 제거 특권", 1398f, 193f, font);

            Image glow = UISeasonPassUIFactory.Picture(p, "CardGlow", "MemberShip/MemberShip_Card_Bg",
                new Vector2(832f, 832f), UISeasonPassUIFactory.Pos(792.75f, 1460.75f));
            glow.color = AdGlowColor;
            Tilted(UISeasonPassUIFactory.Picture(p, "Card", "MemberShip/MemberShip_AD_Card",
                new Vector2(640f, 640f), UISeasonPassUIFactory.Pos(789.5f, 1457f)), CardTilt);

            // 광고 그림은 고기와 달리 카드와 같이 기울어 있다(x1.8, 오차 6.0).
            Tilted(UISeasonPassUIFactory.Picture(p, "CardIcon", "Shop/Icon_AD",
                new Vector2(230f, 230f), UISeasonPassUIFactory.Pos(792.5f, 1455.5f)), CardTilt);

            dialog.adBuyButton = PriceButton(p, "AdBuyButton", 811.5f, 1645f, "3,000원", font, out dialog.adPriceText);

            // 산 뒤 — 가격 버튼 자리에 "보유 중". 카드는 내리지 않는다.
            dialog.adOwnedMark = StateText(p, "Owned", "보유 중", 811.5f, 1645f, font);
        }

        // ── 조립 도우미 ───────────────────────────────────────────────

        /// <summary>
        /// 상품 상자 — 보이는 x 30~1048, y <paramref name="y0"/>~<paramref name="y1"/>. 그림(보이는 340x190)을
        /// 그 칸에 맞춰 늘린다(가로 x2.994 · 세로 x2.942, 시안 주석 "x3"). 무늬가 있어 9슬라이스로 늘리지 않는다.
        /// </summary>
        private static void Box(Transform p, string name, string path, float y0, float y1)
        {
            float sx = 1018f / 340f;
            float sy = (y1 - y0) / 190f;

            // 그림 512 안에서 보이는 곳이 (86,163)~(426,353) — 가운데보다 2px 아래다.
            UISeasonPassUIFactory.Picture(p, name, path, new Vector2(512f * sx, 512f * sy),
                UISeasonPassUIFactory.Pos(539f, (y0 + y1) * 0.5f - 2f * sy));
        }

        /// <summary>상자 위 제목 줄 — 왼쪽은 꽉 찬 색, 가운데부터 오른쪽 끝 <paramref name="x1"/> 까지 사라진다. 높이 85.</summary>
        private static void Strip(Transform p, string name, Color color, float x1, float y0)
        {
            const float x0 = 40f;
            Image strip = UISeasonPassUIFactory.Picture(p, name, "MemberShip/MemberShip_Title_Fade",
                new Vector2(x1 - x0, 85f), UISeasonPassUIFactory.Pos((x0 + x1) * 0.5f, y0 + 42.5f));
            strip.color = color;
        }

        private static void Name(Transform p, string text, float centerY, TMP_FontAsset font)
        {
            TMP_Text name = UISeasonPassUIFactory.CreateText("Name", p, text, 70f, TextAlignmentOptions.Left, Color.white, font);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            UISeasonPassUIFactory.SetRect(name.rectTransform, new Vector2(500f, 80f), UISeasonPassUIFactory.Pos(80f + 250f, centerY));
        }

        /// <summary>혜택 머리 줄 — SmallBox x4 불투명, 보이는 x 58~571 (513x76). 여백(좌·위 6, 우·아래 7) x4 → +52.</summary>
        private static void Header(Transform p, string name, string text, float y0, Color color, TMP_FontAsset font)
        {
            Image pill = UISeasonPassUIFactory.Sliced(p, name, "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(565f, 128f), UISeasonPassUIFactory.Pos(316.5f, y0 + 40f), color);
            pill.raycastTarget = false;
            TMP_Text label = UISeasonPassUIFactory.CreateText("Label", pill.transform, text, 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(480f, 60f), new Vector2(-2f, 3f));
        }

        /// <summary>
        /// 혜택 한 줄 — SmallBox x4 검정 30%, 보이는 x 58~571 · y <paramref name="y0"/>~+76.
        /// 아이콘은 재화 그림 x2(PSD 위치 그대로), 글자는 왼쪽 끝 <paramref name="textLeft"/> 40pt.
        /// </summary>
        private static void BenefitRow(Transform p, string name, string iconPath, Vector2 iconCenter, Vector2 iconSize,
            string text, float y0, float textLeft, TMP_FontAsset font)
        {
            Image row = UISeasonPassUIFactory.Sliced(p, name, "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(565f, 128f), UISeasonPassUIFactory.Pos(316.5f, y0 + 40f), BenefitRowColor);
            row.raycastTarget = false;

            UISeasonPassUIFactory.Picture(p, name + "Icon", iconPath, iconSize, UISeasonPassUIFactory.Pos(iconCenter.x, iconCenter.y));

            const float right = 560f;
            TMP_Text label = UISeasonPassUIFactory.CreateFittedText(name + "Label", p, text, 40f, 28f,
                TextAlignmentOptions.Left, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(right - textLeft, 56f),
                UISeasonPassUIFactory.Pos((textLeft + right) * 0.5f, y0 + 36f));
        }

        private static Button PriceButton(Transform p, string name, float x, float y, string text, TMP_FontAsset font, out TMP_Text price)
        {
            Image buy = UISeasonPassUIFactory.Sliced(p, name, "Ingame/Big_Btn_Yellow", 3f,
                new Vector2(347f, 124f), UISeasonPassUIFactory.Pos(x, y - 6f), Color.white);
            Button button = UISeasonPassUIFactory.SpriteButton(buy);
            price = UISeasonPassUIFactory.CreateFittedText("Price", buy.transform, text, 45f, 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(price.rectTransform, new Vector2(290f, 60f), new Vector2(5.5f, -1f));
            return button;
        }

        private static GameObject StateText(Transform p, string name, string text, float x, float y, TMP_FontAsset font)
        {
            TMP_Text label = UISeasonPassUIFactory.CreateText(name, p, text, 45f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(323f, 70f), UISeasonPassUIFactory.Pos(x, y));
            label.gameObject.SetActive(false);
            return label.gameObject;
        }

        private static void Tilted(Graphic graphic, float degrees)
        {
            graphic.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
