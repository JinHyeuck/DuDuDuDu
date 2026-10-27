using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.SeasonPass;
using OJ.UI;

namespace OJ.Shop
{
    /// <summary>
    /// 특별한 7일 창. (기획서 ShopPackageDesign 4장 · BMDesign 6장)
    ///
    /// <b>1열 7줄이다</b>(사용자 지정 2026-10-06). 그림은 시안 <c>7DaysEvent_Layout.png</c> ·
    /// <c>7일출석.psd</c>(2026-10-27) — 위에 배너·리본 제목, 가운데 7줄, 아래에 캐시백 문구와 가격 버튼.
    ///
    /// <b>미구매 상태에서도 쌓인 보상을 보여 준다</b>(4.3) — 지난 날의 줄은 어두운 판 + 자물쇠로,
    /// 보상은 그대로. 사는 순간 쌓인 날을 한꺼번에 받는다(6.1).
    ///
    /// <b>글자에 <c>·</c> 를 쓰지 않는다</b> — BM HANNA 에 없는 글리프라 빈칸으로 찍힌다(Tools/ui/PORTING.md).
    /// </summary>
    public sealed class UISpecialSevenDaysDialog : DialogBase
    {
        [SerializeField] private UISpecialSevenDaysRow[] rows = new UISpecialSevenDaysRow[OJ.Core.SpecialSevenDaysRules.DayCount];
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyText;

        protected override void OnLoad()
        {
            UseBackBtn = true;

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
            if (SpecialSevenDaysManager.Instance != null)
                SpecialSevenDaysManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        protected override void OnExit()
        {
            if (SpecialSevenDaysManager.Instance != null)
                SpecialSevenDaysManager.Instance.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            SpecialSevenDaysManager manager = SpecialSevenDaysManager.Instance;
            if (manager == null)
            {
                // 컨테이너가 서기 전에 열렸다. "안 샀음" 으로 그리지 않는다 — 배선 사고다.
                Debug.LogError("[특별한 7일] SpecialSevenDaysManager 가 없다. 창을 그리지 못한다.");
                return;
            }

            List<SpecialSevenDaysCellView> cells = manager.GetCells();
            for (int i = 0; i < rows.Length && i < cells.Count; i++)
            {
                if (rows[i] != null)
                    rows[i].Bind(cells[i], OnClickClaim);
            }

            int today = manager.CurrentDay;
            int claimable = manager.ClaimableCount;

            if (infoText != null)
            {
                infoText.SetText(manager.Purchased
                    ? (today > OJ.Core.SpecialSevenDaysRules.DayCount ? "출석 완료" : "오늘은 출석 " + today + "일차")
                      + (claimable > 0 ? ", 받을 보상 " + claimable + "일치" : string.Empty)
                    : InfoBeforePurchase);
            }

            // 하단 버튼 — 사기 전엔 구입, 산 뒤엔 받을 것이 있을 때만 "모두 받기".
            bool showBuy = manager.CanPurchase || (manager.Purchased && claimable > 0);
            if (buyButton != null)
                buyButton.gameObject.SetActive(showBuy);

            if (buyText != null)
            {
                buyText.SetText(manager.Purchased
                    ? "모두 받기"
                    : manager.PriceWon.ToString("N0", CultureInfo.InvariantCulture) + "원");
            }
        }

        private void OnClickBuy()
        {
            SpecialSevenDaysManager manager = SpecialSevenDaysManager.Instance;
            if (manager == null)
                return;

            List<PointRewardEntry> granted;
            bool ok = manager.Purchased
                ? manager.ClaimAll(out granted) > 0
                : manager.TryPurchase(out granted);

            if (ok)
                ShowRewardPopup(granted);
        }

        private void OnClickClaim(int day)
        {
            SpecialSevenDaysManager manager = SpecialSevenDaysManager.Instance;
            if (manager != null && manager.TryClaim(day, out List<PointRewardEntry> granted))
                ShowRewardPopup(granted);
        }

        /// <summary>획득 팝업. 다른 보상과 같은 <c>UIRewardResultDialog</c>. 못 열어도 지급은 이미 끝났다.</summary>
        private static void ShowRewardPopup(IReadOnlyList<PointRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return;

            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[특별한 7일] UIRewardResultDialog 를 못 열었다. 지급은 이미 끝났다 — " +
                               PointRewardUtility.BuildRewardSummary(rewards));
                return;
            }

            dialog.Open(rewards, "특별한 7일 보상을 받았습니다.");
        }

        // ── 굽기 ───────────────────────────────────────────────────────
        //
        // 시안 Art/Layout/7DaysEvent_Layout.png · Art/0PSD/7일출석.psd (2026-10-27).
        // 좌표는 1080x1920 PSD 픽셀(좌상단 원점) 그대로. 기록: Docs/SpecialSevenDaysUI.md
        //
        // 긴 화면(22:9 등)에서는 위 묶음(배너·제목)을 위에, 아래 묶음(캐시백·구입·뒤로)을 아래에 붙이고
        // 7줄은 가운데 둔다. 묶음마다 1080x1920 판을 깔아 안의 좌표는 PSD 그대로 쓴다.

