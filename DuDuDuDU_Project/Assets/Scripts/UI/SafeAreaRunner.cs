using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OJ.UI
{
    /// <summary>
    /// <see cref="SafeAreaFitter"/> 를 씬이 열린 직후 몇 번 돌린다. 캔버스 스케일러와 로비 레이아웃이 자리를 잡은 뒤여야 하고,
    /// 늦게 켜지는 요소도 있어 몇 번 훑는다. 끝나면 스스로 사라진다.
    /// </summary>
    public sealed class SafeAreaRunner : MonoBehaviour
    {
        public void Run(Scene scene)
        {
            StartCoroutine(Co(scene));
        }

        private IEnumerator Co(Scene scene)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                yield return new WaitForSecondsRealtime(pass == 0 ? 0.1f : 0.5f);
                if (!scene.isLoaded)
                    break;

                SafeAreaFitter.ApplyScene(scene);
            }

            Destroy(gameObject);
        }
    }
}
