using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using OJ.Bounty;
using OJ.Dice;
using OJ.Element;
using OJ.Hunting;
using OJ.SceneFlow;

namespace OJ.DI
{
    /// <summary>
    /// BattleScene 의 자식 스코프. 씬에 상주하는 매니저 14개를 컨테이너에 잇는다.
    /// (MIGRATION_BASELINE 8.3b)
    ///
    /// <b>씬 파일을 건드리지 않는다.</b> 이 스코프의 GameObject 를 씬에 배치하지 않고
    /// <see cref="Install"/> 이 <c>sceneLoaded</c> 에서 코드로 만든다. 이유는 두 가지다.
    ///
    /// 하나, <b>되돌리기.</b> 씬 YAML 은 손으로 고치면 안 되고(절대 규칙 3) 에디터에서
    /// 고치면 diff 가 크다. 코드로 만들면 이 파일 하나를 지우는 것이 곧 롤백이다.
    ///
    /// 둘, <b>시점.</b> <c>sceneLoaded</c> 는 <b>씬의 모든 <c>Awake</c>·<c>OnEnable</c> 뒤,
    /// 모든 <c>Start</c> 앞</b>이다. 그래서 8.6("<c>Awake</c> 초기화를 주입 이후로")이
    /// 새 개념 없이 <b>"<c>Awake</c> → <c>Start</c>"</b> 한 줄로 끝난다. 씬에 스코프를
    /// 배치했다면 그 <c>Awake</c> 가 다른 <c>Awake</c> 들과 순서 경쟁을 했을 것이다.
    ///
    /// <b>주입 시점이 둘로 갈린다 — 헷갈리기 쉬운 자리다.</b>
    /// <list type="bullet">
    /// <item>씬에 놓인 컴포넌트: 이 스코프가 <c>sceneLoaded</c> 에서 훑으므로
    ///       <b>자기 <c>Awake</c> 뒤</b>에 채워진다. <c>Awake</c> 에서 읽으면 null 이다.</item>
    /// <item>런타임 생성물(<c>resolver.Instantiate</c>): VContainer 가 프리팹을
    ///       <c>SetActive(false)</c> 로 껐다 찍고 주입한 뒤에 켠다. 그래서 클론의
    ///       <c>Awake</c> 는 <b>주입 뒤</b>에 돈다 — 여기서는 <c>Awake</c> 도 안전하다.</item>
    /// </list>
    ///
    /// <b>부모 연결은 <see cref="FindParent"/> 뿐이다.</b> 직렬화된 <c>parentReference</c> 는
    /// 코드로 만든 루트를 가리킬 수 없고, <c>parentReference.Type</c> 은 내부적으로
    /// <c>FindAnyObjectByType</c> 이라 <b>자기 자신을 부모로 물 수 있다.</b>
    /// <c>VContainerSettings</c> 는 이 프로젝트에 에셋 자체가 없다 — 그리고 <b>만들면 안 된다.</b>
    /// 만드는 순간 <c>IsRoot</c> 가 살아나 <c>resolver.Instantiate</c> 가
    /// <c>DontDestroyOnLoad</c> 분기를 타서 전투 오브젝트가 씬을 넘어 남는다.
    /// </summary>
    public sealed class BattleScope : LifetimeScope
    {
        private const string ObjectName = "BattleScope";

        private BattleContext context;

        /// <summary>
        /// <see cref="GameContainer"/> 부트스트랩이 한 번 부른다. 이후 BattleScene 이
        /// 로드될 때마다 스코프를 만든다.
        /// </summary>
        internal static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (SceneCatalog.IdOf(scene.name) != SceneId.Battle)
                return;

            // Single 로드만 받는다. Additive 로 같은 씬이 또 열리면 스코프가 둘 서고,
            // 둘 다 같은 루트 창구(BattleContext)를 Bind 해 나중 것이 앞 것을 덮는다.
            // 그러면 한쪽 스코프의 OnDestroy 가 살아 있는 다른 쪽 참조까지 Clear 한다.
            // 지금 로드 경로는 전부 Single 이라 실제로 일어나지 않지만, 조용히 어긋나는
            // 종류라 여기서 미리 끊는다.
            if (mode != LoadSceneMode.Single)
            {
                Debug.LogError("[배틀] Additive 로 BattleScene 이 열렸다. 스코프를 만들지 않는다 — " +
                               "창구는 한 번에 하나만 가리킬 수 있다.");
                return;
            }

            // 로드된 씬 안에 만들어야 한다. RegisterComponentInHierarchy 는 스코프
            // GameObject 가 속한 씬만 뒤지므로, DontDestroyOnLoad 에 두면 빈 씬을 뒤진다.
            var go = new GameObject(ObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);

            // 컴포넌트를 붙이는 순간 LifetimeScope.Awake 가 돌아 빌드까지 끝난다.
            go.AddComponent<BattleScope>();
        }

