using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;

namespace OJ.Tower
{
    /// <summary>
    /// 무한의 탑 한 판의 두뇌. <b>MonoBehaviour 가 아니다</b> —
    /// <c>BountyManager</c> 와 같은 이유이고 같은 방식으로 태어난다(배틀 스코프가
    /// 코드로 만든다). 씬 YAML 을 건드리지 않기 위해서다(AGENTS 절대 규칙 3).
    ///
    /// <b>본편 전투와의 경계가 이 클래스다.</b> <c>GameManager</c>·<c>MonsterSpawner</c>·
    /// <c>Monster</c> 에 들어간 탑 분기는 전부 <c>battle.Tower.IsActive</c> 한 줄로
    /// 시작해서 여기로 넘어온다. 분기를 저쪽에 흩어 두면 본편을 고칠 때마다 탑이
    /// 조용히 깨지고, 그 반대도 마찬가지다.
    ///
    /// <b>로비에서는 존재하되 비활성이다.</b> <see cref="IsActive"/> 가 false 이면
    /// 이 객체의 어떤 메서드도 아무 일을 하지 않는다 — 전투 씬 밖에서 호출되는
    /// 경로가 실제로 있고(다이얼로그 정리 등), 거기서 터지면 안 된다.
    /// </summary>
    [Preserve]
    public sealed class TowerRunManager
    {
        private readonly IBattleRefs battle;

        /// <summary>이번 판의 층 계획. 탑이 아니면 null 이다.</summary>
        public TowerFloorPlan Plan { get; private set; }

        /// <summary>이번 판의 편성. 탑이 아니면 null 이다.</summary>
        public TowerLoadout Loadout { get; private set; }

        /// <summary>지금 전투가 탑인가. <b>모든 분기의 유일한 조건</b>이다.</summary>
        public bool IsActive => Plan != null;

        /// <summary>
        /// 이 층에서 <b>끝까지 가면</b> 잡게 될 총 마리 수(분열 자식 포함).
        /// 편성 화면의 미리보기가 쓴다 — 전투 중 목표는 아래
        /// <see cref="EffectiveKillTarget"/> 다.
        /// </summary>
        public int PredictedKillTarget => Plan != null ? Plan.KillTarget : 0;

        /// <summary>
        /// 지금까지 실제로 갈라져 나온 분열 자식 수. 진단 로그가 쓴다 —
        /// 목표가 안 맞을 때 "부모가 안 죽은 것" 과 "자식이 안 나온 것" 을 가른다.
        /// </summary>
        private int childrenSpawned;

        /// <summary>지금까지 흘러간 시간(초). 판이 끝나면 멈춘다.</summary>
        public float ElapsedSeconds { get; private set; }

        private bool timerRunning;

        /// <summary>이번 판에 실제로 소환된 일반 몬스터 수. 스포너가 여기를 보고 멈춘다.</summary>
        public int SpawnedCount { get; private set; }

        /// <summary>이 층에서 아직 소환할 몬스터가 남았는가.</summary>
        public bool HasMoreToSpawn => IsActive && SpawnedCount < Plan.MonsterCount;

        /// <summary>
        /// 남은 적 체력의 합. 실패했을 때 "적 체력 12% 잔여" 를 만드는 분자다.
        /// <b>소환되지 않은 몬스터도 센다</b> — 안 세면 초반에 실패했을 때 잔여율이
        /// 0 에 가깝게 나와 "거의 다 잡았다" 는 거짓말이 된다.
        /// </summary>
        public long RemainingHp
        {
            get
            {
                if (!IsActive)
                    return 0;

                long unspawned = (long)(Plan.MonsterCount - SpawnedCount) * Plan.MonsterHp;

                long alive = 0;
                List<Monster> monsters = battle.Monsters.activeMonsters;
                for (int i = 0; i < monsters.Count; i++)
                {
                    Monster monster = monsters[i];
                    if (monster != null && monster._hp > 0)
                        alive += monster._hp;
                }

                // 아직 갈라지지 않은 분열 자식도 남은 적이다. 부모가 살아 있는 만큼
                // 앞으로 나올 자식이 있다.
                long pendingChildren = 0;
                if (Plan.SplitChildCount > 0)
                {
                    int parentsLeft = Plan.MonsterCount - splitParentsResolved;
                    if (parentsLeft > 0)
                        pendingChildren = (long)parentsLeft * Plan.SplitChildCount * Plan.SplitChildHp;
                }

                return unspawned + alive + pendingChildren;
            }
        }

