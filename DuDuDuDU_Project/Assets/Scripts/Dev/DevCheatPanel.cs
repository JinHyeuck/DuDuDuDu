#if UNITY_EDITOR || DEV_DEFINE
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OJ.DI;

namespace OJ.Dev
{
    /// <summary>치트창의 탭. 순서가 곧 탭 버튼 순서다.</summary>
    internal enum DevCheatTab
    {
        Currency = 0,
        Gem,
        Dice,
        Battle,
        Progress,
        Time,
        Mode,
    }

    /// <summary>
    /// 개발용 치트창. 예전 <c>PointCheatController</c> 의 <c>OnGUI</c> 창을 uGUI 로 옮긴 것이다.
    ///
    /// <b>왜 바꿨나.</b> IMGUI 창은 한 화면에 모든 항목을 세로로 쌓아서, 재화 하나 넣으려면
    /// 재화 19종 격자와 보석 수십 칸을 지나 스크롤해야 했다. 탭으로 가르면 찾는 것이
    /// 한 화면 안에 들어온다.
    ///
    /// <b>프리팹이 없다.</b> 이 파일 전체가 <c>#if</c> 안이라 릴리스 빌드에는 아무것도 남지
    /// 않아야 하는데, 프리팹으로 두면 그것을 가리키는 참조 때문에 빌드에 딸려 들어가고
    /// 컴포넌트만 사라져 <b>스크립트가 빠진 프리팹</b>이 남는다(Missing script 기준선은 0 이다).
    /// 그래서 <see cref="DevCheatUI"/> 가 런타임에 세운다.
    ///
    /// <b>로비 치트와 전투 치트가 다르다.</b> 전투 매니저는 BattleScene 에만 있어서
    /// 로비에서 누르면 아무 일도 일어나지 않는데, 그건 "고장" 으로 읽힌다. 그래서 전투
    /// 전용 줄은 <b>지우지 않고 흐리게</b> 두고 왜 못 쓰는지 적는다
    /// (<see cref="MarkBattleOnly"/>). 지우면 "치트가 어디 갔지" 하고 찾게 된다.
    /// </summary>
    internal sealed class DevCheatPanel : MonoBehaviour
    {
        private static DevCheatPanel instance;

        private Canvas canvas;
        private GameObject window;
        private RectTransform contentRoot;
        private TMP_Text scopeText;

        private readonly List<Button> tabButtons = new List<Button>();
        private DevCheatTab tab = DevCheatTab.Currency;

        /// <summary>
        /// 탭을 다시 그릴 때 살아남아야 하는 입력값. 창을 닫았다 열어도 유지된다 —
        /// 금액을 매번 다시 치게 하면 그 자체로 불편하다.
        /// </summary>
        internal int Amount = 100000;
        internal int MonsterHp = 1000;
        internal int GemAmount = 10;
        internal int DiceLevel = 12;
        internal int DiceStar = 1;
        internal int TowerFloor = 30;
        internal int CurrencyIndex;
        internal int GemIndex;
        internal int DiceIndex;

        // ── 설치 ───────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance != null)
                return;

            var go = new GameObject(nameof(DevCheatPanel));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevCheatPanel>();
        }

        private void Awake()
        {
            // 치트는 <b>무엇보다도 위</b>다. 페이드(short.MaxValue - 100)보다도 위라야
            // 전환 중에도 눌러 볼 수 있고, 그것이 FadeView 주석이 적어 둔 규약이다.
            //
            // 팝업(UIPopupRoot)은 "씬 최고 정렬 + 1" 로 올라오는데, UIService 가 이 띠를
            // 건너뛰도록 해 두었다(FadeView.SortingOrder 이상은 제외). 안 그러면 팝업이
            // 매번 치트 위로 한 칸 올라타 전투에서 치트가 가려진다.
            //
            // 맨 위(short.MaxValue)가 아니라 그 세 칸 아래다 — 고르는 칸의 펼친 목록과 그 클릭 막이가
            // 치트 창보다 위에 와야 한다(DevCheatDropdown). 페이드(-100)보다는 여전히 위다.
            canvas = DevCheatUI.CreateCanvas("DevCheatCanvas", DevCheatDropdown.ListSortingOrder - 3);
            canvas.transform.SetParent(transform, false);

            BuildWindow(canvas.transform);
            DevCheatButton.Create(canvas.transform);

            window.SetActive(false);
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Escape — 치트가 열려 있으면 게임의 뒤로가기보다 먼저 가져간다(한 번에 둘이 닫히지 않게).
            OJ.Utils.AOSBackBtnManager.EscapeInterceptor = HandleEscape;
        }