        /// <summary>
        /// 루트는 <c>LifetimeScope.Create</c> 로 코드에서 만들어져 씬 계층에 없다.
        /// 그래서 자동 탐색으로는 찾을 수 없고 여기서 직접 준다.
        /// </summary>
        protected override LifetimeScope FindParent() => GameContainer.Root;

        protected override void Configure(IContainerBuilder builder)
        {
            // 비활성 오브젝트도 찾는다(FindComponentProvider 가 includeInactive: true).
            // BattleScene 의 UIDiceBoardUI 가 실제로 비활성이라 이 성질에 기대고 있다.
            //
            // 못 찾으면 VContainerException 을 던진다 — 조용히 null 이 되지 않는다.
            // 그것이 이 방식을 쓰는 이유의 절반이다.
            builder.RegisterComponentInHierarchy<GameManager>();
            builder.RegisterComponentInHierarchy<PlayerController>();
            builder.RegisterComponentInHierarchy<MonsterManager>();
            builder.RegisterComponentInHierarchy<MonsterSpawner>();
            builder.RegisterComponentInHierarchy<AttackContent>();
            builder.RegisterComponentInHierarchy<MergeSystem>();
            builder.RegisterComponentInHierarchy<UIBoard>();
            builder.RegisterComponentInHierarchy<UIDiceBoardUI>();
            builder.RegisterComponentInHierarchy<UIDiceSummonSystem>();
            builder.RegisterComponentInHierarchy<DiceTypeStarManager>();
            builder.RegisterComponentInHierarchy<ElementUpgradeManager>();
            builder.RegisterComponentInHierarchy<BulletPool>();
            builder.RegisterComponentInHierarchy<BulletEffectPool>();
            builder.RegisterComponentInHierarchy<DamageTextPool>();

            builder.RegisterBuildCallback(Bind);
        }

        private void Bind(IObjectResolver resolver)
        {
            context = resolver.Resolve<BattleContext>();

            // 현상금 매니저만 씬에 없다. 씬 YAML 을 건드리지 않으려고 순수 객체로 두었고
            // (절대 규칙 3), 그래서 컨테이너가 아니라 여기서 손으로 만든다.
            //
            // <b>창구 자신을 넘긴다.</b> 아직 Bind 전이라 context 의 프로퍼티는 전부 null 인데,
            // BountyManager 는 생성자에서 참조를 들고만 있고 실제로 읽는 것은 웨이브가
            // 시작된 뒤다. 컨테이너에 등록하지 않는 이유는 두 가지다 — 등록해도 루트에서
            // 태어나는 다이얼로그가 못 보고(자식 → 부모 단방향), 그렇다고 루트에 등록하면
            // 로비·타이틀에도 살아 있게 된다.
            var bounty = new BountyManager(context);

            context.Bind(
                resolver.Resolve<GameManager>(),
                resolver.Resolve<PlayerController>(),
                resolver.Resolve<MonsterManager>(),
                resolver.Resolve<MonsterSpawner>(),
                resolver.Resolve<AttackContent>(),
                resolver.Resolve<MergeSystem>(),
                resolver.Resolve<UIBoard>(),
                resolver.Resolve<UIDiceBoardUI>(),
                resolver.Resolve<UIDiceSummonSystem>(),
                resolver.Resolve<DiceTypeStarManager>(),
                resolver.Resolve<ElementUpgradeManager>(),
                resolver.Resolve<BulletPool>(),
                resolver.Resolve<BulletEffectPool>(),
                resolver.Resolve<DamageTextPool>(),
                bounty);

            // 등록한 14개 말고도 씬에는 창구가 필요한 컴포넌트가 있다
            // (Wall · PlayerFireRateUI · UIRemoveDice). 그것들은 다른 곳에서
            // 해석될 일이 없어 <b>등록만으로는 주입이 닿지 않는다.</b> 그래서 씬을 한 번 훑는다.
            //
            // <b>비용은 씬 로드당 한 번뿐이다.</b> InjectGameObject 는 [Inject] 가 없는
            // 컴포넌트를 건너뛰므로(TypeAnalyzer 가 타입별로 캐시한다) 훑는 것 자체가 싸다.
            //
            // 여기서 하지 않으면 그 세 파일은 <b>필드가 null 인 채로 조용히 돈다</b> —
            // 컴파일도 F9 도 통과하고, 실제로 벽이 부서지는 순간에야 드러난다.
            Scene scene = gameObject.scene;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                resolver.InjectGameObject(roots[i]);

            Debug.Log("[배틀] 스코프 빌드 완료 — 매니저 14개 연결, 씬 루트 " +
                      roots.Length + "개 주입. 이 줄이 어떤 Start 로그보다 먼저 나와야 한다.");
        }

        protected override void OnDestroy()
        {
            // 비우지 않으면 로비로 나간 뒤에도 파괴된 오브젝트를 가리킨다. Unity 의
            // 파괴된 오브젝트는 == null 이 true 인 가짜 null 이라 IsActive 검사가
            // 통과해 버리고, 실제로 만지는 순간 사고 지점에서 멀리 떨어진 곳에서 터진다.
            context?.Clear();
            context = null;

            base.OnDestroy();
        }
    }
}
