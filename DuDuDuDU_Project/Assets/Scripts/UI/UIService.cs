using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using OJ.SceneFlow;

namespace OJ.UI
{
    /// <summary>
    /// 팝업을 카탈로그에서 꺼내 띄운다. (MIGRATION_BASELINE 10.1)
    ///
    /// <b>처음 열 때 만들고 계속 재사용한다.</b> 닫을 때 파괴하지 않는다 —
    /// 지금 <c>DialogBase</c> 는 <c>_isLoaded</c> 로 초기화를 한 번만 하고,
    /// <c>OnDestroy</c> 가 <c>_isEnter</c> 를 끄지 않아 백키 스택에 죽은 항목이 남는다.
    /// 파괴 방식으로 바꾸면 그 두 가지를 동시에 건드리게 되고, 그건 리팩토링이 아니라
    /// 동작 변경이다. 씬 인스턴스가 계속 살아 있던 지금 동작과 같게 맞춘다.
    ///
    /// <b>팝업은 씬과 함께 죽는다.</b> 로비에서 연 창이 전투로 따라가면 안 되므로
    /// 씬이 바뀌면 루트와 캐시를 통째로 버린다.
    /// </summary>
    // 완전수식이 필요하다. using VContainer 를 들이면 VContainer.PreserveAttribute 와
    // UnityEngine.Scripting.PreserveAttribute 가 같은 이름으로 겹친다(CS0104).
    [UnityEngine.Scripting.Preserve]
    public sealed class UIService
    {
        private readonly DialogCatalog catalog;
        private readonly IObjectResolver resolver;
        private readonly Dictionary<string, DialogBase> opened = new Dictionary<string, DialogBase>(StringComparer.Ordinal);

        private Transform root;
        private string rootScene;

        /// <summary>
        /// <paramref name="resolver"/> 는 루트 컨테이너다. 여기서 찍는 다이얼로그는
        /// 씬 순회가 끝난 <b>뒤에</b> 태어나므로 <c>BattleScope</c> 의 주입을 못 받는다.
        /// 리졸버로 찍어야 프리팹 안의 <c>[Inject]</c> 가 채워진다.
        /// 컨테이너가 이 인자를 알아서 넣어 주므로 등록부는 그대로다.
        /// </summary>
        public UIService(DialogCatalog catalog, IObjectResolver resolver)
        {
            this.catalog = catalog;
            this.resolver = resolver;
        }

        /// <summary>
        /// 팝업을 띄우고 그 인스턴스를 돌려준다. 열 수 없으면 null —
        /// <b>부르는 쪽이 조용히 넘어가지 않도록</b> 사유는 로그로 남긴다.
        /// </summary>
        public T Show<T>() where T : DialogBase
        {
            T dialog = Get<T>();
            if (dialog != null)
                dialog.Enter();

            return dialog;
        }

        public void Hide<T>() where T : DialogBase
        {
            if (opened.TryGetValue(typeof(T).Name, out DialogBase dialog) && dialog != null)
                dialog.Exit();
        }

        /// <summary>
        /// 인스턴스를 얻는다(띄우지는 않는다). 이미 만들어 뒀으면 그것을 준다.
        ///
        /// 오프너가 <c>[SerializeField]</c> 로 들고 있던 참조를 대신하는 자리다.
        /// 예전에는 씬 인스턴스를 직접 가리켰고, 그 참조가 <c>None</c> 이 되면
        /// <b>아무 로그 없이</b> 창이 안 열렸다.
        /// </summary>
        public T Get<T>() where T : DialogBase
        {
            return Get<T>(null);
        }

