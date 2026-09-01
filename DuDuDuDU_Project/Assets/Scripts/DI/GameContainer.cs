using UnityEngine;
using VContainer;
using VContainer.Unity;
using OJ.Analytics;
using OJ.Dice;
using OJ.Equipment;
using OJ.IdleReward;
using OJ.Point;
using OJ.Relic;
using OJ.Save;
using OJ.SceneFlow;
using OJ.Stage;
using OJ.StageReward;
using OJ.StageStar;
using OJ.UI;
using OJ.Utils;

namespace OJ.DI
{
    /// <summary>
    /// 앱 전체의 컴포지션 루트. (MIGRATION_BASELINE 8.2 / 8.4)
    ///
    /// <b>여기가 유일한 진입점이 되어야 한다.</b> 지금 이 프로젝트에는
    /// <c>RuntimeInitializeOnLoadMethod</c> 가 9개 흩어져 있고, 각자 <c>new GameObject</c> +
    /// <c>AddComponent</c> 로 자기 자신을 만든다. <c>AddComponent</c> 는 <c>Awake</c> 를
    /// <b>동기로</b> 부르므로, 그 <c>Awake</c> 안에서 또 다른 매니저를 읽으면 그쪽 <c>Awake</c> 가
    /// 다시 그 자리에서 실행된다 — 초기화가 재진입하는 구조다. 순서가 어디서 정해지는지
    /// 코드만 보고는 알 수 없고, 실제로 그것 때문에 <c>StageStarManager</c> 와
    /// <c>StageRewardManager</c> 는 <c>Awake</c> 와 <c>Start</c> 에서 구독을 두 번 시도하고 있다.
    ///
    /// 컨테이너로 옮기면 순서가 <b>등록 순서와 생성자 의존</b>으로 명시된다.
    ///
    /// <b>왜 씬에 두지 않고 코드로 만드나.</b> 9단계에서 Boot 씬이 생기면 그때 옮긴다.
    /// 지금 씬을 새로 만들면 빌드 인덱스·씬 흐름을 함께 건드려야 해서 9단계를 앞당기게 된다.
    /// <c>BeforeSceneLoad</c> 로 만들면 <b>어떤 씬 오브젝트의 <c>Awake</c> 보다도 먼저</b> 돌아서
    /// 지금의 부트스트랩들과 순서 보장이 같다.
    ///
    /// <b>여기 등록되는 클래스에는 <c>[Preserve]</c> 가 붙어 있다.</b> (8.8)
    /// VContainer 1.19 는 리플렉션으로 생성자를 찾아 부른다(<c>ReflectionInjector</c>).
    /// IL2CPP 링커는 그 호출을 정적으로 볼 수 없어서, 실기 빌드에서 그 생성자들이
    /// "아무도 안 부르는 코드"로 판정돼 벗겨질 수 있다. 그러면 <b>앱이 시작하자마자
    /// 컨테이너가 터지는데, 에디터와 헤드리스 러너는 스트리핑을 하지 않으므로 둘 다 초록불이다.</b>
    /// 안드로이드가 <c>managedStrippingLevel: 1</c> 이라 실제로 걸릴 수 있는 구성이다.
    ///
    /// <c>link.xml</c> 대신 특성으로 둔 이유는 <b>목록이 조용히 어긋나기 때문</b>이다.
    /// 등록을 추가하면서 별도 파일을 고치는 것을 잊으면 그 클래스만 실기에서 죽는다.
    /// 특성은 클래스와 같이 움직이므로 그 사고가 성립하지 않는다.
    /// </summary>
    public static class GameContainer
    {
        /// <summary>
        /// 루트 스코프. 씬을 넘어 산다.
        ///
        /// <c>UnityEngine.Object</c> 라 파기되면 <c>== null</c> 이 참이 된다(가짜 null).
        /// 도메인 리로드를 끈 에디터에서 이 static 이 남아 있어도 재생성 판정이 맞게 도는 이유다.
        /// </summary>
        public static LifetimeScope Root { get; private set; }

        /// <summary>통합 세이브 서비스. 진단 도구(F10)가 쓴다.</summary>
        public static SaveService SaveService { get; private set; }

        /// <summary>씬 전환기. <c>SceneFlowManager</c> 다리가 쓴다. (9.4)</summary>
        public static SceneRouter SceneRouter { get; private set; }

        /// <summary>팝업 서비스. 오프너가 쓴다. (10.1)</summary>
        public static UIService UI { get; private set; }

