using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.Mission
{
    /// <summary>
    /// 로비 우측 상단의 퀘스트 입구. 받을 것이 있으면 빨간 점이 뜬다.
    ///
    /// <b>창을 들고 있지 않는다.</b> <c>UIService</c> 에서 꺼내 띄운다 — 예전처럼
    /// <c>[SerializeField]</c> 로 씬 인스턴스를 가리키면 그 참조가 <c>None</c> 이 됐을 때
    /// <b>아무 로그 없이</b> 창이 안 열린다.
    ///
    /// <b>빨간 점은 <c>OnClaimableChanged</c> 만 본다.</b> <c>OnChanged</c> 를 구독하면
    /// 전투 중 적 처치마다 다시 그리게 되는데, 이 버튼이 알아야 하는 것은 개수가 아니라
    /// 0 인지 아닌지뿐이다.
    /// </summary>
    public sealed class UIQuestLobbyButton : MonoBehaviour
    {
        [SerializeField] private Button button;
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

            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnClaimableChanged += RefreshDot;

            // 켜질 때 한 번 직접 본다. 전투에서 쌓인 카운트는 이벤트를 쏘지 않고
            // (구독자가 없었으므로) 돌아왔을 때 이 줄이 유일한 갱신 계기다.
            RefreshDot();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);

            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnClaimableChanged -= RefreshDot;
        }

        private void RefreshDot()
        {
            if (redDot == null)
                return;

            MissionManager missions = MissionManager.Instance;
            redDot.SetActive(missions != null && missions.HasClaimable());
        }

        private void Open()
        {
            UIQuestDialog dialog = GameContainer.UI?.Show<UIQuestDialog>();
            if (dialog == null)
                Debug.LogError("[퀘스트] 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UIQuestLobbyButton Create(Transform parent, TMP_FontAsset font)
        {
            Image root = UIMissionUIFactory.CreateImage(
                "UIQuestLobbyButton", parent, UIMissionUIFactory.TabOnColor);
            UIMissionUIFactory.SetRect(root.rectTransform, new Vector2(150f, 150f), Vector2.zero);

            var entry = root.gameObject.AddComponent<UIQuestLobbyButton>();

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            entry.button = button;

            TMP_Text label = UIMissionUIFactory.CreateText(
                "Label", root.transform, "퀘스트", 34f,
                TextAlignmentOptions.Center, UIMissionUIFactory.LightText, font);
            UIMissionUIFactory.SetRect(label.rectTransform, new Vector2(150f, 50f), new Vector2(0f, -48f));

            Image dot = UIMissionUIFactory.CreateImage("RedDot", root.transform, UIMissionUIFactory.RedDotColor);
            UIMissionUIFactory.SetRect(dot.rectTransform, new Vector2(34f, 34f), new Vector2(60f, 60f));
            dot.raycastTarget = false;
            entry.redDot = dot.gameObject;

            return entry;
        }
    }
}
