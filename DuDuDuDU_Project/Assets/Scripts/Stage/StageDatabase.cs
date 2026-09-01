using System.Collections.Generic;
using UnityEngine;
using OJ.Hunting;

namespace OJ.Stage
{
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Stage/Stage Database")]
    public class StageDatabase : ScriptableObject
    {
        // 이 3개는 더 이상 "정본"이 아니다. 정본은 StageDatabase 에셋이다. (4단계)
        // 남겨 둔 이유는 두 가지뿐이다.
        //   1) PopulateDefaults — 에셋이 비었을 때 30스테이지를 처음 만들어 주는 씨앗값.
        //   2) RefreshMonsterHpBalance — 사람이 인스펙터에서 일부러 누르는 되돌리기 메뉴.
        // 자동 실행 경로(OnEnable / GetStage)에서는 전부 빠졌으므로 이 값이 에셋을 덮는 일은
        // 이제 없다. 밸런스를 고치려면 에셋을 고칠 것. 여기 숫자를 고쳐도 에셋은 안 바뀐다.
        private const float DefaultWaveHpLinearFactor = 0.145f;
        private const float DefaultWaveHpQuadraticFactor = 0.018f;
        private const float DefaultBossHpMultiplier = 6.4f;

        [SerializeField] private List<StageData> stages = new List<StageData>();

        private readonly Dictionary<int, StageData> stageMap = new Dictionary<int, StageData>();

        public IReadOnlyList<StageData> Stages => stages;
        public int StageCount => stages.Count;

        private void OnEnable()
        {
            if (stages == null)
                stages = new List<StageData>();

            if (stages.Count == 0)
                PopulateDefaults(30);

            // ApplyMonsterHpBalance() 를 뺐다. 에셋을 로드할 때마다 코드 기본값이 에셋 위를
            // 덮어써서, 인스펙터에서 고친 baseMonsterHp / waveHp*Factor / bossHpMultiplier 가
            // 다음 로드에 사라졌다. 정본이 에셋이 된 이상 로드는 읽기만 한다.
            RebuildMap();
        }

        public StageData GetStage(int stageIndex)
        {
            // 여기서도 ApplyMonsterHpBalance() 를 뺐다. 조회 한 번마다 30스테이지 전체의
            // HP 를 다시 계산해 써 넣던 자리다 — 조회가 쓰기였다.
            if (stageMap.Count != stages.Count)
                RebuildMap();

            if (stageMap.TryGetValue(stageIndex, out StageData stageData))
                return stageData;

            return null;
        }

        [ContextMenu("Populate Default 30 Stages")]
        public void PopulateDefaults()
        {
            PopulateDefaults(30);
        }

        // <b>파괴적 메뉴다.</b> 4단계 이후로 이 메뉴의 의미가 뒤집혔다. 예전에는 로드마다
        // 자동으로 벌어지던 일을 손으로 한 번 더 하는 것이었지만, 이제는 <b>에셋에 손으로 넣은
        // HP 밸런스를 코드 기본값으로 되돌리는</b> 유일한 경로다. 누르면 이 에셋의 모든 스테이지에서
        // baseMonsterHp / waveHpLinearFactor / waveHpQuadraticFactor / bossHpMultiplier 4개가
        // 코드 산출값으로 덮인다. 나머지 필드(방어력·SP·웨이브 수 등)는 건드리지 않는다.
        // 지우지 않고 남긴 이유: 에셋이 망가졌을 때 알려진 기준선으로 돌아갈 수단이 있어야 한다.
        // 되돌린 결과를 파일로 남기려면 인스펙터에서 저장(Ctrl+S)까지 해야 한다 —
        // 여기서 SetDirty 를 부르지 않으므로 에디터를 그냥 닫으면 메모리에서만 바뀌고 끝난다.
        [ContextMenu("Refresh Monster Hp Balance")]
        public void RefreshMonsterHpBalance()
        {
            ApplyMonsterHpBalance();
            RebuildMap();
        }

