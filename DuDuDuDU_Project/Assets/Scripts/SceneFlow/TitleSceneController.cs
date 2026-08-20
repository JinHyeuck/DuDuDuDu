using System.Collections;
using UnityEngine;

namespace OJ
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
            _ = DiceLevelManager.Instance;
            _ = EquipmentManager.Instance;
            yield return new WaitForSecondsRealtime(staySeconds);
            SceneFlowManager.LoadLobby();
        }
    }
}