        /// <summary>
        /// 부모를 지정해 얻는다. <paramref name="parent"/> 가 null 이면 팝업 루트에 붙는다.
        ///
        /// <b>왜 부모를 받을 수 있어야 하나.</b> 모든 UI 가 팝업인 것은 아니다.
        /// 로비 탭 내용물(장비·주사위·유물 페이지)은 화면 위에 떠야 하는 것이 아니라
        /// <c>Content</c> 영역 <b>안에</b> 들어가야 한다. 팝업 루트에 붙이면 전체 화면을
        /// 덮어 버린다. 그렇다고 카탈로그 밖에 둘 이유는 없다 — 만들어지는 곳만 다르다.
        /// </summary>
        public T Get<T>(Transform parent) where T : DialogBase
        {
            string key = typeof(T).Name;

            DropIfSceneChanged();

            if (opened.TryGetValue(key, out DialogBase cached))
            {
                // 씬과 함께 파괴됐는데 캐시에 남아 있는 경우. UnityEngine.Object 의
                // 가짜 null 이라 == null 로 잡힌다.
                if (cached != null)
                    return cached as T;

                opened.Remove(key);
            }

            if (catalog == null)
            {
                Debug.LogError("[UI] DialogCatalog 가 없다. " + key + " 를 열 수 없다.");
                return null;
            }

            GameObject prefab = catalog.Find(key);
            if (prefab == null)
            {
                Debug.LogError("[UI] 카탈로그에 " + key + " 가 없다. " +
                               "OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌릴 것.");
                return null;
            }

            // 부모를 반드시 넘긴다. 부모 없는 오버로드는 스코프 아래 만들었다가
            // SetParent(null) 하는 분기를 타서 팝업이 엉뚱한 씬에 남는다.
            GameObject go = resolver.Instantiate(prefab, parent != null ? parent : EnsureRoot());
            go.name = key;

            var dialog = go.GetComponent<T>();
            if (dialog == null)
            {
                Debug.LogError("[UI] " + key + " 프리팹 루트에 " + key + " 컴포넌트가 없다.");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            opened[key] = dialog;
            return dialog;
        }

        /// <summary>
        /// 팝업이 올라갈 자리. <b>자체 Canvas 를 쓴다.</b>
        ///
        /// 씬 계층에 끼어들면 레이어가 형제 순서에 좌우되는데, 그 순서는 씬을 편집하다
        /// 쉽게 바뀐다. 별도 Canvas 로 두면 정렬 순서가 숫자 하나로 명시된다.
        /// 9.2 에서 해상도를 1080x1920 으로 통일했기 때문에 스케일러를 같은 값으로
        /// 두는 것만으로 기존 UI 와 크기가 어긋나지 않는다.
        /// </summary>
        private Transform EnsureRoot()
        {
            if (root != null)
                return root;

            var go = new GameObject("UIPopupRoot");
            rootScene = SceneManager.GetActiveScene().name;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // 씬에서 가장 높은 정렬 순서 위에 놓는다. 페이드(short.MaxValue - 100)보다는
            // 아래여야 한다 — 전환 중에는 팝업도 같이 덮여야 한다.
            canvas.sortingOrder = HighestSortingOrderInScene() + 1;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            go.AddComponent<GraphicRaycaster>();

            root = go.transform;
            return root;
        }

        private static int HighestSortingOrderInScene()
        {
            int highest = 0;
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                // 페이드는 일부러 아주 높게 잡혀 있다. 그것까지 넘어서면 전환 중에
                // 팝업이 페이드 위로 뜬다.
                if (canvases[i] is null || canvases[i].GetComponent<FadeView>() != null)
                    continue;

                highest = Mathf.Max(highest, canvases[i].sortingOrder);
            }

            return highest;
        }

        /// <summary>
        /// 씬이 바뀌었으면 루트와 캐시를 버린다.
        ///
        /// 루트는 <c>DontDestroyOnLoad</c> 가 아니므로 씬과 함께 이미 파괴돼 있다.
        /// 여기서 하는 일은 <b>죽은 참조를 들고 있지 않게</b> 하는 것이다.
        /// </summary>
        private void DropIfSceneChanged()
        {
            string active = SceneManager.GetActiveScene().name;
            if (rootScene == active && root != null)
                return;

            if (rootScene != active)
            {
                opened.Clear();
                root = null;
                rootScene = active;
            }
        }
    }
}
