using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 로비의 시즌 패스 입구. 받을 것이 있으면 빨간 점이 뜬다.
    ///
    /// <b>창을 들고 있지 않는다.</b> <c>UIService</c> 에서 꺼내 띄운다 —
    /// <c>[SerializeField]</c> 로 씬 인스턴스를 가리키면 그 참조가 <c>None</c> 이 됐을 때
    /// <b>아무 로그 없이</b> 창이 안 열린다.
    /// </summary>
    public sealed class UISeasonPassLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text summaryText;
        [SerializeField] private GameObject redDot;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(Open);

            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass != null)
                pass.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);

            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass != null)
                pass.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            SeasonPassManager pass = SeasonPassManager.Instance;
            if (pass == null)
                return;

            if (summaryText != null)
                summaryText.SetText(SeasonPassText.LobbySummary(pass.Level, pass.TimeUntilSeasonEnd));

            if (redDot != null)
                redDot.SetActive(pass.HasClaimable());
        }

        private void Open()
        {
            UISeasonPassDialog dialog = GameContainer.UI?.Show<UISeasonPassDialog>();
            if (dialog == null)
                Debug.LogError("[시즌패스] 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UISeasonPassLobbyButton Create(Transform parent, TMP_FontAsset font)
        {
            Image root = UISeasonPassUIFactory.CreateImage(
                "UISeasonPassLobbyButton", parent, UISeasonPassUIFactory.HeaderColor);
            UISeasonPassUIFactory.SetRect(root.rectTransform, new Vector2(460f, 130f), Vector2.zero);

            var entry = root.gameObject.AddComponent<UISeasonPassLobbyButton>();

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            entry.button = button;

            TMP_Text title = UISeasonPassUIFactory.CreateText(
                "Title", root.transform, "시즌 패스", 40f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.LightText, font);
            UISeasonPassUIFactory.SetRect(title.rectTransform, new Vector2(440f, 50f), new Vector2(0f, 24f));

            entry.summaryText = UISeasonPassUIFactory.CreateText(
                "Summary", root.transform, "Lv.1", 28f,
                TextAlignmentOptions.Center, UISeasonPassUIFactory.MutedText, font);
            UISeasonPassUIFactory.SetRect(
                entry.summaryText.rectTransform, new Vector2(440f, 40f), new Vector2(0f, -28f));

            Image dot = UISeasonPassUIFactory.CreateImage(
                "RedDot", root.transform, new Color(0.92f, 0.22f, 0.22f, 1f));
            UISeasonPassUIFactory.SetRect(dot.rectTransform, new Vector2(30f, 30f), new Vector2(212f, 48f));
            dot.raycastTarget = false;
            entry.redDot = dot.gameObject;

            return entry;
        }
    }
}