        private void Update()
        {
            // 뒤로가기 관리자가 없는 자리(컨테이너가 서기 전 · 테스트 씬)에서도 Escape 로 닫히게 한다.
            // 관리자가 있으면 그쪽이 EscapeInterceptor 로 먼저 부르므로 여기서 또 처리하지 않는다.
            if (OJ.Utils.AOSBackBtnManager.Instance == null && Input.GetKeyUp(KeyCode.Escape))
                HandleEscape();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (OJ.Utils.AOSBackBtnManager.EscapeInterceptor == HandleEscape)
                OJ.Utils.AOSBackBtnManager.EscapeInterceptor = null;

            if (instance == this)
                instance = null;
        }

        /// <summary>
        /// 씬이 바뀌면 다시 그린다. 전투 전용 줄의 흐림 여부가 씬에 달려 있어서,
        /// 열어 둔 채 전투로 들어가면 <b>로비 기준으로 흐려진 화면이 그대로 남는다.</b>
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (window != null && window.activeSelf)
                Rebuild();
        }

        /// <summary>
        /// Escape 한 번. 펼친 고르는 칸이 있으면 그것만 접고, 아니면 창을 닫는다.
        /// 창이 닫혀 있으면 false — 게임의 뒤로가기가 그대로 처리한다.
        /// </summary>
        private static bool HandleEscape()
        {
            if (instance == null || instance.window == null || !instance.window.activeSelf)
                return false;

            if (instance.CollapseDropdowns())
                return true;

            instance.window.SetActive(false);
            return true;
        }

        /// <summary>
        /// 펼친 고르는 칸을 접는다. 접은 것이 있으면 true.
        /// <b>창을 끄기 전에 반드시 부른다</b> — 펼친 목록의 클릭 막이는 창이 아니라 캔버스에 붙어서,
        /// 접지 않고 창만 끄면 보이지 않는 막이가 남아 화면 전체가 안 눌린다.
        /// </summary>
        private bool CollapseDropdowns()
        {
            bool any = false;
            foreach (TMPro.TMP_Dropdown dropdown in window.GetComponentsInChildren<TMPro.TMP_Dropdown>(true))
            {
                if (dropdown.IsExpanded)
                {
                    dropdown.Hide();
                    any = true;
                }
            }

            return any;
        }

        internal static void Toggle()
        {
            if (instance == null || instance.window == null)
                return;

            bool next = !instance.window.activeSelf;
            if (!next)
                instance.CollapseDropdowns();

            instance.window.SetActive(next);

            if (next)
                instance.Rebuild();
        }

        // ── 뼈대 ───────────────────────────────────────────────────────

        private void BuildWindow(Transform parent)
        {
            window = DevCheatUI.CreateRect("Window", parent);
            DevCheatUI.Stretch(window.GetComponent<RectTransform>());

            // 뒤를 막는다. 없으면 치트창 위를 눌렀는데 뒤의 게임 버튼이 눌린다.
            Image backdrop = DevCheatUI.CreateImage("Backdrop", window.transform, DevCheatUI.Backdrop);
            DevCheatUI.Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            Image panel = DevCheatUI.CreateImage("Panel", window.transform, DevCheatUI.PanelColor);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1020f, 1500f);
            panelRect.anchoredPosition = new Vector2(0f, 60f);

            var panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(14, 14, 14, 14);
            panelLayout.spacing = 10f;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            BuildHeader(panel.transform);
            BuildTabBar(panel.transform);

            // 남은 자리를 전부 목록이 먹는다. flexibleHeight 가 없으면 내용이 없을 때
            // 창이 접혀 탭만 남는다.
            GameObject listHolder = DevCheatUI.CreateRect("List", panel.transform);
            var holderElement = listHolder.AddComponent<LayoutElement>();
            holderElement.flexibleHeight = 1f;
            holderElement.minHeight = 600f;

            Image listBackground = listHolder.AddComponent<Image>();
            listBackground.color = DevCheatUI.SectionColor;

            contentRoot = DevCheatUI.CreateScroll(listHolder.transform);
            RectTransform viewport = contentRoot.parent as RectTransform;
            DevCheatUI.Stretch(viewport);
        }

        private void BuildHeader(Transform parent)
        {
            RectTransform row = DevCheatUI.CreateRow(parent);

            TMP_Text title = DevCheatUI.CreateLabel("Title", row, "개발 치트", 40f, DevCheatUI.TextColor);
            var titleElement = title.gameObject.AddComponent<LayoutElement>();
            titleElement.flexibleWidth = 1f;

            scopeText = DevCheatUI.CreateLabel(
                "Scope", row, string.Empty, 30f, DevCheatUI.MutedColor, TextAlignmentOptions.Right);
            var scopeElement = scopeText.gameObject.AddComponent<LayoutElement>();
            scopeElement.flexibleWidth = 1f;

            Button close = DevCheatUI.CreateButton(
                "Close", row, "닫기", DevCheatUI.DangerColor, () => window.SetActive(false));
            var closeElement = close.GetComponent<LayoutElement>();
            closeElement.flexibleWidth = 0f;
            closeElement.preferredWidth = 160f;
        }

        /// <summary>
        /// 탭 버튼. 한 줄에 셋씩 접는다 — 1080 폭에서 일곱을 한 줄에 넣으면 글자가 안 읽힌다.
        /// </summary>
        private void BuildTabBar(Transform parent)
        {
            tabButtons.Clear();

            var tabs = (DevCheatTab[])Enum.GetValues(typeof(DevCheatTab));
            RectTransform row = null;

            for (int i = 0; i < tabs.Length; i++)
            {
                if (i % 3 == 0)
                    row = DevCheatUI.CreateRow(parent, 8f);

                DevCheatTab value = tabs[i];
                Button button = DevCheatUI.CreateButton(
                    value.ToString(), row, TabName(value), DevCheatUI.TabOffColor,
                    () => Select(value), 76f, 30f);

                tabButtons.Add(button);
            }
        }

        private static string TabName(DevCheatTab value)
        {
            switch (value)
            {
                case DevCheatTab.Currency: return "재화";
                case DevCheatTab.Gem: return "보석";
                case DevCheatTab.Dice: return "다이스";
                case DevCheatTab.Battle: return "전투";
                case DevCheatTab.Progress: return "진행";
                case DevCheatTab.Time: return "시간";
                case DevCheatTab.Mode: return "모드";
                default: return value.ToString();
            }
        }

        private void Select(DevCheatTab value)
        {
            tab = value;
            Rebuild();
        }

        // ── 그리기 ─────────────────────────────────────────────────────

        /// <summary>
        /// 내용을 통째로 다시 짓는다. <b>갱신이 아니라 재생성</b>이다 — 치트는 자주 여는
        /// 화면이 아니고, 부분 갱신으로 두면 "값은 바뀌었는데 화면이 그대로" 가 생긴다.
        /// </summary>
        internal void Rebuild()
        {
            for (int i = 0; i < tabButtons.Count; i++)
                DevCheatUI.SetToggleState(tabButtons[i], (DevCheatTab)i == tab);

            if (scopeText != null)
                scopeText.SetText(IsBattle ? "전투 중" : "로비/타이틀");

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);

            DevCheatSections.Build(this, tab, contentRoot);
        }

        // ── 로비 / 전투 ────────────────────────────────────────────────

        /// <summary>
        /// 지금 전투 안인가. <c>IsActive</c> 는 배틀 스코프가 창구를 채웠을 때만 true 라
        /// 씬 이름을 보는 것보다 정확하다 — 씬은 떴는데 매니저가 아직 안 선 순간이 있다.
        /// </summary>
        internal static bool IsBattle => GameContainer.Battle != null && GameContainer.Battle.IsActive;

        /// <summary>
        /// 전투에서만 되는 줄을 흐리게 만들고 이유를 적는다.
        /// <b>버튼을 지우지 않는 이유</b>는 클래스 주석에 적어 두었다.
        /// </summary>
        internal static void MarkBattleOnly(Button button)
        {
            if (button == null || IsBattle)
                return;

            button.interactable = false;

            Image image = button.targetGraphic as Image;
            if (image != null)
                image.color = DevCheatUI.DisabledColor;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.color = DevCheatUI.MutedColor;
                label.SetText(label.text + " (전투에서만)");
            }
        }
    }
}
#endif
