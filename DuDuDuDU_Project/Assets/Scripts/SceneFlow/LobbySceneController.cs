using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using OJ.UI;

namespace OJ.SceneFlow
{
    public class LobbySceneController : MonoBehaviour
    {
        /// <summary>
        /// 씬에 직접 놓인 버튼에 눌림 연출을 붙인다.
        ///
        /// <b>창 안의 버튼은 <c>DialogBase</c> 가 알아서 한다.</b> 여기서 챙기는 것은 그
        /// 바깥 — 하단 탭과 입구 버튼들이고, 하필 가장 많이 눌리는 것들이다.
        ///
        /// <b><c>Awake</c> 가 아니라 <c>Start</c> 인 이유.</b> 씬의 모든 <c>Awake</c> 가
        /// 끝난 뒤여야 코드로 심어진 버튼까지 함께 잡힌다.
        /// </summary>
        private void Start()
        {
            UIButtonPress.AttachToScene(gameObject.scene);
        }
    }
}
