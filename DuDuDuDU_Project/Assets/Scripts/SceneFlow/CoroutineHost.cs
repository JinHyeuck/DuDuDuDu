using System.Collections;
using UnityEngine;
using OJ.DI;

namespace OJ.SceneFlow
{
    /// <summary>
    /// 순수 클래스가 코루틴을 돌릴 수 있게 해 주는 얹을 자리. (MIGRATION_BASELINE 9.4)
    ///
    /// <b>왜 필요한가.</b> <c>StartCoroutine</c> 은 MonoBehaviour 만 가진다. 그런데
    /// <see cref="SceneRouter"/> 는 컨테이너가 만드는 순수 클래스여야 한다 — 씬을 넘나드는
    /// 것이 일인데 자기가 씬에 속하면 <b>전환 도중에 자기가 파괴된다.</b>
    ///
    /// 그래서 "코루틴을 대신 돌려 줄 컴포넌트" 하나만 씬 밖에 두고 빌려 쓴다.
    /// <see cref="ApplicationLifecycleRelay"/> 와 같은 발상이다 — Unity 만 할 수 있는 일을
    /// 한 곳에 몰아 두고, 나머지는 순수 클래스로 남긴다.
    /// </summary>
    public sealed class CoroutineHost : MonoBehaviour
    {
        public static CoroutineHost Create()
        {
            var go = new GameObject(nameof(CoroutineHost));
            DontDestroyOnLoad(go);
            return go.AddComponent<CoroutineHost>();
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }
    }
}
