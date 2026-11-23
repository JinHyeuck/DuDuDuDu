using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Mission;
using OJ.Point;

namespace OJ.EventMinigame
{
    /// <summary>
    /// 로비의 이벤트 입구. 지금 열린 이벤트의 이름과 남은 시간을 적고, 눌러 그 이벤트 화면을 연다.
    /// 쓸 재화(낚싯대·곡괭이·보트)가 남아 있으면 빨간 점이 뜬다.
    ///
    /// <b>창을 들고 있지 않는다.</b> <c>UIService</c> 에서 꺼내 띄운다(퀘스트 입구와 같다).
    /// </summary>
    public sealed class UIEventMinigameLobbyButton : MonoBehaviour
    {
        /// <summary>글자를 다시 쓰는 간격(초). 남은 시간은 분 단위라 자주 고칠 이유가 없다.</summary>
        private const float RefreshSeconds = 20f;

        [SerializeField] private Button button;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private GameObject redDot;

        private float nextRefresh;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(Open);

            nextRefresh = 0f;
            Refresh();
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(Open);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh)
                return;

            nextRefresh = Time.unscaledTime + RefreshSeconds;
            Refresh();
        }

        private void Refresh()
        {
            EventMinigameManager manager = EventMinigameManager.Instance;
            if (manager == null)
                return;

            DateTime now = EventMinigameClock.UtcNow;
            EventKind kind = manager.OpenKind(now);

            nameText.text = EventMinigameText.EventName(kind);
            timerText.text = EventMinigameText.Remaining(EventMinigameRules.OccurrenceEndUtc(now) - now);

            int held = PointManager.Instance != null ? PointManager.Instance.Get(EventMinigameRules.CurrencyOf(kind)) : 0;
            redDot.SetActive(held > 0);
        }

        private void Open()
        {
            EventMinigameManager manager = EventMinigameManager.Instance;
            if (manager == null || GameContainer.UI == null)
                return;

            DateTime now = EventMinigameClock.UtcNow;
            UIEventMinigameDialog dialog;
            switch (manager.OpenKind(now))
            {
                case EventKind.Fishing:
                    dialog = GameContainer.UI.Show<UIFishingPage>();
                    break;
                case EventKind.TreasureHunt:
                    dialog = GameContainer.UI.Show<UITreasureHuntPage>();
                    break;
                default:
                    dialog = GameContainer.UI.Show<UIRaftingPage>();
                    break;
            }

            if (dialog == null)
                Debug.LogError("[이벤트] 화면을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것('OJ/개발/다이얼로그 카탈로그/훑어서 갱신').");
        }

        // ── 굽기 ───────────────────────────────────────────────────────

        public static UIEventMinigameLobbyButton Create(Transform parent, TMP_FontAsset font)
        {
            Image root = UIMissionUIFactory.CreateImage("UIEventMinigameLobbyButton", parent, UIMissionUIFactory.TabOnColor);
            UIMissionUIFactory.SetRect(root.rectTransform, new Vector2(150f, 150f), Vector2.zero);

            var entry = root.gameObject.AddComponent<UIEventMinigameLobbyButton>();

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            entry.button = button;

            entry.nameText = UIMissionUIFactory.CreateText(
                "Name", root.transform, "이벤트", 34f, TextAlignmentOptions.Center, UIMissionUIFactory.LightText, font);
            UIMissionUIFactory.SetRect(entry.nameText.rectTransform, new Vector2(150f, 50f), new Vector2(0f, 14f));

            entry.timerText = UIMissionUIFactory.CreateText(
                "Timer", root.transform, string.Empty, 24f, TextAlignmentOptions.Center, UIEventMinigameFactory.MutedText, font);
            UIMissionUIFactory.SetRect(entry.timerText.rectTransform, new Vector2(150f, 36f), new Vector2(0f, -30f));

            Image dot = UIMissionUIFactory.CreateImage("RedDot", root.transform, UIMissionUIFactory.RedDotColor);
            UIMissionUIFactory.SetRect(dot.rectTransform, new Vector2(34f, 34f), new Vector2(60f, 60f));
            dot.raycastTarget = false;
            entry.redDot = dot.gameObject;

            return entry;
        }
    }
}