        /// <summary>남은 적 체력 비율(0~100). 결과 화면이 그대로 쓴다.</summary>
        public int RemainingHpPercent
        {
            get
            {
                if (!IsActive || Plan.TotalHpPool <= 0)
                    return 0;

                float ratio = (float)RemainingHp / Plan.TotalHpPool;
                return Mathf.Clamp(Mathf.RoundToInt(ratio * 100f), 0, 100);
            }
        }

        /// <summary>분열 부모 중 이미 갈라진(또는 사라진) 수.</summary>
        private int splitParentsResolved;

#if UNITY_EDITOR || DEV_DEFINE
        /// <summary>
        /// 진단용. 처치가 하나 세어질 때마다 지금 상태를 한 줄로 찍는다.
        /// <c>GameManager.RemoveMonsterDeadCount</c> 가 부른다.
        ///
        /// <b>분열 층에서만 찍는다.</b> 다른 층은 목표가 곧 소환 수라 셀 것이 없고,
        /// 매 처치마다 줄이 나오면 진짜 봐야 할 로그가 묻힌다. 카운트가 자명하지 않은
        /// 것은 <b>부모가 죽으면서 자식이 늘어나는</b> 이 한 가지뿐이다.
        /// </summary>
        public void DevLogKill(int dead, int target)
        {
            if (!IsActive || Plan.SplitChildCount <= 0)
                return;

            int expectedChildren = Plan.MonsterCount * Plan.SplitChildCount;

            Debug.Log(
                "[탑] 처치 " + dead + "/" + target +
                " · 부모 소환 " + SpawnedCount + "/" + Plan.MonsterCount +
                " · 부모 정리 " + splitParentsResolved +
                " · 자식 " + childrenSpawned + "/" + expectedChildren +
                " · 포기 " + ForfeitedKills +
                " · 살아있음 " + battle.Monsters.activeMonsters.Count);
        }
#endif

        public TowerRunManager(IBattleRefs battle)
        {
            this.battle = battle;
        }

        // ──────────────────────────────────────────────────────────────
        // 판의 시작과 끝
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 이 층으로 판을 연다. <c>GameManager.InitializeStage</c> 가 부른다.
        ///
        /// <b>여기서 상태를 전부 세운다.</b> 씬을 다시 로드하지 않고 재도전하게 되는 날
        /// (지금은 항상 다시 로드한다) 지난 판의 값이 새어 나가지 않게 하는 자리다 —
        /// <c>RunState.BeginRun</c> 이 존재하는 이유와 같다.
        /// </summary>
        public void BeginRun(TowerFloorPlan plan, TowerLoadout loadout)
        {
            Plan = plan;
            Loadout = loadout;

            ElapsedSeconds = 0f;
            timerRunning = false;
            SpawnedCount = 0;
            splitParentsResolved = 0;
            ForfeitedKills = 0;
            childrenSpawned = 0;

            // 기여도는 여기서 리셋하지 않는다. 추적기는 본편과 공유하는 것이고
            // 리셋 시점이 <b>웨이브 진입</b>이라, 그 한 곳(GameManager.ChangeState)이
            // 탑의 층 시작도 겸한다 — 한 층이 곧 한 웨이브이기 때문이다.
            // 여기서도 하면 탑에서만 두 번 도는, 이유를 알 수 없는 줄이 된다.
        }

        /// <summary>탑이 아닌 판으로 되돌린다. 스코프가 내려갈 때와 본편 진입에 쓴다.</summary>
        public void Clear()
        {
            Plan = null;
            Loadout = null;
            timerRunning = false;
        }

        /// <summary>
        /// 시계를 켠다. 웨이브가 시작되는 순간이다.
        ///
        /// <b><see cref="Time.unscaledDeltaTime"/> 이 아니라 <see cref="Time.deltaTime"/> 로
        /// 잰다.</b> 배속 버튼(x1/x2/x3)이 <c>Time.timeScale</c> 을 바꾸는데, 실제 시간으로
        /// 재면 배속을 올린 사람이 언제나 기록을 이긴다. 게임 시간으로 재면 배속은
        /// <b>기다리는 시간만</b> 줄이고 기록은 공정하게 남는다 — 기획서 5.6 의 기록 비교가
        /// 성립하려면 이쪽이어야 한다.
        /// </summary>
        public void StartTimer()
        {
            if (!IsActive)
                return;

            timerRunning = true;
        }

