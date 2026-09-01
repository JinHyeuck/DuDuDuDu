using System;
using UnityEngine;

namespace OJ.DI
{
    /// <summary>
    /// Unity 앱 생명주기 콜백을 이벤트로 중계한다. (MIGRATION_BASELINE 8.2)
    ///
    /// <b>왜 이것만 MonoBehaviour 로 남나.</b> <c>OnApplicationPause</c> 는 <b>컴포넌트만</b>
    /// 받을 수 있다. <c>UnityEngine</c> 에 이에 대응하는 static 이벤트가 없다 —
    /// <c>Application.focusChanged</c> 는 포커스이지 pause 가 아니고 모바일에서 의미가 다르다.
    /// 그래서 순수 클래스가 저장 훅을 가지려면 <b>누군가는</b> 컴포넌트여야 한다.
    /// 그 "누군가"를 이 파일 하나로 몰아 두는 것이 목적이다.
    ///
    /// 이 클래스는 상태를 갖지 않는다. 중계만 한다.
    /// </summary>
    public sealed class ApplicationLifecycleRelay : MonoBehaviour
    {
        /// <summary>앱이 백그라운드로 갔다. <b>모바일에서 여기가 마지막 저장 기회다.</b></summary>
        public event Action Paused;

        /// <summary>앱이 종료된다. 모바일에서는 불리지 않는 경우가 많다.</summary>
        public event Action Quitting;

        private void OnApplicationPause(bool pauseStatus)
        {
            // pauseStatus == false 는 "돌아왔다"이므로 저장할 일이 아니다.
            if (pauseStatus)
                Paused?.Invoke();
        }

        private void OnApplicationQuit()
        {
            Quitting?.Invoke();
        }
    }
}
