using UnityEngine;
using System.Collections;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Mission;
using OJ.Relic;
using OJ.Rewind;
using OJ.Tower;
using OJ.Utils;
using VContainer;

namespace OJ.Hunting
{
    public class Monster : MonoBehaviour
    {
        // 8.3b: 배틀 스코프가 채운다. 이 몬스터는 MonsterSpawner 가 런타임에 찍는 프리팹이라
        // 씬 순회로는 안 잡히고, 생성부의 resolver.Instantiate 가 찍는 그 순간에 주입된다.
        //
        // Awake 에서는 읽지 않는다. 주입이 Awake 보다 먼저냐 나중이냐가 생성부에 달려 있어서다
        // — 지금 생성부(MonsterSpawner 3곳)는 resolver.Instantiate 라 VContainer 가 프리팹을
        // 껐다 찍고 주입한 뒤 켜므로 Awake 가 나중에 돌지만, 평범한 Object.Instantiate 로
        // 바뀌는 순간 순서가 뒤집혀 Awake 에서 null 이 된다. Awake 가 하는 일은 localScale 을
        // 읽어 두는 것뿐이라 애초에 창구에 닿지 않는다.
        //
        // 아래 사용처는 OnSpawn / Update / TakeDamage / 코루틴이라 전부 생성이 끝난 뒤다.
        // OnDisable 도 안전하다 — 풀 예열이 오브젝트를 끄는 것은 resolver.Instantiate 가
        // 돌아온 다음 문장이고, 주입은 그 안에서 이미 끝나 있다.
        // null 이면 그것은 사고이니 새 ?. 를 쓰지 않는다.
        [Inject] private IBattleRefs battle;

        CharacterState CharacterState = CharacterState.None;

        private bool IsAlive => gameObject.activeInHierarchy && isActiveAndEnabled && CharacterState != CharacterState.Dead && _hp > 0;

        public int MonsterID = -1;
        public int _hp = 3;
        public int defense = 0;
        /// <summary>
        /// 벽을 통과했을 때 벽에 주는 피해의 <b>기준값</b>. 실제 피해는
        /// <c>OJ.Core.WallLeakFormula.LeakDamage</c> 가 여기에 배수를 걸어 낸다.
        ///
        /// <c>attackInterval</c> 이 옆에 있었다. 통과한 몬스터가 벽 앞에 남아
        /// 그 간격마다 때리던 시절의 값인데, 지금은 한 대 때리고 사라지므로
        /// <b>읽는 곳이 없다</b> — 인스펙터에 남겨 두면 "조절되는 값" 으로 보여서 지웠다.
        /// 프리팹 YAML 에 남은 키는 Unity 가 다음 저장 때 떨군다.
        /// </summary>
        public int attackDamage = 1;

        public float moveSpeed = 2f;
        public float ApplyMoveSpeed = 2f;
        private bool isAttacking = false;

        public CharacterAnimation characterAnimation;

        /// <summary>
        /// 이 개체가 현상금 몬스터인가. <c>MonsterSpawner</c> 가 <see cref="ConfigureAsBounty"/>
        /// 로 켜고 <c>OnSpawn</c>/<c>OnDisable</c> 이 끈다.
        ///
        /// <b>켜지면 이동을 막는 효과가 전부 무효가 된다</b> — 슬로우·스턴·밀기·당기기.
        /// 연출이 아니라 <b>안전장치</b>다. 웨이브 종료 조건이 "현상금이 정리될 때까지"
        /// 인데, 붙잡혀서 벽에 못 닿으면 웨이브가 영영 끝나지 않는다.
        ///
        /// <b>피해증가 디버프는 그대로 걸린다.</b> 막는 것은 이동뿐이다 — 전부 막으면
        /// 킹아이스처럼 슬로우와 피해증가를 한 세트로 거는 다이스가 현상금 앞에서
        /// 통째로 무용지물이 된다.
        /// </summary>
        public bool IsBounty { get; private set; }

        /// <summary>
        /// 이 개체가 그 웨이브의 보스인가. <c>MonsterSpawner.SpawnBossMonster</c> 가 켠다.
        ///
        /// 쓰는 곳은 지금 <see cref="WallLeakFormula"/> 하나다 — 보스가 통과하면 일반
        /// 몬스터보다 아프다. <b>스포너의 보스 풀 집합으로 판정하지 않는 이유</b>는 그
        /// 집합이 "이 몸이 보스 프리팹에서 나왔는가" 이고, 현상금이 보스 프리팹을 빌려
        /// 쓰기 때문이다. 필요한 것은 몸이 아니라 역할이다.
        /// </summary>
        public bool IsStageBoss { get; private set; }

        /// <summary>
        /// 이 개체가 화면에 나온 자리. <b>되돌리기 연출이 여기로 되감는다.</b>
        ///
        /// <c>MonsterSpawner</c> 가 위치를 넣는 네 곳에서 같이 적어 준다. 여기서 스스로
        /// <c>transform.position</c> 을 읽지 않는 이유는 <c>OnSpawn</c> 이 <b>위치를 넣기
        /// 전에</b> 불리기 때문이다 — 그 시점의 좌표는 지난번에 죽은 자리다.
        /// </summary>
        public Vector2 SpawnPosition { get; private set; }

        /// <summary>
        /// 이 개체가 화면에 나온 <b>실제 시각</b>. 되감기가 타임라인을 역재생할 때 쓴다.
        ///
        /// <b><c>Time.unscaledTime</c> 이다.</b> 되감기 길이의 기준은 "유저가 실제로 본 시간"
        /// 이고, 게임 시간은 배속에 눌려 그것과 다르다 — 3배속으로 20초를 본 웨이브는
        /// 게임 시간으로 60초다. 여기에 게임 시간을 넣으면 3배속에서만 되감기가 3배 길어진다.
        /// </summary>
        public float SpawnRealTime { get; private set; }

        /// <summary>소환 위치와 시각을 적는다. <c>MonsterSpawner</c> 만 부른다.</summary>
        public void MarkSpawnPosition(Vector2 position)
        {
            SpawnPosition = position;
            SpawnRealTime = Time.unscaledTime;
        }