        /// <summary>
        /// 전투 씬 매니저로 가는 창구. (8.3b)
        ///
        /// <b>로비·타이틀에서는 <c>IsActive</c> 가 false 이고 참조가 전부 null 이다.</b>
        /// 그것이 정상이다 — 전투가 없으니까. 반대로 BattleScene 안에서 null 이면 사고다.
        ///
        /// 정적 접근자를 남기는 대상은 <b>정적 클래스와 개발 도구뿐</b>이다
        /// (<c>DiceMetaDataProvider</c> 951줄·148곳, <c>SelfCheck</c>, <c>DevSceneHotkeys</c>).
        /// 그 외에는 <c>IBattleRefs</c> 를 주입받아라 — 이 프로퍼티를 쓰는 것은
        /// <c>.Instance</c> 를 다른 static 으로 바꾸는 것에 지나지 않는다.
        /// </summary>
        public static IBattleRefs Battle => battleContext;

        private static readonly BattleContext battleContext = new BattleContext();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Root != null)
                return;

            // 릴레이를 먼저 만든다. Configure 안에서 만들면 안 된다 —
            // LifetimeScope.Create 는 GameObject 를 활성화하면서 Awake -> Build ->
            // 엔트리포인트 실행까지 <b>그 호출 안에서 끝낸다.</b> 즉 아래 Root 대입이
            // 일어나기 전에 릴레이가 해석되므로, Configure 가 Root 를 참조하면 null 이다.
            var relay = new GameObject(nameof(ApplicationLifecycleRelay))
                .AddComponent<ApplicationLifecycleRelay>();
            Object.DontDestroyOnLoad(relay.gameObject);

            // 페이드와 코루틴 자리도 같은 이유로 먼저 만든다. 둘 다 씬을 넘어 살아야 하는데
            // (전환 도중이 바로 씬이 사라지는 순간이다) 컨테이너 빌드 중에 해석되므로
            // Root 대입 전에 존재해야 한다.
            CoroutineHost coroutineHost = CoroutineHost.Create();
            FadeView fade = FadeView.Create();

            Root = LifetimeScope.Create(
                builder => Configure(builder, relay, coroutineHost, fade), nameof(GameContainer));
            Object.DontDestroyOnLoad(Root.gameObject);

            InstallBridges(Root.Container);

