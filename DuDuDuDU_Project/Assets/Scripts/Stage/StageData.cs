using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace OJ
{
    public enum StageTheme
    {
        [InspectorName("어두운 숲")]
        DarkForest = 0,
        [InspectorName("얼음/눈")]
        IceSnow = 1,
        [InspectorName("화산")]
        Volcano = 3,
        [InspectorName("사막")]
        Desert = 2,
        [InspectorName("공동묘지")]
        Cemetery = 4,
    }

    public enum StageClearGrade
    {
        None = 0,
        Minimum = 1,
        Half = 2,
        Perfect = 3,
    }

    [Flags]
    public enum StageRewardTierFlags
    {
        None = 0,
        Minimum = 1 << 0,
        Half = 1 << 1,
        Perfect = 1 << 2,
    }

    [Serializable]
    public class StageData
    {
        [Min(1)] public int stageIndex = 1;
        [FormerlySerializedAs("stageResourceId")]
        public StageTheme theme = StageTheme.DarkForest;
        [Min(1)] public int totalWaves = 8;
        [Min(1)] public int monstersPerWave = 20;
        [Min(1)] public int wallHp = 100;
        [Min(0)] public int initialSP = 100;
        [Min(0)] public int waveClearSP = 12;
        [Min(1)] public int baseMonsterHp = 20;
        [Min(0)] public int baseMonsterDefense = 0;
        [Min(0.01f)] public float waveHpLinearFactor = 0.16f;
        [Min(0f)] public float waveHpQuadraticFactor = 0.02f;
        [Min(0f)] public float waveDefenseLinearFactor = 0.12f;
        [Min(0f)] public float waveDefenseQuadraticFactor = 0.015f;
        [Min(0.1f)] public float bossHpMultiplier = 7f;
        [Min(0.1f)] public float bossDefenseMultiplier = 2.5f;
        [Min(0.1f)] public float bossScaleMultiplier = 1.45f;

        public int GetMonsterHpForWave(int waveIndex)
        {
            int validWave = Mathf.Max(1, waveIndex);
            float waveOffset = validWave - 1;
            float multiplier = 1f + (waveOffset * waveHpLinearFactor) + (waveOffset * waveOffset * waveHpQuadraticFactor);
            return Mathf.Max(1, Mathf.RoundToInt(baseMonsterHp * multiplier));
        }

        public int GetMonsterDefenseForWave(int waveIndex)
        {
            int validWave = Mathf.Max(1, waveIndex);
            float waveOffset = validWave - 1;
            float multiplier = 1f + (waveOffset * waveDefenseLinearFactor) + (waveOffset * waveOffset * waveDefenseQuadraticFactor);
            return Mathf.Max(0, Mathf.RoundToInt(GetResolvedBaseMonsterDefense() * multiplier));
        }

        public int GetBossHpForWave(int waveIndex)
        {
            return Mathf.Max(1, Mathf.RoundToInt(GetMonsterHpForWave(waveIndex) * bossHpMultiplier));
        }

        public int GetBossDefenseForWave(int waveIndex)
        {
            return Mathf.Max(0, Mathf.RoundToInt(GetMonsterDefenseForWave(waveIndex) * bossDefenseMultiplier));
        }

        public int GetBossSpawnThreshold()
        {
            return Mathf.Clamp(Mathf.CeilToInt(monstersPerWave * 0.5f), 1, Mathf.Max(1, monstersPerWave));
        }

        private int GetResolvedBaseMonsterDefense()
        {
            if (baseMonsterDefense > 0)
                return baseMonsterDefense;

            float stageOffset = Mathf.Max(0, stageIndex - 1);
            float defenseValue = 4f + (stageOffset * 1.35f) + (Mathf.Pow(stageOffset, 1.12f) * 0.65f);
            if (totalWaves == 10)
                defenseValue *= 1.12f;
            else if (totalWaves == 20)
                defenseValue *= 0.96f;

            return Mathf.Max(0, Mathf.RoundToInt(defenseValue));
        }

        public static string GetStageDisplayName(int stageIndex)
        {
            switch (stageIndex)
            {
                case 1:
                    return "\uC5B4\uB460\uC758 \uC232\uC18D";
                case 2:
                    return "\uACA8\uC6B8 \uC232\uC18D";
                case 3:
                    return "\uC0AC\uB9C9 \uB3C4\uC2DC";
                default:
                    return string.Format("Stage {0}", stageIndex);
            }
        }
    }
}
