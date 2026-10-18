using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
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
                return;

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
                    await AnnounceAsync(d);

                    if (d.presentation == UnlockPresentation.Guide)
                        await GuideAsync(d);

                    overlay.ClearAll();
                    manager.MarkAnnounced(d.contentId);
                }
            }
            catch (OperationCanceledException)
            {
                // 로비가 내려갔다. 기록하지 않은 것은 다음 진입에 다시 나온다.
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