        /// <summary>시계를 멈춘다. 클리어·실패가 확정된 순간이다.</summary>
        public void StopTimer()
        {
            timerRunning = false;
        }

        /// <summary>
        /// 한 프레임 흘린다. <c>GameManager.Update</c> 가 부른다.
        ///
        /// <b>왜 매니저가 스스로 못 도는가.</b> MonoBehaviour 가 아니라서다.
        /// <c>ITickable</c> 로 컨테이너에 태울 수도 있지만, 이 객체는 배틀 스코프가
        /// 손으로 만들어 창구에 꽂는 것이라(<c>BountyManager</c> 와 같다) 등록 대상이 아니다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!timerRunning)
                return;

            ElapsedSeconds += deltaTime;
        }

        /// <summary>기록으로 남길 시간(밀리초).</summary>
        public int ElapsedMilliseconds => Mathf.Max(1, Mathf.RoundToInt(ElapsedSeconds * 1000f));

        // ──────────────────────────────────────────────────────────────
        // 소환
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 다음 몬스터의 스탯을 정한다. 스포너가 개체를 꺼낸 뒤 이것을 먹인다.
        ///
        /// <b>혼합 부대만 개체마다 다르다.</b> 짝수 번째는 빠르고 약하게, 홀수 번째는
        /// 느리고 단단하게 — 기획서 6.2 "빠른 적과 단단한 적이 함께 등장".
        /// 난수를 쓰지 않는 것은 <b>같은 층이 매번 같아야</b> 하기 때문이다.
        /// 무작위성은 이 콘텐츠가 없애기로 한 것이다(기획서 2장 표의 "무작위성" 줄).
        /// </summary>
        public TowerMonsterSpec GetSpawnSpec()
        {
            var spec = new TowerMonsterSpec
            {
                Hp = Plan.MonsterHp,
                Defense = Plan.MonsterDefense,
                Scale = Plan.ScaleMultiplier,
                SpeedMultiplier = Plan.MoveSpeedMultiplier,
                RegenPercentPerSecond = Plan.RegenPercentPerSecond,
                ShieldHitCharges = Plan.ShieldHitCharges,
                SplitChildCount = Plan.SplitChildCount,
                SplitChildHpRatio = Plan.SplitChildHpRatio,
            };

            if (Plan.MixesFastAndTough)
            {
                bool fast = (SpawnedCount % 2) == 0;
                if (fast)
                {
                    spec.Hp = Mathf.Max(1, Mathf.RoundToInt(spec.Hp * 0.55f));
                    spec.SpeedMultiplier *= 1.9f;
                    spec.Scale *= 0.85f;
                }
                else
                {
                    spec.Hp = Mathf.Max(1, Mathf.RoundToInt(spec.Hp * 1.45f));
                    spec.Defense = Mathf.RoundToInt(spec.Defense * 1.6f);
                    spec.SpeedMultiplier *= 0.8f;
                    spec.Scale *= 1.15f;
                }
            }

            return spec;
        }

        /// <summary>스포너가 한 마리를 내보냈다.</summary>
        public void NotifySpawned()
        {
            SpawnedCount++;
        }

