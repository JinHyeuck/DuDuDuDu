using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;
using OJ.Tower;

namespace OJ.Pinball
{
    /// <summary>
    /// 로비의 핀볼 입구.
    ///
    /// <b>왜 씬에 놓는 컴포넌트인가.</b> 핀볼 화면은 필요할 때 카탈로그에서 만들어지지만,
    /// 입구는 <b>로비 화면에 상주해야</b> 눈에 띈다. 씬에 넣는 일은 에디터 도구
    /// (<c>PinballLobbyEntryInstaller</c>)가 하고, 이 파일은 그 오브젝트가 하는 일만 갖는다 —
    /// <c>UITowerEntryButton</c> 과 같은 구조다.
    ///
    /// <b>로비가 이것을 몰라도 된다.</b> <c>LobbyLayoutController</c> 에 필드를 늘리면
    /// 씬의 직렬화 데이터를 고쳐야 하고(AGENTS.md 절대 규칙 3), 로비가 핀볼을 알게 된다.
    /// 이 컴포넌트는 스스로 붙고 스스로 연다.
    /// </summary>
    public class UIPinballEntryButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text ticketText;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }

        private void OnEnable()
        {
            PointManager points = PointManager.Instance;
            if (points != null)
                points.OnPointChanged += HandlePointChanged;

            Refresh();
        }

        private void OnDisable()
        {
            PointManager points = PointManager.Instance;
            if (points != null)
                points.OnPointChanged -= HandlePointChanged;
        }

        private void HandlePointChanged(PointType pointType, int amount)
        {
            if (pointType == PointType.PinballTicket)
                Refresh();
        }

        private void Refresh()
        {
            if (titleText != null)
                titleText.SetText("핀볼");

            if (ticketText == null)
                return;

            PointManager points = PointManager.Instance;
            int tickets = points != null ? points.Get(PointType.PinballTicket) : 0;

            // 티켓이 없어도 버튼을 잠그지 않는다. 컨텐츠가 무엇인지 보여 주는 것이
            // 입구의 일이고, 티켓이 모자란다는 사실은 화면 안에서 말한다.
            ticketText.SetText(tickets > 0 ? "티켓 " + tickets + "장" : "티켓 없음");
        }

        private void OnClick()
        {
            UIPinballPage page = GameContainer.UI?.Get<UIPinballPage>();
            if (page == null)
            {
                Debug.LogError("[핀볼] 화면을 열지 못했다. " +
                               "OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌렸는지 볼 것.");
                return;
            }

            page.Enter();
        }

        internal const float Width = 300f;
        internal const float Height = 132f;

        /// <summary>
        /// 에디터 설치 전용. <c>PinballLobbyEntryInstaller</c> 가 부른다.
        ///
        /// <b>탑의 UI 팩토리를 그대로 쓴다.</b> 이름이 <c>UITowerUIFactory</c> 라 어색하지만,
        /// 같은 일을 하는 팩토리를 하나 더 만들면 로비 버튼의 생김새 정본이 둘이 된다.
        /// 팩토리 이름을 중립적으로 바꾸는 것은 이 작업이 아니라 그 다음 작업이다.
        /// </summary>
        public static UIPinballEntryButton Create(Transform parent, TMP_FontAsset font)
        {
            Image background = UITowerUIFactory.CreateImage(
                "UIPinballEntryButton", parent, UITowerUIFactory.AccentSoft);
            UITowerUIFactory.SetRect(background.rectTransform, new Vector2(Width, Height), Vector2.zero);

            var entry = background.gameObject.AddComponent<UIPinballEntryButton>();
            entry.button = background.gameObject.AddComponent<Button>();
            entry.button.targetGraphic = background;

            entry.titleText = UITowerUIFactory.CreateText("Title", background.transform, "핀볼", 32f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(entry.titleText.rectTransform,
                new Vector2(Width - 16f, 44f), new Vector2(0f, 20f));

            entry.ticketText = UITowerUIFactory.CreateText("Ticket", background.transform, "티켓 없음", 24f,
                TextAlignmentOptions.Center, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(entry.ticketText.rectTransform,
                new Vector2(Width - 16f, 34f), new Vector2(0f, -22f));

            return entry;
        }
    }
}
