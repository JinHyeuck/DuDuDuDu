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
    /// <b>산 뒤의 모습</b>(5.2-3·4): 광고제거권 카드는 내린다. 고기 멤버십 카드는 "잔여 n일" 을
    /// 띄우고, 만료 3일 전부터 다시 가격 버튼을 연다 — 그때 사면 만료일 뒤로 붙는다.
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
        [SerializeField] private TMP_Text meatRemainingText;
        [SerializeField] private TMP_Text meatDaysText;

        [SerializeField] private GameObject adCard;
        [SerializeField] private Button adBuyButton;
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

            if (meatDaysText != null)
                meatDaysText.SetText("(" + offers.meatMembershipDays + "일)");

            if (meatRemainingText != null)
            {
                meatRemainingText.gameObject.SetActive(active);
                meatRemainingText.SetText("잔여 " + entitlements.MeatMembershipRemainingDays + "일");
            }

            // 3일 전까지는 버튼을 숨기고 남은 기간만 보인다. 그 전에 사게 두면 "언제 사도 손해 없음" 이
            // 맞더라도 화면이 계속 결제를 권하는 꼴이 된다(기획서 5.2-4).
            if (meatBuyButton != null)
                meatBuyButton.gameObject.SetActive(canBuy);

            if (meatPriceText != null)
                meatPriceText.SetText(Won(offers.meatMembershipPriceWon));

            // ── 광고제거권 ── 산 뒤에는 카드를 내린다(기획서 5.2-3).
            if (adCard != null)
                adCard.SetActive(!entitlements.AdFree);

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
        // PSD 가 없다. 레퍼런스(카드 두 장 · 왼쪽 혜택 줄 · 오른쪽 이름·엠블럼·가격)를 시즌 패스와
        // 같은 그림·색으로 옮겼다. 좌표는 1080x1920 화면 픽셀(좌상단 원점). 기록: Docs/MembershipUI.md

        private static readonly Color CardOutline = Color.black;
        private static readonly Color MeatCardColor = UISeasonPassUIFactory.Hex(0xffb21e);
        private static readonly Color MeatPillColor = UISeasonPassUIFactory.Hex(0xe0730a);
        private static readonly Color MeatRowColor = UISeasonPassUIFactory.Hex(0xffe9a8);
        private static readonly Color AdCardColor = UISeasonPassUIFactory.Hex(0x23223a);
        private static readonly Color AdCardOutline = UISeasonPassUIFactory.Hex(0x9fd8ff);
        private static readonly Color AdPillColor = UISeasonPassUIFactory.Hex(0x6b4ce0);
        private static readonly Color AdRowColor = UISeasonPassUIFactory.Hex(0x3a3960);
        private static readonly Color AdIconColor = UISeasonPassUIFactory.Hex(0x2f7bff);
        private static readonly Color SlashColor = UISeasonPassUIFactory.Hex(0xff2d2d);
        private static readonly Color DarkText = UISeasonPassUIFactory.Hex(0x4a2a00);

        /// <summary>
        /// 창 하나를 조립한다. 에디터 굽기 경로에서만 부른다.
        /// <paramref name="plainMaterial"/> 은 외곽선 없는 글자 재질 — 밝은 줄 위의 어두운 글자에 쓴다.
        /// </summary>
        public static UIMembershipDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UIMembershipDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIMembershipDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Page);

            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            Transform p = view.transform;

            // 바탕 — 시즌 패스 무료 트랙과 같은 색·무늬. 클릭을 먹어 뒤의 로비가 안 눌리게 한다.
            Image background = UISeasonPassUIFactory.CreateImage("Background", p, UISeasonPassUIFactory.FreeTrackColor);
            UISeasonPassUIFactory.Stretch(background.rectTransform);
            foreach (Vector2 c in new[] { new Vector2(270f, 480f), new Vector2(810f, 480f), new Vector2(270f, 1260f), new Vector2(810f, 1260f) })
            {
                Image pattern = UISeasonPassUIFactory.Picture(p, "Pattern", "Pass/Pass_Pattern",
                    new Vector2(1024f, 1024f), UISeasonPassUIFactory.Pos(c.x, c.y));
                pattern.color = UISeasonPassUIFactory.FreePatternTint;
            }

            // 제목 띠 — 패스 프리미엄 버튼 그림 x3, 보이는 880x102
            Image banner = UISeasonPassUIFactory.Sliced(p, "TitleBanner", "Pass/Pass_premium_Btn", 3f,
                new Vector2(952f, 384f), UISeasonPassUIFactory.Pos(540f, 200f), Color.white);
            banner.raycastTarget = false;
            TMP_Text title = UISeasonPassUIFactory.CreateText("Title", banner.transform, "멤버십", 60f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(700f, 90f), new Vector2(0f, 2f));

            BakeMeatCard(dialog, p, font, plainMaterial);
            BakeAdCard(dialog, p, font);

            // 뒤로 — 시즌 패스 창과 같은 자리·그림
            Image back = UISeasonPassUIFactory.Sliced(p, "MembershipBackButton", "Ingame/Btn_Gray", 5f,
                new Vector2(204f, 209f), UISeasonPassUIFactory.Pos(87f, 1813.5f), Color.white);
            dialog.AddExitButton(UISeasonPassUIFactory.SpriteButton(back));
            UISeasonPassUIFactory.Picture(back.transform, "Icon", "Main/Icon_Back",
                new Vector2(128f, 128f), new Vector2(2f, -1.5f));

            return dialog;
        }

        private static void BakeMeatCard(UIMembershipDialog dialog, Transform p, TMP_FontAsset font, Material plainMaterial)
        {
            // 카드 — 보이는 x 40~1040, y 330~930
            GameObject card = Panel(p, "MeatCard", 40f, 330f, 1040f, 930f, MeatCardColor, CardOutline).gameObject;
            Transform c = card.transform;

            Pill(c, "Header", "고기 멤버십 혜택", 300f, 400f, MeatPillColor, font);
            BenefitRow(c, "EntryBenefit", "Gem/Gem_GamePlay", "스테이지 입장 고기 면제", 500f, MeatRowColor, DarkText, plainMaterial, font);
            BenefitRow(c, "SweepBenefit", "Main/Icon_Battle", "소탕 횟수 무제한", 600f, MeatRowColor, DarkText, plainMaterial, font);

            TMP_Text name = UISeasonPassUIFactory.CreateText("Name", c, "고기 멤버십", 55f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(name.rectTransform, new Vector2(460f, 70f), Local(790f, 405f, 540f, 630f));

            dialog.meatDaysText = UISeasonPassUIFactory.CreateText("Days", c, "(30일)", 40f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.meatDaysText.rectTransform, new Vector2(300f, 56f), Local(790f, 465f, 540f, 630f));

            // 엠블럼 — 고기 x5 위에 왕관 x3
            UISeasonPassUIFactory.Picture(c, "EmblemMeat", "Gem/Gem_GamePlay", new Vector2(320f, 320f), Local(790f, 640f, 540f, 630f));
            UISeasonPassUIFactory.Picture(c, "EmblemCrown", "Pass/Pass_Crown", new Vector2(128f, 128f), Local(790f, 525f, 540f, 630f));

            // 흰 글자 — 노란 글자는 금색 카드 위에서 묻혔다.
            dialog.meatRemainingText = UISeasonPassUIFactory.CreateText("Remaining", c, "잔여 30일", 44f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.meatRemainingText.rectTransform, new Vector2(420f, 60f), Local(790f, 770f, 540f, 630f));

            // 가격 — Big_Btn_Green x4, 보이는 420x110 (y 795~905). 여백 좌우 4 · 위 6 · 아래 2 px x4
            Image buy = UISeasonPassUIFactory.Sliced(c, "MeatBuyButton", "Ingame/Big_Btn_Green", 4f,
                new Vector2(452f, 142f), Local(790f, 842f, 540f, 630f), Color.white);
            dialog.meatBuyButton = UISeasonPassUIFactory.SpriteButton(buy);
            dialog.meatPriceText = UISeasonPassUIFactory.CreateFittedText("Price", buy.transform, "5,000원", 50f, 32f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.meatPriceText.rectTransform, new Vector2(400f, 70f), new Vector2(0f, 8f));
        }

        private static void BakeAdCard(UIMembershipDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 카드 — 보이는 x 40~1040, y 980~1460. 짙은 남색 + 밝은 하늘색 테두리(레퍼런스의 영구 상품 카드)
            Image card = Panel(p, "AdCard", 40f, 980f, 1040f, 1460f, AdCardColor, AdCardOutline);
            dialog.adCard = card.transform.parent.gameObject;
            Transform c = card.transform;
            const float cy = 1220f;

            Pill(c, "Header", "광고제거권 혜택", 300f, 1050f, AdPillColor, font, cy);

            // 혜택은 한 줄뿐이다(기획서 5.2-1). 여기에 줄을 더하지 말 것.
            Image row = UISeasonPassUIFactory.Sliced(c, "AdBenefit", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(492f, 136f), Local(302f, 1152f, 540f, cy), AdRowColor);
            row.raycastTarget = false;
            AdIcon(row.transform, new Vector2(-150f, 0f), 0.6f, font);
            TMP_Text label = UISeasonPassUIFactory.CreateText("Label", row.transform, "광고 제거", 36f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(280f, 56f), new Vector2(50f, 0f));

            TMP_Text name = UISeasonPassUIFactory.CreateText("Name", c, "광고제거권", 55f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(name.rectTransform, new Vector2(460f, 70f), Local(790f, 1050f, 540f, cy));
            TMP_Text days = UISeasonPassUIFactory.CreateText("Days", c, "(영구)", 40f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(days.rectTransform, new Vector2(300f, 56f), Local(790f, 1110f, 540f, cy));

            AdIcon(c, Local(790f, 1235f, 540f, cy), 1.4f, font);

            Image buy = UISeasonPassUIFactory.Sliced(c, "AdBuyButton", "Ingame/Big_Btn_Green", 4f,
                new Vector2(452f, 142f), Local(790f, 1372f, 540f, cy), Color.white);
            dialog.adBuyButton = UISeasonPassUIFactory.SpriteButton(buy);
            dialog.adPriceText = UISeasonPassUIFactory.CreateFittedText("Price", buy.transform, "3,000원", 50f, 32f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.adPriceText.rectTransform, new Vector2(400f, 70f), new Vector2(0f, 8f));
        }

        // ── 조립 도우미 ───────────────────────────────────────────────

        /// <summary>
        /// 카드 판 — 테두리용 SmallBox 위에 색 SmallBox(x4, 여백 6/7px). 반환값은 색 판이고,
        /// 둘을 묶는 부모가 그 위에 있다(카드를 통째로 끄고 켤 때 부모를 쓴다).
        /// </summary>
        private static Image Panel(Transform parent, string name, float x0, float y0, float x1, float y1, Color fill, Color outline)
        {
            GameObject group = UISeasonPassUIFactory.CreateRect(name, parent);
            UISeasonPassUIFactory.Stretch(group.GetComponent<RectTransform>());

            const float edge = 8f;
            UISeasonPassUIFactory.Sliced(group.transform, "Outline", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(x1 - x0 + edge * 2f + 52f, y1 - y0 + edge * 2f + 52f),
                UISeasonPassUIFactory.Pos((x0 + x1) * 0.5f + 2f, (y0 + y1) * 0.5f + 2f), outline);

            return UISeasonPassUIFactory.Sliced(group.transform, "Fill", "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(x1 - x0 + 52f, y1 - y0 + 52f),
                UISeasonPassUIFactory.Pos((x0 + x1) * 0.5f + 2f, (y0 + y1) * 0.5f + 2f), fill);
        }

        /// <summary>화면 좌표(x, y) → 카드 판(중심 cx, cy) 안의 anchoredPosition.</summary>
        private static Vector2 Local(float x, float y, float cx, float cy)
        {
            return new Vector2(x - cx - 2f, cy - y + 2f);
        }

        private static void Pill(Transform card, string name, string text, float x, float y, Color color, TMP_FontAsset font, float cy = 630f)
        {
            Image pill = UISeasonPassUIFactory.Sliced(card, name, "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(452f, 116f), Local(x + 2f, y + 2f, 540f, cy), color);
            pill.raycastTarget = false;
            TMP_Text label = UISeasonPassUIFactory.CreateText("Label", pill.transform, text, 36f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(400f, 56f), new Vector2(-2f, 2f));
        }

        private static void BenefitRow(Transform card, string name, string iconPath, string text, float y,
            Color color, Color textColor, Material plainMaterial, TMP_FontAsset font)
        {
            Image row = UISeasonPassUIFactory.Sliced(card, name, "Upgrade/Ui_Popup_SmallBox", 4f,
                new Vector2(492f, 136f), Local(302f, y + 2f, 540f, 630f), color);
            row.raycastTarget = false;

            UISeasonPassUIFactory.Picture(row.transform, "Icon", iconPath, new Vector2(100f, 100f), new Vector2(-172f, 0f));

            TMP_Text label = UISeasonPassUIFactory.CreateFittedText("Label", row.transform, text, 34f, 24f,
                TextAlignmentOptions.Center, textColor, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(330f, 56f), new Vector2(40f, 0f));
            if (plainMaterial != null)
                label.fontSharedMaterial = plainMaterial;
        }

        /// <summary>
        /// 광고 아이콘 — 파란 판에 "AD", 빨간 사선. 프로젝트에 광고 그림이 없어 조립한다(그림이 오면 교체).
        /// </summary>
        private static void AdIcon(Transform parent, Vector2 position, float scale, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("AdIcon", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(150f, 110f), position);
            root.transform.localScale = new Vector3(scale, scale, 1f);

            Image box = UISeasonPassUIFactory.Sliced(root.transform, "Box", "Upgrade/Ui_Popup_SmallBox", 2f,
                new Vector2(150f, 110f), Vector2.zero, AdIconColor);
            box.raycastTarget = false;

            TMP_Text ad = UISeasonPassUIFactory.CreateText("Label", root.transform, "AD", 64f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(ad.rectTransform, new Vector2(150f, 100f), new Vector2(0f, 2f));

            Image slash = UISeasonPassUIFactory.CreateImage("Slash", root.transform, SlashColor);
            slash.raycastTarget = false;
            UISeasonPassUIFactory.SetRect(slash.rectTransform, new Vector2(190f, 14f), Vector2.zero);
            slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
        }
    }
}