        // ──────────────────────────────────────────────────────────────
        // 전투 중 통보
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 몬스터가 죽었다. 분열 자식을 내보내는 계기다.
        ///
        /// <c>Monster.TakeDamage</c> 의 사망 처리에서 <b>풀에 돌려보낸 뒤</b> 불린다.
        /// 순서가 그래야 부모의 <c>OnDisable</c> 정리가 자식 생성과 겹치지 않는다.
        /// </summary>
        public void NotifyMonsterDefeated(Monster monster, Vector3 deathPosition)
        {
            if (!IsActive || monster == null)
                return;

            if (Plan.SplitChildCount <= 0)
                return;

            // 자식은 갈라지지 않는다. 갈라지면 마리 수가 기하급수로 늘어 웨이브 목표를
            // 미리 셀 수 없게 되고, 그 순간 웨이브 종료 판정이 통째로 무너진다.
            if (monster.TowerSplitChildCount <= 0)
                return;

            splitParentsResolved++;

            // <b>실제로 나온 만큼만</b> 목표에 더한다. 약속한 수를 더하면 풀이 비어
            // 덜 나왔을 때 나오지도 않은 자식을 기다리며 층이 안 끝난다.
            childrenSpawned += battle.Spawner.SpawnTowerSplitChildren(monster, deathPosition);

            // 목표가 늘었다는 것을 그 자리에서 알린다. 이 호출이 <b>부모의 처치가
            // 세어지기 전에</b> 끝나야 한다 — 순서가 뒤집히면 자식이 한 마리인 층에서
            // 부모를 잡는 순간 목표(1)를 채워 층이 그대로 끝난다.
            battle.Game?.RefreshTowerWaveTarget();
        }

        /// <summary>
        /// 죽지 않고 사라져 목표에서 빠진 몫.
        /// </summary>
        public int ForfeitedKills { get; private set; }

        /// <summary>
        /// 몬스터 하나가 <b>죽지 않고</b> 목록에서 빠졌다(화면 밖 이탈 등).
        /// <c>MonsterManager.UnregisterMonster</c> 가 <c>countAsKill == false</c> 로
        /// 지나갈 때 부른다.
        ///
        /// <b>이 통보가 없으면 층이 안 끝난다.</b> 목표는 "나온 것을 다 잡는다" 인데
        /// 나온 것 하나가 죽지 않고 사라지면 처치 수가 영영 목표에 못 닿는다.
        ///
        /// <b>부모·자식을 가리지 않는다.</b> 예전에는 분열 부모만 봤는데, 목표가
        /// 자식까지 세는 지금은 <b>자식이 사라져도</b> 똑같이 한 칸이 빈다.
        ///
        /// 분열 부모였다면 그 자식들은 영영 안 나오므로, 남은 자식 몫을 세는
        /// <see cref="splitParentsResolved"/> 도 같이 올린다.
        /// </summary>
        public void NotifyKillForfeited(Monster monster)
        {
            if (!IsActive || monster == null)
                return;

            // <b>자기 몫 1 + 나오지 못할 자식 몫.</b> 목표가 자식까지 미리 세고 있으므로
            // 분열 부모가 사라지면 그 부모의 한 칸과 자식들의 칸이 <b>같이</b> 빈다.
            // 자식(TowerSplitChildCount == 0)이 사라지면 자기 몫 1 만 빠진다.
            ForfeitedKills += 1 + monster.TowerSplitChildCount;

            if (monster.TowerSplitChildCount > 0)
                splitParentsResolved++;

            // 목표를 줄인 사실을 그 자리에서 알린다. 다음 처치를 기다리면, 마지막
            // 하나가 이탈한 판에서는 그 "다음 처치" 가 영영 오지 않는다.
            //
            // <b><c>?.</c> 를 쓴다.</b> 이 통보는 씬을 내릴 때도 오는데, 그때는 배틀
            // 스코프가 이미 창구를 비웠을 수 있고 파괴 순서는 정해져 있지 않다.
            battle.Game?.RefreshTowerWaveTarget();
        }

        /// <summary>
        /// 지금 층을 끝내는 데 필요한 처치 수. 분모가 <b>층 내내 고정</b>이다.
        ///
        /// <b>분열 자식까지 미리 센다.</b> 31층이면 부모 8 + 자식 16 = 24 이고,
        /// 화면에 8마리뿐인 시작 시점에도 24 로 뜬다. 나온 만큼만 세어 분모를 키우는
        /// 방식도 해 봤지만 <b>초반 진행도를 부풀린다</b> — 부모 하나를 잡은 시점에
        /// 1/10(10%)로 보이는데 실제로 한 층의 1/24(4%)를 한 것이다.
        /// 진행도 게이지는 남은 일의 크기를 말해야 한다.
        ///
        /// 24 가 어디서 온 숫자인지는 두 곳이 설명한다 — 편성 화면의
        /// "분열 2마리 (총 24마리 처치)" 와 전투 화면 상단의 "31층 · 분열형 적".
        ///
        /// 줄어드는 것은 <b>죽지 않고 사라진 몫</b>뿐이다(<see cref="NotifyKillForfeited"/>).
        /// <c>GameManager</c> 가 웨이브 목표로 쓴다.
        /// </summary>
        public int EffectiveKillTarget => Mathf.Max(1, PredictedKillTarget - ForfeitedKills);

