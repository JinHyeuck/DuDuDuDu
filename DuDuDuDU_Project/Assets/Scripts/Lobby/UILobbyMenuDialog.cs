using System;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bag;
using OJ.ContentUnlock;
using OJ.Core;
using OJ.DI;
using OJ.Mission;
using OJ.UI;

namespace OJ.Lobby
{
    /// <summary>
    /// 로비 왼쪽 위 메뉴 버튼이 여는 옆 메뉴. 아이템·우편·퀘스트·설정 네 줄이다.
    ///
    /// 우편과 설정은 아직 시스템이 없다. 줄은 시안대로 두고 누르면 "준비 중" 알림만 띄운다 —
    /// 줄을 빼면 시안과 달라지고, 눌러도 아무 일이 없으면 고장으로 읽힌다.
    ///
    /// 줄을 누르면 메뉴를 먼저 닫고 대상 창을 연다. 메뉴가 열린 채로 위에 창을 쌓으면 백키가 두 번 필요하다.
    /// </summary>
    public sealed class UILobbyMenuDialog : DialogBase
    {
        [SerializeField] private Button itemButton;
        [SerializeField] private Button mailButton;
        [SerializeField] private Button questButton;
        [SerializeField] private Button settingButton;
        [SerializeField] private GameObject mailDot;
        [SerializeField] private GameObject questDot;

        protected override void OnLoad()
        {
            UseBackBtn = true;

            Bind(itemButton, OpenItems);
            Bind(mailButton, () => Notice("우편"));
            Bind(questButton, OpenQuest);
            Bind(settingButton, () => Notice("설정"));
        }

        protected override void OnEnter()
        {
            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnClaimableChanged += RefreshDots;

            RefreshDots();
        }

        protected override void OnExit()
        {
            MissionManager missions = MissionManager.Instance;
            if (missions != null)
                missions.OnClaimableChanged -= RefreshDots;
        }

        /// <summary>메뉴 안에 새 소식이 있는가. 로비 메뉴 버튼의 빨간 점이 본다.</summary>
        public static bool HasNews()
        {
            MissionManager missions = MissionManager.Instance;
            return IsQuestOpen() && missions != null && missions.HasClaimable();
        }

        private static void Bind(Button button, Action action)
        {
            if (button != null)
                button.onClick.AddListener(() => action());
        }

        private void RefreshDots()
        {
            // 우편 시스템이 없어 우편 점은 늘 꺼 둔다.
            if (mailDot != null)
                mailDot.SetActive(false);

            if (questDot != null)
                questDot.SetActive(HasNews());
        }

        private void OpenItems()
        {
            Exit();
            UIItemDialog dialog = GameContainer.UI?.Show<UIItemDialog>();
            if (dialog == null)
                Debug.LogError("[메뉴] 가방을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        private void OpenQuest()
        {
            if (!IsQuestOpen())
            {
                ExplainQuestLocked();
                return;
            }

            Exit();
            UIQuestDialog dialog = GameContainer.UI?.Show<UIQuestDialog>();
            if (dialog == null)
                Debug.LogError("[메뉴] 퀘스트 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
        }

        private static bool IsQuestOpen()
        {
            LobbyContentGate gate = LobbyContentGate.Current;
            if (gate != null)
                return gate.IsAccessible(ContentId.Quest);

            ContentUnlockManager manager = ContentUnlockManager.Instance;
            return manager == null || manager.IsVisible(ContentId.Quest);
        }

        private static void ExplainQuestLocked()
        {
            ContentUnlockDefinition definition = ContentUnlockManager.Instance?.GetDefinition(ContentId.Quest);
            string message = definition != null && definition.requiredClear > 0
                ? definition.requiredClear + "스테이지를 클리어하면 열려요."
                : "곧 열려요.";

            Alert("퀘스트", message);
        }

        private static void Notice(string title)
        {
            Alert(title, "준비 중이에요.");
        }

        private static void Alert(string title, string message)
        {
            UIConfirmDialog dialog = GameContainer.UI?.Get<UIConfirmDialog>();
            if (dialog != null)
                dialog.Open(title, message, "확인", null, null);
        }
    }
}
