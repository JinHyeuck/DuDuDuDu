using UnityEngine.Scripting;
using OJ.Dice;
using OJ.Element;
using OJ.Hunting;

namespace OJ.DI
{
    /// <summary>
    /// <see cref="IBattleRefs"/> 의 구현. <b>루트 컨테이너가 소유하고 배틀 스코프가 채운다.</b>
    ///
    /// 왜 이 모양인지는 <see cref="IBattleRefs"/> 주석에 있다. 여기서는 <b>수명</b>만 다룬다.
    ///
    /// <b>Bind 와 Clear 는 짝이다.</b> 배틀 스코프가 만들어질 때 채우고 파괴될 때 비운다.
    /// 비우지 않으면 로비로 나간 뒤에도 파괴된 <c>GameManager</c> 를 가리키는데,
    /// Unity 의 파괴된 오브젝트는 <c>== null</c> 이 true 인 <b>가짜 null</b> 이라
    /// <c>IsActive</c> 같은 검사가 통과해 버리고, 실제로 만지는 순간
    /// <c>MissingReferenceException</c> 이 난다 — 사고 지점에서 한참 떨어진 곳에서.
    /// </summary>
    // IL2CPP 스트리핑 대비. 이유는 GameContainer 주석 참고.
    [Preserve]
    public sealed class BattleContext : IBattleRefs
    {
        public bool IsActive { get; private set; }

        public GameManager Game { get; private set; }
        public PlayerController Player { get; private set; }
        public MonsterManager Monsters { get; private set; }
        public MonsterSpawner Spawner { get; private set; }
        public AttackContent Attack { get; private set; }
        public MergeSystem Merge { get; private set; }
        public UIBoard Board { get; private set; }
        public UIDiceBoardUI BoardUI { get; private set; }
        public UIDiceSummonSystem Summon { get; private set; }
        public DiceTypeStarManager DiceStars { get; private set; }
        public ElementUpgradeManager ElementUpgrade { get; private set; }
        public BulletPool Bullets { get; private set; }
        public BulletEffectPool BulletEffects { get; private set; }
        public DamageTextPool DamageTexts { get; private set; }

        /// <summary>
        /// 배틀 스코프가 빌드된 직후 한 번 부른다.
        ///
        /// 인자를 전부 받는 하나의 메서드인 것이 의도다. 프로퍼티를 하나씩 대입하게 두면
        /// <b>일부만 채워진 상태</b>가 성립하고, 그 상태는 <c>IsActive</c> 가 true 인데
        /// 어떤 참조는 null 인 형태로 나타난다 — 가장 찾기 나쁜 종류다.
        /// </summary>
        public void Bind(
            GameManager game,
            PlayerController player,
            MonsterManager monsters,
            MonsterSpawner spawner,
            AttackContent attack,
            MergeSystem merge,
            UIBoard board,
            UIDiceBoardUI boardUI,
            UIDiceSummonSystem summon,
            DiceTypeStarManager diceStars,
            ElementUpgradeManager elementUpgrade,
            BulletPool bullets,
            BulletEffectPool bulletEffects,
            DamageTextPool damageTexts)
        {
            Game = game;
            Player = player;
            Monsters = monsters;
            Spawner = spawner;
            Attack = attack;
            Merge = merge;
            Board = board;
            BoardUI = boardUI;
            Summon = summon;
            DiceStars = diceStars;
            ElementUpgrade = elementUpgrade;
            Bullets = bullets;
            BulletEffects = bulletEffects;
            DamageTexts = damageTexts;

            IsActive = true;
        }

        /// <summary>배틀 스코프가 파괴될 때 부른다. 위 주석의 "가짜 null" 참조.</summary>
        public void Clear()
        {
            IsActive = false;

            Game = null;
            Player = null;
            Monsters = null;
            Spawner = null;
            Attack = null;
            Merge = null;
            Board = null;
            BoardUI = null;
            Summon = null;
            DiceStars = null;
            ElementUpgrade = null;
            Bullets = null;
            BulletEffects = null;
            DamageTexts = null;
        }
    }
}