        // ──────────────────────────────────────────────────────────────
        // 결과
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 기여도를 큰 순서로 돌려준다. 결과 화면의 막대가 이 순서 그대로다.
        /// 피해가 하나도 없으면(즉사한 판) 빈 목록이다.
        ///
        /// <b>세는 것은 공용 추적기가 한다.</b> 여기 있는 것은 결과 화면이 탑 매니저
        /// 하나만 보면 되게 하는 위임 한 줄이다 — 그러지 않으면 결과 화면이
        /// 창구 두 개(<c>Tower</c>·<c>Contribution</c>)를 각각 알아야 한다.
        /// </summary>
        public List<DiceDamageShare> BuildDamageShares()
        {
            return battle.Contribution.BuildShares();
        }

        /// <summary>
        /// 이 층의 보상을 만든다. <b>최초 클리어에만 준다.</b>
        ///
        /// 기획서 7.1 이 "각 층 <b>최초 클리어</b> 시 기본 재화" 라고 적고 있고,
        /// 9장의 "최초 클리어 보상과 반복 보상의 차이" 는 열려 있던 항목이었다.
        ///
        /// <b>반복 플레이 자체가 없는 것으로 확정됐다</b>(2026-09-05).
        /// 1층을 깨면 보상을 받고 끝이고, 그다음에는 2층만 도전한다. 그래서
        /// <paramref name="firstClear"/> 는 지금 언제나 true 다 — 그래도 인자로 받는 이유는
        /// <c>TowerProgressManager.RecordClear</c> 주석에 있다(이중 지급을 막는 잠금장치).
        /// </summary>
        public static List<PointRewardEntry> BuildClearRewards(int floor, bool firstClear)
        {
            var rewards = new List<PointRewardEntry>();
            if (!firstClear)
                return rewards;

            rewards.Add(new PointRewardEntry(PointType.Gold, TowerFormula.FirstClearGold(floor)));

            if (TowerFormula.IsBandLastFloor(floor))
            {
                rewards.Add(new PointRewardEntry(PointType.FreeGem, TowerFormula.BandRewardDia(floor)));
                // <b>BattleEnhanceStone 을 주면 안 된다.</b> 그것은 판이 시작될 때 0 으로
                // 밀리는 인게임 재화라(ElementUpgradeManager.ResetRunState), 받자마자 다음
                // 층에 들어가면 사라지고 로비에는 쓸 곳조차 없다 — 주는 척만 하는 보상이었다.
                //
                // 신화 스크롤로 바꾼 이유: <b>탑이 여는 것이 킹 다이스</b>이고, 킹을 올리는
                // 재료가 신화 스크롤이다(PointManager.ToScrollType). 탑이 킹을 주고 그것을
                // 키울 재료도 주는 것이 한 줄로 읽힌다. 게다가 신화 스크롤의 다른 수급처는
                // 스테이지 등급 보너스뿐이라 1회성이고 상한이 낮다.
                rewards.Add(new PointRewardEntry(
                    PointType.MythicScroll, TowerFormula.BandRewardMaterial(floor)));
            }

            return rewards;
        }
    }

    /// <summary>
    /// 몬스터 한 마리에게 먹일 값. <see cref="TowerRunManager.GetSpawnSpec"/> 이 만들고
    /// <c>MonsterSpawner</c> 가 <c>Monster</c> 에 적용한다.
    ///
    /// <b>구조체로 넘기는 이유.</b> 인자가 여덟 개라 메서드 시그니처로 늘어놓으면
    /// 순서를 바꿔 부르는 사고가 난다 — 전부 숫자라 컴파일러가 못 잡는다.
    /// </summary>
    public struct TowerMonsterSpec
    {
        public int Hp;
        public int Defense;
        public float Scale;
        public float SpeedMultiplier;
        public float RegenPercentPerSecond;
        public int ShieldHitCharges;
        public int SplitChildCount;
        public float SplitChildHpRatio;
    }
}
