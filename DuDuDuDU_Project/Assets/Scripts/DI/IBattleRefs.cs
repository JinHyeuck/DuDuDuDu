using OJ.Bounty;
using OJ.Dice;
using OJ.Element;
using OJ.Hunting;

namespace OJ.DI
{
    /// <summary>
    /// BattleScene 에 상주하는 매니저 14개로 가는 창구. (MIGRATION_BASELINE 8.3b)
    ///
    /// <b>왜 서로 주입하지 않고 홀더를 두나.</b> 14개 중 <b>9개가 하나의 강결합 덩어리</b>다 —
    /// <c>GameManager → UIDiceSummonSystem → DiceTypeStarManager → PlayerController →
    /// GameManager</c> 처럼 되돌아온다. VContainer 는 <c>ContainerBuilder.Build</c> 에서
    /// <c>TypeAnalyzer.CheckCircularDependency</c> 를 <b>무조건</b> 부르고 필드·프로퍼티
    /// 주입까지 따라가므로, <c>[Inject]</c> 로 직결하면 <b>씬 로드 때 컨테이너 빌드가
    /// 예외로 죽는다.</b> 추측이 아니라 VContainer 소스에 그렇게 적혀 있다.
    ///
    /// <b>왜 루트에 등록하나.</b> VContainer 의 해석은 <b>자식 → 부모 단방향</b>이다.
    /// 루트 스코프는 자식(배틀) 스코프의 등록을 <b>영원히 볼 수 없다.</b> 그런데 이 창구를
    /// 읽어야 하는 것 중에 <c>RelicManager</c>·<c>UIService</c> 처럼 <b>루트에 사는 것</b>이
    /// 있다. 특히 <c>UIService</c> 가 찍는 다이얼로그의 호출부가 62곳으로 가장 큰 덩어리다.
    /// 그래서 <b>창구는 루트에 두고 배틀 스코프가 채우기만 한다.</b>
    ///
    /// <b>null 은 사고가 아니다.</b> 로비·타이틀에서는 전투가 없으므로 전부 null 이 맞다.
    /// 반대로 <b>BattleScene 안에서 null 이면 그것은 사고</b>이고 울어야 한다 —
    /// 새 <c>?.</c> 를 넣지 말 것. 2단계에서 봉인한 병이다.
    ///
    /// <b>여기에 로직을 넣지 마라.</b> 참조를 들고 있는 것 외의 일을 하는 순간
    /// <c>GameManager</c> 를 두 개 가진 것과 같아진다.
    /// </summary>
    public interface IBattleRefs
    {
        /// <summary>지금 전투 씬이 살아 있는가. 로비·타이틀에서는 false 다.</summary>
        bool IsActive { get; }

        GameManager Game { get; }
        PlayerController Player { get; }
        MonsterManager Monsters { get; }
        MonsterSpawner Spawner { get; }
        AttackContent Attack { get; }
        MergeSystem Merge { get; }
        UIBoard Board { get; }
        UIDiceBoardUI BoardUI { get; }
        UIDiceSummonSystem Summon { get; }
        DiceTypeStarManager DiceStars { get; }
        ElementUpgradeManager ElementUpgrade { get; }
        BulletPool Bullets { get; }
        BulletEffectPool BulletEffects { get; }
        DamageTextPool DamageTexts { get; }

        /// <summary>
        /// 현상금. <b>씬 컴포넌트가 아닌 유일한 항목</b>이라 배틀 스코프가 코드로 만들어 넘긴다.
        ///
        /// 그래도 여기 두는 이유는 <c>UIService</c> 가 찍는 다이얼로그 때문이다.
        /// 그 프리팹들은 <b>루트</b> 리졸버로 태어나므로 배틀 스코프의 등록을 영원히 못 본다
        /// (해석은 자식 → 부모 단방향이다). 창구에 얹어야 배너와 선택 창이 닿는다.
        /// </summary>
        BountyManager Bounty { get; }
    }
}
