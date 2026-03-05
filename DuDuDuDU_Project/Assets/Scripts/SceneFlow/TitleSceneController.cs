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
            DiceLevelManager.Instance.LoadAll();
            EquipmentManager.Instance.LoadAll();
            yield return new WaitForSecondsRealtime(staySeconds);
            SceneFlowManager.LoadLobby();
        }
    }
}
