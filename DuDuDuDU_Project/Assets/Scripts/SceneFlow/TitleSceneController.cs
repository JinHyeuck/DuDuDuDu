using System.Collections;
using UnityEngine;
using OJ.Dice;
using OJ.Equipment;
using OJ.Utils;

namespace OJ.SceneFlow
{
    public class TitleSceneController : MonoBehaviour
    {
        [SerializeField] private float staySeconds = 2f;

        private void Start()
        {
            StartCoroutine(CoMoveToLobby());
        }

        private IEnumerator CoMoveToLobby()
        {
            // 여기 있던 두 줄(`_ = DiceLevelManager.Instance; _ = EquipmentManager.Instance;`)을
            // 지웠다. 값을 버리는 접근으로, MonoSingleton 의 Instance 게터가 "없으면 만들어
            // 내는" 성질을 이용해 로비로 가기 전에 세이브 로드를 끝내 두려던 줄이다.
            // 즉 생성 순서를 코드로 강제하고 있다는 자백이었다.
            //
            // 8.3a 에서 둘 다 컨테이너가 BeforeSceneLoad 에 만든다. 여기보다 이르므로
            // 이 줄은 할 일이 없어졌다.
            yield return new WaitForSecondsRealtime(staySeconds);
            SceneFlowManager.LoadLobby();
        }
    }
}
