using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.IdleReward;
using OJ.Lobby;
using OJ.Mission;
using OJ.Pinball;
using OJ.SeasonPass;
using OJ.Shop;
using OJ.Stage;
using OJ.StageReward;
using OJ.StageStar;
using OJ.Tower;

namespace OJ.ContentUnlock
{
    /// <summary>
    /// 로비 입구를 해금 상태에 맞춰 숨기거나 잠근다. (Docs/ContentUnlock.md)
    ///
    /// <b>입구들은 이것을 모른다.</b> 입구마다 판정 줄을 넣으면 열한 군데가 해금 규칙을 알게 되고,
    /// 새 입구를 만들 때마다 빠뜨리는 쪽이 기본값이 된다. 대신 컨텐츠 → 입구 대응을
    /// <see cref="EntryTypes"/> 한 표에 두고, 씬에서 그 컴포넌트를 찾아 <b>그 오브젝트를 통째로</b> 켜고 끈다
    /// (입구 컴포넌트는 전부 자기 입구의 루트에 붙어 있다 — 2026-10-13 LobbyScene 확인).
    ///
    /// <b>하단 탭은 숨기지 않고 잠근다.</b> 탭이 빠지면 나머지 탭 너비가 바뀌어 하단 바가 출렁인다.
    ///
    /// <b>씬에 놓지 않는다.</b> <c>LobbySceneController.Awake</c> 가 코드로 붙인다 — 씬 YAML 을
    /// 고치지 않기 위해서다(AGENTS 절대 규칙 3). 거기서 숨기므로 숨은 입구는 <c>Start</c> 가 안 돈다
    /// (특별한 7일의 자동 팝업이 그렇게 막힌다).
    /// </summary>
    public sealed class LobbyContentGate : MonoBehaviour
    {
        /// <summary>지금 로비의 문지기. 로비가 아니면 null — 그때 탭은 전부 열린 것으로 본다.</summary>
        public static LobbyContentGate Current { get; private set; }

        /// <summary>컨텐츠 → 로비 입구 컴포넌트. 탭으로 들어가는 것은 <see cref="TabOf"/> 에 있다.</summary>
        private static readonly Dictionary<ContentId, Type> EntryTypes = new Dictionary<ContentId, Type>
        {
            { ContentId.StageReward, typeof(UIStageRewardLobbyButton) },
            { ContentId.IdleReward, typeof(UIIdleRewardLobbyButton) },
            { ContentId.StageStar, typeof(UIStageStarLobbyButton) },
            { ContentId.MeatFestival, typeof(UIMeatFestivalLobbyButton) },
            { ContentId.Sweep, typeof(UISweepLobbyButton) },
            { ContentId.SpecialSevenDays, typeof(UISpecialSevenDaysLobbyButton) },
            { ContentId.Quest, typeof(UIQuestLobbyButton) },
            { ContentId.Pinball, typeof(UIPinballEntryButton) },
            { ContentId.Membership, typeof(UIMembershipLobbyButton) },
            { ContentId.SeasonPass, typeof(UISeasonPassLobbyButton) },
            { ContentId.Tower, typeof(UITowerEntryButton) },
        };

        /// <summary>탭 → 컨텐츠. 홈은 언제나 열려 있다.</summary>
        public static bool TryGetContentOfTab(LobbyTab tab, out ContentId id)
        {
            switch (tab)
            {
                case LobbyTab.Bullet: id = ContentId.Dice; return true;
                case LobbyTab.Equipment: id = ContentId.Equipment; return true;
                case LobbyTab.Shop: id = ContentId.Shop; return true;
                case LobbyTab.Helper: id = ContentId.Relic; return true;
                default: id = default; return false;
            }
        }

        private static bool TabOf(ContentId id, out LobbyTab tab)
        {
            foreach (LobbyTab candidate in (LobbyTab[])Enum.GetValues(typeof(LobbyTab)))
            {
                if (TryGetContentOfTab(candidate, out ContentId mapped) && mapped == id)
                {
                    tab = candidate;
                    return true;
                }
            }

            tab = default;
            return false;
        }

