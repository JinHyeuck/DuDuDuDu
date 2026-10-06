using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Save;
using OJ.SeasonPass;

namespace OJ.Shop
{
    /// <summary>
    /// 로비 좌상단의 멤버십 입구. (기획서 ShopPackageDesign 2.2 — 버튼 3번)
    ///
    /// <b>배지</b>: 고기 멤버십 만료 3일 전부터 <c>D-n</c>. <b>숨김</b>: 두 상품을 모두 갖고 있고
    /// 아직 재구매 구간이 아닐 때 — 살 것이 없는 입구는 자리만 먹는다.
    ///
    /// 숨길 때 루트가 아니라 <see cref="view"/> 를 끈다. 루트를 끄면 <c>OnDisable</c> 로 구독이 풀려
    /// 다시 살 수 있게 되는 날(만료 3일 전) 버튼이 돌아오지 못한다.
    /// </summary>
    public sealed class UIMembershipLobbyButton : MonoBehaviour
    {
        [SerializeField] private GameObject view;
        [SerializeField] private Button button;
        [SerializeField] private GameObject badge;
        [SerializeField] private TMP_Text badgeText;

        /// <summary>D-n 을 다시 계산하는 간격(초). 날짜 단위라 자주 볼 이유가 없다.</summary>
        private const float RefreshSeconds = 60f;

        private float nextRefresh;

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(Open);

            if (EntitlementManager.Instance != null)
                EntitlementManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);

            if (EntitlementManager.Instance != null)
                EntitlementManager.Instance.OnChanged -= Refresh;
        }

        private void Update()
        {
            // 로비를 켜 둔 채 만료가 다가오거나 지나가도 배지와 숨김이 따라오게 한다.
            if (Time.unscaledTime < nextRefresh)
                return;

            Refresh();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            EntitlementManager entitlements = EntitlementManager.Instance;
            if (entitlements == null)
                return;

            int rebuyWindow = ShopDatabaseProvider.Database.Membership.rebuyWindowDays;
            bool canBuyMeat = entitlements.CanRebuyMeatMembership(rebuyWindow);
            bool hasSomethingToSell = canBuyMeat || !entitlements.AdFree;

            if (view != null)
                view.SetActive(hasSomethingToSell);

            // D-n 은 "가진 멤버십이 곧 끝난다" 일 때만이다. 한 번도 안 산 사람에게 D-0 을 띄우지 않는다.
            bool expiring = entitlements.MeatMembershipActive && canBuyMeat;
            if (badge != null)
                badge.SetActive(expiring);

            if (badgeText != null && expiring)
                badgeText.SetText("D-" + entitlements.MeatMembershipRemainingDays);
        }

        private void Open()
        {
            if (GameContainer.UI?.Show<UIMembershipDialog>() == null)
                Debug.LogError("[멤버십] 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        /// <summary>
        /// 입구 하나를 조립한다. 메뉴(≡) 버튼 아래 왼쪽 가장자리, 보이는 140x140 + 이름 줄.
        /// 아이콘은 주황 보상 칸(Itme_Slot_5) 위에 고기와 왕관 — 시즌 패스 유료 칸과 같은 계열이다.
        /// </summary>
        public static UIMembershipLobbyButton Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UIMembershipLobbyButton", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(160f, 190f), Vector2.zero);
            var entry = root.AddComponent<UIMembershipLobbyButton>();

            entry.view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(entry.view.GetComponent<RectTransform>());
            Transform v = entry.view.transform;

            // 바탕 — Itme_Slot_5 x3.2(보이는 45 → 144). 오른쪽·아래 여백이 1px 넓어 중심이 (1.6,-1.6) 밀린다.
            // 누르는 자리는 보이는 칸만큼.
            Image slot = UISeasonPassUIFactory.Picture(v, "Slot", "ItemSlot/Itme_Slot_5",
                new Vector2(204.8f, 204.8f), new Vector2(1.6f, 21.4f));
            slot.raycastTarget = true;
            slot.raycastPadding = new Vector4(28.8f, 32f, 32f, 28.8f);

            // 버튼은 루트에 단다 — 이름 줄까지 한 덩이로 눌리고, 바탕 그림이 눌림 틴트를 받는다.
            // 숨길 때 view 가 꺼지면 그림이 사라져 레이캐스트도 같이 사라진다.
            entry.button = root.AddComponent<Button>();
            entry.button.targetGraphic = slot;

            UISeasonPassUIFactory.Picture(slot.transform, "Meat", "Gem/Gem_GamePlay", new Vector2(160f, 160f), new Vector2(-1.6f, -4.4f));
            UISeasonPassUIFactory.Picture(slot.transform, "Crown", "Pass/Pass_Crown", new Vector2(72f, 72f), new Vector2(-1.6f, 59.6f));

            TMP_Text label = UISeasonPassUIFactory.CreateText("Label", v, "멤버십", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(170f, 44f), new Vector2(0f, -72f));

            // D-n 배지 — 오른쪽 위 모서리에 걸친 빨간 알약.
            Image badge = UISeasonPassUIFactory.Sliced(v, "Badge", "Upgrade/Ui_Popup_SmallBox", 2f,
                new Vector2(96f, 52f), new Vector2(56f, 88f), UISeasonPassUIFactory.Hex(0xe8323c));
            badge.raycastTarget = false;
            entry.badge = badge.gameObject;
            entry.badgeText = UISeasonPassUIFactory.CreateText("Text", badge.transform, "D-3", 28f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(entry.badgeText.rectTransform, new Vector2(90f, 44f), new Vector2(0f, 1f));
            entry.badge.SetActive(false);

            return entry;
        }
    }
}