        private readonly WaitForSeconds poisonDelay = new WaitForSeconds(0.5f);
        private int _baseDefense;
        private int _defenseDownAmount;
        private float _stunnedUntilTime;
        private float _slowUntilTime;
        private float _poisonUntilTime;
        private float _poisonDamageMultiplier = 1f;
        // 중독 틱을 딜미터기에 올릴 다이스. 마지막으로 중독을 건 쪽이다.
        private DiceType _poisonSource = DiceType.Poison;
        private int _poisonDamageTakenBonusPercent;
        private int _stunDamageTakenBonusPercent;
        private float _stunDamageTakenBonusUntil;
        private int _armorBreakDamageTakenBonusPercent;
        private float _armorBreakDamageTakenBonusUntil;
        private int _thunderDamageTakenBonusPercent;
        private float _thunderDamageTakenBonusUntil;
        private int _windDamageTakenBonusPercent;
        private float _windDamageTakenBonusUntil;
        private int _relicDamageTakenBonusPercent;
        private float _relicDamageTakenBonusUntil;
        private Coroutine _defenseDownRoutine;
        private MonsterStatusIcons _statusIcons;
        private Coroutine _poisonRoutine;
        private Coroutine _attackRoutine;
        private Wall _attackWall;
        private Coroutine _pullRoutine;
        private float _pendingPullDistance;
        private float _pullUntilTime;
        private Vector2 _pullCenter;
        private Vector3 _defaultLocalScale;

        // ── 무한의 탑 전용 상태 ──────────────────────────────────────────
        //
        // <b>넷 다 탑에서만 켜진다.</b> 본편 전투에서는 OnSpawn 이 전부 0 으로 깔고
        // 아무도 다시 켜지 않으므로, 아래 분기들은 그 자리에서 빠져나간다.
        //
        // <b>현상금(IsBounty)과 달리 이동 제어 면역을 주지 않는다.</b> 저쪽은 웨이브
        // 종료 조건이 "현상금이 정리될 때까지" 라 붙잡히면 판이 멈추지만, 탑은 종료
        // 조건이 <b>처치 수</b>라 붙잡혀 있어도 언젠가 끝난다. 오히려 둔화·기절이
        // 초고속 돌진 구간의 정답이므로(기획서 6.2) 면역을 주면 그 구간이 죽는다.

        /// <summary>최대 체력. 회복형 적의 회복량 계산에만 쓴다.</summary>
        private int _maxHp;

        /// <summary>
        /// 이 층의 이동 속도 배수. <b>슬로우가 풀렸을 때 되돌릴 기준</b>이기도 하다.
        /// 1 이 기본이고, 본편 전투에서는 언제나 1 이다.
        /// </summary>
        private float _towerSpeedMultiplier = 1f;

        /// <summary>초당 최대 체력의 몇 %를 회복하는가. 0 이면 회복 없음.</summary>
        private float _towerRegenPercentPerSecond;

        /// <summary>회복의 소수점 나머지. 1 미만이 매 프레임 버려지면 회복이 0 이 된다.</summary>
        private float _towerRegenCarry;

        /// <summary>남은 보호막 타격 수. 0 이면 보호막 없음.</summary>
        private int _towerShieldCharges;

        /// <summary>
        /// 죽을 때 갈라질 자식 수. 0 이면 분열하지 않는다.
        /// <c>TowerRunManager</c> 가 이 값으로 <b>부모와 자식을 구분</b>한다 —
        /// 자식은 0 이라 다시 갈라지지 않는다.
        /// </summary>
        public int TowerSplitChildCount { get; private set; }

        /// <summary>분열 자식이 물려받는 체력 비율.</summary>
        public float TowerSplitChildHpRatio { get; private set; }

        private void Awake()
        {
            _defaultLocalScale = transform.localScale;
        }

        /// <summary>
        /// 시간 소스. (5.5) 상태이상 만료·중독 틱은 <b>게임 시간</b>이라 배속의 영향을 받아야
        /// 한다 — 배속 2배에서 슬로우가 두 배로 빨리 풀리는 것이 현행 동작이고 의도다.
        /// 실시간(<c>RealTime</c>)으로 바꾸면 그 동작이 조용히 달라진다.
        ///
        /// 프로퍼티로 두는 것은 나중에 <c>RunState</c> 가 시계를 소유하게 될 때 여기만
        /// 갈아 끼우면 되게 하려는 것이다. 지금은 Unity 시간을 그대로 돌려준다.
        /// </summary>
        private static IClock Clock => UnityClock.Instance;

