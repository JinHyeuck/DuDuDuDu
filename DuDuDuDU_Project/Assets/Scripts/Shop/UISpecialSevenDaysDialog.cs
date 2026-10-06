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
    /// <b>1열 7줄이다</b>(사용자 지정 2026-10-06). 레퍼런스(출석 리턴 패키지)의 줄 구성 —
    /// "출석 n일차 · 접속하기 · 보상 · 보상 받기" — 을 2열 대신 한 줄씩 쌓는다. 기획서 4.1 의
    /// "7칸 1줄" 안을 대체하므로 4.2 의 "칸 안에 이름을 넣지 않는다 · 탭 툴팁" 도 필요가 없어졌다
    /// (줄에 이름이 다 들어간다).
    ///
    /// <b>미구매 상태에서도 쌓인 보상을 보여 준다</b>(4.3) — 지난 날의 줄은 회색 버튼 + 자물쇠로,
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
                    : "안 사도 출석은 쌓이고, 사는 순간 쌓인 보상을 한 번에 받습니다");
            }

            // 하단 버튼 — 사기 전엔 구입, 산 뒤엔 받을 것이 있을 때만 "모두 받기".
            bool showBuy = manager.CanPurchase || (manager.Purchased && claimable > 0);
            if (buyButton != null)
                buyButton.gameObject.SetActive(showBuy);

            if (buyText != null)
            {
                buyText.SetText(manager.Purchased
                    ? "모두 받기"
                    : manager.PriceWon.ToString("N0", CultureInfo.InvariantCulture) + "원에 구입하기");
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
        // PSD 가 없다. 레퍼런스(출석 리턴 패키지) 구성을 멤버십·시즌 패스와 같은 그림·색으로 옮겼다.
        // 좌표는 1080x1920 화면 픽셀(좌상단 원점). 기록: Docs/SpecialSevenDaysUI.md

        /// <summary>첫 줄 중심(y)과 줄 간격. 7줄이 y 402~1574 에 든다.</summary>
        private const float FirstRowY = 480f;
        private const float RowPitch = 168f;

        public static UISpecialSevenDaysDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISpecialSevenDaysDialog", parent);
            UISeasonPassUIFactory.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISpecialSevenDaysDialog>();
            dialog.BakeOpenStyle(DialogOpenStyle.Page);

            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;
            Transform p = view.transform;

            // 바탕 — 멤버십 창과 같은 색·무늬. 클릭을 먹어 뒤의 로비가 안 눌리게 한다.
            Image background = UISeasonPassUIFactory.CreateImage("Background", p, UISeasonPassUIFactory.FreeTrackColor);
            UISeasonPassUIFactory.Stretch(background.rectTransform);
            foreach (Vector2 c in new[] { new Vector2(270f, 480f), new Vector2(810f, 480f), new Vector2(270f, 1260f), new Vector2(810f, 1260f) })
            {
                Image pattern = UISeasonPassUIFactory.Picture(p, "Pattern", "Pass/Pass_Pattern",
                    new Vector2(1024f, 1024f), UISeasonPassUIFactory.Pos(c.x, c.y));
                pattern.color = UISeasonPassUIFactory.FreePatternTint;
            }

            // 제목 띠
            Image banner = UISeasonPassUIFactory.Sliced(p, "TitleBanner", "Pass/Pass_premium_Btn", 3f,
                new Vector2(952f, 384f), UISeasonPassUIFactory.Pos(540f, 170f), Color.white);
            banner.raycastTarget = false;
            TMP_Text title = UISeasonPassUIFactory.CreateText("Title", banner.transform, "특별한 7일", 60f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(700f, 90f), new Vector2(0f, 2f));

            TMP_Text headline = UISeasonPassUIFactory.CreateFittedText("Headline", p,
                "꾸준히 접속만 해도 패키지 구입 비용 100% 캐시백!", 44f, 30f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.TitleColor, font);
            UISeasonPassUIFactory.SetRect(headline.rectTransform, new Vector2(980f, 60f), UISeasonPassUIFactory.Pos(540f, 272f));

            dialog.infoText = UISeasonPassUIFactory.CreateFittedText("Info", p,
                "안 사도 출석은 쌓이고, 사는 순간 쌓인 보상을 한 번에 받습니다", 32f, 22f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.infoText.rectTransform, new Vector2(980f, 46f), UISeasonPassUIFactory.Pos(540f, 332f));

            for (int i = 0; i < dialog.rows.Length; i++)
            {
                UISpecialSevenDaysRow row = UISpecialSevenDaysRow.Create(p, font);
                row.name = "Day" + (i + 1);
                row.GetComponent<RectTransform>().anchoredPosition = UISeasonPassUIFactory.Pos(540f, FirstRowY + i * RowPitch);
                dialog.rows[i] = row;
            }

            // 구입 — Big_Btn_Green x4, 보이는 620x140. 뒤로 버튼(x 0~190)을 피해 오른쪽으로 민다.
            Image buy = UISeasonPassUIFactory.Sliced(p, "BuyButton", "Ingame/Big_Btn_Green", 4f,
                new Vector2(652f, 172f), UISeasonPassUIFactory.Pos(600f, 1700f), Color.white);
            dialog.buyButton = UISeasonPassUIFactory.SpriteButton(buy);
            dialog.buyText = UISeasonPassUIFactory.CreateFittedText("Label", buy.transform, "8,000원에 구입하기", 56f, 34f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(dialog.buyText.rectTransform, new Vector2(560f, 80f), new Vector2(0f, 8f));

            // 뒤로 — 멤버십·시즌 패스 창과 같은 자리·그림
            Image back = UISeasonPassUIFactory.Sliced(p, "SevenDaysBackButton", "Ingame/Btn_Gray", 5f,
                new Vector2(204f, 209f), UISeasonPassUIFactory.Pos(87f, 1813.5f), Color.white);
            dialog.AddExitButton(UISeasonPassUIFactory.SpriteButton(back));
            UISeasonPassUIFactory.Picture(back.transform, "Icon", "Main/Icon_Back",
                new Vector2(128f, 128f), new Vector2(2f, -1.5f));

            return dialog;
        }
    }
}
