using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Stage/Stage Database")]
    public class StageDatabase : ScriptableObject
    {
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

            RebuildMap();
        }

        public StageData GetStage(int stageIndex)
        {
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

        public void PopulateDefaults(int stageCount)
        {
            stages.Clear();

            for (int i = 1; i <= Mathf.Max(1, stageCount); i++)
            {
                int totalWaves = GetDefaultWaveCount(i);
                stages.Add(new StageData
                {
                    stageIndex = i,
                    totalWaves = totalWaves,
                    monstersPerWave = 20,
                    wallHp = 100,
                    initialSP = GetInitialSp(totalWaves),
                    waveClearSP = GetWaveClearSp(totalWaves),
                    baseMonsterHp = GetBaseMonsterHp(i, totalWaves),
                    baseMonsterDefense = GetBaseMonsterDefense(i, totalWaves),
                    waveHpLinearFactor = 0.16f,
                    waveHpQuadraticFactor = 0.02f,
                    waveDefenseLinearFactor = 0.12f,
                    waveDefenseQuadraticFactor = 0.015f,
                    bossHpMultiplier = 7f,
                    bossDefenseMultiplier = 2.5f,
                    bossScaleMultiplier = 1.45f,
                });
            }

            RebuildMap();
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
                return 10;

            if (stageIndex % 5 == 0)
                return 5;

            return 8;
        }

        private static int GetInitialSp(int totalWaves)
        {
            switch (totalWaves)
            {
                case 5:
                    return 100;
                case 8:
                    return 85;
                case 10:
                    return 72;
                default:
                    return Mathf.Max(60, 105 - (totalWaves * 4));
            }
        }

        private static int GetWaveClearSp(int totalWaves)
        {
            switch (totalWaves)
            {
                case 5:
                    return 18;
                case 8:
                    return 14;
                case 10:
                    return 13;
                default:
                    return Mathf.Max(8, 20 - totalWaves);
            }
        }

        private static int GetBaseMonsterHp(int stageIndex, int totalWaves)
        {
            float waveModeMultiplier = totalWaves == 5 ? 1.18f : totalWaves == 10 ? 0.95f : 1f;
            float stageOffset = Mathf.Max(0, stageIndex - 1);
            float hp = (22f + (stageOffset * 4.5f) + (Mathf.Pow(stageOffset, 1.35f) * 1.8f)) * waveModeMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(hp));
        }

        private static int GetBaseMonsterDefense(int stageIndex, int totalWaves)
        {
            float waveModeMultiplier = totalWaves == 5 ? 1.12f : totalWaves == 10 ? 0.96f : 1f;
            float stageOffset = Mathf.Max(0, stageIndex - 1);
            float defense = (4f + (stageOffset * 1.35f) + (Mathf.Pow(stageOffset, 1.12f) * 0.65f)) * waveModeMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt(defense));
        }
    }
}
