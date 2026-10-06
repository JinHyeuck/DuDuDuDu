using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.SeasonPass;

namespace OJ.Shop
{
    /// <summary>
    /// 로비의 특별한 7일 입구. 멤버십 입구 바로 아래(사용자 지정 2026-10-06). (기획서 ShopPackageDesign 2.2 — 버튼 2번)
    ///
    /// <b>배지</b>: 받을 보상이 있으면 빨간 점.
    /// <b>숨김</b>: 안 샀으면 7일차가 지나면, 샀으면 7칸을 다 받으면 내린다 — 끝난 상품이다.
    /// <b>자동 노출</b>: 로비에 들어올 때 하루 한 번 창을 띄운다(2.3 — 오늘 쌓인 보상이 있을 때).
    /// </summary>
    public sealed class UISpecialSevenDaysLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject view;
        [SerializeField] private GameObject redDot;

        /// <summary>날짜가 바뀌는지 다시 보는 간격(초).</summary>
        private const float RefreshSeconds = 60f;

        private float nextRefresh;

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(Open);

            if (SpecialSevenDaysManager.Instance != null)
                SpecialSevenDaysManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void Start()
        {
            // Start 에서 한다 — 같은 프레임의 다른 Awake/OnEnable 이 UI 를 다 세운 뒤다.
            SpecialSevenDaysManager manager = SpecialSevenDaysManager.Instance;
            if (manager != null && manager.IsEntryVisible && manager.ConsumeAutoOpen())
                Open();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);

            if (SpecialSevenDaysManager.Instance != null)
                SpecialSevenDaysManager.Instance.OnChanged -= Refresh;
        }

        private void Update()
        {
            // 로비를 켜 둔 채 자정을 넘기면 새 줄이 열린다.
            if (Time.unscaledTime < nextRefresh)
                return;

            Refresh();
        }

        private void Refresh()
        {
            nextRefresh = Time.unscaledTime + RefreshSeconds;

            SpecialSevenDaysManager manager = SpecialSevenDaysManager.Instance;
            bool visible = manager != null && manager.IsEntryVisible;

            // 루트를 끄면 Update 가 멈춰 다시 켤 수 없다 — 그림만 끈다.
            if (view != null)
                view.SetActive(visible);

            if (redDot != null)
                redDot.SetActive(visible && manager.HasClaimable);
        }

        private void Open()
        {
            if (GameContainer.UI?.Show<UISpecialSevenDaysDialog>() == null)
                Debug.LogError("[특별한 7일] 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        /// <summary>
        /// 입구 하나를 조립한다. 멤버십 입구와 같은 크기(160x190) — 보이는 140x140 + 이름 줄.
        /// 아이콘은 초록 보상 칸(Itme_Slot_1) 위에 유료젬 — 이 상품이 돌려주는 것이 젬이다.
        /// </summary>
        public static UISpecialSevenDaysLobbyButton Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UISeasonPassUIFactory.CreateRect("UISpecialSevenDaysLobbyButton", parent);
            UISeasonPassUIFactory.SetRect(root.GetComponent<RectTransform>(), new Vector2(160f, 190f), Vector2.zero);
            var entry = root.AddComponent<UISpecialSevenDaysLobbyButton>();

            GameObject view = UISeasonPassUIFactory.CreateRect("View", root.transform);
            UISeasonPassUIFactory.Stretch(view.GetComponent<RectTransform>());
            entry.view = view;
            Transform v = view.transform;

            Image slot = UISeasonPassUIFactory.Picture(v, "Slot", "ItemSlot/Itme_Slot_1",
                new Vector2(204.8f, 204.8f), new Vector2(1.6f, 21.4f));
            slot.raycastTarget = true;
            slot.raycastPadding = new Vector4(28.8f, 32f, 32f, 28.8f);

            // 버튼을 그림에 단다 — 루트에 달면 숨긴 뒤에도 빈 자리가 눌린다.
            entry.button = slot.gameObject.AddComponent<Button>();
            entry.button.targetGraphic = slot;

            UISeasonPassUIFactory.Picture(slot.transform, "Gem", "Gem/Gem_Diamond", new Vector2(112f, 112f), new Vector2(-1.6f, 10f));

            TMP_Text days = UISeasonPassUIFactory.CreateText("Days", slot.transform, "7일", 34f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(days.rectTransform, new Vector2(120f, 44f), new Vector2(-1.6f, -48f));

            TMP_Text label = UISeasonPassUIFactory.CreateText("Label", v, "특별한 7일", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UISeasonPassUIFactory.SetRect(label.rectTransform, new Vector2(190f, 44f), new Vector2(0f, -72f));

            // 빨간 점 — 오른쪽 위 모서리
            Image dot = UISeasonPassUIFactory.Sliced(v, "RedDot", "Upgrade/Ui_Popup_SmallBox", 2f,
                new Vector2(48f, 48f), new Vector2(64f, 88f), UISeasonPassUIFactory.Hex(0xe8323c));
            dot.raycastTarget = false;
            entry.redDot = dot.gameObject;
            entry.redDot.SetActive(false);

            return entry;
        }
    }
}
