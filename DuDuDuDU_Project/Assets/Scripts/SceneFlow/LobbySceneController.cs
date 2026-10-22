using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using OJ.ContentUnlock;
using OJ.UI;

namespace OJ.SceneFlow
{
    public class LobbySceneController : MonoBehaviour
    {
        private LobbyContentGate contentGate;

        /// <summary>
        /// 안 열린 입구를 <b>첫 프레임이 그려지기 전에</b> 숨긴다(Docs/ContentUnlock.md).
        /// 씬에 놓지 않고 코드로 붙인다 — 씬 YAML 을 고치지 않기 위해서다(AGENTS 절대 규칙 3).
        /// </summary>
        private void Awake()
        {
            contentGate = gameObject.AddComponent<LobbyContentGate>();
            // 강화할 수 있는 장비·다이스가 있으면 하단 탭에 레드닷을 건다(씬을 고치지 않고 코드로 붙인다).
            gameObject.AddComponent<OJ.Lobby.LobbyUpgradeBadges>();
        }

        /// <summary>
        /// 씬에 직접 놓인 버튼에 눌림 연출을 붙인다.
        ///
        /// <b>창 안의 버튼은 <c>DialogBase</c> 가 알아서 한다.</b> 여기서 챙기는 것은 그
        /// 바깥 — 하단 탭과 입구 버튼들이고, 하필 가장 많이 눌리는 것들이다.
        ///
        /// <b><c>Awake</c> 가 아니라 <c>Start</c> 인 이유.</b> 씬의 모든 <c>Awake</c> 가
        /// 끝난 뒤여야 코드로 심어진 버튼까지 함께 잡힌다.
        ///
        /// 그 뒤에 새로 열린 컨텐츠의 해금 연출과 가이드를 시작한다.
        /// </summary>
        private void Start()
        {
            UIButtonPress.AttachToScene(gameObject.scene);

            new LobbyUnlockDirector(contentGate).RunAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}
