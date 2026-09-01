using System;

namespace OJ.Core
{
    /// <summary>
    /// 한 판(스테이지 1회 도전)의 상태. (MIGRATION_BASELINE 6.1)
    ///
    /// <b>왜 필요한가.</b> 지금 런 범위 상태가 매니저 여럿에 흩어져 있다 —
    /// 벽 HP 는 <c>GameManager</c> 와 <c>Wall</c> 에 이중으로, 웨이브 인덱스는
    /// <c>GameManager</c> 에, SP 는 <c>UIDiceSummonSystem</c> 에, 원소 레벨은
    /// <c>ElementUpgradeManager</c> 에 있다. 그래서 "판이 끝났다"를 표현하려면
    /// 다섯 곳을 각각 리셋해야 하고, <b>하나만 빠뜨리면 다음 판에 이전 값이 새어 나간다.</b>
    /// 지금은 씬을 다시 로드해서 우연히 초기화되고 있다.
    ///
    /// <b>여기 들어오는 것과 안 오는 것.</b>
    /// 판마다 리셋되는 것만 온다. 장비·유물 레벨·재화·스테이지 진행도는 PlayerPrefs 로
    /// 저장되는 <b>영구</b> 상태라 오지 않는다.
    ///
    /// <b>Seed 를 여기 두는 이유.</b> 5.0 조사에서 <c>UnityEngine.Random</c> 호출이 34곳이고
    /// 전역 static 시드를 쓰며 판·씬·앱 어디서도 리셋되지 않는다는 것이 확인됐다.
    /// 소환 타입·머지 결과·크리티컬·유물 발동이 전부 거기 걸려 있다. 시드를 런이 소유하지
    /// 않으면 <b>같은 판을 다시 돌려도 결과가 달라져</b> 순수 규칙을 테스트할 수 없다.
    /// 지금은 필드만 두고 호출부 교체는 점진적으로 한다 — 34곳을 한 번에 바꾸면 되돌리기 어렵다.
    ///
    /// <c>OJ.Core</c> 소속이라 <c>UnityEngine</c> 타입도 <c>DiceType</c> 같은 enum 도 못 쓴다.
    /// 보드 위 주사위처럼 enum 이 필요한 상태는 6.2 에서 int 로 담거나 별도 타입을 만든다.
    /// </summary>
    public sealed class RunState
    {
        /// <summary>이 판의 난수 시드. 판을 시작할 때 정해지고 그 뒤로 바뀌지 않는다.</summary>
        public int Seed { get; private set; }

        /// <summary>도전 중인 스테이지 번호(1부터).</summary>
        public int StageIndex { get; private set; }

        /// <summary>현재 웨이브. 0 이면 아직 시작 전이다 — 초반 웨이브 보너스가
        /// <c>waveIndex &lt;= 0</c> 에서 0 을 돌려주는 것이 그 때문이다.</summary>
        public int WaveIndex { get; set; }

        /// <summary>이번 웨이브에서 잡은 몬스터 수.</summary>
        public int WaveMonsterDeadCount { get; set; }

        /// <summary>웨이브당 몬스터 수. 스테이지 데이터에서 온다.</summary>
        public int WaveMonsterCount { get; set; }

        /// <summary>벽 최대 체력. 스테이지 시작 시 정해진다.</summary>
        public int WallMaxHp { get; set; }

        /// <summary>벽 현재 체력.</summary>
        public int WallHp { get; set; }

        /// <summary>소환에 쓰는 SP.</summary>
        public int SummonPoint { get; set; }

        /// <summary>다음 소환 비용. 소환할수록 오른다.</summary>
        public int SummonCost { get; set; }

        /// <summary>판이 끝났는가.</summary>
        public bool IsGameOver { get; set; }

        /// <summary>
        /// 판을 시작한다. <b>모든 런 범위 필드를 여기서 한 번에</b> 세운다 —
        /// 필드를 늘리면 반드시 여기도 늘려야 하고, 그러지 않으면 이전 판 값이 새어 나간다.
        /// 그것이 이 클래스가 존재하는 이유다.
        /// </summary>
        public void BeginRun(int seed, int stageIndex, int wallMaxHp, int monstersPerWave, int initialSummonPoint, int initialSummonCost)
        {
            Seed = seed;
            StageIndex = stageIndex;
            WaveIndex = 0;
            WaveMonsterDeadCount = 0;
            WaveMonsterCount = monstersPerWave;
            WallMaxHp = wallMaxHp;
            WallHp = wallMaxHp;
            SummonPoint = initialSummonPoint;
            SummonCost = initialSummonCost;
            IsGameOver = false;
        }
    }
}
