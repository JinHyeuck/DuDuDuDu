using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.Tower
{
    /// <summary>
    /// 로비의 무한의 탑 입구. 기획서 5.1 의 첫 칸("로비 — 탑 입장")이다.
    ///
    /// <b>왜 씬에 놓는 컴포넌트인가.</b> 다른 탑 UI 셋은 팝업이라 카탈로그에서
    /// 필요할 때 만들어지지만, 입구는 <b>로비 화면에 상주해야</b> 눈에 띈다.
    /// 씬에 넣는 일은 에디터 도구(<c>TowerLobbyEntryInstaller</c>)가 하고,
    /// 이 파일은 그 오브젝트가 하는 일만 갖는다 —
    /// <c>UICombatPowerDisplay</c> 와 같은 구조다.
    ///
    /// <b>로비가 이것을 몰라도 된다.</b> <c>LobbyLayoutController</c> 에 필드를 늘리면
    /// 씬의 직렬화 데이터를 고쳐야 하고(절대 규칙 3), 로비가 탑을 알게 된다.
    /// 이 컴포넌트는 스스로 붙고 스스로 연다.
    /// </summary>
    public class UITowerEntryButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private GameObject newBadge;

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
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress != null)
                progress.OnProgressChanged += Refresh;

            Refresh();
            TryOpenPendingLoadout();
        }

        private void OnDisable()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress != null)
                progress.OnProgressChanged -= Refresh;
        }

        private void Refresh()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;

            if (titleText != null)
                titleText.SetText("무한의 탑");

            if (progressText != null)
            {
                progressText.SetText(progress == null || !progress.HasAnyClear
                    ? "1층부터 도전"
                    : progress.HighestUnlockedFloor + "층 도전 중");
            }

            if (newBadge != null)
            {
                // 아직 한 층도 못 깬 사람에게만 NEW 를 띄운다. 새 콘텐츠를 한 번은
                // 눌러 보게 하는 것이 목적이고, 그 뒤로는 진행도 자체가 안내가 된다.
                newBadge.SetActive(progress == null || !progress.HasAnyClear);
            }
        }

        private void OnClick()
        {
            UITowerFloorSelectDialog dialog = GameContainer.UI?.Get<UITowerFloorSelectDialog>();
            if (dialog == null)
            {
                Debug.LogError("[탑] 층 선택 창을 열지 못했다. " +
                               "OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌렸는지 볼 것.");
                return;
            }

            dialog.Enter();
        }

        /// <summary>
        /// 결과 화면이 "편성 변경" 으로 나왔으면 그 층의 편성 화면을 바로 연다.
        /// 기획서 7.2 의 순환(재도전 → 편성 개선 → 재도전)에서 로비를 <b>지나가는</b>
        /// 화면으로 만드는 자리다 — 여기서 탭을 두 번 더 하게 하면 그 순환이 끊긴다.
        /// </summary>
        private void TryOpenPendingLoadout()
        {
            TowerProgressManager progress = TowerProgressManager.Instance;
            if (progress == null)
                return;

            int floor = progress.ConsumePendingLobbyFloor();
            if (floor <= 0)
                return;

            UITowerLoadoutDialog loadout = GameContainer.UI?.Get<UITowerLoadoutDialog>();
            if (loadout == null)
            {
                Debug.LogError("[탑] 편성 창을 열지 못했다. 카탈로그에 UITowerLoadoutDialog 가 있는지 볼 것.");
                return;
            }

            loadout.Open(floor);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 설치 전용. 값을 아는 것은 코드이므로 인스펙터에 좌표를 옮겨 적지 않는다.
        // ──────────────────────────────────────────────────────────────

        internal const float Width = 300f;
        internal const float Height = 132f;

        /// <summary>에디터 설치 전용. <c>TowerLobbyEntryInstaller</c> 가 부른다.</summary>
        public static UITowerEntryButton Create(Transform parent, TMP_FontAsset font)
        {
            Image background = UITowerUIFactory.CreateImage("UITowerEntryButton", parent, UITowerUIFactory.AccentSoft);
            UITowerUIFactory.SetRect(background.rectTransform, new Vector2(Width, Height), Vector2.zero);

            var entry = background.gameObject.AddComponent<UITowerEntryButton>();
            entry.button = background.gameObject.AddComponent<Button>();
            entry.button.targetGraphic = background;

            entry.titleText = UITowerUIFactory.CreateText("Title", background.transform, "무한의 탑", 32f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(entry.titleText.rectTransform,
                new Vector2(Width - 16f, 44f), new Vector2(0f, 20f));

            entry.progressText = UITowerUIFactory.CreateText("Progress", background.transform, "1층부터 도전", 24f,
                TextAlignmentOptions.Center, UITowerUIFactory.GoldText, font);
            UITowerUIFactory.SetRect(entry.progressText.rectTransform,
                new Vector2(Width - 16f, 34f), new Vector2(0f, -22f));

            Image badge = UITowerUIFactory.CreateImage("NewBadge", background.transform,
                new Color(0.95f, 0.35f, 0.42f, 1f));
            UITowerUIFactory.SetRect(badge.rectTransform,
                new Vector2(56f, 28f), new Vector2(Width * 0.5f - 34f, Height * 0.5f - 18f));
            badge.raycastTarget = false;
            entry.newBadge = badge.gameObject;

            TMP_Text badgeText = UITowerUIFactory.CreateText("Text", badge.transform, "NEW", 18f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(badgeText.rectTransform, new Vector2(56f, 28f), Vector2.zero);

            return entry;
        }
    }
}