        /// <summary>첫 줄 판 중심(y)과 줄 간격. PSD 판 위 끝 585, 740, 896 … 1517 (높이 141).</summary>
        private const float FirstRowY = 655.5f;
        private const float RowPitch = 155.33f;
        private const float RowCenterX = 546.5f;

        private const string InfoBeforePurchase = "매일 보상이 쌓이고, 구매하면 한 번에 받아요";

        private static readonly Color BackgroundColor = UISeasonPassUIFactory.Hex(0x311a20);
        private static readonly Color PointColor = UISeasonPassUIFactory.Hex(0xfee805);
        private static readonly Color InfoColor = UISeasonPassUIFactory.Hex(0xefbeff);
        private static readonly Color CashbackColor = UISeasonPassUIFactory.Hex(0xfde705);

        /// <summary>배너 왼쪽 58px 빈자리. 배너 왼쪽 끝 색의 평균이라 이음매가 안 보인다.</summary>
        private static readonly Color BannerFillColor = UISeasonPassUIFactory.Hex(0x55310f);

        public static UISpecialSevenDaysDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISpecialSevenDaysDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISpecialSevenDaysDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Page);

            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;

            // 바탕 — 화면 전체. 클릭을 먹어 뒤의 로비가 안 눌리게 한다.
            Image background = UISeasonPassUIFactory.CreateImage("Background", view.transform, BackgroundColor);
            UISeasonPassUIFactory.Stretch(background.rectTransform);

            Transform middle = Group("Middle", view.transform, 0.5f);
            Transform top = Group("Top", view.transform, 1f);
            Transform bottom = Group("Bottom", view.transform, 0f);

            // 티켓 무늬 — PSD 불투명도 15. 원본 1254px 를 x0.5.
            foreach (Vector2 c in new[]
                     {
                         new Vector2(229f, 873f), new Vector2(882f, 873f),
                         new Vector2(229f, 1391f), new Vector2(882f, 1391f),
                         new Vector2(229f, 2020f), new Vector2(882f, 2020f),
                     })
            {
                Image pattern = UISeasonPassUIFactory.Picture(middle, "Pattern", "7DaysEvent/7DaysEvent_Pattern",
                    new Vector2(627f, 627f), UISeasonPassUIFactory.Pos(c.x, c.y));
                pattern.color = new Color(1f, 1f, 1f, 15f / 255f);
            }

            BuildTop(dialog, top, font);

            for (int i = 0; i < dialog.rows.Length; i++)
            {
                UISpecialSevenDaysRow row = UISpecialSevenDaysRow.Create(middle, font, plainMaterial);
                row.name = "Day" + (i + 1);
                row.GetComponent<RectTransform>().anchoredPosition = UISeasonPassUIFactory.Pos(RowCenterX, FirstRowY + i * RowPitch);
                dialog.rows[i] = row;
            }

            BuildBottom(dialog, bottom, font);
            return dialog;
        }

        /// <summary>배너·리본·제목·안내 + "프리미엄 7일 이벤트" 띠.</summary>
        private static void BuildTop(UISpecialSevenDaysDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 배너 — x2, 보이는 y -13~477. 오른쪽 끝을 화면 끝(1082)에 맞춘 자리(시안 픽셀 대조)라 왼쪽 58px 이 빈다.
            UISeasonPassUIFactory.Solid(p, "BannerFill", BannerFillColor, 0f, -300f, 1080f, 478f);
            UISeasonPassUIFactory.Picture(p, "Banner", "7DaysEvent/7DaysEvent_Baneer",
                new Vector2(1024f, 1024f), UISeasonPassUIFactory.Pos(570f, 233f));

            // 띠 — 검정 외곽선 5 · 노란 줄 5 · 갈색(PSD 사각형 27).
            UISeasonPassUIFactory.Solid(p, "BandOutline", Color.black, 0f, 478f, 1080f, 570f);
            UISeasonPassUIFactory.Solid(p, "BandLine", UISeasonPassUIFactory.Hex(0xfecf0d), 0f, 483f, 1080f, 565f);
            UISeasonPassUIFactory.Solid(p, "Band", UISeasonPassUIFactory.Hex(0xa94800), 0f, 488f, 1080f, 560f);

            // 띠 위 제목 판 — Pass_premium_Btn x3, 보이는 701x102. 여백(좌우 12, 위아래 47) x3.
            Image header = UISeasonPassUIFactory.Sliced(p, "HeaderPlate", "Pass/Pass_premium_Btn", 3f,
                new Vector2(773f, 384f), UISeasonPassUIFactory.Pos(545.5f, 483f), Color.white);
            header.raycastTarget = false;
            TMP_Text headerText = UISeasonPassUIFactory.CreateText("Label", header.transform, "프리미엄 7일 이벤트", 45f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(headerText.rectTransform, new Vector2(600f, 60f), new Vector2(19.5f, 0.5f));

            // 리본 — x2, 보이는 572x188
            UISeasonPassUIFactory.Picture(p, "Ribbon", "7DaysEvent/7DaysEvent_Ribon",
                new Vector2(1024f, 1024f), UISeasonPassUIFactory.Pos(300f, 112f));

            TMP_Text title = UISeasonPassUIFactory.CreateText("Title", p,
                "특별한 " + Colored("7일!", PointColor), 70f, TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(480f, 90f), UISeasonPassUIFactory.Pos(299.5f, 116.5f));

            TMP_Text headline = UISeasonPassUIFactory.CreateText("Headline", p,
                Colored("7일", PointColor) + " 접속하고 " + Colored("풍성한 보상", PointColor) + " 받자!", 50f,
                TextAlignmentOptions.Left, Color.white, font);
            headline.textWrappingMode = TextWrappingModes.NoWrap;
            UISeasonPassUIFactory.SetRect(headline.rectTransform, new Vector2(700f, 64f), UISeasonPassUIFactory.Pos(33f + 350f, 231.5f));

            dialog.infoText = UISeasonPassUIFactory.CreateFittedText("Info", p, InfoBeforePurchase, 38f, 26f,
                TextAlignmentOptions.Left, InfoColor, font);
            UISeasonPassUIFactory.SetRect(dialog.infoText.rectTransform, new Vector2(700f, 50f), UISeasonPassUIFactory.Pos(36f + 350f, 290f));
        }

        /// <summary>캐시백 문구 · 구입 버튼 · 뒤로.</summary>
        private static void BuildBottom(UISpecialSevenDaysDialog dialog, Transform p, TMP_FontAsset font)
        {
            // 반짝이 — 7DaysEvent_Point x0.7 (PSD 보이는 33x36)
            UISeasonPassUIFactory.Picture(p, "SparkleLeft", "7DaysEvent/7DaysEvent_Point",
                new Vector2(45f, 45f), UISeasonPassUIFactory.Pos(265.5f, 1696f));
            UISeasonPassUIFactory.Picture(p, "SparkleRight", "7DaysEvent/7DaysEvent_Point",
                new Vector2(45f, 45f), UISeasonPassUIFactory.Pos(864.5f, 1700f));

            TMP_Text cashback = UISeasonPassUIFactory.CreateText("Cashback", p, "패키지 구입 비용 100% 캐시백!", 40f,
                TextAlignmentOptions.Center, CashbackColor, font);
            cashback.textWrappingMode = TextWrappingModes.NoWrap;
            UISeasonPassUIFactory.SetRect(cashback.rectTransform, new Vector2(560f, 50f), UISeasonPassUIFactory.Pos(561f, 1697.5f));

            // 구입 — Big_Btn_Yellow x4, 보이는 647x169. 여백 x4 → +32, 그림이 8 아래로 치우쳐 위로 8.
            Image buy = UISeasonPassUIFactory.Sliced(p, "BuyButton", "Ingame/Big_Btn_Yellow", 4f,
                new Vector2(679f, 201f), UISeasonPassUIFactory.Pos(561.5f, 1816.5f), Color.white);
            dialog.buyButton = UISeasonPassUIFactory.SpriteButton(buy);
            dialog.buyText = UISeasonPassUIFactory.CreateFittedText("Label", buy.transform, "8,000원", 70f, 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.buyText.rectTransform, new Vector2(580f, 96f), new Vector2(2.5f, -5.5f));

            // 뒤로 — Btn_Gray x5, 보이는 164x169. 아이콘 Icon_Back x4.
            Image back = UISeasonPassUIFactory.Sliced(p, "SevenDaysBackButton", "Ingame/Btn_Gray", 5f,
                new Vector2(204f, 209f), UISeasonPassUIFactory.Pos(96f, 1815.5f), Color.white);
            dialog.AddExitButton(UISeasonPassUIFactory.SpriteButton(back));
            UISeasonPassUIFactory.Picture(back.transform, "Icon", "Main/Icon_Back",
                new Vector2(128f, 128f), new Vector2(2f, -1.5f));
        }

        /// <summary>
        /// 1080x1920 판을 화면 위·가운데·아래(<paramref name="anchorY"/> 1·0.5·0)에 붙인다.
        /// 판 안은 <c>Pos()</c> 좌표 그대로 — 1920 화면에서는 세 판이 겹쳐 PSD 와 같고, 긴 화면에서만 벌어진다.
        /// </summary>
        private static Transform Group(string name, Transform parent, float anchorY)
        {
            GameObject group = UISeasonPassUIFactory.CreateRect(name, parent);
            var rect = group.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, anchorY);
            rect.anchorMax = new Vector2(0.5f, anchorY);
            rect.pivot = new Vector2(0.5f, anchorY);
            rect.sizeDelta = new Vector2(1080f, 1920f);
            rect.anchoredPosition = Vector2.zero;
            return group.transform;
        }

        private static string Colored(string text, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";
        }
    }
}
