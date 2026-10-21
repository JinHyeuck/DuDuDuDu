using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.Dice;
using OJ.IdleReward;
using OJ.Lobby;
using OJ.Point;
using OJ.Tower;
using OJ.DI;
using OJ.Tutorial;
using OJ.UI;

namespace OJ.ContentUnlock
{
    /// <summary>
    /// 로비에 들어올 때 새로 열린 컨텐츠를 보여 준다 — 해금 연출, 그리고 가이드. (Docs/ContentUnlock.md)
    ///
    /// <b>화면은 인게임 튜토리얼의 오버레이를 그대로 쓴다</b>(<see cref="UITutorialOverlay"/>).
    /// 딤드·손가락·말풍선이 이미 있고, 오버레이는 "무엇을 언제" 를 모른다 — 그 판단만 여기 있다.
    ///
    /// <b>한 진입의 흐름.</b> <see cref="ContentUnlockManager.GetVisitQueue"/> 가 준 것을 표 순서대로:
    /// <list type="number">
    /// <item>입구를 꺼내 튀어나오게 하고, 딤드로 그 자리만 연 채 "무엇이 열렸다" 를 말한다(탭으로 넘김).</item>
    /// <item>가이드면 손가락으로 입구를 가리키고 <b>실제로 누를 때까지</b> 기다린 뒤, 열린 화면 위에서 한 마디 더 한다.</item>
    /// <item>끝까지 봤으면 그때 기록한다 — 도중에 앱을 끄면 다음 진입에 처음부터다.</item>
    /// </list>
    /// 대기열은 가이드 하나에서 끊기므로 한 진입에 가이드는 많아야 하나다.
    /// </summary>
    public sealed class LobbyUnlockDirector
    {
        /// <summary>입구가 튀어나오는 시간. 튜토리얼의 선물 연출(0→1.3→1)과 같은 모양이다.</summary>
        private const float PopSeconds = 0.4f;
        private const float PopOvershoot = 1.3f;

        /// <summary>누른 뒤 화면이 열리고 자리를 잡을 때까지. 열리는 연출 위에 말풍선이 겹치면 어색하다.</summary>
        private const float SettleSeconds = 0.35f;

        /// <summary>홈으로 돌아온 뒤 다음 가이드를 걸기까지 잠깐 쉰다. 화면 전환 연출과 겹치지 않게.</summary>
        private const float ChainRestSeconds = 0.6f;

        /// <summary>고기 축제 가이드 때 채워 두는 세트 수. 한 세트가 고기 30개다(<c>IdleRewardManager.MeatPerSet</c>).</summary>
        private const int GuideMeatSets = 1;

        private readonly LobbyContentGate gate;
        private UITutorialOverlay overlay;
        private CancellationToken token;

        public LobbyUnlockDirector(LobbyContentGate gate)
        {
            this.gate = gate;
        }