        public void PopulateDefaults(int stageCount)
        {
            stages.Clear();

            for (int i = 1; i <= Mathf.Max(1, stageCount); i++)
            {
                int totalWaves = GetDefaultWaveCount(i);
                stages.Add(new StageData
                {
                    stageIndex = i,
                    theme = (StageTheme)((i - 1) % 5),
                    totalWaves = totalWaves,
                    monstersPerWave = 20,
                    wallHp = 100,
                    initialSP = GetInitialSp(totalWaves),
                    waveClearSP = GetWaveClearSp(totalWaves),
                    baseMonsterHp = GetBaseMonsterHp(i, totalWaves),
                    baseMonsterDefense = GetBaseMonsterDefense(i, totalWaves),
                    waveHpLinearFactor = DefaultWaveHpLinearFactor,
                    waveHpQuadraticFactor = DefaultWaveHpQuadraticFactor,
                    waveDefenseLinearFactor = 0.12f,
                    waveDefenseQuadraticFactor = 0.015f,
                    bossHpMultiplier = DefaultBossHpMultiplier,
                    bossDefenseMultiplier = 2.5f,
                    bossScaleMultiplier = 1.45f,
                });
            }

            // ApplyMonsterHpBalance() 를 뺐다. 여기서는 원래부터 아무 일도 하지 않았다.
            // 위 초기화자가 이미 baseMonsterHp = GetBaseMonsterHp(i, totalWaves) 로,
            // 나머지 3개를 같은 상수로 넣는다. ApplyMonsterHpBalance 가 다시 쓰는 값은
            // GetBaseMonsterHp(stage.stageIndex, stage.totalWaves) 인데 stage.stageIndex == i,
            // stage.totalWaves == totalWaves 이므로 같은 정적 함수에 같은 인자다 — 결과가 같다.
            // 즉 float 를 다시 계산하는 것도 아니고, 방금 넣은 값을 그대로 다시 넣던 것이다.
            RebuildMap();
        }

        // 이제 RefreshMonsterHpBalance(사람이 누르는 ContextMenu) 에서만 불린다.
        // 자동 경로에서 다시 부르지 말 것 — 그러면 에셋이 또 코드에 종속된다.
        private void ApplyMonsterHpBalance()
        {
            if (stages == null)
                return;

            for (int i = 0; i < stages.Count; i++)
            {
                StageData stage = stages[i];
                if (stage == null)
                    continue;

                stage.baseMonsterHp = GetBaseMonsterHp(stage.stageIndex, stage.totalWaves);
                stage.waveHpLinearFactor = DefaultWaveHpLinearFactor;
                stage.waveHpQuadraticFactor = DefaultWaveHpQuadraticFactor;
                stage.bossHpMultiplier = DefaultBossHpMultiplier;
            }
        }

        private void RebuildMap()
        {
            stageMap.Clear();

            for (int i = 0; i < stages.Count; i++)
            {
                StageData stage = stages[i];
                if (stage == null)
                    continue;

                stageMap[stage.stageIndex] = stage;
            }
        }

        private static int GetDefaultWaveCount(int stageIndex)
        {
            if (stageIndex % 10 == 0)
                return 20;

            if (stageIndex % 5 == 0)
                return 10;

            return 15;
        }

        private static int GetInitialSp(int totalWaves)
        {
            switch (totalWaves)
            {
                case 10:
                    return 117;
                case 15:
                    return 100;
                case 20:
                    return 84;
                default:
                    return Mathf.Max(90, Mathf.RoundToInt((105 - (totalWaves * 4)) * 1.5f));
            }
        }

        private static int GetWaveClearSp(int totalWaves)
        {
            switch (totalWaves)
            {
                case 10:
                    return 103;
                case 15:
                    return 80;
                case 20:
                    return 74;
                default:
                    return Mathf.Max(72, (20 - totalWaves) * 9);
            }
        }

        private static int GetBaseMonsterHp(int stageIndex, int totalWaves)
        {
            float waveModeMultiplier = totalWaves == 10 ? 1.1f : totalWaves == 20 ? 0.9f : 1f;
            float stageOffset = Mathf.Max(0, stageIndex - 1);
            float hp = (7f + (stageOffset * 1.9f) + (Mathf.Pow(stageOffset, 1.22f) * 0.75f)) * waveModeMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(hp));
        }

        private static int GetBaseMonsterDefense(int stageIndex, int totalWaves)
        {
            float waveModeMultiplier = totalWaves == 10 ? 1.12f : totalWaves == 20 ? 0.96f : 1f;
            float stageOffset = Mathf.Max(0, stageIndex - 1);
            float defense = (4f + (stageOffset * 1.35f) + (Mathf.Pow(stageOffset, 1.12f) * 0.65f)) * waveModeMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt(defense));
        }
    }
}
