using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace OJ.DI
{
    /// <summary>
    /// 앱이 멈추거나 끝날 때 등록된 것들을 전부 저장한다. (MIGRATION_BASELINE 8.2)
    ///
    /// 예전에는 매니저 9개가 각자 <c>OnApplicationPause</c> 를 들고 있었다. 저장 순서도
    /// 없었고, 하나가 예외를 내면 <b>같은 프레임의 나머지가 저장됐는지 안 됐는지 알 수 없었다.</b>
    /// 여기로 모으면 순서가 등록 순서로 정해지고, 예외가 나도 다음 것이 계속 저장된다.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고 — 에디터에서는 안 드러난다.
    [Preserve]
    public sealed class SaveOnApplicationLifecycle : IInitializable, IDisposable
    {
        private readonly ApplicationLifecycleRelay relay;
        private readonly IReadOnlyList<ISaveOnApplicationLifecycle> targets;

        public SaveOnApplicationLifecycle(
            ApplicationLifecycleRelay relay,
            IReadOnlyList<ISaveOnApplicationLifecycle> targets)
        {
            this.relay = relay;
            this.targets = targets;
        }

        public void Initialize()
        {
            // 목록이 비면 "앱이 죽어도 아무것도 저장되지 않는" 상태가 된다. 그런데 그건
            // <b>아무 증상 없이</b> 조용히 일어난다 — 게임은 멀쩡히 돌고 다음 실행에서야
            // 진행도가 없다는 걸 알게 된다. 등록을 빠뜨렸거나(인스톨러에서 .As&lt;&gt; 누락)
            // 컬렉션 주입이 기대와 다르게 도는 경우 둘 다 여기서 걸린다.
            if (targets == null || targets.Count == 0)
            {
                Debug.LogError(
                    "[저장] ISaveOnApplicationLifecycle 등록이 하나도 없다. " +
                    "앱이 백그라운드로 가거나 종료돼도 아무것도 저장되지 않는다. " +
                    "GameContainer 의 인스톨러에서 .As<ISaveOnApplicationLifecycle>() 를 확인할 것.");
            }

            relay.Paused += SaveAll;
            relay.Quitting += SaveAll;
        }

        public void Dispose()
        {
            // 컨테이너가 파기될 때(플레이 종료·씬 언로드) 구독을 푼다. 릴레이가 더 오래 살면
            // 죽은 객체의 SaveAll 이 불려 이미 파기된 상태를 저장하게 된다.
            if (relay != null)
            {
                relay.Paused -= SaveAll;
                relay.Quitting -= SaveAll;
            }

            // 마지막 저장. 에디터에서 플레이를 멈출 때 OnApplicationQuit 이 오지 않는
            // 경우가 있어서, 컨테이너 파기 시점에도 한 번 더 시도한다. 두 번 저장되는 것은
            // 무해하다 — 같은 값을 다시 쓸 뿐이다.
            SaveAll();
        }

        private void SaveAll()
        {
            for (int i = 0; i < targets.Count; i++)
            {
                try
                {
                    targets[i].SaveAll();
                }
                catch (Exception ex)
                {
                    // 하나가 실패해도 나머지는 저장돼야 한다. 종료 경로에서 예외를 위로
                    // 올리면 뒤에 있는 것들이 통째로 저장되지 않는다.
                    Debug.LogError(
                        "[저장] " + targets[i].GetType().Name + " 저장 실패: " + ex);
                }
            }
        }
    }
}