        public void OnSpawn()
        {
            StopAllCoroutines();
            isAttacking = false;

            _baseDefense = defense;
            _defenseDownAmount = 0;
            _statusIcons?.Clear();
            _stunnedUntilTime = -1f;
            _slowUntilTime = -1f;
            _poisonUntilTime = -1f;
            _poisonDamageMultiplier = 1f;
            _poisonDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusUntil = -1f;
            _armorBreakDamageTakenBonusPercent = 0;
            _armorBreakDamageTakenBonusUntil = -1f;
            _thunderDamageTakenBonusPercent = 0;
            _thunderDamageTakenBonusUntil = -1f;
            _windDamageTakenBonusPercent = 0;
            _windDamageTakenBonusUntil = -1f;
            _relicDamageTakenBonusPercent = 0;
            _relicDamageTakenBonusUntil = -1f;
            RecalculateDefense();

            IsBounty = false;
            IsStageBoss = false;
            _contactDamage = WallLeakFormula.ContactDamagePerSecond;
            ClearTowerTraits();

            battle.Monsters.RegisterMonster(this);
            ApplyMoveSpeed = moveSpeed;
            characterAnimation.PlayAnimation(CharacterState.Run);
            CharacterState = CharacterState.Run;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            isAttacking = false;

            _defenseDownRoutine = null;
            _defenseDownAmount = 0;
            _statusIcons?.Clear();
            _stunnedUntilTime = -1f;
            _slowUntilTime = -1f;
            _poisonUntilTime = -1f;
            _poisonDamageMultiplier = 1f;
            _poisonDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusPercent = 0;
            _stunDamageTakenBonusUntil = -1f;
            _armorBreakDamageTakenBonusPercent = 0;
            _armorBreakDamageTakenBonusUntil = -1f;
            _thunderDamageTakenBonusPercent = 0;
            _thunderDamageTakenBonusUntil = -1f;
            _windDamageTakenBonusPercent = 0;
            _windDamageTakenBonusUntil = -1f;
            _relicDamageTakenBonusPercent = 0;
            _relicDamageTakenBonusUntil = -1f;
            _attackRoutine = null;
            _attackWall = null;
            _pullRoutine = null;
            _poisonRoutine = null;
            _pendingPullDistance = 0f;
            _pullUntilTime = -1f;
            ApplyMoveSpeed = moveSpeed;
            RecalculateDefense();

            // 현상금이 어떤 경로로 사라지든 여기를 지난다 — 벽을 때리고 스스로 물러난
            // 경우, 화면 밖으로 빠진 경우, 씬이 내려간 경우. 그래서 "정리됨" 통보를
            // 벽 처리부가 아니라 <b>여기</b>에 둔다. 경로마다 따로 적으면 하나를
            // 빠뜨렸을 때 웨이브가 끝나지 않는 형태로만 드러난다.
            //
            // 죽어서 사라진 경우에는 TakeDamage 가 이미 NotifyDefeated 를 불러
            // 대상 참조를 비웠으므로 이 호출은 그냥 지나간다 — 보상이 두 번 서지 않는다.
            if (IsBounty)
                battle.Bounty?.NotifyEscaped(this);

            // 탑의 "죽지 않고 사라짐" 통보는 여기 없다. <c>MonsterManager.UnregisterMonster</c>
            // 가 <c>countAsKill == false</c> 하나로 부모·자식을 가리지 않고 처리한다 —
            // 목표가 자식까지 세는 지금은 자식이 사라져도 한 칸이 비기 때문이다.
            IsBounty = false;
            IsStageBoss = false;
            _contactDamage = WallLeakFormula.ContactDamagePerSecond;
            ClearTowerTraits();

            // 여기 ?. 는 새로 넣은 것이 아니라 원래 MonsterManager.Instance 뒤에 붙어 있던
            // 것을 그대로 옮긴 것이다. OnDisable 은 씬을 내릴 때도 도는데 그때는 배틀 스코프가
            // 이미 Clear() 해서 창구가 비어 있을 수 있다 — 파괴 순서가 정해져 있지 않다.
            // 떼면 씬 전환 때마다 터진다.
            battle.Monsters?.UnregisterMonster(this, false);
        }

        void Update()
        {
            UpdateTimedStates();

            if (IsStunned())
                return;

            if (IsBeingPulled())
                return;

            if (!isAttacking)
            {
                transform.Translate(Vector2.down * ApplyMoveSpeed * Time.deltaTime);

                if (Mathf.Abs(transform.position.x) > 10f || Mathf.Abs(transform.position.y) > 10f)
                    battle.Spawner.PoolMonster(this);
            }
        }

