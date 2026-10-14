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

    /// <summary>
    /// 스테이지 단위 콘셉트. 무한의 탑의 구간 콘셉트를 본편에 옮긴 것이지만 <b>표시용 이름표</b>다.
    /// 실제 수치는 이 enum 이 아니라 <see cref="StageData"/> 의 필드(마리 수·체력·속도·간격·크기)가
    /// 정한다 — 정본은 에셋이고, 콘셉트를 바꿔도 숫자는 저절로 안 바뀐다.
    /// </summary>
    public enum StageConcept
    {
        [InspectorName("기본")]
        Normal = 0,
        [InspectorName("대규모 물량")]
        Horde = 1,
        [InspectorName("정예")]
        Elite = 2,
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

        // 스테이지 콘셉트. 필드가 없는 옛 항목은 기본값(1배)으로 읽히므로 지금과 똑같이 돈다.
        public StageConcept concept = StageConcept.Normal;
        [Tooltip("일반·보스 몬스터 이동속도 배수. 현상금은 자기 배수를 따로 쓴다.")]
        [Min(0.05f)] public float monsterMoveSpeedMultiplier = 1f;
        [Tooltip("일반 몬스터 크기 배수. 보스는 bossScaleMultiplier 를 쓴다.")]
        [Min(0.1f)] public float monsterScaleMultiplier = 1f;
        [Tooltip("등장 간격 배수. 스포너 기본 간격(2초)에 곱한다. 물량은 작게, 정예는 크게.")]
        [Min(0.1f)] public float spawnIntervalMultiplier = 1f;

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

        public static string GetConceptName(StageConcept concept)
        {
            switch (concept)
            {
                case StageConcept.Horde: return "대규모 물량";
                case StageConcept.Elite: return "정예";
                default: return "기본";
            }
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