        public async UniTaskVoid RunAsync(CancellationToken cancellationToken)
        {
            token = cancellationToken;

            ContentUnlockManager manager = ContentUnlockManager.Instance;
            if (manager == null || gate == null)
                return;

            List<ContentUnlockDefinition> queue = manager.GetVisitQueue();
            if (queue.Count == 0)
            {
                // 연출이 남아 있지 않은 진입에서만 킹 다이스 강화 안내를 한다.
                if (ShouldGuideKingUpgrade())
                    await KingUpgradeGuideAsync();

                return;
            }

            try
            {
                while (queue.Count > 0)
                {
                    await RunQueueAsync(manager, queue);

                    // 가이드에서 끊긴 뒤에도 남은 연출이 있으면, 유저가 가이드로 연 화면을 닫고 홈으로 돌아왔을 때
                    // 이어서 보여 준다. 예전에는 다음 로비 진입까지 안 나와서 "다이스를 보고 왔는데 고기 축제 가이드가
                    // 안 나온다" 로 보였다. 두 가이드가 연달아 뜨지는 않는다 — 홈으로 돌아와야 다음이 나온다.
                    queue = manager.GetVisitQueue();
                    if (queue.Count == 0)
                        break;

                    await UniTask.WaitUntil(() => LobbyLayoutController.CurrentTab == LobbyTab.Home,
                        PlayerLoopTiming.Update, token);

                    float rest = Time.unscaledTime + ChainRestSeconds;
                    await UniTask.WaitUntil(() => Time.unscaledTime >= rest, PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 로비가 내려갔다. 기록하지 않은 것은 다음 진입에 다시 나온다.
            }
        }

        /// <summary>대기열 한 판. 화면(오버레이)을 열고 표 순서대로 보여 준 뒤 닫는다.</summary>
        private async UniTask RunQueueAsync(ContentUnlockManager manager, List<ContentUnlockDefinition> queue)
        {
            try
            {
                // 한 프레임 미룬다. 입구들의 Start 와 레이아웃이 자리를 잡은 뒤에 재야 구멍이 맞는다.
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                overlay = GameContainer.UI?.Get<UITutorialOverlay>();
                if (overlay == null)
                {
                    // 화면이 없으면 알릴 방법이 없다. 그렇다고 입구를 영영 숨겨 둘 수는 없으니
                    // 연출 없이 꺼내고 본 것으로 적는다. 사유는 UIService 가 이미 로그로 남겼다.
                    Debug.LogError("[해금] 오버레이를 열지 못해 연출 없이 입구만 꺼낸다. " +
                                   "OJ/개발/튜토리얼/UI 프리팹 굽기 → 다이얼로그 카탈로그 갱신을 확인할 것.");
                    foreach (ContentUnlockDefinition d in queue)
                    {
                        gate.Reveal(d.contentId);
                        manager.MarkAnnounced(d.contentId);
                    }

                    return;
                }

                overlay.Enter();

                foreach (ContentUnlockDefinition d in queue)
                {
                    // 고기 축제는 "눌러서 받아요" 를 가르치는 가이드라 받을 게 있어야 한다. 한 세트(30개)를 채워 둔다.
                    if (d.contentId == ContentId.MeatFestival)
                        IdleRewardManager.Instance?.EnsureMeatSets(GuideMeatSets);

                    await AnnounceAsync(d);

                    if (d.presentation == UnlockPresentation.Guide)
                        await GuideAsync(d);

                    overlay.ClearAll();
                    manager.MarkAnnounced(d.contentId);
                }
            }
            finally
            {
                if (overlay != null)
                {
                    overlay.ClearAll();
                    overlay.Exit();
                }
            }
        }

        private async UniTask AnnounceAsync(ContentUnlockDefinition d)
        {
            RectTransform target = gate.Reveal(d.contentId);
            if (target == null)
                Debug.LogError("[해금] " + d.contentId + " 의 입구를 못 찾았다. 화면 전체를 막은 채 말만 한다.");

            // 켜진 입구가 레이아웃에 자리를 잡은 뒤에 잰다.
            await UniTask.Yield(PlayerLoopTiming.Update, token);

            overlay.SetFinger(null);
            overlay.SetDim(target);
            overlay.SetTalk(null);

            if (target != null)
                await PopAsync(target);

            overlay.SetTalk("새로운 컨텐츠가 열렸어요!\n<b>" + d.displayName + "</b>\n" + d.description, tapToContinue: true);
            await UniTask.WaitUntil(overlay.ConsumeTap, PlayerLoopTiming.Update, token);
        }

        private async UniTask GuideAsync(ContentUnlockDefinition d)
        {
            RectTransform target = gate.TargetOf(d.contentId);
            Button button = gate.ButtonOf(d.contentId);
            if (target == null || button == null)
            {
                // 누를 것이 없으면 가이드를 할 수 없다. 막힌 채 멈추는 것보다 건너뛰는 편이 낫다.
                Debug.LogError("[해금] " + d.contentId + " 가이드의 버튼을 못 찾아 건너뛴다.");
                return;
            }

            bool clicked = false;
            void OnClick() => clicked = true;
            button.onClick.AddListener(OnClick);

            try
            {
                overlay.SetDim(target);
                overlay.SetFinger(target);
                overlay.SetTalk(d.guideTapText);
                await UniTask.WaitUntil(() => clicked, PlayerLoopTiming.Update, token);
            }
            finally
            {
                if (button != null)
                    button.onClick.RemoveListener(OnClick);
            }

            if (string.IsNullOrEmpty(d.guideDetailText))
                return;

            // 열린 화면 위에서 말한다. 화면 전체를 막아 탭이 말풍선만 넘기게 한다 —
            // 막지 않으면 설명을 읽기도 전에 그 화면의 버튼이 눌린다.
            overlay.SetFinger(null);
            overlay.SetDim();
            overlay.SetTalk(null);

            float settle = Time.unscaledTime + SettleSeconds;
            await UniTask.WaitUntil(() => Time.unscaledTime >= settle, PlayerLoopTiming.Update, token);

            overlay.SetTalk(d.guideDetailText, tapToContinue: true, UITutorialOverlay.TalkPlacement.Top);
            await UniTask.WaitUntil(overlay.ConsumeTap, PlayerLoopTiming.Update, token);

            if (d.contentId == ContentId.Tower && ShouldGuideTowerLoadout())
                await TowerLoadoutGuideAsync(d);

            // 다이스 가이드는 설명으로 끝나지 않는다 — 노멀 다이스를 한 번 강화해 보게 한다(강제). 강화할 줄 모르면 뉴비가 약한 채로 막힌다.
            if (d.contentId == ContentId.Dice && ShouldGuideDiceUpgrade())
                await DiceUpgradeGuideAsync();
        }

        // ── 다이스 가이드: 노멀 1강화 강제 ───────────────────────────────

        /// <summary>노멀 다이스가 1레벨일 때만 — 이미 올렸으면(재시도·옛 세이브) 다시 시키지 않는다.</summary>
        private static bool ShouldGuideDiceUpgrade()
        {
            DiceLevelManager levels = DiceLevelManager.Instance;
            return levels != null && levels.GetLevel(DiceType.Normal) <= 1;
        }

        /// <summary>
        /// 1강화에 필요한 재화(골드·노멀 두루마리)가 모자라면 <b>미리 채워 둔다</b>. 이미 충분하면 건드리지 않는다.
        /// 가이드가 "눌러 보세요" 라고 했는데 재화가 없어 버튼이 안 눌리면 뉴비가 그 자리에서 멈춘다.
        /// </summary>
        private static void EnsureUpgradeResources()
        {
            PointManager points = PointManager.Instance;
            if (points == null)
                return;

            var cost = DiceMetaDataProvider.GetUpgradeCost(DiceType.Normal, 1);
            int missingGold = cost.goldCost - points.Get(PointType.Gold);
            if (missingGold > 0)
                points.Add(PointType.Gold, missingGold);

            int missingScroll = cost.scrollCost - points.Get(PointType.NormalScroll);
            if (missingScroll > 0)
                points.Add(PointType.NormalScroll, missingScroll);
        }

        private async UniTask DiceUpgradeGuideAsync()
        {
            EnsureUpgradeResources();

            // 목록이 그려질 때까지 기다린다(탭을 연 직후 한두 프레임은 비어 있다).
            UIDiceGrowthItem item = null;
            for (int i = 0; i < 120 && item == null; i++)
            {
                foreach (UIDiceGrowthItem candidate in UnityEngine.Object.FindObjectsByType<UIDiceGrowthItem>(FindObjectsSortMode.None))
                {
                    if (candidate.isActiveAndEnabled && candidate.DiceType == DiceType.Normal && candidate.Button != null)
                    {
                        item = candidate;
                        break;
                    }
                }

                if (item == null)
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            if (item == null)
            {
                Debug.LogError("[해금] 다이스 목록에서 노멀 다이스를 못 찾아 강화 가이드를 건너뛴다.");
                return;
            }

            // 1) 노멀 다이스를 눌러 상세를 연다.
            bool opened = false;
            void OnItemClick() => opened = true;
            item.Button.onClick.AddListener(OnItemClick);
            try
            {
                overlay.SetDim(item.transform as RectTransform);
                overlay.SetFinger(item.transform as RectTransform);
                overlay.SetTalk("<b>노멀 다이스</b>를 눌러서\n강화해 봐요!");
                await UniTask.WaitUntil(() => opened, PlayerLoopTiming.Update, token);
            }
            finally
            {
                if (item != null && item.Button != null)
                    item.Button.onClick.RemoveListener(OnItemClick);
            }

            overlay.SetFinger(null);
            overlay.SetDim();
            overlay.SetTalk(null);

            // 2) 상세가 열리고 강화 버튼이 보일 때까지.
            UIDiceGrowthDetailPanel panel = GameContainer.UI?.Get<UIDiceGrowthDetailPanel>();
            if (panel == null)
            {
                Debug.LogError("[해금] 다이스 상세 창을 못 찾아 강화 가이드를 건너뛴다.");
                return;
            }

            await UniTask.WaitUntil(() => panel.isEnter && panel.UpgradeButton != null && panel.UpgradeButton.isActiveAndEnabled,
                PlayerLoopTiming.Update, token);

            float settle = Time.unscaledTime + SettleSeconds;
            await UniTask.WaitUntil(() => Time.unscaledTime >= settle, PlayerLoopTiming.Update, token);

            // 3) 강화 버튼을 눌러 노멀이 2레벨이 될 때까지. 강화가 안 되는 상태(재화 부족)면 위에서 채워 두었다.
            RectTransform upgrade = panel.UpgradeButton.transform as RectTransform;
            overlay.SetDim(upgrade);
            overlay.SetFinger(upgrade);
            overlay.SetTalk("<b>강화</b> 버튼을 눌러요!");
            await UniTask.WaitUntil(() => DiceLevelManager.Instance != null
                && DiceLevelManager.Instance.GetLevel(DiceType.Normal) >= 2, PlayerLoopTiming.Update, token);

            overlay.SetFinger(null);
            overlay.SetDim();
            overlay.SetTalk("강화했어요! 레벨이 오를수록\n다이스가 더 강해져요.", tapToContinue: true, UITutorialOverlay.TalkPlacement.Top);
            await UniTask.WaitUntil(overlay.ConsumeTap, PlayerLoopTiming.Update, token);
        }

        // ── 탑 1층: 킹 썬더 장착 ────────────────────────────────────────

        /// <summary>킹 썬더가 있는데 탑을 한 번도 안 깼을 때만 — 장착을 눌러 보게 하는 첫 안내다.</summary>
        private static bool ShouldGuideTowerLoadout()
        {
            TowerProgressManager tower = TowerProgressManager.Instance;
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            return tower != null && ownership != null
                && tower.HighestClearedFloor == 0 && ownership.IsOwned(DiceType.KingThunder);
        }

        private async UniTask TowerLoadoutGuideAsync(ContentUnlockDefinition d)
        {
            UITowerFloorSelectDialog floorSelect = GameContainer.UI?.Get<UITowerFloorSelectDialog>();
            Button challenge = floorSelect != null ? floorSelect.ChallengeButton : null;
            if (challenge == null)
            {
                Debug.LogError("[해금] 탑 도전 버튼을 못 찾아 편성 가이드를 건너뛴다.");
                return;
            }

            bool clicked = false;
            void OnClick() => clicked = true;
            challenge.onClick.AddListener(OnClick);
            try
            {
                overlay.SetFinger(challenge.transform as RectTransform);
                overlay.SetDim(challenge.transform as RectTransform);
                overlay.SetTalk("1층에 도전해 볼까요?");
                await UniTask.WaitUntil(() => clicked, PlayerLoopTiming.Update, token);
            }
            finally
            {
                if (challenge != null)
                    challenge.onClick.RemoveListener(OnClick);
            }

            overlay.SetFinger(null);
            overlay.SetDim();
            overlay.SetTalk(null);

            UITowerLoadoutDialog loadout = GameContainer.UI?.Get<UITowerLoadoutDialog>();
            if (loadout == null)
                return;

            await UniTask.WaitUntil(() => loadout.isEnter, PlayerLoopTiming.Update, token);

            // 추천 편성이 킹 썬더를 끼워 놓았으면 빼서, 유저가 직접 눌러 끼우게 한다.
            loadout.ClearForGuide(DiceType.KingThunder);

            float settle = Time.unscaledTime + SettleSeconds;
            await UniTask.WaitUntil(() => Time.unscaledTime >= settle, PlayerLoopTiming.Update, token);

            RectTransform cell = loadout.GuideCellRect(DiceType.KingThunder);
            if (cell == null)
            {
                Debug.LogError("[해금] 편성 화면에서 킹 썬더 칸을 못 찾아 가이드를 건너뛴다.");
                return;
            }

            overlay.SetDim(cell);
            overlay.SetFinger(cell);
            overlay.SetTalk("<b>킹 썬더</b>를 눌러 장착해요!\n신화 다이스는 강력해요.");
            await UniTask.WaitUntil(() => loadout.IsEquipped(DiceType.KingThunder) || !loadout.isEnter,
                PlayerLoopTiming.Update, token);

            // 장착까지 봤으면 여기서 기록한다 — 이어지는 출발은 씬이 바뀌어 이 코드가 끝까지 못 간다.
            ContentUnlockManager.Instance?.MarkAnnounced(d.contentId);

            if (!loadout.isEnter)
                return;

            Button start = loadout.StartButton;
            if (start == null)
                return;

            bool started = false;
            void OnStart() => started = true;
            start.onClick.AddListener(OnStart);
            try
            {
                overlay.SetDim(start.transform as RectTransform);
                overlay.SetFinger(start.transform as RectTransform);
                overlay.SetTalk("좋아요! 전투를 시작해요.");
                await UniTask.WaitUntil(() => started || !loadout.isEnter, PlayerLoopTiming.Update, token);
            }
            finally
            {
                if (start != null)
                    start.onClick.RemoveListener(OnStart);
            }
        }

        // ── 탑 1층 클리어 뒤: 킹 다이스 강화 ────────────────────────────

        private static bool ShouldGuideKingUpgrade()
        {
            TowerProgressManager tower = TowerProgressManager.Instance;
            DiceOwnershipManager ownership = DiceOwnershipManager.Instance;
            return tower != null && ownership != null && !tower.KingUpgradeGuideDone
                && tower.HighestClearedFloor >= 1 && ownership.IsOwned(DiceType.KingThunder);
        }

        /// <summary>1층 보상으로 받은 신화석으로 킹 썬더를 강화해 보게 한다. 다이스 탭을 눌러 열고 한 마디 한다.</summary>
        private async UniTask KingUpgradeGuideAsync()
        {
            Button tab = gate != null ? gate.ButtonOf(ContentId.Dice) : null;
            RectTransform target = gate != null ? gate.TargetOf(ContentId.Dice) : null;
            if (tab == null || target == null)
                return;

            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);

                overlay = GameContainer.UI?.Get<UITutorialOverlay>();
                if (overlay == null)
                    return;

                overlay.Enter();
                overlay.SetFinger(target);
                overlay.SetDim(target);
                overlay.SetTalk("탑 보상으로 <b>신화석</b>을 받았어요!\n다이스에서 킹 썬더를 강화해 봐요.");

                bool clicked = false;
                void OnClick() => clicked = true;
                tab.onClick.AddListener(OnClick);
                try
                {
                    await UniTask.WaitUntil(() => clicked, PlayerLoopTiming.Update, token);
                }
                finally
                {
                    if (tab != null)
                        tab.onClick.RemoveListener(OnClick);
                }

                overlay.SetFinger(null);
                overlay.SetDim();
                overlay.SetTalk(null);

                float settle = Time.unscaledTime + SettleSeconds;
                await UniTask.WaitUntil(() => Time.unscaledTime >= settle, PlayerLoopTiming.Update, token);

                overlay.SetTalk("<b>킹 썬더</b>를 눌러 신화석으로 강화하면\n탑과 전투에서 훨씬 강해져요!",
                    tapToContinue: true, UITutorialOverlay.TalkPlacement.Top);
                await UniTask.WaitUntil(overlay.ConsumeTap, PlayerLoopTiming.Update, token);

                TowerProgressManager.Instance?.MarkKingUpgradeGuideDone();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (overlay != null)
                {
                    overlay.ClearAll();
                    overlay.Exit();
                }
            }
        }

        /// <summary>0 → 1.3 → 1. 배율만 쓴다 — 입구 대부분이 레이아웃 아래라 위치를 쓰면 배치가 무너진다(UIAppear 주석).</summary>
        private async UniTask PopAsync(Transform target)
        {
            float elapsed = 0f;
            while (elapsed < PopSeconds)
            {
                if (target == null)
                    return;

                float t = Mathf.Clamp01(elapsed / PopSeconds);
                float scale = t < 0.6f
                    ? Mathf.Lerp(0f, PopOvershoot, UIEase.OutQuad(t / 0.6f))
                    : Mathf.Lerp(PopOvershoot, 1f, UIEase.OutQuad((t - 0.6f) / 0.4f));
                target.localScale = Vector3.one * scale;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.unscaledDeltaTime;
            }

            if (target != null)
                target.localScale = Vector3.one;
        }
    }
}