        /// <param name="isFixed">true 면 방어력 · 받는 피해 증가를 모두 건너뛰고 <paramref name="dmg"/> 그 숫자만큼만 들어간다(시너지 고정 피해).</param>
        public int TakeDamage(int dmg, bool isFixed = false)
        {
            if (!gameObject.activeInHierarchy || CharacterState == CharacterState.Dead)
                return 0;

            if (dmg <= 0)
                return 0;

            // 산술은 전부 OJ.Core.IncomingDamageFormula 로 내려갔다. 여기 남은 일은 "엔진과 싱글톤에서
            // 값을 긁어모아 넘기고, 그 결과로 상태를 바꾸는 것"뿐이다.
            //
            // 원본은 방어력 감쇄(armor/damageMultiplier)를 CleanupExpiredDamageBonuses() **앞에서**
            // 계산했다. 한 함수로 합치면서 그 순서가 뒤집혔는데 안전하다 — cleanup 이 쓰는 것은
            // 6개 percent 필드와 5개 until 필드뿐이고 defense / _baseDefense / _defenseDownAmount 는
            // 한 글자도 건드리지 않는다. defense 를 바꾸는 것은 RecalculateDefense 하나이고 그것을
            // 부르는 곳은 OnSpawn / OnDisable / ApplyDefenseDown / CoApplyDefenseDown /
            // SetCombatStats 인데 cleanup 은 그중 무엇도 부르지 않는다.
            CleanupExpiredDamageBonuses();

            // 단축평가를 푸는 대신 판정을 여기 남긴다. DiceMetaDataProvider.HasKing*DamageBonus() 는
            // DiceTypeStarManager / DiceLevelManager 의 MonoSingleton getter 를 타는데, 그 getter 는
            // 읽기가 아니라 FindObjectOfType + 생성 시도다(Utils/Singleton.cs). RelicManager 쪽도
            // Database 프로퍼티가 지연 로드를 유발한다. 즉 "미리 평가해서 bool 로 넘기기"는
            // 값은 같아도 관측 가능한 변화라 하지 않는다.
            //
            // IsSlowed() 는 원본에서 두 번 불렸다. 같은 프레임의 Time.time 은 고정이고 그 사이
            // _slowUntilTime 을 건드리는 것이 없어 값이 같으므로 한 번만 부른다.
            bool isSlowed = IsSlowed();
            int stateBonusPercent = IncomingDamageFormula.StateBonusPercent(
                isSlowed && DiceMetaDataProvider.HasKingIceDamageBonus(),
                IsPoisoned() && DiceMetaDataProvider.HasKingPoisonDamageBonus(),
                isSlowed && RelicManager.Instance != null ? RelicManager.Instance.GetSlowDamageTakenBonusPercent() : 0);

            int totalBonusPercent = IncomingDamageFormula.TotalBonusPercent(
                _poisonDamageTakenBonusPercent,
                _stunDamageTakenBonusPercent,
                _armorBreakDamageTakenBonusPercent,
                _thunderDamageTakenBonusPercent,
                _windDamageTakenBonusPercent,
                _relicDamageTakenBonusPercent,
                stateBonusPercent);
            int appliedDamage = isFixed ? dmg : IncomingDamageFormula.AppliedDamage(dmg, defense, totalBonusPercent);

            // 탑의 보호막. <b>피해가 아니라 타격 한 번을 통째로 먹는다.</b>
            // 기획서 6.2 가 보호막의 대응으로 "다단 공격" 을 적어 둔 것이 이 뜻이다 —
            // 흡수량으로 만들면 한 방이 센 다이스가 그대로 뚫어 다단의 자리가 없어진다.
            //
            // <b>피해 계산이 다 끝난 뒤에 가로챈다.</b> 앞에 두면 방어력 감소·상태
            // 피해증가가 보호막에 막힌 타격에서는 소모되지 않은 것이 되어, 같은 다이스가
            // 보호막 앞에서만 다르게 동작한다.
            if (_towerShieldCharges > 0)
            {
                _towerShieldCharges--;
                ShowShieldAbsorb();
                return 0;
            }

            _hp -= appliedDamage;
            if (_hp <= 0)
            {
                if (CharacterState != CharacterState.Dead)
                {
                    bool wasPoisoned = IsPoisoned();
                    Vector3 deathPosition = transform.position;
                    CharacterState = CharacterState.Dead;
                    _statusIcons?.Clear();
                    StopAllCoroutines();
                    // 벽을 여기서 넘긴다. 예전에는 EquipmentManager 가 안에서
                    // GameManager.Instance.wall 을 직접 붙잡았는데, 영구 메타 서비스가
                    // 전투 씬 오브젝트를 이름으로 찾는 거꾸로 된 방향이었다. (8.3a)
                    // GameManager 를 창구로 바꾸면서도 != null 검사는 남긴다. 지우면
                    // "벽 없이 죽인 경우"의 동작이 달라지고, 이 트랜치는 참조를 얻는
                    // 경로만 바꾸는 자리다.
                    EquipmentManager.Instance?.OnMonsterKilled(
                        battle.Game != null ? battle.Game.wall : null);
                    RelicManager.Instance?.OnMonsterKilled(this, wasPoisoned, deathPosition);

                    // 되돌리기 연출이 되감을 대상. <b>PoolMonster 보다 먼저여야 한다</b> —
                    // 저것이 SetActive(false) 를 부르면 OnDisable 이 스프라이트를 포함한
                    // 상태를 지워, 무엇이 어떻게 죽었는지 읽을 수 없게 된다.
                    // (바로 아래 분열 통보가 같은 이유로 같은 자리에 있다.)
                    battle.Rewind.RecordDeath(this, deathPosition);
                    // 현상금은 웨이브 목표 수에 들어가지 않으므로 처치 수로 세지 않는다.
                    // true 로 넘기면 웨이브가 한 마리 일찍 끝난다.
                    // 분열을 <b>처치를 세기 전에</b> 통보한다. 순서가 중요하다 —
                    // 목표 처치 수는 "이미 나온 것" 만 세므로, 자식이 목표에 더해지기
                    // 전에 부모의 처치가 세어지면 <b>그 순간 목표를 채워 층이 끝난다.</b>
                    // (부모 1마리·자식 1마리짜리 층이면 1/1 로 즉시 클리어가 된다.)
                    //
                    // 풀에 돌려보내기 전이기도 해야 한다. PoolMonster 가 부르는
                    // OnDisable 이 위치·자식 수를 지우기 때문이다.
                    if (TowerSplitChildCount > 0)
                    {
                        battle.Tower.NotifyMonsterDefeated(this, deathPosition);

                        // <b>여기서 반드시 0 으로 만든다.</b> 아래 PoolMonster 가 돌리는
                        // OnDisable 은 "죽지 않고 사라졌다" 경로를 타는데, 그때 이 값이
                        // 남아 있으면 같은 부모가 죽음과 이탈로 두 번 세어진다.
                        TowerSplitChildCount = 0;
                    }

                    if (IsBounty)
                    {
                        battle.Bounty?.NotifyDefeated(this, deathPosition);
                        battle.Monsters.UnregisterMonster(this, false);
                    }
                    else
                    {
                        battle.Monsters.UnregisterMonster(this, true);

                        // 영혼 보너스: 죽은 자리에서 영혼이 날아간다. 현상금은 따로 보상이 있어 세지 않는다.
                        battle.Game.SoulBonus?.OnMonsterKilled(deathPosition);
                    }

                    // <b>분기 밖이어야 한다.</b> 위 UnregisterMonster 의 countAsKill 은
                    // "웨이브 목표 수에 넣을 것인가" 이고 현상금은 거기서 빠진다 —
                    // 미션의 "적 처치" 는 그 축이 아니라 <b>실제로 죽은 것</b>이므로
                    // 현상금도 함께 센다(현상금 처치 업적은 BountyManager 가 따로 올린다).
                    MissionManager.Instance?.Notify(MissionAction.MonsterKill);

                    // 시너지 전투계(처치·보스·현상금). 발동은 다음 프레임에 하므로 여기서 재진입하지 않는다.
                    battle.Game?.Synergy?.NotifyKilled(this);

                    battle.Spawner.PoolMonster(this);
                }
            }

            return appliedDamage;
        }

        void OnTriggerEnter2D(Collider2D col)
        {
            if (col.CompareTag("Wall") && !isAttacking)
            {
                StartAttack(col.GetComponent<Wall>());
            }
        }

        void OnTriggerExit2D(Collider2D col)
        {
            if (!isAttacking || col == null || !col.CompareTag("Wall"))
                return;

            Wall wall = col.GetComponent<Wall>();
            if (wall == null || wall == _attackWall)
                StopAttack();
        }