        private readonly Dictionary<ContentId, GameObject> entries = new Dictionary<ContentId, GameObject>();
        private readonly Dictionary<LobbyTab, LobbyBottomBtn> tabs = new Dictionary<LobbyTab, LobbyBottomBtn>();

        /// <summary>이번 진입에서 연출이 꺼내 보인 것. 연출을 다 보기 전에도 눌러야 하므로(가이드) 따로 든다.</summary>
        private readonly HashSet<ContentId> revealed = new HashSet<ContentId>();

        private void Awake()
        {
            Current = this;
            Collect();
            Apply();

            if (ContentUnlockManager.Instance != null)
                ContentUnlockManager.Instance.OnChanged += Apply;
        }

        private void OnDestroy()
        {
            if (ContentUnlockManager.Instance != null)
                ContentUnlockManager.Instance.OnChanged -= Apply;

            if (Current == this)
                Current = null;
        }

        private void Collect()
        {
            foreach (KeyValuePair<ContentId, Type> pair in EntryTypes)
            {
                // 이미 꺼진 입구도 찾아야 한다 — 다시 켜는 것이 이 클래스의 일이다.
                UnityEngine.Object[] found = FindObjectsByType(pair.Value, FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found.Length == 0)
                {
                    // 입구가 없는 로비도 있다(설치 메뉴를 안 돌린 작업 사본). 막을 것이 없을 뿐 사고는 아니다.
                    Debug.LogWarning("[해금] 로비에 " + pair.Value.Name + " 가 없다. " + pair.Key + " 입구를 다루지 않는다.");
                    continue;
                }

                entries[pair.Key] = ((Component)found[0]).gameObject;
            }

            foreach (LobbyBottomBtn button in FindObjectsByType<LobbyBottomBtn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                tabs[button._tab] = button;
        }

        /// <summary>입구를 지금 해금 상태에 맞춘다.</summary>
        public void Apply()
        {
            ContentUnlockManager manager = ContentUnlockManager.Instance;
            if (manager == null)
                return;

            foreach (KeyValuePair<ContentId, GameObject> pair in entries)
            {
                if (pair.Value == null)
                    continue;

                bool visible = IsAccessible(pair.Key);
                if (pair.Value.activeSelf != visible)
                    pair.Value.SetActive(visible);
            }

            foreach (KeyValuePair<LobbyTab, LobbyBottomBtn> pair in tabs)
            {
                if (pair.Value != null && TryGetContentOfTab(pair.Key, out ContentId id))
                    pair.Value.SetLocked(!IsAccessible(id));
            }
        }

        /// <summary>눌러도 되는가 — 보이거나, 이번 진입의 연출이 꺼내 놓았거나.</summary>
        public bool IsAccessible(ContentId id)
        {
            ContentUnlockManager manager = ContentUnlockManager.Instance;
            return manager == null || manager.IsVisible(id) || revealed.Contains(id);
        }

        /// <summary>
        /// 연출이 입구를 꺼내 놓는다. 가리킬 사각형을 돌려준다(없으면 null — 그 로비에 입구가 없다).
        /// </summary>
        public RectTransform Reveal(ContentId id)
        {
            revealed.Add(id);
            Apply();
            return TargetOf(id);
        }

        /// <summary>연출과 가이드가 가리키는 사각형. 탭이면 탭 버튼, 아니면 입구 루트.</summary>
        public RectTransform TargetOf(ContentId id)
        {
            if (TabOf(id, out LobbyTab tab))
                return tabs.TryGetValue(tab, out LobbyBottomBtn button) && button != null ? button.transform as RectTransform : null;

            return entries.TryGetValue(id, out GameObject entry) && entry != null ? entry.transform as RectTransform : null;
        }

        /// <summary>가이드가 "눌렀는가" 를 듣는 버튼.</summary>
        public Button ButtonOf(ContentId id)
        {
            if (TabOf(id, out LobbyTab tab))
                return tabs.TryGetValue(tab, out LobbyBottomBtn button) && button != null ? button.Button : null;

            return entries.TryGetValue(id, out GameObject entry) && entry != null ? entry.GetComponentInChildren<Button>(true) : null;
        }
    }
}
