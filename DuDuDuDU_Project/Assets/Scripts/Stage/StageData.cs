using System;
using OJ.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace OJ.Stage
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

        // 아래 성장식 6개는 OJ.Core.StageGrowthFormula 로 옮겼다. 여기 남은 것은 직렬화 필드를
        // 기본형으로 풀어서 넘기는 얇은 위임뿐이다 — public 시그니처와 호출 순서는 그대로다.
        // 식을 다시 여기로 인라인하지 말 것. 골든 기준선(Tests/Golden/formula_baseline.txt)이
        // 정수값을 박제하고 있어 표현식이 한 글자만 달라져도 반올림이 어긋난다.
        public int GetMonsterHpForWave(int waveIndex)
        {
            return StageGrowthFormula.MonsterHp(baseMonsterHp, waveHpLinearFactor, waveHpQuadraticFactor, waveIndex);
        }

        public int GetMonsterDefenseForWave(int waveIndex)
        {
            // 원본이 매 호출마다 GetResolvedBaseMonsterDefense() 를 다시 계산했으므로 캐시하지 않는다.
            return StageGrowthFormula.MonsterDefense(GetResolvedBaseMonsterDefense(), waveDefenseLinearFactor, waveDefenseQuadraticFactor, waveIndex);
        }

        public int GetBossHpForWave(int waveIndex)
        {
            // 반올림된 정수 몬스터 체력을 넘긴다. 이 이중 반올림이 원본 동작이다.
            return StageGrowthFormula.BossHp(GetMonsterHpForWave(waveIndex), bossHpMultiplier);
        }

        public int GetBossDefenseForWave(int waveIndex)
        {
            return StageGrowthFormula.BossDefense(GetMonsterDefenseForWave(waveIndex), bossDefenseMultiplier);
        }

        public int GetBossSpawnThreshold()
        {
            return StageGrowthFormula.BossSpawnThreshold(monstersPerWave);
        }

        private int GetResolvedBaseMonsterDefense()
        {
            return StageGrowthFormula.ResolvedBaseDefense(baseMonsterDefense, stageIndex, totalWaves);
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