        /// <summary>
        /// 이 다이스가 상태이상을 걸고 있음을 머리 위 아이콘으로 알린다. <b>표시만 한다</b> — 효과는 아래 Apply* 가 한다.
        /// <paramref name="source"/> 가 <c>DiceType.Max</c>(출처 모름)이면 아무것도 띄우지 않는다.
        /// </summary>
        private void MarkStatus(DiceType source, float duration)
        {
            if (source == DiceType.Max)
                return;

            if (_statusIcons == null)
                _statusIcons = MonsterStatusIcons.Attach(this);

            _statusIcons.Add(source, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplySlow(float duration = 2f, float multiplier = 0.8f, DiceType source = DiceType.Max)
        {
            if (!IsAlive)
                return;

            MarkStatus(source, duration);

            // <b>상태는 걸고 속도만 안 깎는다.</b> 둘을 함께 막으면 IsSlowed() 가 false 로
            // 남아 킹아이스의 피해증가(TakeDamage 의 stateBonusPercent)까지 같이 죽는다 —
            // 현상금 앞에서만 특정 다이스가 조용히 약해지는, 화면으로는 알 수 없는 손실이다.
            _slowUntilTime = Mathf.Max(_slowUntilTime, Clock.GameTime + Mathf.Max(0.1f, duration));

            if (IsBounty)
                return;

            ApplyMoveSpeed = Mathf.Max(moveSpeed * 0.2f, ApplyMoveSpeed * Mathf.Clamp(multiplier, 0.1f, 1f));
        }

        public void ApplyPoison(float duration = 4f, float damageMultiplier = 1f, DiceType source = DiceType.Poison)
        {
            if (!IsAlive)
                return;

            _poisonSource = source;
            MarkStatus(source, duration);

            _poisonUntilTime = Mathf.Max(_poisonUntilTime, Clock.GameTime + Mathf.Max(0.1f, duration));
            _poisonDamageMultiplier = Mathf.Max(_poisonDamageMultiplier, Mathf.Max(0.1f, damageMultiplier));

            if (_poisonRoutine == null)
                _poisonRoutine = StartCoroutine(PlayPoison());
        }

        public void ApplyStun(float duration, DiceType source = DiceType.Max)
        {
            if (!IsAlive)
                return;

            // 스턴은 Update 를 통째로 멈춘다. 현상금에 걸리면 벽까지 가는 시간이
            // 다이스 구성에 따라 무한정 늘어나고, 그것이 곧 안 끝나는 웨이브다.
            // 피해증가(ApplyStunDamageTakenBonus)는 별도 경로라 그대로 걸린다.
            if (IsBounty)
                return;

            float validDuration = Mathf.Max(0.1f, duration);
            _stunnedUntilTime = Mathf.Max(_stunnedUntilTime, Clock.GameTime + validDuration);
            MarkStatus(source, validDuration);
        }

        public void ApplyPoisonDamageTakenBonus(int percent)
        {
            _poisonDamageTakenBonusPercent = Mathf.Max(_poisonDamageTakenBonusPercent, Mathf.Max(0, percent));
            _poisonUntilTime = Mathf.Max(_poisonUntilTime, Clock.GameTime + 4f);
        }

        public void ApplyStunDamageTakenBonus(int percent, float duration)
        {
            _stunDamageTakenBonusPercent = Mathf.Max(_stunDamageTakenBonusPercent, Mathf.Max(0, percent));
            _stunDamageTakenBonusUntil = Mathf.Max(_stunDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyArmorBreakDamageTakenBonus(int percent, float duration)
        {
            _armorBreakDamageTakenBonusPercent = Mathf.Max(_armorBreakDamageTakenBonusPercent, Mathf.Max(0, percent));
            _armorBreakDamageTakenBonusUntil = Mathf.Max(_armorBreakDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyThunderDamageTakenBonus(int percent, float duration, DiceType source = DiceType.Max)
        {
            MarkStatus(source, duration);
            _thunderDamageTakenBonusPercent = Mathf.Max(_thunderDamageTakenBonusPercent, Mathf.Max(0, percent));
            _thunderDamageTakenBonusUntil = Mathf.Max(_thunderDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyWindDamageTakenBonus(int percent, float duration, DiceType source = DiceType.Max)
        {
            MarkStatus(source, duration);
            _windDamageTakenBonusPercent = Mathf.Max(_windDamageTakenBonusPercent, Mathf.Max(0, percent));
            _windDamageTakenBonusUntil = Mathf.Max(_windDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyRelicDamageTakenBonus(int percent, float duration)
        {
            _relicDamageTakenBonusPercent = Mathf.Max(_relicDamageTakenBonusPercent, Mathf.Max(0, percent));
            _relicDamageTakenBonusUntil = Mathf.Max(_relicDamageTakenBonusUntil, Clock.GameTime + Mathf.Max(0.1f, duration));
        }

        public void ApplyDefenseDown(float duration, int percent, DiceType source = DiceType.Max)
        {
            if (!IsAlive)
                return;

            int validPercent = Mathf.Max(0, percent);
            int validAmount = Mathf.RoundToInt(_baseDefense * (validPercent * 0.01f));
            if (validAmount <= 0)
                return;

            _defenseDownAmount = Mathf.Max(_defenseDownAmount, validAmount);
            RecalculateDefense();
            MarkStatus(source, duration);

            if (_defenseDownRoutine != null)
                StopCoroutine(_defenseDownRoutine);

            _defenseDownRoutine = StartCoroutine(CoApplyDefenseDown(Mathf.Max(0.1f, duration)));
        }

        public void PushBy(Vector2 direction, float distance)
        {
            if (!IsAlive)
                return;

            // 밀기는 현상금을 위로 되돌린다. 윈드 장판 위에 서 있는 동안 영영
            // 벽에 못 닿는 형태가 되므로 무효로 둔다.
            if (IsBounty)
                return;

            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.position += (Vector3)(direction.normalized * Mathf.Max(0f, distance));
        }

        public void PullTowards(Vector2 center, float distance)
        {
            if (!IsAlive)
                return;

            if (IsBounty)
                return;

            Vector2 direction = center - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            transform.position += (Vector3)(direction.normalized * Mathf.Max(0f, distance));
        }

        public void AddSmoothPull(Vector2 center, float distance, float duration, DiceType source = DiceType.Max)
        {
            if (!IsAlive)
                return;

            // 토네이도는 당기는 동안 Update 의 이동을 통째로 건너뛴다(IsBeingPulled).
            // 현상금에 걸리면 하강이 멈추므로 여기서 끊는다.
            if (IsBounty)
                return;

            float addDistance = Mathf.Max(0f, distance);
            if (addDistance <= 0.0001f)
                return;

            // 당기는 시간은 0.x초라 그대로 띄우면 깜빡이고 끝난다 — 눈에 읽히도록 최소 1초는 남긴다.
            MarkStatus(source, Mathf.Max(1f, duration));

            _pullCenter = center;
            _pendingPullDistance += addDistance;
            _pullUntilTime = Mathf.Max(_pullUntilTime, Clock.GameTime + Mathf.Max(0.01f, duration));

            if (_pullRoutine == null)
                _pullRoutine = StartCoroutine(CoSmoothPull());
        }

        IEnumerator PlayPoison()
        {
            while (_hp > 0 && Clock.GameTime < _poisonUntilTime)
            {
                // 남은 체력의 1% x 배수. 10% 였다가 1% 로 대폭 낮췄다(체력이 작으면 최소 1).
                int intdamage = Mathf.CeilToInt((_hp * 0.01f) * _poisonDamageMultiplier);
                if (intdamage <= 0)
                    intdamage = 1;

                GameObject dtObj = battle.DamageTexts.GetDamageText();
                dtObj.transform.position = transform.position;
                dtObj.transform.ResetLocalZ();
                Color typeColor = DiceMetaDataProvider.GetColor(DiceType.Poison);

                int appliedDamage = TakeDamage(intdamage);
                // 중독 틱도 딜미터기에 올린다. 직접 타격(AttackContent.HitMonster)만 세면
                // 독 다이스의 몫이 대부분 빠진다.
                battle.Contribution?.Record(_poisonSource, appliedDamage);
                if (appliedDamage > 0)
                    dtObj.GetComponent<DamageText>().SetText(appliedDamage, typeColor);
                else
                    dtObj.SetActive(false);

                yield return poisonDelay;
            }

            _poisonDamageMultiplier = 1f;
            _poisonDamageTakenBonusPercent = 0;
            _poisonRoutine = null;
        }

        /// <summary>
        /// 벽에 닿았다. <b>종류와 무관하게 벽 앞에 남아 <see cref="WallLeakFormula.ContactDamagePerSecond"/> 씩 매초 때린다</b>
        /// (2026-10-20 사용자 지시 — 사라지지 않고 초당 2, 현상금도 같다). 잡을 때까지 벽 앞에 머물고, 그동안 다이스의 표적으로 남는다.
        /// 현상금은 웨이브 목표 수에 들지 않으므로 벽 앞에 남아도 웨이브 종료를 막지 않는다.
        ///
        /// 기절 중에는 때리지 않는다. 밀려나서 벽에서 떨어지면 <c>OnTriggerExit2D</c> 가 <see cref="StopAttack"/> 을 부르고,
        /// 다시 걸어와 닿으면 새로 시작한다. 벽이 0 이 되면 멈춘다.
        /// </summary>
        IEnumerator AttackWall(Wall wall)
        {
            while (wall != null && wall.CurrentHp > 0)
            {
                if (IsStunned())
                {
                    yield return null;
                    continue;
                }

                int damage = SynergyBuffs.ApplyWallGuard(_contactDamage);
                wall.TakeDamage(damage);
                battle.Game?.Synergy?.NotifyWallHit();

                GameObject dtObj = battle.DamageTexts.GetDamageText();
                dtObj.transform.position = transform.position;
                dtObj.transform.ResetLocalZ();
                dtObj.GetComponent<DamageText>().SetText(damage * -1, Color.red);

                yield return contactTickDelay;
            }

            StopAttack();
        }

        private static readonly WaitForSeconds contactTickDelay = new WaitForSeconds(1f);

        IEnumerator CoApplyDefenseDown(float duration)
        {
            yield return new WaitForSeconds(duration);

            _defenseDownAmount = 0;
            RecalculateDefense();
            _defenseDownRoutine = null;
        }

        private void RecalculateDefense()
        {
            defense = _baseDefense - _defenseDownAmount;
        }

        private IEnumerator CoSmoothPull()
        {
            while (IsAlive && _pendingPullDistance > 0.0001f)
            {
                float remainTime = Mathf.Max(0.01f, _pullUntilTime - Clock.GameTime);
                float step = _pendingPullDistance * (Time.deltaTime / remainTime);
                step = Mathf.Min(step, _pendingPullDistance);
                if (step > 0f)
                    PullTowards(_pullCenter, step);

                _pendingPullDistance -= step;
                yield return null;
            }

            _pendingPullDistance = 0f;
            _pullUntilTime = -1f;
            _pullRoutine = null;
        }

        private bool IsStunned()
        {
            return Clock.GameTime < _stunnedUntilTime;
        }

        public bool IsSlowed()
        {
            return IncomingDamageFormula.IsStateActive(Clock.GameTime, _slowUntilTime);
        }

        public bool IsPoisoned()
        {
            return IncomingDamageFormula.IsStateActive(Clock.GameTime, _poisonUntilTime);
        }

        /// <summary>
        /// 만료된 상태 피해증가를 0 으로 되돌린다. <b>판정만</b> OJ.Core 로 내려갔다 —
        /// 필드 write 는 여기 남는다(순수 함수가 될 수 없는 부분이다).
        ///
        /// 원본은 Time.time 을 6번 읽었다. 같은 프레임 안에서 Time.time 은 고정값이고 이 메서드는
        /// 중간에 yield 하지 않으므로 한 번 읽어 쓰는 것과 결과가 같다.
        ///
        /// poison 만 전용 until 필드가 없고 IsPoisoned() 가 쓰는 _poisonUntilTime 을 공유한다.
        /// 그래서 (a) poison 만 until 을 -1f 로 되돌리지 않고, (b) ApplyPoisonDamageTakenBonus 가
        /// 중독 지속시간 자체를 Time.time + 4f 로 연장하는 부작용을 갖는다. 나머지 5종에는 없다.
        /// 의도인지 사고인지 코드로는 판정할 수 없어 <b>그대로 보존한다.</b>
        /// </summary>
        private void CleanupExpiredDamageBonuses()
        {
            float now = Clock.GameTime;

            if (IncomingDamageFormula.IsBonusExpired(now, _poisonUntilTime))
                _poisonDamageTakenBonusPercent = 0;

            if (IncomingDamageFormula.IsBonusExpired(now, _stunDamageTakenBonusUntil))
            {
                _stunDamageTakenBonusPercent = 0;
                _stunDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _armorBreakDamageTakenBonusUntil))
            {
                _armorBreakDamageTakenBonusPercent = 0;
                _armorBreakDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _thunderDamageTakenBonusUntil))
            {
                _thunderDamageTakenBonusPercent = 0;
                _thunderDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _windDamageTakenBonusUntil))
            {
                _windDamageTakenBonusPercent = 0;
                _windDamageTakenBonusUntil = -1f;
            }

            if (IncomingDamageFormula.IsBonusExpired(now, _relicDamageTakenBonusUntil))
            {
                _relicDamageTakenBonusPercent = 0;
                _relicDamageTakenBonusUntil = -1f;
            }
        }

        private bool IsBeingPulled()
        {
            return _pullRoutine != null && Clock.GameTime < _pullUntilTime;
        }

        private void UpdateTimedStates()
        {
            TickTowerRegen();

            // 현상금은 여기서 빠진다. 이 줄은 "슬로우가 풀렸으면 원래 속도로" 인데,
            // 현상금의 느린 걸음은 슬로우가 아니라 <b>기본 속도</b>다. 걸러 내지 않으면
            // _slowUntilTime 이 -1 인 첫 프레임에 조건이 참이 되어 느린 걸음이
            // 그 자리에서 지워진다 — 화면에서는 "왜 안 느리지" 로만 보인다.
            if (IsBounty)
                return;

            // 탑의 속도 배수도 슬로우가 아니라 <b>기본 속도</b>다. 아래 줄이
            // "슬로우가 풀렸으면 moveSpeed 로" 되돌리는데, 그러면 초고속 돌진 구간의
            // 몬스터가 한 번 둔화됐다 풀리는 순간 <b>영영 느려진 채</b>로 남는다.
            // 그래서 되돌릴 기준을 프리팹 속도가 아니라 이 층의 속도로 바꾼다.
            float restoreSpeed = moveSpeed * _towerSpeedMultiplier;
            if (Clock.GameTime >= _slowUntilTime && ApplyMoveSpeed != restoreSpeed)
                ApplyMoveSpeed = restoreSpeed;
        }

        /// <summary>
        /// 회복형 적의 체력 회복. 기획서 6.2 "일정 시간마다 체력 회복 → 순간 화력".
        ///
        /// <b>소수점을 이월한다.</b> 초당 4% 라도 체력이 작으면 한 프레임 회복량이
        /// 1 미만이고, 그때마다 버리면 <c>RoundToInt</c> 가 0 을 돌려줘 회복이 아예
        /// 일어나지 않는다 — 체력이 작은 층에서만 기믹이 사라지는, 눈으로는
        /// 못 찾는 종류의 사고다.
        ///
        /// <b>최대 체력을 넘지 않는다.</b> 넘게 두면 "잡을 뻔했다" 가 아니라
        /// "처음보다 더 단단해졌다" 가 되어 결과 화면의 잔여 체력이 100%를 넘는다.
        /// </summary>
        private void TickTowerRegen()
        {
            if (_towerRegenPercentPerSecond <= 0f || !IsAlive || _hp >= _maxHp)
                return;

            _towerRegenCarry += _maxHp * (_towerRegenPercentPerSecond * 0.01f) * Time.deltaTime;
            if (_towerRegenCarry < 1f)
                return;

            int healed = Mathf.FloorToInt(_towerRegenCarry);
            _towerRegenCarry -= healed;
            _hp = Mathf.Min(_maxHp, _hp + healed);
        }

        /// <summary>
        /// 보호막이 한 대를 막았다. <b>남은 횟수를 숫자로 띄운다.</b>
        ///
        /// 아무것도 안 띄우면 화면에서는 "공격이 안 먹는다" 로만 보이고,
        /// 그것은 기획서 8.2 가 금지한 "실패 원인을 알 수 없는" 상태와 같은 종류다.
        /// 3 → 2 → 1 로 줄어드는 숫자가 보이면 "몇 대 더 때리면 된다" 가 읽힌다.
        /// </summary>
        private void ShowShieldAbsorb()
        {
            GameObject dtObj = battle.DamageTexts.GetDamageText();
            dtObj.transform.position = transform.position;
            dtObj.transform.ResetLocalZ();
            dtObj.GetComponent<DamageText>().SetText(_towerShieldCharges, ShieldTextColor);
        }

        private static readonly Color ShieldTextColor = new Color(0.55f, 0.85f, 1f, 1f);

        private void StartAttack(Wall wall)
        {
            if (wall == null || isAttacking)
                return;

            isAttacking = true;
            _attackWall = wall;
            _attackRoutine = StartCoroutine(AttackWall(wall));
        }

        private void StopAttack()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            _attackWall = null;
            isAttacking = false;
        }

        /// <summary>남은 체력 백분율(올림, 살아 있으면 최소 1). 패배 결과창이 "보스 체력 N% 남김" 을 보여 주는 데 쓴다.</summary>
        public int HpPercent => _maxHp > 0 && _hp > 0 ? Mathf.Clamp(Mathf.CeilToInt(100f * _hp / _maxHp), 1, 100) : 0;

        public void SetHp(int hp)
        {
            _hp = hp;
        }

        /// <summary>
        /// 이 개체를 현상금으로 만든다. <b><c>OnSpawn</c> 뒤에 불러야 한다</b> —
        /// <c>OnSpawn</c> 이 <c>ApplyMoveSpeed</c> 를 원래 속도로 되돌리므로
        /// 먼저 부르면 느린 걸음이 그 자리에서 지워진다.
        /// </summary>
        public void ConfigureAsBounty(float moveSpeedMultiplier)
        {
            IsBounty = true;
            AttachRoleOutline();

            // moveSpeed 자체는 건드리지 않는다. 그것은 프리팹 값이고, 풀에서 재사용될 때
            // OnSpawn 이 ApplyMoveSpeed 를 여기로 되돌리는 기준점이다 — 깎아 두면
            // 같은 인스턴스가 일반 보스로 나왔을 때 느려진 채로 나온다.
            ApplyMoveSpeed = Mathf.Max(0.05f, moveSpeed * Mathf.Max(0.05f, moveSpeedMultiplier));
        }

        /// <summary>
        /// 이 개체를 그 웨이브의 보스로 표시한다. <b><c>OnSpawn</c> 뒤에 불러야 한다</b> —
        /// <c>OnSpawn</c> 이 이 표시를 끈다(<see cref="ConfigureAsBounty"/> 와 같은 제약).
        ///
        /// 속도·체력·크기는 건드리지 않는다. 그것들은 스포너가
        /// <c>SetCombatStats</c> · <c>ConfigureStageSpeed</c> 로 이미 세웠고, 여기서
        /// 또 만지면 "보스 스탯을 세우는 곳" 이 둘이 된다.
        /// </summary>
        public void ConfigureAsStageBoss(int contactDamagePerSecond = WallLeakFormula.ContactDamagePerSecond)
        {
            IsStageBoss = true;
            _contactDamage = Mathf.Max(WallLeakFormula.ContactDamagePerSecond, contactDamagePerSecond);
            AttachRoleOutline();
        }

        /// <summary>보스·현상금에 빨간 테두리를 붙인다. 켜고 끄는 것은 테두리가 역할을 읽어 스스로 한다.</summary>
        private void AttachRoleOutline()
        {
            if (characterAnimation != null)
                MonsterOutline.Attach(this, characterAnimation.spriteRenderer);
        }

        /// <summary>벽 앞에서 초당 주는 피해. 풀에서 재사용될 때 <c>OnSpawn</c> 이 기본값으로 되돌린다.</summary>
        private int _contactDamage = WallLeakFormula.ContactDamagePerSecond;

        /// <summary>
        /// 이 개체를 탑의 몬스터로 만든다. <b><c>OnSpawn</c> · <c>SetCombatStats</c> 뒤에
        /// 불러야 한다</b> — <c>OnSpawn</c> 이 여기서 세우는 값을 전부 지우고,
        /// 속도 배수는 <c>ApplyMoveSpeed</c> 를 덮으므로 순서가 뒤집히면 사라진다.
        /// (<see cref="ConfigureAsBounty"/> 와 같은 제약이고 같은 이유다.)
        /// </summary>
        public void ConfigureAsTowerMonster(TowerMonsterSpec spec)
        {
            _towerSpeedMultiplier = Mathf.Max(0.05f, spec.SpeedMultiplier);
            _towerRegenPercentPerSecond = Mathf.Max(0f, spec.RegenPercentPerSecond);
            _towerRegenCarry = 0f;
            _towerShieldCharges = Mathf.Max(0, spec.ShieldHitCharges);
            TowerSplitChildCount = Mathf.Max(0, spec.SplitChildCount);
            TowerSplitChildHpRatio = Mathf.Clamp01(spec.SplitChildHpRatio);

            // moveSpeed 자체는 건드리지 않는다. 그것은 프리팹 값이고, 풀에서 재사용될 때
            // OnSpawn 이 ApplyMoveSpeed 를 여기로 되돌리는 기준점이다 — 깎아 두면
            // 같은 인스턴스가 본편 몬스터로 나왔을 때 느려진 채로 나온다.
            ApplyMoveSpeed = moveSpeed * _towerSpeedMultiplier;
        }

        /// <summary>
        /// 본편 스테이지의 속도 배수. <b><c>OnSpawn</c> 뒤에 불러야 한다.</b>
        /// 탑의 속도 배수와 같은 칸을 쓴다 — 둘 다 "슬로우가 아니라 기본 속도" 라서,
        /// 슬로우가 풀릴 때 되돌아갈 기준이 이 값이어야 한다(<c>UpdateTimedStates</c>).
        /// </summary>
        public void ConfigureStageSpeed(float speedMultiplier)
        {
            _towerSpeedMultiplier = Mathf.Max(0.05f, speedMultiplier);
            ApplyMoveSpeed = moveSpeed * _towerSpeedMultiplier;
        }

        /// <summary>
        /// 분열 자식으로 만든다. <b>자식은 다시 갈라지지 않는다</b> —
        /// 갈라지면 마리 수가 기하급수로 늘어 목표 처치 수를 미리 셀 수 없게 되고,
        /// 그 순간 웨이브 종료 판정이 통째로 무너진다.
        ///
        /// 보호막과 회복도 물려받지 않는다. 자식까지 그것을 들면 "분열 + 보호막" 구간이
        /// 조합이 아니라 같은 대응을 세 배로 요구하는 벽이 된다.
        /// </summary>
        public void ConfigureAsTowerSplitChild(float speedMultiplier)
        {
            _towerSpeedMultiplier = Mathf.Max(0.05f, speedMultiplier);
            _towerRegenPercentPerSecond = 0f;
            _towerRegenCarry = 0f;
            _towerShieldCharges = 0;
            TowerSplitChildCount = 0;
            TowerSplitChildHpRatio = 0f;

            ApplyMoveSpeed = moveSpeed * _towerSpeedMultiplier;
        }

        /// <summary>
        /// 탑 상태를 전부 끈다. <c>OnSpawn</c> 과 <c>OnDisable</c> 이 부른다 —
        /// 풀에서 재사용되는 개체가 지난 층의 보호막을 들고 나오지 않게 하는 자리다.
        /// </summary>
        private void ClearTowerTraits()
        {
            _towerSpeedMultiplier = 1f;
            _towerRegenPercentPerSecond = 0f;
            _towerRegenCarry = 0f;
            _towerShieldCharges = 0;
            TowerSplitChildCount = 0;
            TowerSplitChildHpRatio = 0f;
        }

        public void SetCombatStats(int hp, int baseDefenseValue, float scaleMultiplier = 1f)
        {
            _hp = Mathf.Max(1, hp);

            // 회복형 적의 상한. 여기서만 세운다 — SetHp 는 유물·치트가 부르는 경로라
            // 거기서도 세우면 "최대 체력" 의 뜻이 호출부마다 달라진다.
            _maxHp = _hp;

            _baseDefense = Mathf.Max(0, baseDefenseValue);
            _defenseDownAmount = 0;
            RecalculateDefense();

            if (_defaultLocalScale == Vector3.zero)
                _defaultLocalScale = Vector3.one;

            transform.localScale = _defaultLocalScale * Mathf.Max(0.1f, scaleMultiplier);
        }
    }
}