            // BattleScene 이 로드될 때마다 자식 스코프를 세운다. (8.3b)
            // 여기서 구독만 해 두고 실제 생성은 sceneLoaded 가 한다 — 그 시점이
            // 씬의 모든 Awake 뒤이자 모든 Start 앞이라, 배선이 끝난 뒤에 게임이 시작된다.
            BattleScope.Install();
        }

        private static void Configure(
            IContainerBuilder builder,
            ApplicationLifecycleRelay relay,
            CoroutineHost coroutineHost,
            FadeView fade)
        {
            // 이미 만들어 둔 인스턴스를 넣는다. 컨테이너가 만들게 하지 않는 이유는 위 참조.
            // 전투 창구. <b>자식(배틀) 스코프가 아니라 루트에 둔다.</b> VContainer 의
            // 해석은 자식 → 부모 단방향이라, 자식에 두면 루트에 사는 RelicManager 와
            // UIService 가 영원히 못 본다. UIService 가 찍는 다이얼로그 호출부만 62곳이다.
            builder.RegisterInstance(battleContext)
                .AsSelf()
                .As<IBattleRefs>();

            builder.RegisterComponent(relay);
            builder.RegisterComponent(coroutineHost);
            builder.RegisterComponent(fade);

            builder.Register<SceneRouter>(Lifetime.Singleton).AsSelf();

            // 팝업 카탈로그는 StaticResource 가 들고 있다. 없으면 UIService 가
            // null 을 받고 팝업을 열 때마다 시끄럽게 실패한다 — 조용히 안 열리는 것보다 낫다.
            builder.Register(_ => StaticResource.Instance != null
                ? StaticResource.Instance.DialogCatalog
                : null, Lifetime.Singleton);

            builder.Register<UIService>(Lifetime.Singleton).AsSelf();

            // 등록 순서가 곧 WriteTo 순서다(SaveService.Capture 가 이 순서대로 돈다).
            // 의존이 얕은 것부터 적는다 — 읽을 때 의존 방향이 위에서 아래로 흐른다.
            //
            // 7.5 이후 매니저는 ISaveOnApplicationLifecycle 이 아니다. 각자 저장할 것이
            // 없어졌기 때문이다 — 저장은 SaveService 가 파일 하나로 한다. 여기에 매니저를
            // 다시 넣으면 앱이 멈출 때마다 같은 파일을 8번 더 쓴다.
            builder.Register<PointManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            builder.Register<DiceLevelManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            // 스테이지 계열. StageProgressManager 가 뿌리이고 나머지 셋이 그것을 받는다.
            // 순서를 신경 쓸 필요는 없다 — 컨테이너가 생성자 의존을 보고 알아서 정한다.
            // 예전에 Awake 순서를 못 믿어 구독을 두 번 시도하던 것이 이걸로 사라졌다.
            builder.Register<StageProgressManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            builder.Register<StageRewardManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            builder.Register<StageStarManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            builder.Register<EquipmentManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            builder.Register<RelicManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            builder.Register<RunHistoryManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveOnApplicationLifecycle>();

            // ITickable 이라 엔트리포인트로 등록한다. 컨테이너가 PlayerLoop 에 끼워
            // 매 프레임 Tick 을 부른다 — 예전 Update() 자리다.
            builder.RegisterEntryPoint<AOSBackBtnManager>().AsSelf();

            builder.Register<IdleRewardManager>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveStateOwner>();

            // 통합 세이브. 7.5 이후 <b>이것이 유일한 진행도 저장소다.</b>
            // ISaveOnApplicationLifecycle 로 등록해 앱이 멈출 때 파일이 쓰이게 한다.
            //
            // RunHistoryManager 는 별개다 — SaveState 밖이고 자기 PlayerPrefs 키를
            // 계속 쓴다. 7.5 의 대상은 "진행도를 담은 구 키"이지 PlayerPrefs 자체가 아니다.
            builder.Register<SaveService>(Lifetime.Singleton)
                .AsSelf()
                .As<ISaveOnApplicationLifecycle>();

            builder.RegisterEntryPoint<SaveOnApplicationLifecycle>();
        }

        /// <summary>
        /// 과도기 다리를 잇는다.
        ///
        /// <b>왜 필요한가.</b> <c>PointManager.Instance</c> 호출부만 70곳(16개 파일)이다.
        /// 전부 생성자 주입으로 바꾸는 것이 최종 형태지만 한 번에 하면 되돌릴 수 없는 크기가
        /// 된다. 그래서 <b>인스턴스를 만드는 경로는 컨테이너 하나로 통일하고</b>, 읽는 경로만
        /// 당분간 <c>Instance</c> 로 남긴다.
        ///
        /// 8단계 머리말이 경고하는 "같은 상태의 인스턴스가 2개 생겨 마지막에 저장한 쪽이
        /// 이긴다"는 <b>만드는 경로가 둘일 때</b> 생긴다. 여기서는 하나뿐이다 — 그래서 안전하다.
        /// 매니저 쪽의 <c>RuntimeInitializeOnLoadMethod</c> 를 지우는 것이 그 조건이고,
        /// 지우지 않은 채 등록만 추가하는 중간 상태를 절대 만들지 말 것.
        ///
        /// 호출부가 전부 주입으로 옮겨지면 이 메서드는 통째로 사라진다.
        /// </summary>
        private static void InstallBridges(IObjectResolver container)
        {
            PointManager.Instance = container.Resolve<PointManager>();
            DiceLevelManager.Instance = container.Resolve<DiceLevelManager>();
            StageProgressManager.Instance = container.Resolve<StageProgressManager>();
            StageRewardManager.Instance = container.Resolve<StageRewardManager>();
            StageStarManager.Instance = container.Resolve<StageStarManager>();
            IdleRewardManager.Instance = container.Resolve<IdleRewardManager>();
            EquipmentManager.Instance = container.Resolve<EquipmentManager>();
            RelicManager.Instance = container.Resolve<RelicManager>();
            RunHistoryManager.Instance = container.Resolve<RunHistoryManager>();
            AOSBackBtnManager.Instance = container.Resolve<AOSBackBtnManager>();

            // SaveService 를 여기서 한 번 해석해 둔다. 등록만 해 두면 Lifetime.Singleton 은
            // 게을러서 아무도 안 찾으면 만들어지지 않고, 그러면 저장 훅에도 안 걸린다.
            SaveService = container.Resolve<SaveService>();
            SceneRouter = container.Resolve<SceneRouter>();
            UI = container.Resolve<UIService>();

            // 통합 세이브를 읽어 매니저들에게 덮는다. <b>순서가 중요하다</b> —
            // 매니저 생성자가 컬렉션 초기화와 초기 지급을 이미 끝내 둔 상태이고,
            // 그 위에 파일이 덮인다.
            //
            // 파일이 없으면(첫 실행) 아무 일도 일어나지 않고 생성자가 만든 초기 상태가
            // 그대로 남는다. <b>7.5 에서 초기화를 로드 경로 밖으로 꺼낸 이유가 이것이다</b> —
            // 초기화가 PlayerPrefs 로드 안에 있던 채로 구 경로를 지웠다면 신규 설치가
            // 빈 컬렉션으로 시작해 보석 장착에서 죽었다.
            //
            // 기존 유저의 PlayerPrefs 는 읽지 않는다. AGENTS.md 「확정된 결정」 2번
            // (기존 유저 세이브 버림)에 따른 것이다.
            SaveService.TryLoadAll();
        }
    }
}
