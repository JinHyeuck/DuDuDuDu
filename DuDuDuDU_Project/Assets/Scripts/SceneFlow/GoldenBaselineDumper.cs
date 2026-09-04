#if UNITY_EDITOR || DEV_DEFINE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OJ.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Dice;
using OJ.Element;
using OJ.Equipment;
using OJ.Hunting;
using OJ.IdleReward;
using OJ.Point;
using OJ.Relic;
using OJ.Stage;
using OJ.Utils;

using OJ.DI;
namespace OJ.SceneFlow
{
    /// <summary>
    /// 3단계 특성화 테스트용 <b>골든 기준선</b>을 뜬다. (MIGRATION_BASELINE 3.3)
    ///
    /// 목표는 "현행 동작을 그대로 고정"이지 개선이 아니다. 그래서 <b>계산식을 개조하기
    /// 전에</b> 지금 코드가 내놓는 숫자를 파일로 박아 둔다. 개조 후 같은 파일과 비교해
    /// 값이 하나라도 달라지면 그건 리팩토링이 아니라 사양 변경이다.
    ///
    /// 왜 에디터 메뉴가 아니라 플레이 중 핫키인가: CalculateDamage 계열이
    /// StaticResource 를 타는데, 2.3 에서 넣은 에디터 모드 가드가 <b>플레이 중이 아니면
    /// 프리팹 싱글톤을 만들지 않도록</b> 막는다. 플레이 중에 뜨면 실제 경로 그대로 지난다.
    ///
    /// 파일은 두 구획으로 나뉜다.
    ///
    ///   [stable]      에셋과 인자만으로 결정된다. <b>커밋해서 테스트가 비교하는 대상.</b>
    ///   [environment] 세이브 진행도(장비·유물·원소)에 좌우된다. 기기마다 다르므로
    ///                 테스트가 걸지 않는다. 같은 기기에서 개조 전후를 눈으로 대조할 때 쓴다.
    /// </summary>
    internal static class GoldenBaselineDumper
    {
        private const string OutputRelativePath = "../Tests/Golden/formula_baseline.txt";

        /// <summary>
        /// 매니저가 없을 때 찍는 값. 키를 <b>지우지 않고</b> 값만 바꾸는 이유는, 키가 통째로
        /// 사라지면 diff 에서 "값이 0 이 됐다"와 "매니저가 없었다"가 구분되지 않기 때문이다.
        /// 앞의 것은 세이브 변화이고 뒤의 것은 배선 사고다.
        /// </summary>
        private const string NoManagerValue = "<no manager>";

        private static readonly DiceType[] DumpDiceTypes =
        {
            DiceType.Normal, DiceType.Fire, DiceType.Ice, DiceType.Thunder, DiceType.Poison,
            DiceType.Tornado, DiceType.Stun, DiceType.ArmorBreak, DiceType.Wind, DiceType.Time,
            DiceType.KingNormal, DiceType.KingFire, DiceType.KingIce, DiceType.KingThunder, DiceType.KingPoison,
        };

        private static readonly int[] DumpStages = { 1, 2, 3, 5, 10, 20, 30 };
        private static readonly int[] DumpWaves = { 1, 2, 3, 5, 8, 10, 15, 20 };
        private static readonly int[] DumpLevels = { 1, 2, 3, 5, 6, 9, 12, 15 };

        public static void Dump()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# DuDuDuDu 계산식 골든 기준선");
            sb.AppendLine("# MIGRATION_BASELINE 3단계. 손으로 고치지 말 것 — 다시 뜨면 diff 로 보인다.");

            // 뜬 조건을 박아 둔다. [environment] 구획의 데미지는 GameManager.CurrentWaveIndex 에
            // 좌우된다 — 로비에서는 GameManager 가 없어 웨이브 0 이고, 그러면
            // GetFirstNWavesDamageFlatBonus 가 즉시 0 을 돌려준다. 배틀에서 웨이브를 돌리는
            // 중에 뜨면 초반 웨이브 보너스가 붙어 값이 커지고, 그게 "리팩토링 때문에 바뀐 것"
            // 처럼 보인다. 조건이 다르면 여기서 먼저 드러나게 한다.
            //
            // '#' 으로 시작하므로 키 단위 비교에서는 무시되고, git diff 에서만 눈에 띈다.
            // 값 비교를 오염시키지 않으면서 조건 변화만 알리려는 것이다.
            int waveIndex = GameContainer.Battle.IsActive ? GameContainer.Battle.Game.CurrentWaveIndex : -1;
            sb.AppendLine(string.Format(
                "# 뜬 조건: 씬={0} / GameManager={1} / CurrentWaveIndex={2}",
                SceneManager.GetActiveScene().name,
                GameContainer.Battle.IsActive ? "있음" : "없음",
                waveIndex >= 0 ? waveIndex.ToString() : "해당없음"));
            sb.AppendLine("# 권장: 항상 로비에서 뜰 것. 웨이브 0 이 보장되어 재현 가능하다.");
            sb.AppendLine();

            sb.AppendLine("[stable]");
            DumpStageGrowth(sb);
            DumpStageRewards(sb);
            DumpAutoBattleRewards(sb);
            DumpDiceMeta(sb);
            DumpIdleConversion(sb);
            DumpCoreFormulas(sb);
            DumpCriticalFormula(sb);
            DumpHitPathConstants(sb);
            DumpEquipmentUpgrade(sb);
            DumpGemBonus(sb);

            sb.AppendLine();
            sb.AppendLine("[environment]");
            sb.AppendLine("# 세이브 진행도에 좌우된다. 테스트가 걸지 않는다.");

            // 순서를 지킨다. 아래 세 구획은 전부 같은 싱글톤 5종(DiceLevelManager /
            // DiceTypeStarManager / EquipmentManager / RelicManager / ElementUpgradeManager)을
            // 건드리는데, MonoSingleton.Instance 는 <b>처음 접근할 때 인스턴스를 만든다.</b>
            // DumpDamage 가 예전부터 첫 접근자였으므로 그 자리를 그대로 둔다 — 새 구획을
            // 앞에 끼우면 생성 순서가 바뀌고, 그 차이로 값이 흔들리면 "리팩토링 때문"으로
            // 오독된다. 새 구획은 반드시 뒤에 붙일 것.
            DumpDamage(sb);
            DumpDamagePathState(sb);
            DumpEffectParameters(sb);
            DumpUnlockableAxes(sb);

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, OutputRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[Golden] 기준선을 떴다: {path}\n{CountLines(sb)}줄");
        }

        private static void DumpStageGrowth(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## stage.growth  (StageDatabase 에셋 + StageData 성장식)");

            for (int s = 0; s < DumpStages.Length; s++)
            {
                int stageIndex = DumpStages[s];
                StageData stage = StageDatabaseProvider.GetStage(stageIndex);
                if (stage == null)
                {
                    sb.AppendLine($"stage[{stageIndex}] = <null>");
                    continue;
                }

                // 입력 필드도 함께 적는다. 이게 없으면 OJ.Core.Tests 가 골든을 재현할 수
                // 없다 — 테스트 어셈블리는 Assembly-CSharp 을 참조할 수 없어서 에셋의
                // StageData 를 읽지 못하기 때문이다. 입력과 출력을 한 파일에 같이 둬야
                // 테스트가 자립한다.
                sb.AppendLine($"stage[{stageIndex}].in.stageIndex = {stage.stageIndex}");
                sb.AppendLine($"stage[{stageIndex}].in.baseMonsterHp = {stage.baseMonsterHp}");
                sb.AppendLine($"stage[{stageIndex}].in.baseMonsterDefense = {stage.baseMonsterDefense}");
                sb.AppendLine($"stage[{stageIndex}].in.waveHpLinearFactor = {Num(stage.waveHpLinearFactor)}");
                sb.AppendLine($"stage[{stageIndex}].in.waveHpQuadraticFactor = {Num(stage.waveHpQuadraticFactor)}");
                sb.AppendLine($"stage[{stageIndex}].in.waveDefenseLinearFactor = {Num(stage.waveDefenseLinearFactor)}");
                sb.AppendLine($"stage[{stageIndex}].in.waveDefenseQuadraticFactor = {Num(stage.waveDefenseQuadraticFactor)}");
                sb.AppendLine($"stage[{stageIndex}].in.bossHpMultiplier = {Num(stage.bossHpMultiplier)}");
                sb.AppendLine($"stage[{stageIndex}].in.bossDefenseMultiplier = {Num(stage.bossDefenseMultiplier)}");

                sb.AppendLine($"stage[{stageIndex}].totalWaves = {stage.totalWaves}");
                sb.AppendLine($"stage[{stageIndex}].monstersPerWave = {stage.monstersPerWave}");
                sb.AppendLine($"stage[{stageIndex}].wallHp = {stage.wallHp}");
                sb.AppendLine($"stage[{stageIndex}].initialSP = {stage.initialSP}");
                sb.AppendLine($"stage[{stageIndex}].waveClearSP = {stage.waveClearSP}");
                sb.AppendLine($"stage[{stageIndex}].bossSpawnThreshold = {stage.GetBossSpawnThreshold()}");

                for (int w = 0; w < DumpWaves.Length; w++)
                {
                    int wave = DumpWaves[w];
                    sb.AppendLine($"stage[{stageIndex}].wave[{wave}].monsterHp = {stage.GetMonsterHpForWave(wave)}");
                    sb.AppendLine($"stage[{stageIndex}].wave[{wave}].monsterDefense = {stage.GetMonsterDefenseForWave(wave)}");
                    sb.AppendLine($"stage[{stageIndex}].wave[{wave}].bossHp = {stage.GetBossHpForWave(wave)}");
                    sb.AppendLine($"stage[{stageIndex}].wave[{wave}].bossDefense = {stage.GetBossDefenseForWave(wave)}");
                }
            }
        }

        private static void DumpStageRewards(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## stage.reward  (StageRewardCalculator 결정적 부분)");

            for (int s = 1; s <= 30; s++)
                sb.AppendLine($"reward.guaranteedGold[{s}] = {StageRewardCalculator.GetGuaranteedNormalGold(s)}");

            int[] clearedSamples = { 0, 1, 4, 7, 8, 12 };
            int[] totalSamples = { 8, 10, 20 };
            for (int s = 0; s < DumpStages.Length; s++)
            {
                for (int t = 0; t < totalSamples.Length; t++)
                {
                    for (int c = 0; c < clearedSamples.Length; c++)
                    {
                        int value = StageRewardCalculator.GetAccumulatedGuaranteedGold(
                            DumpStages[s], clearedSamples[c], totalSamples[t]);
                        sb.AppendLine($"reward.accumulatedGold[{DumpStages[s]}][{clearedSamples[c]}/{totalSamples[t]}] = {value}");
                    }
                }
            }

            int[] wallSamples = { 0, 1, 49, 50, 51, 99, 100 };
            for (int i = 0; i < wallSamples.Length; i++)
            {
                StageClearGrade grade = StageRewardCalculator.GetClearGrade(wallSamples[i], 100);
                sb.AppendLine($"reward.clearGrade[{wallSamples[i]}/100] = {grade}");
            }

            // 등급별로 한 번씩만 적는다. 예전에는 벽 체력 표본마다 적어서
            // reward.rewardFlags[Minimum] 이 세 번(0/1/49) 나왔다 — 값은 같아도
            // 같은 키가 여러 줄이면 무엇을 검사하는지 흐려진다.
            StageClearGrade[] grades =
            {
                StageClearGrade.Minimum, StageClearGrade.Half, StageClearGrade.Perfect,
            };
            for (int i = 0; i < grades.Length; i++)
                sb.AppendLine($"reward.rewardFlags[{grades[i]}] = {StageRewardCalculator.GetRewardFlagsForGrade(grades[i])}");

            sb.AppendLine($"reward.clearGrade[10/0] = {StageRewardCalculator.GetClearGrade(10, 0)}");
        }

        private static void DumpAutoBattleRewards(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## idle.autoBattleRewards  (System.Random 고정 시드라 결정적)");

            double[] clearCounts = { 0d, 0.5d, 1d, 1.5d, 3d, 12d, 24d, 30d };
            int[] seeds = { 0, 1, 12345 };

            for (int s = 0; s < seeds.Length; s++)
            {
                for (int c = 0; c < clearCounts.Length; c++)
                {
                    List<PointRewardEntry> rewards = StageRewardCalculator.BuildAutoBattleRewards(
                        7, clearCounts[c], seeds[s]);
                    sb.AppendLine($"autoReward[seed={seeds[s]}][clear={Num(clearCounts[c])}] = {Describe(rewards)}");
                }
            }
        }

        private static void DumpDiceMeta(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## dice.meta  (DiceMetaDataDatabase 에셋 + 레벨 배수)");

            for (int d = 0; d < DumpDiceTypes.Length; d++)
            {
                DiceType diceType = DumpDiceTypes[d];
                sb.AppendLine($"dice[{diceType}].baseCooldown = {Num(DiceMetaDataProvider.GetBaseCooldown(diceType))}");
                sb.AppendLine($"dice[{diceType}].isMythic = {DiceMetaDataProvider.IsMythic(diceType)}");
                sb.AppendLine($"dice[{diceType}].summonable = {DiceMetaDataProvider.IsSummonable(diceType)}");
                sb.AppendLine($"dice[{diceType}].canMerge = {DiceMetaDataProvider.CanMerge(diceType)}");
                sb.AppendLine($"dice[{diceType}].baseElement = {DiceMetaDataProvider.GetBaseElementType(diceType)}");

                // 4단계 사전 조사에서 드러난 사각지대를 덮는다. 진화 배선·표시 텍스트·마일스톤·
                // 원소는 골든에 키가 하나도 없어서, MergeMeta 를 제거할 때 무엇이 바뀌는지
                // 아무 증거도 남지 않았다. 수치가 아니라고 빼 두면 "조용히 바뀌는" 자리가 된다.
                //
                // 조합식(recipe)이 있던 자리다. 조합을 진화로 갈아엎으면서 키 이름도 바뀐다 —
                // 지키려는 것은 같다: <b>상위 다이스로 가는 길이 조용히 바뀌지 않을 것.</b>
                // 이제 그 길은 에셋이 아니라 OJ.Dice.DiceEvolution 의 표에 있고, 비용까지
                // 같이 찍는다(진화 개편에서 비용이 규칙의 일부가 됐다).
                sb.AppendLine($"dice[{diceType}].tier = {DiceEvolution.GetTier(diceType)}");
                sb.AppendLine($"dice[{diceType}].evolveTo = " +
                              (DiceEvolution.TryGetEvolveTarget(diceType, out DiceType evolveTarget)
                                  ? evolveTarget.ToString()
                                  : "<none>"));
                sb.AppendLine($"dice[{diceType}].evolveCost = {DiceEvolution.GetEvolveCost(diceType)}");
                sb.AppendLine($"dice[{diceType}].exchangeCost = {DiceEvolution.GetExchangeCost(diceType)}");

                var meta = DiceMetaDataProvider.GetMeta(diceType);
                if (meta == null)
                {
                    sb.AppendLine($"dice[{diceType}].meta = <null>");
                    continue;
                }

                sb.AppendLine($"dice[{diceType}].displayName = {meta.displayName}");
                sb.AppendLine($"dice[{diceType}].description = {OneLine(meta.description)}");
                sb.AppendLine($"dice[{diceType}].elementType = {DescribeElements(meta.elementType)}");
                sb.AppendLine($"dice[{diceType}].showStarUI = {meta.showStarUI}");
                sb.AppendLine($"dice[{diceType}].baseAttack = {meta.baseAttack}");
                sb.AppendLine($"dice[{diceType}].levelUpAttackIncrease = {meta.levelUpAttackIncrease}");
                sb.AppendLine($"dice[{diceType}].milestones = {DescribeMilestones(meta.milestones)}");

                for (int l = 0; l < DumpLevels.Length; l++)
                {
                    int level = DumpLevels[l];
                    sb.AppendLine($"dice[{diceType}].levelDamageMul[{level}] = {Num(DiceMetaDataProvider.GetLevelDamageMultiplier(diceType, level))}");
                    sb.AppendLine($"dice[{diceType}].levelCooldownMul[{level}] = {Num(DiceMetaDataProvider.GetLevelCooldownMultiplier(diceType, level))}");

                    var cost = DiceMetaDataProvider.GetUpgradeCost(diceType, level);
                    sb.AppendLine($"dice[{diceType}].upgradeCost[{level}] = {cost.goldCost}g/{cost.scrollCost}s");
                }
            }

            for (int l = 0; l < DumpLevels.Length; l++)
            {
                int level = DumpLevels[l];
                sb.AppendLine($"dice.thunderTargets[{level}] = {DiceMetaDataProvider.GetThunderTargetCount(level)}");
                sb.AppendLine($"dice.windPushChance[{level}] = {Num(DiceMetaDataProvider.GetWindPushChancePercent(level))}");
                sb.AppendLine($"dice.windTargets[{level}] = {DiceMetaDataProvider.GetWindTargetCount(level)}");
                sb.AppendLine($"dice.windDistanceMul[{level}] = {Num(DiceMetaDataProvider.GetWindDistanceMultiplier(level))}");
                sb.AppendLine($"dice.timeCooldownReduce[{level}] = {Num(DiceMetaDataProvider.GetTimeCooldownReducePercent(level))}");
                sb.AppendLine($"dice.timeTargets[{level}] = {DiceMetaDataProvider.GetTimeTargetCount(level)}");
                sb.AppendLine($"dice.stunChance[{level}] = {Num(DiceMetaDataProvider.GetStunChancePercent(level))}");
                sb.AppendLine($"dice.armorBreak[{level}] = {DiceMetaDataProvider.GetArmorBreakPercent(level)}");
            }
        }

        private static void DumpIdleConversion(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## idle.conversion  (경과 시간 → 클리어 횟수 / 고기 세트)");

            sb.AppendLine($"idle.autoBattleMaxSeconds = {Num(IdleRewardManager.AutoBattleMaxSeconds)}");
            sb.AppendLine($"idle.secondsPerClear = {Num(IdleRewardManager.SecondsPerAutoBattleClear)}");
            sb.AppendLine($"idle.meatSetIntervalSeconds = {Num(IdleRewardManager.MeatSetIntervalSeconds)}");
            sb.AppendLine($"idle.meatPerSet = {IdleRewardManager.MeatPerSet}");
            sb.AppendLine($"idle.maxMeatSetCount = {IdleRewardManager.MaxMeatSetCount}");

            double[] elapsedSamples =
            {
                0d, 1d, 600d, 1200d, 1800d, 3600d, 21600d, 28800d, 28801d, 100000d,
            };

            for (int i = 0; i < elapsedSamples.Length; i++)
            {
                double elapsed = elapsedSamples[i];
                double capped = Math.Min(IdleRewardManager.AutoBattleMaxSeconds, elapsed);
                double clearCount = capped / IdleRewardManager.SecondsPerAutoBattleClear;
                int meatSets = (int)Math.Floor(elapsed / IdleRewardManager.MeatSetIntervalSeconds);
                meatSets = Mathf.Clamp(meatSets, 0, IdleRewardManager.MaxMeatSetCount);

                sb.AppendLine($"idle.cappedSeconds[{Num(elapsed)}] = {Num(capped)}");
                sb.AppendLine($"idle.clearCount[{Num(elapsed)}] = {Num(clearCount)}");
                sb.AppendLine($"idle.meatSets[{Num(elapsed)}] = {meatSets}");
            }
        }

        /// <summary>
        /// OJ.Core 순수 함수를 <b>합성 입력</b>으로 직접 두드린다.
        ///
        /// 왜 필요한가 — 에셋만으로는 검출력에 구멍이 뚫린다. 적대적 검증에서 실제로
        /// 확인된 사실이다:
        ///
        /// * 현재 30스테이지가 전부 `monstersPerWave = 20`(짝수)이라
        ///   `CeilToInt(x * 0.5f)` 를 `x / 2`(정수 나눗셈)로 바꿔도 <b>골든 231줄이
        ///   하나도 안 틀린다.</b> 홀수에서만 갈리기 때문이다.
        /// * 전부 `baseMonsterDefense = 0`이라 `ResolvedBaseDefense` 의 조기 반환 분기가
        ///   한 번도 안 밟힌다.
        /// * `totalWaves` 가 10/20 이 아닌 값뿐이라 그 두 분기도 안 밟힌다.
        ///
        /// 그래서 여기서는 <b>일부러 그 경계를 밟는 값</b>을 넣는다. 이 구획은 순수 함수와
        /// 기본형 인자만 쓰므로 OJ.Core.Tests 가 그대로 재현할 수 있다 —
        /// 에셋 기반 구획과 달리 테스트 어셈블리에서 검증 가능한 유일한 부분이다.
        /// </summary>
        /// <summary>
        /// 배수 3단(크리 → 일반 lv12 더블 → 유물)을 잠근다. (5.1-b)
        ///
        /// 이 구획이 지키는 것은 <b>세 단이 각각 따로 정수로 접힌다</b>는 사실이다.
        /// 1단과 2단을 <c>RoundToInt(d * 크리배수 * 2f)</c> 로 합치면 값이 갈리고,
        /// 3단의 하한 1 을 빼면 damage 0 근처에서 갈린다.
        ///
        /// 격자는 경계를 일부러 밟는다:
        ///  - 배수 2.0/2.2 는 실제로 쓰이는 두 값(KingNormal lv≥12 여부로 갈린다)
        ///  - damage 1·2·3·7·8·12 는 <b>1·2단 접기가 실제로 갈리는 점</b>으로, 초안이
        ///    Mono 실측으로 찾아 둔 값이다. 재구현으로 뽑은 것이 아니다
        ///  - damage 0 은 3단 하한 1 을 밟는 유일한 입력이다
        ///  - 0.5f/1.5f 는 은행가 반올림이 짝수로 내리는 것을 잡는다
        /// </summary>
        private static void DumpCriticalFormula(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## core.crit  (배수 3단 — 각 단이 따로 접힌다)");

            int[] damages = { 0, 1, 2, 3, 7, 8, 12, 13, 25, 99, 100, 1000 };
            float[] critMuls = { 0.5f, 1f, 1.5f, 2f, 2.2f, 3f };
            float[] relicMuls = { 0f, 0.5f, 1f, 1.15f, 1.5f, 2f };

            for (int d = 0; d < damages.Length; d++)
            {
                for (int c = 0; c < critMuls.Length; c++)
                {
                    sb.AppendLine($"core.crit.criticalDamage[{damages[d]}][{Num(critMuls[c])}] = " +
                                  $"{CriticalFormula.CriticalDamage(damages[d], critMuls[c])}");
                }

                sb.AppendLine($"core.crit.doubleHitDamage[{damages[d]}] = " +
                              $"{CriticalFormula.DoubleHitDamage(damages[d])}");

                for (int r = 0; r < relicMuls.Length; r++)
                {
                    sb.AppendLine($"core.crit.relicDamage[{damages[d]}][{Num(relicMuls[r])}] = " +
                                  $"{CriticalFormula.RelicDamage(damages[d], relicMuls[r])}");
                }
            }

            // 3단 전부를 통과시키는 조합. 여기서 "각 단이 따로 접힌다"가 잠긴다.
            bool[] flags = { false, true };
            for (int d = 0; d < damages.Length; d++)
            {
                for (int ci = 0; ci < flags.Length; ci++)
                {
                    for (int di = 0; di < flags.Length; di++)
                    {
                        for (int ri = 0; ri < flags.Length; ri++)
                        {
                            int applied = CriticalFormula.ApplyCritical(
                                damages[d], flags[ci], 2.2f, flags[di], flags[ri], 1.15f);
                            sb.AppendLine($"core.crit.applied[{damages[d]}][{flags[ci]}][{flags[di]}][{flags[ri]}] = {applied}");
                        }
                    }
                }
            }

            // 술어 4종. 단축평가 때문에 호출부에서 두 함수로 갈라 둔 것들이다.
            float[] chances = { -1f, 0f, 0.0001f, 10f, 50f, 100f };
            for (int i = 0; i < chances.Length; i++)
            {
                sb.AppendLine($"core.crit.chanceActive[{Num(chances[i])}] = " +
                              $"{CriticalFormula.IsCriticalChanceActive(chances[i])}");
            }

            float[] rolls = { 0f, 0.09f, 0.1f, 0.100001f, 0.2f, 0.200001f, 0.5f, 1f };
            for (int r = 0; r < rolls.Length; r++)
            {
                for (int i = 0; i < chances.Length; i++)
                {
                    sb.AppendLine($"core.crit.rollHitsCritical[{Num(rolls[r])}][{Num(chances[i])}] = " +
                                  $"{CriticalFormula.RollHitsCritical(rolls[r], chances[i])}");
                }

                sb.AppendLine($"core.crit.rollHitsDoubleHit[{Num(rolls[r])}] = " +
                              $"{CriticalFormula.RollHitsDoubleHit(rolls[r])}");
            }

            int[] levels = { 1, 8, 9, 11, 12, 13, 20 };
            for (int i = 0; i < levels.Length; i++)
            {
                sb.AppendLine($"core.crit.doubleHitLevel[{levels[i]}] = " +
                              $"{CriticalFormula.IsDoubleHitLevel(levels[i])}");
            }
        }

        private static void DumpCoreFormulas(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## core.stageGrowth  (합성 입력 — 에셋이 안 밟는 경계를 노린다)");

            // 홀수 포함. 짝수만으로는 CeilToInt -> 정수 나눗셈 변이를 못 잡는다.
            int[] monsterCounts = { 1, 2, 3, 7, 19, 20, 21, 40, 41 };
            for (int i = 0; i < monsterCounts.Length; i++)
                sb.AppendLine($"core.bossSpawnThreshold[{monsterCounts[i]}] = {StageGrowthFormula.BossSpawnThreshold(monsterCounts[i])}");

            // baseMonsterDefense > 0 은 조기 반환, 0 은 Mathf.Pow 경로.
            // totalWaves 10 / 20 / 그 외로 세 분기를 전부 밟는다.
            int[] baseDefenses = { 0, 1, 7 };
            int[] stageIndexes = { 1, 2, 3, 7, 10, 17, 30, 100 };
            int[] totalWavesSet = { 8, 10, 15, 20 };
            for (int b = 0; b < baseDefenses.Length; b++)
            {
                for (int s = 0; s < stageIndexes.Length; s++)
                {
                    for (int w = 0; w < totalWavesSet.Length; w++)
                    {
                        int value = StageGrowthFormula.ResolvedBaseDefense(
                            baseDefenses[b], stageIndexes[s], totalWavesSet[w]);
                        sb.AppendLine($"core.resolvedBaseDefense[{baseDefenses[b]}][{stageIndexes[s]}][{totalWavesSet[w]}] = {value}");
                    }
                }
            }

            int[] waves = { 1, 2, 3, 7, 12, 20 };
            for (int w = 0; w < waves.Length; w++)
            {
                sb.AppendLine($"core.monsterHp[20][0.16][0.02][{waves[w]}] = {StageGrowthFormula.MonsterHp(20, 0.16f, 0.02f, waves[w])}");
                sb.AppendLine($"core.monsterHp[7][0.145][0.018][{waves[w]}] = {StageGrowthFormula.MonsterHp(7, 0.145f, 0.018f, waves[w])}");
                sb.AppendLine($"core.monsterDefense[4][0.12][0.015][{waves[w]}] = {StageGrowthFormula.MonsterDefense(4, 0.12f, 0.015f, waves[w])}");
                sb.AppendLine($"core.bossHp[{waves[w]}] = {StageGrowthFormula.BossHp(waves[w] * 13, 6.4f)}");
                sb.AppendLine($"core.bossDefense[{waves[w]}] = {StageGrowthFormula.BossDefense(waves[w] * 3, 2.5f)}");
            }

            sb.AppendLine();
            sb.AppendLine("## core.stageReward  (합성 입력)");

            int[] rewardStages = { 1, 9, 10, 11, 20, 21, 30, 100 };
            for (int i = 0; i < rewardStages.Length; i++)
            {
                sb.AppendLine($"core.stageBonus[{rewardStages[i]}] = {StageRewardFormula.StageBonus(rewardStages[i])}");
                sb.AppendLine($"core.guaranteedGold[{rewardStages[i]}] = {StageRewardFormula.GuaranteedNormalGold(rewardStages[i])}");
            }

            // 0.999 / 0.5 임계값을 정확히 밟는다.
            int[][] wallPairs =
            {
                new[] { 0, 100 }, new[] { 1, 100 }, new[] { 49, 100 }, new[] { 50, 100 },
                new[] { 99, 100 }, new[] { 100, 100 }, new[] { 999, 1000 }, new[] { 1000, 1000 },
                new[] { 10, 0 }, new[] { 5, 3 },
            };
            for (int i = 0; i < wallPairs.Length; i++)
            {
                sb.AppendLine($"core.clearGradeTier[{wallPairs[i][0]}/{wallPairs[i][1]}] = " +
                              $"{StageRewardFormula.ClearGradeTier(wallPairs[i][0], wallPairs[i][1])}");
            }

            float[] multipliers = { 0f, 0.25f, 0.5f, 0.999f, 1f, 1.5f, -0.5f };
            int[] amounts = { 0, 1, 3, 20, 150, 999 };
            for (int a = 0; a < amounts.Length; a++)
            {
                for (int m = 0; m < multipliers.Length; m++)
                {
                    sb.AppendLine($"core.scaleAmount[{amounts[a]}][{Num(multipliers[m])}] = " +
                                  $"{StageRewardFormula.ScaleAmount(amounts[a], multipliers[m])}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## core.idleReward  (합성 입력 — 아래 3종은 다른 구획이 안 덮는다)");

            // 예전 idle 구획은 식을 덤퍼 안에 다시 써서 비교했다. 그러면 코드를 바꿔도
            // 항상 일치해 검사가 무의미하다. 이제 실제 순수 함수를 부른다.
            long[] startTicks = { 0L, -1L, 1_000_000_000L, 638_000_000_000_000_000L };
            long[] nowOffsets = { -1L, 0L, 1L, 10_000L, 10_000_000L, 288_000_000_000L };
            for (int s = 0; s < startTicks.Length; s++)
            {
                for (int n = 0; n < nowOffsets.Length; n++)
                {
                    long now = startTicks[s] + nowOffsets[n];
                    sb.AppendLine($"core.elapsedSeconds[{startTicks[s]}][{now}] = " +
                                  $"{Num(IdleRewardFormula.ElapsedSeconds(startTicks[s], now))}");
                }
            }

            double[] elapsed = { 0d, 0.0000004d, 1d, 1199d, 1200d, 1201d, 21599d, 21600d, 28800d, 28801d, 1e9d };
            for (int i = 0; i < elapsed.Length; i++)
            {
                double capped = IdleRewardFormula.CappedElapsedSeconds(elapsed[i], IdleRewardManager.AutoBattleMaxSeconds);
                sb.AppendLine($"core.capped[{Num(elapsed[i])}] = {Num(capped)}");
                sb.AppendLine($"core.clearCount[{Num(elapsed[i])}] = " +
                              $"{Num(IdleRewardFormula.AutoBattleClearCount(capped, IdleRewardManager.SecondsPerAutoBattleClear))}");
                sb.AppendLine($"core.progress01[{Num(elapsed[i])}] = " +
                              $"{Num(IdleRewardFormula.Progress01(capped, IdleRewardManager.AutoBattleMaxSeconds))}");
                sb.AppendLine($"core.meatSets[{Num(elapsed[i])}] = " +
                              $"{IdleRewardFormula.StoredMeatSetCount(elapsed[i], IdleRewardManager.MeatSetIntervalSeconds, IdleRewardManager.MaxMeatSetCount)}");
                sb.AppendLine($"core.secondsUntilNextMeatSet[{Num(elapsed[i])}] = " +
                              $"{Num(IdleRewardFormula.SecondsUntilNextMeatSet(elapsed[i], IdleRewardManager.MeatSetIntervalSeconds))}");
            }

            sb.AppendLine();
            sb.AppendLine("## core.damage  (합성 입력 — 세이브와 무관해서 stable 이다)");

            // 싱글톤이 null 이던 경로를 중립값으로 재현한 것이 맞는지 여기서 잠근다.
            DumpDamageCase(sb, "neutral", 12, 3, 1, 1);
            DumpDamageCase(sb, "pip6lv12", 12, 3, 6, 12);
            DumpDamageCase(sb, "king", 104, 20, 3, 9, isKing: true, levelMul: 1.3f, kingSynergy: 1.2f);
            DumpDamageCase(sb, "equipped", 10, 4, 4, 6,
                equipmentAttack: 37, attackPercent: 0.25f, attackFlat: 12,
                earlyWaveFlat: 5, finalPercent: 0.1f, elementMul: 1.4f, relicMul: 1.15f);
            DumpDamageCase(sb, "halfBoundary", 15, 0, 1, 6, levelMul: 1.1f);
            DumpDamageCase(sb, "zeroAttack", 0, 0, 1, 1);
            DumpDamageCase(sb, "clampedInputs", 12, 3, -5, -5);

            DumpDamageMultiplierChain(sb);
            DumpIncomingDamage(sb);
        }

        /// <summary>
        /// <c>DamageFormula.Calculate</c> 의 <b>배수 곱셈 연쇄</b>를 격자로 뜬다.
        ///
        /// 왜 따로 뜨는가 — 위 core.damage 7줄은 케이스 이름만 키에 있고 인자는
        /// DamageFormulaTests 가 <b>손으로 옮겨 적어</b> 재현한다. 그래서 케이스를 늘리려면
        /// 테스트 파일도 같이 고쳐야 하고, 늘린 직후에는 골든에 그 키가 없어 테스트가 빨개진다
        /// (기준선 갱신은 사람이 F7 로만 할 수 있다). 반대로 여기는 <b>인자가 키에 전부 박혀
        /// 있어</b> 미래의 픽스처가 키만 파싱하면 손으로 옮겨 적을 것이 없다 —
        /// core.scaleAmount / core.resolvedBaseDefense 와 같은 방식이다.
        ///
        /// 키 접두사가 왜 core.damageChain 이 아닌가: DamageFormulaTests.KeyPrefix 가
        /// <c>"core.damage"</c> 이고 <b>StartsWith 로</b> 훑는다. core.damage 로 시작하는 키를
        /// 새로 만들면 그 픽스처의 EveryGoldenKeyIsConsumed 가 "테스트가 안 두드리는 키"라며
        /// 터진다 — 그것도 <b>사람이 F7 로 기준선을 다시 뜬 뒤에야</b> 터져서, 덤퍼를 고친
        /// 사람이 아니라 기준선을 뜬 사람이 맞는다. 그래서 접두사를 겹치지 않게 잘랐다.
        /// (같은 이유로 core.capped / core.clearCount / core.progress01 / core.meatSets /
        /// core.secondsUntilNextMeatSet / core.elapsedSeconds / core.bossHp / core.bossDefense /
        /// core.bossSpawnThreshold / core.resolvedBaseDefense / core.monsterHp /
        /// core.monsterDefense / core.stageBonus / core.guaranteedGold / core.clearGradeTier /
        /// core.scaleAmount 로 시작하는 이름도 쓸 수 없다.)
        ///
        /// 무엇을 노리는가 — 5.1 은 CalculateDamage 의 값 수집을 스냅샷으로 바꾼다. 그때
        /// <c>scaled *= a; scaled *= b;</c> 를 <c>scaled *= (a * b)</c> 로 합치고 싶어지는데,
        /// float 곱은 결합법칙이 성립하지 않아 <b>Mono 에서만</b> 결과가 갈릴 수 있다.
        /// 기존 7줄은 배수가 대부분 1f 라 이 변이를 통과시킨다.
        ///
        /// <b>실측해서 고른 격자다.</b> 처음에는 "배수를 1f 가 아닌 값으로 채우고 표본을 넓히면
        /// 하나쯤 걸리겠지"라는 밀도 논리로 baseAttack 을 {7, 15, 33, 104, 255} 로 잡았는데,
        /// float32 로 270개를 차분 대조해 보니 <b>접기 변이를 잡는 케이스가 0개였다.</b>
        /// (반올림 <i>전</i> float 비트가 갈린 것은 67개인데, 그 중 정수 경계를 넘는 것이
        /// 하나도 없었다. 비트가 갈리는 것과 회귀가 보이는 것은 다르다.)
        ///
        /// 그래서 반대로 <b>갈리는 입력을 먼저 찾아서</b> 넣었다. baseAttack = 125 는
        /// pip=3, levelMul=1.3, elementMul=1.4, relicMul=1.2 에서
        /// <c>scaled *= em; scaled *= rm</c> 를 <c>scaled *= (em * rm)</c> 로 합치면
        /// 409 → 410 으로 갈린다. 이 한 줄이 이 구획의 존재 이유다 — 나머지 323개는 밀도다.
        ///
        /// 같은 실측에서 나온 것 둘:
        ///  * .5 정확히 위에 떨어지는 케이스가 20개다(양방향으로 갈린다). 그래서
        ///    <c>Mathf.RoundToInt</c> 를 <c>(int)(x + 0.5f)</c> 로 바꾸는 변이를 잡는다.
        ///    <b>기존 core.damage 7줄은 이걸 하나도 못 잡는다</b> — 이름이 halfBoundary 인
        ///    케이스조차 반올림 직전 값이 8.25 라 .5 경계가 아니다.
        ///  * <c>scaled *= LevelDamageMultiplier; scaled *= KingSynergyMultiplier;</c> 쪽
        ///    접기는 <b>이 격자로 못 잡는다.</b> KingSynergy 를 1f 로 고정했기 때문이고,
        ///    IEEE754 에서 x * 1f 는 비트가 보존되므로 그 접기는 애초에 무연산이다.
        ///    KingSynergy 축을 {1f, 1.2f} 로 늘려 648줄까지 키워 봤지만 그래도 0개였다 —
        ///    줄만 두 배가 되고 검출은 안 늘어서 넣지 않았다. 아래 '못 잠그는 축' 9번에 남긴다.
        ///
        /// 인자 외의 필드는 전부 중립이다 — LevelUpAttackIncrease=0, BulletLevel=1,
        /// EquipmentAttackTotal=0, KingSynergyMultiplier=1f, IsKingDice=false,
        /// AttackPercentBonus=0f, AttackFlatBonus=0, EarlyWaveFlatBonus=0,
        /// FinalDamagePercentBonus=0f. 미래의 픽스처는 이 목록대로 채워야 재현된다.
        /// </summary>
        private static void DumpDamageMultiplierChain(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## core.dmgChain  (합성 입력 — 배수 곱셈 연쇄. 키 = [baseAttack][pip][levelMul][elementMul][relicMul])");

            // 홀수를 섞는다. pip=1 이면 반올림 직전 값이 baseAttack * 0.5f 라 정확히 .5 로
            // 시작하고, 거기에 배수를 곱하면 경계 근처에 떨어질 확률이 올라간다.
            //
            // 125 는 밀도로 고른 것이 아니라 <b>접기 변이를 잡는다고 실측한 값</b>이다.
            // 이 값을 빼면 이 구획은 접기 변이를 하나도 못 잡는 상태로 되돌아간다.
            // 격자를 줄일 일이 있어도 125 만은 남길 것 — 윗줄 주석에 근거가 있다.
            int[] baseAttacks = { 7, 15, 33, 104, 125, 255 };
            int[] pips = { 1, 3 };
            float[] levelMuls = { 1f, 1.1f, 1.3f };
            float[] elementMuls = { 1f, 1.15f, 1.4f };
            float[] relicMuls = { 1f, 1.05f, 1.2f };

            for (int b = 0; b < baseAttacks.Length; b++)
            {
                for (int p = 0; p < pips.Length; p++)
                {
                    for (int l = 0; l < levelMuls.Length; l++)
                    {
                        for (int e = 0; e < elementMuls.Length; e++)
                        {
                            for (int r = 0; r < relicMuls.Length; r++)
                            {
                                int damage = DamageFormula.Calculate(new DamageInputs
                                {
                                    BaseAttack = baseAttacks[b],
                                    LevelUpAttackIncrease = 0,
                                    DicePip = pips[p],
                                    BulletLevel = 1,
                                    EquipmentAttackTotal = 0,
                                    LevelDamageMultiplier = levelMuls[l],
                                    KingSynergyMultiplier = 1f,
                                    IsKingDice = false,
                                    AttackPercentBonus = 0f,
                                    AttackFlatBonus = 0,
                                    EarlyWaveFlatBonus = 0,
                                    FinalDamagePercentBonus = 0f,
                                    ElementUpgradeMultiplier = elementMuls[e],
                                    RelicDamageMultiplier = relicMuls[r],
                                });

                                sb.AppendLine(
                                    $"core.dmgChain[{baseAttacks[b]}][{pips[p]}][{Num(levelMuls[l])}]" +
                                    $"[{Num(elementMuls[e])}][{Num(relicMuls[r])}] = {damage}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 받는 쪽 순수 함수(<c>IncomingDamageFormula</c>) 전부를 <b>합성 입력</b>으로 두드린다.
        /// (MIGRATION_BASELINE 5.1-a2)
        ///
        /// 왜 이 구획이 생겼는가 — 5.1-a 가 Monster/Wall 의 받는쪽 산술을 OJ.Core 로 내렸지만
        /// <b>아무도 그것을 지키지 않았다.</b> 조사 결과가 이랬다: 골든 2581키 중 받는쪽
        /// 합성식을 밟는 키 0개, EditMode 496개 중 이 경로를 건드리는 것 0개. 즉 감쇄식과
        /// 상태 피해증가를 통째로 지우고 <c>return dmg;</c> 로 바꿔도 전부 초록이었다.
        /// 이 구획이 그 구멍을 닫는다.
        ///
        /// <b>[stable] 인 근거:</b> IncomingDamageFormula 는 순수 함수 + 기본형 인자다.
        /// 싱글톤도 <c>Time.time</c> 도 에셋도 안 본다(만료 판정은 <c>now</c> 를 인자로 받는다).
        /// 그래서 로비 F7 에서 안전하고, OJ.Core.Tests 가 키만 파싱해 그대로 재현할 수 있다 —
        /// core.damage 7줄처럼 인자를 손으로 옮겨 적을 것이 없다.
        ///
        /// <b>격자는 계산이 아니라 실측으로 골랐다.</b> 후보 격자를 놓고 변이체를 실제로
        /// 컴파일해 Unity 의 Mono 로 돌려서 "이 격자가 그 변이를 잡는가"를 셌다. 아래 축 값은
        /// 전부 그 표에서 살아남은 것이다. 잡히는 것으로 확인된 변이:
        ///
        ///   중간 대입 접기   <c>float t = dmg*defMul; ceil(t*incMul)</c>   576점 중 9점만 잡는다
        ///   CeilToInt 치환   RoundToInt / FloorToInt / (int) 절단          237 / 395 / 344점
        ///   double 승격      <c>Math.Ceiling((double)dmg * ...)</c>        41점
        ///   백분율 표기      <c>*0.01f</c> → <c>/100f</c>, <c>0.01f</c> → <c>0.01</c>  각 83점
        ///   하한 추가        <c>Mathf.Max(1, ·)</c> / <c>Mathf.Max(0, ·)</c>  135 / 55점
        ///   감쇄·증가 삭제   각각 310 / 360점,  <c>return dmg</c> 는 417점
        ///   감쇄식 분모      <c>100f - armor</c> → <c>100f + armor</c>     defMul 17점 중 8점
        ///   두 분기 교체 / <c>2f-</c> → <c>1f-</c> / 음수 분기 삭제        16 / 8 / 8점
        ///
        /// <b>이 격자로도 못 잡는 것 둘</b>(고칠 수 있는 구멍이 아니라 성질이다):
        ///  * <c>armor &gt;= 0f</c> → <c>armor &gt; 0f</c> — armor==0 에서 두 분기가 <b>같은 값 1f</b>를
        ///    낸다(양수 100/100, 음수 2-100/100). 어떤 입력으로도 구분되지 않는 등가 변이다.
        ///  * 재괄호화 <c>dmg * (defMul * incMul)</c> — 576점에서 0점, 더 넓은 격자
        ///    (dmg 1e6 / def ±1e5 / bonus 1000)로 넓혀도 0점이었다. Mono 가 중간 결과를 확장
        ///    정밀도로 들고 가다 <b>마지막에 한 번만</b> 접기 때문이다. 접기(fold)가 잡히고
        ///    재괄호화가 안 잡히는 비대칭이 바로 그 증거다.
        ///
        /// <b>격자를 줄이려면 근거를 다시 실측할 것.</b> 6x7x6(252점)까지는 위 변이를 전부 잡는
        /// 것으로 측정됐지만, 그러면 접기를 잡는 점이 9 → 4 로 준다. 접기는 이 파일의 주석이
        /// 명시적으로 경고하는 변이이고 검출이 1.6% 로 가장 희소해서, 그 여유를 돈 주고 샀다.
        /// </summary>
        private static void DumpIncomingDamage(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## core.incoming  (받는쪽 순수 함수 — 격자는 변이 실측으로 골랐다)");

            // ── 방어력 감쇄 ──────────────────────────────────────────────────────────
            // -100 은 <b>변이체의 분모</b>가 0 이 되는 유일한 값이다(정본은 100f - armor 라
            // 멀쩡히 1.5f 를 낸다). 여기를 안 밟으면 100f-armor → 100f+armor 변이가
            // -Infinity 를 내면서도 골든에 흔적을 안 남긴다. ±1 이웃(-101/-99)은 그 지점이
            // 특이점이 아니라는 것을 같이 박아 둔다.
            //
            // 0 은 분기 경계이고, ±1 은 경계 양옆이다. 0 자체는 두 분기가 같은 값을 내서
            // 분기 교체를 구분하지 못한다 — 구분하는 것은 ±1 쪽이다.
            //
            // -300 / -100 / 100 / 300 은 배수가 각각 1.75 / 1.5 / 0.5 / 0.25 로 <b>2의 거듭제곱
            // 분모</b>라 곱이 정확히 떨어진다. 아래 applied 격자에서 CeilToInt 가 올림을 안 하는
            // 쪽(정수 정확)과 정확히 .5 인 쪽을 만드는 것이 이 값들이다.
            // ±100000 은 반환 범위 주장 — 양수 분기 0 초과 1 이하, 음수 분기 1 초과 2 미만 —
            // 의 양 끝을 밟는다.
            int[] armors =
            {
                -100000, -300, -200, -101, -100, -99, -50, -1, 0, 1, 50, 99, 100, 101, 200, 300, 100000,
            };
            for (int i = 0; i < armors.Length; i++)
            {
                sb.AppendLine($"core.incoming.defMul[{armors[i]}] = " +
                              $"{Num(IncomingDamageFormula.DefenseMultiplier(armors[i]))}");
            }

            // ── 상태 피해증가 합산 ───────────────────────────────────────────────────
            // 두 bool 을 전부 펼쳐 합이 0 / 단일(15) / 중첩(30) 을 다 밟는다.
            // slow 축의 -15 는 <b>단일 보너스를 정확히 상쇄해 합이 0</b> 이 되는 값이고,
            // -30 은 중첩을 상쇄한다. "합이 0"과 "아무것도 안 더했다"가 구분되는 자리다.
            // 음수(-100 …)는 원본에 하한이 없다는 사실을 박제한다 — 유물 primaryValue 가
            // 음수면 실제로 도달한다. Mathf.Max(0, ·) 클램프 추가 변이를 9점에서 잡는다.
            //
            // 두 상수(왕얼음 15 / 왕독 15)를 서로 바꾸는 변이는 <b>어떤 입력으로도 못 잡는다</b> —
            // 값이 같기 때문이다. 그래서 상수를 하나로 합치지 말라는 규약은 골든이 아니라
            // 사람이 지켜야 한다.
            bool[] flags = { false, true };
            int[] slowRelicPercents = { -100, -30, -15, -1, 0, 1, 7, 15, 40 };
            for (int i = 0; i < flags.Length; i++)
            {
                for (int j = 0; j < flags.Length; j++)
                {
                    for (int k = 0; k < slowRelicPercents.Length; k++)
                    {
                        int value = IncomingDamageFormula.StateBonusPercent(
                            flags[i], flags[j], slowRelicPercents[k]);
                        sb.AppendLine($"core.incoming.stateBonus[{flags[i]}][{flags[j]}][{slowRelicPercents[k]}] = {value}");
                    }
                }
            }

            // ── 7종 합산 ─────────────────────────────────────────────────────────────
            // 격자가 아니라 행 목록이다. 7축을 곱하면 폭발하는데, 잡아야 할 변이는
            // "인자 하나를 빠뜨렸다" 하나뿐이라 행 몇 개로 충분하다.
            //
            // {1,2,4,8,16,32,64} 가 이 목록의 핵심이다 — 2의 거듭제곱이라 <b>어느 인자를
            // 빠뜨려도 합이 서로 다른 값</b>이 된다. 실측으로 인자 7개의 누락을 전부
            // 잡는 것을 확인했다(각 3~5개 행이 잡는다).
            // 뒤집은 행은 합이 같다 — <b>인자 순서는 관측 불가</b>라는 사실의 박제다.
            // int 덧셈이라 원리적으로 그렇고, 그래서 시그니처 순서는 규약으로만 지켜진다.
            int[][] totalBonusRows =
            {
                new[] { 0, 0, 0, 0, 0, 0, 0 },
                new[] { 1, 2, 4, 8, 16, 32, 64 },
                new[] { 64, 32, 16, 8, 4, 2, 1 },
                new[] { 10, 20, 10, 15, 10, 0, 30 },
                new[] { -100, 0, 0, 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0, 0, 0, -150 },
                new[] { 10, 20, 10, 15, 10, 25, -30 },
            };
            for (int i = 0; i < totalBonusRows.Length; i++)
            {
                int[] row = totalBonusRows[i];
                int value = IncomingDamageFormula.TotalBonusPercent(
                    row[0], row[1], row[2], row[3], row[4], row[5], row[6]);
                sb.AppendLine($"core.incoming.totalBonus[{row[0]}][{row[1]}][{row[2]}][{row[3]}]" +
                              $"[{row[4]}][{row[5]}][{row[6]}] = {value}");
            }

            // ── 최종 합성 ────────────────────────────────────────────────────────────
            // 축마다 근거가 있다(전부 실측):
            //
            //  dmg   0        호출부 게이트가 막는 값이지만 함수 자체는 0 을 돌려준다.
            //                 Mathf.Max(1, ·) 추가 변이를 여기서 잡는다.
            //        1,2,3    2는 defMul 0.5/1.5 와 만나 정수 정확, 3은 정확히 .5 를 만든다.
            //                 CeilToInt → RoundToInt 치환이 갈리는 자리다.
            //        7,9      접기(fold)를 잡는 dmg. 9 는 <b>결과가 음수인 구간</b>에서 접기를
            //                 잡는 유일한 값이다(bonus=-200 과 짝). 양수 구간만 보면 놓친다.
            //        100      접기를 bonus=-1 / 50 양쪽에서 잡는다.
            //        1000000  큰 값. 접기를 [1000000][1][-1] 에서 잡는다 — 상대오차가
            //                 작아도 정수 경계를 넘으면 그대로 새어 나온다는 증거다.
            //        16777217 2^24+1. <b>float 로 표현할 수 없는 첫 홀수 int</b> 다.
            //                 여기까지 와야 잠기는 것이 하나 있다 — <c>dmg</c> 가 곱해지기 전에
            //                 <b>double 이 아니라 float 로</b> 승격된다는 사실이다. C# 이항 수치
            //                 승격이 int→float 를 먼저 하므로 16777217 은 곱하기 전에 이미
            //                 16777216 으로 깎인다. dmg 축이 2^24 아래에만 있으면 이 깎임이
            //                 관측되지 않아서, <c>(float)((double)dmg * a * b)</c> 로 바꾸는 변이가
            //                 <b>1000000 까지의 격자를 전부 통과한다</b>(실측). 이 한 값이 그 변이를
            //                 72개 (def,bonus) 조합 중 48개에서 잡는다.
            //                 더 큰 값(2147483647)은 오히려 0개다 — 거기서는 두 경로가 도로 같아진다.
            //
            //  def   -300,-100  배수 1.75 / 1.5 (정확). 음수 방어=증폭 분기.
            //        -50        배수 1.33333337 (부정확). 접기를 잡는 두 값 중 하나.
            //        -1,0,1     분기 경계와 그 양옆.
            //        50         배수 0.6666667 (부정확). 접기를 잡는 나머지 하나.
            //        100,300    배수 0.5 / 0.25 (정확). dmg 홀짝으로 .5 경계를 만든다.
            //
            //  bonus -200   incMul = -1f. <b>결과가 음수</b>가 되는 유일한 축값이고,
            //               Mathf.Max(0, ·) 클램프 변이를 잡는 <b>유일한</b> 값이다(실측).
            //        -100   incMul = 0f. 주석이 말하는 "0 이 나오는 유일한 조건"의 박제.
            //        -50,50 incMul = 0.5f / 1.5f (정확).
            //        -1     incMul = 0.99f (부정확). 접기를 잡는다.
            //        0      항등.
            //        15     왕얼음/왕독 리터럴과 같은 값. incMul = 1.15f (부정확).
            //        100    incMul = 2f (정확).
            int[] dmgs = { 0, 1, 2, 3, 7, 9, 100, 1000000, 16777217 };
            int[] defenses = { -300, -100, -50, -1, 0, 1, 50, 100, 300 };
            int[] bonuses = { -200, -100, -50, -1, 0, 15, 50, 100 };
            for (int d = 0; d < dmgs.Length; d++)
            {
                for (int a = 0; a < defenses.Length; a++)
                {
                    for (int b = 0; b < bonuses.Length; b++)
                    {
                        int applied = IncomingDamageFormula.AppliedDamage(dmgs[d], defenses[a], bonuses[b]);
                        sb.AppendLine($"core.incoming.applied[{dmgs[d]}][{defenses[a]}][{bonuses[b]}] = {applied}");
                    }
                }
            }

            // ── 시간 판정 ────────────────────────────────────────────────────────────
            // 두 함수의 부등호가 다른 것이 요점이라 <b>같은 표를 양쪽에 먹인다.</b>
            // 나란히 놓여야 "경계에서 둘 다 꺼짐"이 diff 에서 한눈에 보인다.
            //
            // NaN 두 줄이 이 표의 핵심이다. 실측하면 <b>둘 다 False</b> 가 나온다 —
            // 즉 <c>IsBonusExpired</c> 를 <c>!IsStateActive</c> 로 합치는 변이는 NaN 에서만
            // 갈리고, 이 두 줄이 없으면 12줄이 전부 통과한다.
            // -1f 는 OnSpawn 초기값이고, ±1ULP 는 경계가 '<' 인지 '<=' 인지를 가른다.
            float[][] timePairs =
            {
                new[] { 0f, -1f }, new[] { -1f, -1f }, new[] { 0f, 0f }, new[] { 0f, 1f },
                new[] { 1f, 1f }, new[] { 1.00000012f, 1f }, new[] { 0.99999994f, 1f },
                new[] { 100f, 100f }, new[] { 100f, 99.99999f },
                new[] { float.NaN, 1f }, new[] { 1f, float.NaN },
                new[] { float.PositiveInfinity, 1f },
            };
            for (int i = 0; i < timePairs.Length; i++)
            {
                float now = timePairs[i][0];
                float until = timePairs[i][1];
                sb.AppendLine($"core.incoming.stateActive[{Num(now)}][{Num(until)}] = " +
                              $"{IncomingDamageFormula.IsStateActive(now, until)}");
                sb.AppendLine($"core.incoming.bonusExpired[{Num(now)}][{Num(until)}] = " +
                              $"{IncomingDamageFormula.IsBonusExpired(now, until)}");
            }

            // ── 벽 ───────────────────────────────────────────────────────────────────
            // 하한 0 클램프가 실제로 밟히는 줄(10/11, 10/1000, 0/5, -3/2)과 안 밟히는 줄을
            // 같이 둔다. dmg 가 음수인 줄(10/-5 → 15)은 상한이 없다는 사실의 박제다.
            int[][] wallHpPairs =
            {
                new[] { 0, 0 }, new[] { 10, 0 }, new[] { 10, 9 }, new[] { 10, 10 },
                new[] { 10, 11 }, new[] { 10, 1000 }, new[] { 0, 5 }, new[] { 10, -5 },
                new[] { -3, 0 }, new[] { -3, 2 }, new[] { 2147483647, 1 }, new[] { 1000000, 999999 },
            };
            for (int i = 0; i < wallHpPairs.Length; i++)
            {
                sb.AppendLine($"core.incoming.wallHp[{wallHpPairs[i][0]}][{wallHpPairs[i][1]}] = " +
                              $"{IncomingDamageFormula.WallHpAfterDamage(wallHpPairs[i][0], wallHpPairs[i][1])}");
            }

            // 두 비율 함수에 <b>같은 표</b>를 먹인다. 합치면 안 되는 두 함수라, 값이 갈리는
            // 줄이 파일에 나란히 남아야 한다. 실측으로 갈리는 줄은 넷이다:
            //   [0][0]     : 0f/0f = NaN   vs 가드에 걸려 0      ← SetInit(0) 이면 실제로 재현된다
            //   [1][0]     : 1f/0f = Inf   vs 가드에 걸려 0
            //   [101][100] : 1.01          vs Clamp01 로 1
            //   [150][100] : 1.5           vs Clamp01 로 1
            // 이 넷이 없으면 두 함수를 하나로 합치는 변이가 12줄을 통과한다.
            // [-1][0] 은 WallHpBarRatioOnDamage 의 "죽은 가드"를 실제로 밟는 줄이다
            // (-1f/0f = -Infinity < 0 → 0). 짝으로 불리면 도달 불가지만 단독 호출에서는 산다.
            //
            // <b>totalHp 가 음수인 두 줄은 Clamped 쪽 가드의 '모양'을 잠근다.</b>
            // 그 가드는 <c>totalHp &gt; 0</c> 인데, 흔한 리팩토링인 <c>totalHp != 0</c> 으로 바꿔도
            // totalHp 가 양수인 표만 보면 <b>한 줄도 안 갈린다</b>(실측). 갈리려면 분자와 분모가
            // 둘 다 음수여서 비율이 <b>양수</b>가 되어야 한다 — 그때만 Clamp01 이 0 이 아닌 값을
            // 통과시켜 정본의 0f 와 어긋난다:
            //   [-20][-100] : 정본 0  vs  != 0 변이 0.2
            //   [-1][-1]    : 정본 0  vs  != 0 변이 1
            // 이 가드의 유무는 두 비율 함수를 <b>합치지 말라</b>는 근거 두 개 중 하나다
            // (나머지 하나가 Clamp01). 근거인 이상 골든이 그 모양까지 붙잡고 있어야 한다.
            //
            // <c>[0][음수]</c> 는 일부러 안 넣는다. 그 줄은 <c>0f / -100f = -0f</c> 를 만드는데,
            // "R" 표기가 -0f 와 0f 를 둘 다 "0" 으로 찍어 골든이 부호를 못 담는다. 넣으면
            // 값을 잠그지도 못하면서 RoundTripTextIsBitTight 만 깨진다(그 테스트는 정직하게
            // 비트로 비교한다). 부호 있는 0 은 '못 잠그는 축'에 적어 뒀다.
            int[][] wallRatioPairs =
            {
                new[] { 0, 0 }, new[] { 1, 0 }, new[] { -1, 0 },
                new[] { 0, 100 }, new[] { 1, 3 }, new[] { 50, 100 }, new[] { 99, 100 },
                new[] { 100, 100 }, new[] { 101, 100 }, new[] { 150, 100 },
                new[] { -20, 100 }, new[] { 1, 1000000 },
                new[] { -20, -100 }, new[] { -1, -1 },
            };
            for (int i = 0; i < wallRatioPairs.Length; i++)
            {
                int hp = wallRatioPairs[i][0];
                int total = wallRatioPairs[i][1];
                sb.AppendLine($"core.incoming.wallRatioOnDamage[{hp}][{total}] = " +
                              $"{Num(IncomingDamageFormula.WallHpBarRatioOnDamage(hp, total))}");
                sb.AppendLine($"core.incoming.wallRatioClamped[{hp}][{total}] = " +
                              $"{Num(IncomingDamageFormula.WallHpBarRatioClamped(hp, total))}");
            }
        }

        /// <summary>
        /// 명중 경로의 <b>컴파일 상수</b>. 에셋·세이브·씬 어디에도 안 걸리므로 [stable] 이다.
        ///
        /// 왜 필요한가 — 4단계 조사에서 KingNormal 4연타(70% / 10%×3, 0.1초 간격)와
        /// DamageFormula 의 두 상수가 골든에 키가 <b>하나도 없다</b>고 나왔다. 값이 코드에만
        /// 있으면 바꿔도 아무 흔적이 안 남는다.
        ///
        /// 여기서 뜨는 것은 <b>실제 상수를 읽은 것</b>이지 덤퍼가 다시 적은 숫자가 아니다.
        /// 다시 적으면 코드를 바꿔도 항상 일치해 검사가 무의미해진다(예전 idle 구획이 그랬다).
        ///
        /// 못 뜨는 것도 여기 적어 둔다 — 70% / 10% 분할비는
        /// <c>AttackContent.PlayKingNormalMultiHit</c> 안의 리터럴이라 이름이 없다.
        /// private 메서드이고 Monster 인스턴스를 요구해서 덤퍼가 호출할 수도 없다.
        /// 아래 UnlockableAxes 목록에 남긴다.
        /// </summary>
        private static void DumpHitPathConstants(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## hit.constants  (명중 경로 컴파일 상수 — 에셋·세이브·씬 무관)");

            sb.AppendLine($"hit.kingNormal.totalHitCount = {KingNormalDiceEffect.TotalHitCount}");
            sb.AppendLine($"hit.kingNormal.multiHitInterval = {Num(KingNormalDiceEffect.MultiHitInterval)}");
            sb.AppendLine($"hit.kingNormal.splashRadius = {Num(KingNormalDiceEffect.SplashRadius)}");
            sb.AppendLine($"hit.kingNormal.additionalTargetCount = {KingNormalDiceEffect.AdditionalTargetCount}");

            sb.AppendLine($"hit.damageBalanceMultiplier = {Num(DamageFormula.GlobalDamageBalanceMultiplier)}");
            sb.AppendLine($"hit.kingDiceDamageMultiplier = {Num(DamageFormula.KingDiceDamageMultiplier)}");

            // GetPoisonDuration 은 인자를 무시하고 4f 를 돌려준다. 두 타입만 뜨는 이유는
            // 15줄이 전부 같은 값이면 diff 에서 무엇이 바뀐 건지 오히려 흐려지기 때문이다.
            // 타입별로 갈라지는 순간 이 두 줄이 서로 달라져 바로 드러난다.
            sb.AppendLine($"hit.poisonDuration[{DiceType.Poison}] = {Num(DiceMetaDataProvider.GetPoisonDuration(DiceType.Poison))}");
            sb.AppendLine($"hit.poisonDuration[{DiceType.KingPoison}] = {Num(DiceMetaDataProvider.GetPoisonDuration(DiceType.KingPoison))}");
        }

        /// <summary>
        /// 장비 강화 규칙표와 그 산출식(<c>EquipmentUpgradeFormula</c>) 전부. (5.2)
        ///
        /// <b>[stable] 인 근거:</b> 전부 순수 함수 + 기본형 인자다. 여기서 부르는 것 중
        /// EquipmentManager 인스턴스를 건드리는 것이 <b>하나도 없다</b> — 그것이 중요하다.
        /// 이 구획이 [environment] 의 DumpDamage 보다 앞에 오는데, 만약 여기서
        /// <c>EquipmentManager.Instance</c> 를 건드리면 MonoSingleton 생성 순서가 바뀌어
        /// state.* 구획의 값이 흔들리고, 그 흔들림이 "리팩토링 때문"으로 오독된다.
        /// <c>Define</c> 은 static 클래스라 안전하다.
        ///
        /// <b>이 구획에는 float 가 한 줄도 없다.</b> 그래서 AGENTS.md 의 Mono 확장 정밀도
        /// 함정과 무관하고, OJ.Core.Tests 가 키만 파싱해 그대로 재현할 수 있다.
        /// 반대로 말하면 <b>이 구획이 잡는 변이는 전부 "값이 다르다" 뿐</b>이다 —
        /// 반올림·접기·결합순서 같은 것은 애초에 존재하지 않는다.
        ///
        /// 격자는 경계를 일부러 밟는다:
        ///  - 규칙표 인덱스 -1 / 6 은 <c>default:</c> 를 밟는 두 방향이다. 0~5 는 6종 전부.
        ///  - 레벨 -5 / 0 은 <c>Mathf.Max(1, currentLevel)</c> 하한을 밟는 유일한 값이고,
        ///    1 은 그 하한과 <b>붙어 있는</b> 정상값이다. 셋이 같은 비용을 내는 것이 사양이다.
        ///  - 레벨 1 은 동시에 <c>Attack</c> 의 <c>level &lt;= 1 → 0</c> 조기 반환 경계다.
        ///    2 가 그 바로 위다. 10 / 50 은 곱셈항이 실제로 커진 뒤를 본다.
        ///  - 슬롯은 -1 / 0 / 4(표 마지막) / 5(MaxEquipmentSlot 경계) / 6 을 밟는다.
        ///
        /// <b>슬롯 해금은 표를 인자로 받으므로 키에 표를 통째로 적는다.</b> 그러지 않으면
        /// OJ.Core.Tests 가 <c>Define.EquipmentSlotUnlockLevels</c> 를 볼 수 없어(Assembly-CSharp)
        /// 재현이 안 된다. 덤으로 <c>Define</c> 이 바뀌면 <b>키 자체가 바뀌어</b> diff 에 크게 드러난다.
        ///
        /// 표 변형 4종은 지금 <b>도달 불가능한 분기를 일부러 켜는 것</b>이다. 현재
        /// MaxEquipmentSlot(5) 과 표 길이(5) 가 같아서 <c>(slotIndex * 10) + 1</c> 폴백이
        /// 한 번도 안 밟힌다 — 실제 값만 뜨면 그 세 줄을 통째로 지워도 골든이 안 변한다.
        /// null / 빈 표 / 짧은 표 / max 가 더 큰 경우가 그 분기를 켠다.
        /// </summary>
        private static void DumpEquipmentUpgrade(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## core.equipUpgrade  (장비 강화 규칙표 — 전부 정수라 Mono/CoreCLR 무관)");

            // Define 스냅샷. 아래 slotUnlock 키에 박힌 표와 <b>같은 값이어야 한다</b> —
            // 테스트가 그 일치를 검사한다. 두 줄이 따로 있는 이유는 사람이 diff 에서
            // "Define 이 바뀌었다"를 한 줄로 보기 위해서다.
            sb.AppendLine($"core.equipUpgrade.define.maxEquipmentSlot = {Define.MaxEquipmentSlot}");
            sb.AppendLine($"core.equipUpgrade.define.slotUnlockLevels = {DescribeSlotLevels(Define.EquipmentSlotUnlockLevels)}");

            // -1 과 6 이 default: 를 밟는 두 방향이다. 호출부(EquipmentManager.ToRuleIndex)는
            // enum 이 6값뿐이라 -1 을 절대 안 만들지만, 규칙표 자체는 받을 수 있어야 한다.
            int[] ruleIndexes = { -1, 0, 1, 2, 3, 4, 5, 6 };

            // -5 / 0 은 Mathf.Max(1, ·) 하한, 1 은 그 하한과 값이 같아야 하는 정상 최소 레벨,
            // 2 는 Attack 조기 반환 바로 위, 10 / 50 은 곱셈항이 커진 뒤.
            int[] levels = { -5, 0, 1, 2, 10, 50 };

            for (int r = 0; r < ruleIndexes.Length; r++)
            {
                int index = ruleIndexes[r];
                EquipmentUpgradeFormula.EquipmentUpgradeRule rule = EquipmentUpgradeFormula.Rule(index);

                sb.AppendLine($"core.equipUpgrade.rule[{index}][baseGold] = {rule.baseGold}");
                sb.AppendLine($"core.equipUpgrade.rule[{index}][goldPerLevel] = {rule.goldPerLevel}");
                sb.AppendLine($"core.equipUpgrade.rule[{index}][baseScroll] = {rule.baseScroll}");
                sb.AppendLine($"core.equipUpgrade.rule[{index}][scrollPerLevel] = {rule.scrollPerLevel}");
                sb.AppendLine($"core.equipUpgrade.rule[{index}][baseAttack] = {rule.baseAttack}");
                sb.AppendLine($"core.equipUpgrade.rule[{index}][attackPerLevel] = {rule.attackPerLevel}");
            }

            for (int r = 0; r < ruleIndexes.Length; r++)
            {
                for (int l = 0; l < levels.Length; l++)
                {
                    int index = ruleIndexes[r];
                    int level = levels[l];

                    sb.AppendLine($"core.equipUpgrade.goldCost[{index}][{level}] = " +
                                  $"{EquipmentUpgradeFormula.UpgradeGoldCostOf(index, level)}");
                    sb.AppendLine($"core.equipUpgrade.scrollCost[{index}][{level}] = " +
                                  $"{EquipmentUpgradeFormula.UpgradeScrollCostOf(index, level)}");
                    sb.AppendLine($"core.equipUpgrade.attack[{index}][{level}] = " +
                                  $"{EquipmentUpgradeFormula.AttackOf(index, level)}");
                }
            }

            // 표 변형 5종. 첫 줄이 실제 Define 이고 나머지 넷은 도달 불가 분기를 켜는 합성이다.
            // max 8 짜리는 표(5칸)보다 슬롯이 많은 경우라 5~7 에서 폴백이 살아난다.
            int[][] slotLevelTables =
            {
                Define.EquipmentSlotUnlockLevels,
                null,
                new int[0],
                new[] { 1, 10 },
                Define.EquipmentSlotUnlockLevels,
            };
            int[] slotMaxes =
            {
                Define.MaxEquipmentSlot,
                Define.MaxEquipmentSlot,
                Define.MaxEquipmentSlot,
                Define.MaxEquipmentSlot,
                8,
            };

            int[] slotIndexes = { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

            for (int t = 0; t < slotLevelTables.Length; t++)
            {
                for (int s = 0; s < slotIndexes.Length; s++)
                {
                    int value = EquipmentUpgradeFormula.SlotUnlockLevel(
                        slotIndexes[s], slotMaxes[t], slotLevelTables[t]);

                    sb.AppendLine($"core.equipUpgrade.slotUnlock[{slotIndexes[s]}][{slotMaxes[t]}]" +
                                  $"[{DescribeSlotLevels(slotLevelTables[t])}] = {value}");
                }
            }
        }

        /// <summary>
        /// 슬롯 해금 표를 키에 넣을 수 있는 한 토큰으로 만든다.
        /// null 과 빈 배열을 <b>다른 글자</b>로 적는 것이 요점이다 — 둘은 같은 분기로
        /// 떨어지지만 원인이 달라서, 한 글자로 뭉치면 diff 에서 구분이 안 된다.
        /// </summary>
        private static string DescribeSlotLevels(int[] levels)
        {
            if (levels == null)
                return "null";
            if (levels.Length == 0)
                return "empty";

            var text = new StringBuilder();
            for (int i = 0; i < levels.Length; i++)
            {
                if (i > 0)
                    text.Append(',');
                text.Append(levels[i].ToString(CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        // ── core.gemBonus 격자 ────────────────────────────────────────────────────────
        //
        // 전부 OJ.Core.GemBonusFormula 의 순수 함수 + 기본형 인자다. EquipmentManager 인스턴스를
        // 건드리는 것이 하나도 없으므로 [stable] 이고, MonoSingleton 생성 순서에도 영향이 없다.
        // (실제 장착 보석에서 나온 값은 세이브에 좌우돼 [environment] 의 effect.* 구획에 있다.)

        /// <summary>
        /// 다이스 코드 축. 정의된 16종에 <b>정의되지 않은 코드</b>(-1 / 5 / 7 / 99 / 199 / 206 / 1000)를
        /// 섞는다. BaseDiceType 의 default 가지(자기 자신을 그대로 돌려준다)와 ElementTypeOf 의
        /// default 가지(ElementTypeMax)를 밟으려는 것이다 — 그 두 가지가 "매칭이 조용히 실패한다"의
        /// 출구라서, 표를 손대는 변경이 여기서 먼저 드러나야 한다.
        /// </summary>
        private static readonly int[] GemDiceCodes =
        {
            -1, 0, 1, 2, 3, 4, 5, 7, 99, 100, 101, 102, 103, 104,
            199, 200, 201, 202, 203, 204, 205, 206, 1000,
        };

        // 매칭 격자. targetDiceType 에 100(Tornado)과 200(KingNormal)이 들어 있는 것이 핵심이다 —
        // BaseDiceType 의 치역은 {0,1,2,3,4} ∪ {정의 안 된 코드} 라서 둘 다 <b>절대 매칭되지 않는다.</b>
        // 에셋 리맵 사고(f0cccdb / 3a6f5bd)가 판 그 구멍을 값으로 박제한다.
        private static readonly int[] GemMatchTargetDice = { 0, 1, 3, 100, 200, 205 };

        // 원소 축. 2(Water)=Ice, 4(Dark)=Poison 처럼 <b>이름이 어긋나는</b> 대응을 일부러 고른다.
        private static readonly int[] GemMatchTargetElements = { 0, 1, 2, 4, 5 };

        // 다이스 축. 기본 / 합성(100,103) / 킹(200,203) / Max(205) 를 섞어 접기와 조기 반환을 같이 민다.
        private static readonly int[] GemMatchDice = { 0, 1, 3, 4, 100, 103, 200, 203, 205 };

        // 합산 격자의 다이스 축. 100 은 "targetDiceType 이 100 인 효과가 붙는가"가 아니라
        // "Tornado 다이스로 물으면 Normal 로 접히는가"를 본다. 205 는 조기 반환 축이다.
        private static readonly int[] GemSumDice = { 0, 1, 100, 205 };
        private static readonly int[] GemSumPercentStats = { 0, 2 };
        private static readonly int[] GemSumFlatStats = { 1, 5 };
        private static readonly int[] GemCooldownDice = { 0, 1, 3, 100, 200, 205 };
        private static readonly int[] GemWaveDice = { 0, 205 };

        // 웨이브 축. -1/0 은 조기 반환, 1~4 는 intParam 한계(아래 집합의 limit 가 1 과 3)를
        // <b>같을 때와 하나 클 때</b> 양쪽에서 밟는다. 10 은 한참 넘긴 자리다.
        private static readonly int[] GemWaveIndices = { -1, 0, 1, 2, 3, 4, 10 };

        /// <summary>
        /// 효과 집합 격자. <b>키에 통째로 인코딩되므로 테스트가 옮겨 적을 것은 집합뿐이고
        /// 계산 인자는 전부 키에서 복원된다.</b>
        ///
        /// 각 집합이 노리는 것:
        /// <code>
        ///   0  빈 집합 — 효과 0개
        ///   1  효과 1개, target 전부(205/5)
        ///   2  효과 1개, targetDiceType=100 — 리맵 사고로 죽은 모양. diceType=205 로 물을 때만 산다
        ///   3  같은 stat 3개 (0.1, 0.2, 0.3)
        ///   4  3번과 같은 원소를 <b>순서만 뒤집은 것</b>
        ///      ※ (0.1, 0.2, 0.3) 은 <b>어느 순서로 더해도 0.6</b> 이다 — 확인했다.
        ///        그래서 이 쌍은 "순서가 바뀌었다"를 <b>잡지 못한다.</b> 그 일은 18/19번이 한다.
        ///   5  음수 percent + 양수 — 항마다 걸리는 Mathf.Max(0f, ·)
        ///   6~8  쿨감 0.7999999 / 0.8 / 0.8000001 — 캡 0.8 직전·정확히·직후
        ///   9  0.5 + 0.3 — 두 항의 합이 캡 경계에 닿는다
        ///   10 0.5(전부) + 0.4(Fire 한정) — <b>다이스에 따라 캡을 넘고 안 넘는다</b>
        ///   11 flat 7 + (-3) + 5 — 항마다 걸리는 Mathf.Max(0, ·)
        ///   12 flat int.MaxValue + 1 — int 덧셈 오버플로가 음수로 돌고 Max(0, ·) 가 0 으로 접는다
        ///   13 FirstNWaves limit 3 / limit 1 — 웨이브 한계 두 개가 서로 다른 자리에서 끊긴다
        ///   14 FirstNWaves limit 0 / limit -2 — limit&lt;=0 가지
        ///   15 WellHpOnKill + GoldOnKill 을 targetDiceType=100 으로 — 리맵 사고에서 <b>살아남은</b> 19개의 모양
        ///   16 원소 지정 혼합 (Fire/Fire, 전부/Water, Ice/전부)
        ///   17 stat 이 다른 둘 (FinalDamagePercent + AttackPercent) — statType 필터가 실제로 가르는지
        ///   18 (0.1, 0.1, 0.5) — <b>순서에 실제로 민감한</b> 삼중항. 앞에서부터 더하면 0.7
        ///   19 18번을 뒤집은 (0.5, 0.1, 0.1) — 같은 원소인데 0.700000048 이다
        /// </code>
        ///
        /// <b>18/19번이 왜 따로 있는가.</b> 3/4번 쌍은 "순서만 뒤집었다"는 모양은 갖췄지만
        /// (0.1, 0.2, 0.3) 은 어느 순서로 더해도 정확히 0.6 이라 <b>순서 변경을 검출하지 못한다.</b>
        /// 실제로 SumPercent 의 순회를 뒤집는 변이를 심었을 때 이 격자 전체(18집합 × stat 2 ×
        /// dice 4 + 쿨감)가 <b>한 건도</b> 반응하지 않았다. 18/19번은 그 구멍을 메운다 —
        /// 두 집합의 sumPercent 가 <b>1 ulp 다르게</b> 뜨므로, 누적 순서를 바꾸면 두 키가 값을
        /// 맞바꾸며 골든이 깨진다. (골든 없이도 도는 검사는 픽스처의
        /// <c>SumPercentIsOrderSensitive</c> 가 맡는다.)
        private static readonly GemEffectInput[][] GemEffectSets =
        {
            new GemEffectInput[0],

            new[] { Gem(0, 205, 5, 0.25f, 0, 0) },
            new[] { Gem(0, 100, 5, 0.25f, 0, 0) },

            new[] { Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.2f, 0, 0), Gem(0, 205, 5, 0.3f, 0, 0) },
            new[] { Gem(0, 205, 5, 0.3f, 0, 0), Gem(0, 205, 5, 0.2f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0) },

            new[] { Gem(0, 205, 5, -0.5f, 0, 0), Gem(0, 205, 5, 0.25f, 0, 0) },

            new[] { Gem(2, 205, 5, 0.7999999f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.8f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.8000001f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.5f, 0, 0), Gem(2, 205, 5, 0.3f, 0, 0) },
            new[] { Gem(2, 205, 5, 0.5f, 0, 0), Gem(2, 1, 5, 0.4f, 0, 0) },

            new[] { Gem(1, 205, 5, 0f, 7, 0), Gem(1, 205, 5, 0f, -3, 0), Gem(1, 205, 5, 0f, 5, 0) },
            new[] { Gem(1, 205, 5, 0f, int.MaxValue, 0), Gem(1, 205, 5, 0f, 1, 0) },

            new[] { Gem(3, 205, 5, 0f, 10, 3), Gem(3, 205, 5, 0f, 100, 1) },
            new[] { Gem(3, 205, 5, 0f, 10, 0), Gem(3, 205, 5, 0f, 20, -2) },

            new[] { Gem(5, 100, 5, 0f, 3, 0), Gem(9, 100, 5, 0f, 4, 0) },

            new[] { Gem(0, 1, 1, 0.5f, 0, 0), Gem(0, 205, 2, 0.25f, 0, 0), Gem(1, 2, 5, 0f, 6, 0) },

            new[] { Gem(6, 205, 5, 0.15f, 0, 0), Gem(0, 205, 5, 0.15f, 0, 0) },

            // 순서 민감 쌍. 이 둘의 sumPercent 는 1 ulp 다르다(0.7 vs 0.700000048).
            // 값을 바꾸려면 반드시 Mono 에서 두 순서가 실제로 갈리는지 먼저 확인할 것 —
            // 3/4번처럼 "뒤집어 놓기만 하고 값이 같은" 쌍은 검사가 아니다.
            new[] { Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.5f, 0, 0) },
            new[] { Gem(0, 205, 5, 0.5f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0), Gem(0, 205, 5, 0.1f, 0, 0) },
        };

        private static GemEffectInput Gem(int stat, int dice, int element, float percent, int flat, int intParam)
        {
            return new GemEffectInput(stat, dice, element, percent, flat, intParam);
        }

        /// <summary>
        /// 효과 집합을 키에 넣을 한 토큰으로 만든다. <c>stat:dice:elem:percent:flat:intParam</c> 를
        /// <c>|</c> 로 잇고, 빈 집합은 <c>-</c> 다(빈 문자열로 두면 키가 <c>[]</c> 가 되어
        /// 인자 파싱에서 "없는 인자"와 구분이 안 된다).
        ///
        /// <b>계산 인자를 키에 통째로 박는 이유</b>: 그래야 테스트가 덤퍼에서 숫자를 옮겨 적지
        /// 않는다. 옮겨 적은 값으로 함수를 부르면 덤퍼와 테스트가 갈라졌을 때 <b>둘 다 초록</b>인
        /// 상태가 만들어진다. 여기서는 집합이 달라지면 키 자체가 달라져 즉시 드러난다.
        ///
        /// percent 는 <c>Num</c>(라운드트립 "R")로 적는다 — 되읽으면 비트가 같다.
        /// </summary>
        private static string DescribeGemEffects(GemEffectInput[] effects)
        {
            if (effects == null || effects.Length == 0)
                return "-";

            var text = new StringBuilder();
            for (int i = 0; i < effects.Length; i++)
            {
                if (i > 0)
                    text.Append('|');

                text.Append(effects[i].StatType.ToString(CultureInfo.InvariantCulture)).Append(':');
                text.Append(effects[i].TargetDiceType.ToString(CultureInfo.InvariantCulture)).Append(':');
                text.Append(effects[i].TargetElementType.ToString(CultureInfo.InvariantCulture)).Append(':');
                text.Append(Num(effects[i].PercentValue)).Append(':');
                text.Append(effects[i].FlatValue.ToString(CultureInfo.InvariantCulture)).Append(':');
                text.Append(effects[i].IntParam.ToString(CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        /// <summary>
        /// 보석 보너스 합산·매칭 전부. (MIGRATION_BASELINE 5.2)
        ///
        /// <b>이 구획이 지키는 것 셋:</b>
        ///  1. 매칭 규칙이 int 비교로 내려왔고, targetDiceType 100/200 이 <b>영원히 안 맞는다</b>는 사실
        ///  2. 쿨감 캡 0.8 이 실효 상한이라는 사실(호출부 0.05f 하한은 죽은 가지다)
        ///  3. FirstNWaves 의 한계가 <c>waveIndex &lt;= intParam</c> <b>포함</b>이라는 사실
        /// </summary>
        private static void DumpGemBonus(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## core.gemBonus  (보석 보너스 합산 — 효과 목록을 인자로 받는 순수 함수)");

            for (int i = 0; i < GemDiceCodes.Length; i++)
            {
                sb.AppendLine($"core.gemBonus.baseDice[{GemDiceCodes[i]}] = " +
                              $"{GemBonusFormula.BaseDiceType(GemDiceCodes[i])}");
                sb.AppendLine($"core.gemBonus.element[{GemDiceCodes[i]}] = " +
                              $"{GemBonusFormula.ElementTypeOf(GemDiceCodes[i])}");
            }

            for (int t = 0; t < GemMatchTargetDice.Length; t++)
            {
                for (int e = 0; e < GemMatchTargetElements.Length; e++)
                {
                    for (int d = 0; d < GemMatchDice.Length; d++)
                    {
                        bool matched = GemBonusFormula.IsTargetMatched(
                            GemMatchTargetDice[t], GemMatchTargetElements[e], GemMatchDice[d]);

                        sb.AppendLine($"core.gemBonus.match[{GemMatchTargetDice[t]}][{GemMatchTargetElements[e]}]" +
                                      $"[{GemMatchDice[d]}] = {matched}");
                    }
                }
            }

            for (int s = 0; s < GemEffectSets.Length; s++)
            {
                string set = DescribeGemEffects(GemEffectSets[s]);

                for (int i = 0; i < GemSumPercentStats.Length; i++)
                {
                    for (int d = 0; d < GemSumDice.Length; d++)
                    {
                        float value = GemBonusFormula.SumPercent(
                            GemEffectSets[s], GemSumPercentStats[i], GemSumDice[d]);

                        sb.AppendLine($"core.gemBonus.sumPercent[{set}][{GemSumPercentStats[i]}]" +
                                      $"[{GemSumDice[d]}] = {Num(value)}");
                    }
                }

                for (int i = 0; i < GemSumFlatStats.Length; i++)
                {
                    for (int d = 0; d < GemSumDice.Length; d++)
                    {
                        int value = GemBonusFormula.SumFlat(
                            GemEffectSets[s], GemSumFlatStats[i], GemSumDice[d]);

                        sb.AppendLine($"core.gemBonus.sumFlat[{set}][{GemSumFlatStats[i]}]" +
                                      $"[{GemSumDice[d]}] = {value}");
                    }
                }

                for (int d = 0; d < GemCooldownDice.Length; d++)
                {
                    float value = GemBonusFormula.CooldownReductionPercent(GemEffectSets[s], GemCooldownDice[d]);
                    sb.AppendLine($"core.gemBonus.cooldown[{set}][{GemCooldownDice[d]}] = {Num(value)}");
                }

                for (int d = 0; d < GemWaveDice.Length; d++)
                {
                    for (int w = 0; w < GemWaveIndices.Length; w++)
                    {
                        int value = GemBonusFormula.FirstNWavesDamageFlatBonus(
                            GemEffectSets[s], GemWaveDice[d], GemWaveIndices[w]);

                        sb.AppendLine($"core.gemBonus.firstNWaves[{set}][{GemWaveDice[d]}]" +
                                      $"[{GemWaveIndices[w]}] = {value}");
                    }
                }
            }

            // 캡 상수 자체도 뜬다. 위 cooldown 줄은 "0.8 에서 잘렸다"만 보여 주는데, 누가 캡을
            // 0.9 로 올리면 잘린 값들이 통째로 바뀌어 어느 줄이 원인인지 흐려진다. 이 한 줄이
            // 원인을 바로 가리킨다.
            sb.AppendLine($"core.gemBonus.cooldownCap = {Num(GemBonusFormula.CooldownReductionCap)}");
        }

        private static void DumpDamageCase(
            StringBuilder sb,
            string name,
            int baseAttack,
            int levelUpAttackIncrease,
            int dicePip,
            int bulletLevel,
            int equipmentAttack = 0,
            float levelMul = 1f,
            float kingSynergy = 1f,
            bool isKing = false,
            float attackPercent = 0f,
            int attackFlat = 0,
            int earlyWaveFlat = 0,
            float finalPercent = 0f,
            float elementMul = 1f,
            float relicMul = 1f)
        {
            int damage = DamageFormula.Calculate(new DamageInputs
            {
                BaseAttack = baseAttack,
                LevelUpAttackIncrease = levelUpAttackIncrease,
                DicePip = dicePip,
                BulletLevel = bulletLevel,
                EquipmentAttackTotal = equipmentAttack,
                LevelDamageMultiplier = levelMul,
                KingSynergyMultiplier = kingSynergy,
                IsKingDice = isKing,
                AttackPercentBonus = attackPercent,
                AttackFlatBonus = attackFlat,
                EarlyWaveFlatBonus = earlyWaveFlat,
                FinalDamagePercentBonus = finalPercent,
                ElementUpgradeMultiplier = elementMul,
                RelicDamageMultiplier = relicMul,
            });

            sb.AppendLine($"core.damage[{name}] = {damage}");
        }

        private static void DumpDamage(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## dice.damage  (장비·유물·원소 진행도가 섞인다)");

            int[] pips = { 1, 3, 6 };
            for (int d = 0; d < DumpDiceTypes.Length; d++)
            {
                for (int p = 0; p < pips.Length; p++)
                {
                    for (int l = 0; l < DumpLevels.Length; l++)
                    {
                        int damage = DiceMetaDataProvider.CalculateDamage(DumpDiceTypes[d], pips[p], DumpLevels[l]);
                        sb.AppendLine($"damage[{DumpDiceTypes[d]}][pip={pips[p]}][lv={DumpLevels[l]}] = {damage}");
                    }
                }

                for (int star = 1; star <= 5; star++)
                    sb.AppendLine($"cooldown[{DumpDiceTypes[d]}][star={star}] = {Num(DiceMetaDataProvider.GetCooldown(DumpDiceTypes[d], star))}");
            }
        }

        /// <summary>
        /// 데미지·쿨다운 경로가 <b>읽는 상태</b> 자체를 뜬다. 결과가 아니라 입력이다.
        ///
        /// 왜 이게 필요한가 — 지금 골든의 damage[*] 360줄과 cooldown[*] 75줄은 "결과"만
        /// 있고 그 결과를 만든 상태가 파일 어디에도 없다. 그래서 값이 달라졌을 때
        /// <b>코드가 바뀐 건지 세이브가 바뀐 건지 구분할 방법이 없다.</b> 입력을 같이 적으면
        /// diff 를 읽는 사람이 그 자리에서 가른다.
        ///
        /// 더 중요한 것 — 4단계 조사가 지목한 사각지대는 "왕다이스가 소환된 상태의 값이
        /// 골든에 한 줄도 없다"는 것이었다. 덤퍼는 로비에서 도니 보드가 비어 있고
        /// <c>GetTypeCount</c> 가 전부 0 이라, 소환 분기는 <b>영원히</b> 안 밟힌다.
        /// 그 사실을 파일에 적어 두지 않으면 "안 밟혔다"는 것조차 보이지 않는다.
        /// state.summoned[*] 가 전부 0 이라는 줄이 그 증거로 남는다.
        ///
        /// 범위 표기는 조사표를 따른다 — 영구=PlayerPrefs / 런=씬·스테이지 수명 / 틱=매 프레임.
        ///
        /// <b>부작용 있는 게터는 절대 부르지 않는다.</b> 특히
        /// <c>RelicManager.ConsumeAttackDamageMultiplier()</c>(firstWaveAttackUsed 를 쓰고
        /// Random 을 뽑는다)와 <c>RelicManager.TryTriggerLastWall()</c>(lastWallTriggered /
        /// lastWallCooldownWaveIndex 를 쓴다), <c>RollSummonStar</c> / <c>TrySpawnTwinDice</c>
        /// (Random). 기준선을 뜨는 것만으로 유물 1회성 효과가 소모되면 그 판이 망가진다.
        /// 그래서 이 두 축은 골든으로 못 잠근다 — 아래 unlockable 목록에 남긴다.
        /// </summary>
        private static void DumpDamagePathState(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## state.damageInputs  (데미지 경로가 읽는 상태. 결과가 아니라 입력이다)");
            sb.AppendLine("# 범위: 영구=PlayerPrefs / 런=씬·스테이지 수명 / 틱=매 프레임 변할 수 있음");
            sb.AppendLine("# ConsumeAttackDamageMultiplier / TryTriggerLastWall 는 부작용이 있어 여기서 부르지 않는다.");

            DiceLevelManager levels = DiceLevelManager.Instance;
            DiceTypeStarManager stars = GameContainer.Battle.DiceStars;
            EquipmentManager equipment = EquipmentManager.Instance;
            RelicManager relics = RelicManager.Instance;
            ElementUpgradeManager elements = GameContainer.Battle.ElementUpgrade;

            sb.AppendLine($"state.manager.diceLevel = {(levels != null ? "있음" : "없음")}");
            sb.AppendLine($"state.manager.diceTypeStar = {(stars != null ? "있음" : "없음")}");
            sb.AppendLine($"state.manager.equipment = {(equipment != null ? "있음" : "없음")}");
            sb.AppendLine($"state.manager.relic = {(relics != null ? "있음" : "없음")}");
            sb.AppendLine($"state.manager.elementUpgrade = {(elements != null ? "있음" : "없음")}");

            // GetFirstNWavesDamageFlatBonus 만은 인자로 웨이브를 받는다. 즉 이 게터에서는
            // 덤퍼가 경계를 <b>고를 수 있다.</b> 현재 웨이브 하나만 뜨면 "초반 N웨이브" 임계가
            // 영원히 안 드러나므로 0 / 1 / 5 를 밟는다.
            int[] waveSamples = { 0, 1, 5 };

            for (int d = 0; d < DumpDiceTypes.Length; d++)
            {
                DiceType diceType = DumpDiceTypes[d];

                // 영구 — PlayerPrefs OJ.Bullet.Level.*
                sb.AppendLine($"state.diceLevel[{diceType}] = {(levels != null ? levels.GetLevel(diceType).ToString() : NoManagerValue)}");

                // 런 — 보드 위 소환 개수. 로비에서는 전부 0 이고, 그래서 왕다이스 분기가 안 밟힌다.
                sb.AppendLine($"state.summoned[{diceType}] = {(stars != null ? stars.GetTypeCount(diceType).ToString() : NoManagerValue)}");

                // 런 — ElementUpgradeManager 는 Awake 에서 ResetAll 을 하고 PlayerPrefs 가 없다.
                sb.AppendLine($"state.elementMul[{diceType}] = {(elements != null ? Num(elements.GetTotalBonusMultiplier(diceType)) : NoManagerValue)}");

                // 영구 — 장비/젬
                sb.AppendLine($"state.equip.attackPercent[{diceType}] = {(equipment != null ? Num(equipment.GetAttackPercentBonus(diceType)) : NoManagerValue)}");
                sb.AppendLine($"state.equip.attackFlat[{diceType}] = {(equipment != null ? equipment.GetAttackFlatBonus(diceType).ToString() : NoManagerValue)}");
                sb.AppendLine($"state.equip.finalPercent[{diceType}] = {(equipment != null ? Num(equipment.GetFinalDamagePercentBonus(diceType)) : NoManagerValue)}");
                sb.AppendLine($"state.equip.cooldownReduce[{diceType}] = {(equipment != null ? Num(equipment.GetCooldownReductionPercent(diceType)) : NoManagerValue)}");
                sb.AppendLine($"state.equip.fireExplosionRange[{diceType}] = {(equipment != null ? Num(equipment.GetFireExplosionRangeBonus(diceType)) : NoManagerValue)}");
                sb.AppendLine($"state.equip.fireExplosionExtraTargets[{diceType}] = {(equipment != null ? equipment.GetFireExplosionExtraTargetCount(diceType).ToString() : NoManagerValue)}");
                sb.AppendLine($"state.equip.thunderChainExtra[{diceType}] = {(equipment != null ? equipment.GetThunderChainExtraCount(diceType).ToString() : NoManagerValue)}");

                for (int w = 0; w < waveSamples.Length; w++)
                {
                    sb.AppendLine($"state.equip.firstNWavesFlat[{diceType}][wave={waveSamples[w]}] = " +
                                  $"{(equipment != null ? equipment.GetFirstNWavesDamageFlatBonus(diceType, waveSamples[w]).ToString() : NoManagerValue)}");
                }

                // 유물 — 레벨은 영구, IsBoardFull 은 틱, CrownResonance 는 런(소환)이다.
                // 세 범위가 한 값에 섞이는 유일한 자리라 5.1 스냅샷이 가장 굳기 쉬운 곳이다.
                sb.AppendLine($"state.relic.damageMul[{diceType}] = {(relics != null ? Num(relics.GetDamageMultiplier(diceType)) : NoManagerValue)}");
                sb.AppendLine($"state.relic.fireExplosionRangeMul[{diceType}] = {(relics != null ? Num(relics.GetFireExplosionRangeMultiplier(diceType)) : NoManagerValue)}");
                sb.AppendLine($"state.relic.fireExplosionExtraTargets[{diceType}] = {(relics != null ? relics.GetFireExplosionExtraTargetCount(diceType).ToString() : NoManagerValue)}");
                sb.AppendLine($"state.relic.thunderExtraTargets[{diceType}] = {(relics != null ? relics.GetThunderExtraTargetCount(diceType).ToString() : NoManagerValue)}");
            }

            sb.AppendLine($"state.equip.totalAttack = {(equipment != null ? equipment.GetTotalEquipmentAttack().ToString() : NoManagerValue)}");

            // 원소 레벨은 런 범위다(PlayerPrefs 없음). Max 는 값이 아니라 경계표시라 뺀다.
            ElementType[] elementTypes =
            {
                ElementType.Normal, ElementType.Fire, ElementType.Water, ElementType.Light, ElementType.Dark,
            };
            for (int i = 0; i < elementTypes.Length; i++)
                sb.AppendLine($"state.elementLevel[{elementTypes[i]}] = {(elements != null ? elements.GetLevel(elementTypes[i]).ToString() : NoManagerValue)}");

            // 다이스 타입을 안 받는 유물 게터. 상태 피해증가 6종 중 _relicDamageTakenBonusPercent
            // 를 쓰는 세 유물(ParalysisNeedle / TornadoAnchor / TailwindFeather)의 2차값이
            // 여기 들어 있다 — 받는쪽(Monster)을 못 뜨는 대신 <b>주는쪽 값</b>만은 잠근다.
            sb.AppendLine($"state.relic.cooldownReduce = {(relics != null ? Num(relics.GetCooldownReductionPercent()) : NoManagerValue)}");
            sb.AppendLine($"state.relic.slowDamageTaken = {(relics != null ? relics.GetSlowDamageTakenBonusPercent().ToString() : NoManagerValue)}");
            sb.AppendLine($"state.relic.stunChanceBonus = {(relics != null ? Num(relics.GetStunChanceBonusPercent()) : NoManagerValue)}");
            sb.AppendLine($"state.relic.stunDamageTaken = {(relics != null ? relics.GetStunDamageTakenBonusPercent().ToString() : NoManagerValue)}");
            sb.AppendLine($"state.relic.tornadoDamageTaken = {(relics != null ? relics.GetTornadoDamageTakenBonusPercent().ToString() : NoManagerValue)}");
            sb.AppendLine($"state.relic.tornadoDamageTakenDuration = {(relics != null ? Num(relics.GetTornadoDamageTakenBonusDuration()) : NoManagerValue)}");
            sb.AppendLine($"state.relic.armorBreakPercentBonus = {(relics != null ? relics.GetArmorBreakPercentBonus().ToString() : NoManagerValue)}");
            sb.AppendLine($"state.relic.armorBreakDurationBonus = {(relics != null ? Num(relics.GetArmorBreakDurationBonus()) : NoManagerValue)}");
            sb.AppendLine($"state.relic.windPushChanceBonus = {(relics != null ? Num(relics.GetWindPushChanceBonusPercent()) : NoManagerValue)}");
            sb.AppendLine($"state.relic.windDamageTaken = {(relics != null ? relics.GetWindDamageTakenBonusPercent().ToString() : NoManagerValue)}");
        }

        /// <summary>
        /// 효과 파라미터 게터 전수. <b>지금 골든에 키가 하나도 없는</b> 함수들이다.
        ///
        /// 왜 [stable] 이 아니라 여기인가 — 이 게터들은 전부 안에서 DiceTypeStarManager /
        /// DiceLevelManager / ElementUpgradeManager 를 조회한다. 인자만으로 결정되지 않으므로
        /// 규약상 [environment] 다. "로비에서는 소환이 0 이라 사실상 순수하다"는 것은 맞지만,
        /// 그 순수성은 <b>호출된 함수의 분기 구조</b>에 기대는 것이라 그쪽을 고치는 순간
        /// 조용히 깨진다. 덤퍼가 그것을 증명할 수 없으니 [stable] 로 올리지 않는다.
        ///
        /// 그래도 절반은 덮인다 — 레벨 임계(3/6/9/12)는 인자로 밟을 수 있다. 예를 들어
        /// GetFireExplosionRangeMultiplier 의 <c>level >= 9 ? 1.1f : 1f</c> 는 아래 격자에
        /// 그대로 드러난다. 못 밟는 것은 왕다이스 <b>소환 여부</b> 하나뿐이고, 그건 인자가
        /// 아니라 싱글톤이라 덤퍼가 켤 방법이 없다(보드를 건드리면 그건 관측이 아니라 조작이다).
        /// state.summoned[*] 줄이 "안 밟혔다"는 증거로 같이 남는다.
        /// </summary>
        private static void DumpEffectParameters(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## effect.params  (효과 파라미터 게터 — 레벨 임계는 밟히고, 왕다이스 소환 분기는 못 밟는다)");

            // 크리티컬 2종. 인자가 없어 격자를 못 만든다 — DiceTypeStarManager(KingNormal 소환)
            // 와 DiceLevelManager(≥9 / ≥12)만 본다. 로비에서는 소환이 0 이라 항상
            // 0f / 2f 가 나오고, 10f 와 2.2f 리터럴은 이 파일로는 못 잠근다.
            sb.AppendLine($"effect.crit.chancePercent = {Num(DiceMetaDataProvider.GetGlobalCriticalChancePercent())}");
            sb.AppendLine($"effect.crit.damageMultiplier = {Num(DiceMetaDataProvider.GetGlobalCriticalDamageMultiplier())}");

            // 받는쪽(Monster.TakeDamage)이 피해 한 방마다 조회하는 두 플래그.
            // 값 자체는 여기서 뜨지만, 이 플래그가 곱해지는 합성식은 Monster 인스턴스가
            // 필요해 못 뜬다.
            sb.AppendLine($"effect.hasKingIceDamageBonus = {DiceMetaDataProvider.HasKingIceDamageBonus()}");
            sb.AppendLine($"effect.hasKingPoisonDamageBonus = {DiceMetaDataProvider.HasKingPoisonDamageBonus()}");

            // 왕 시너지. Normal/Thunder 만 1.2f 가 될 수 있고 나머지는 상수 1f 다.
            //
            // <b>이 15줄은 로비에서 전부 1 이고, 그래서 검출력이 거의 없다.</b> 처음에는
            // "어느 타입이 시너지를 받는가가 사양이니 표가 줄거나 늘면 diff 로 보인다"고
            // 적었는데 <b>틀렸다.</b> 소환이 0 이면 GetKingSynergyDamageMultiplier 는
            // 어느 타입이든 1f 를 돌려준다 — switch 에 Fire 를 <i>추가</i>해도, Normal 을
            // <i>제거</i>해도 이 15줄은 한 글자도 안 바뀐다. 잡히는 변이는 "1f 라는 기본
            // 반환값 자체를 바꾸는 것" 하나뿐이다.
            //
            // 그래도 남기는 이유는 그 하나 때문이 아니라, 이 줄들이 전부 1 이라는 사실이
            // "이번 덤프에서 왕 시너지 축은 안 밟혔다"는 증거로 state.summoned[*] 와 짝을
            // 이루기 때문이다. 값이 아니라 <b>미검증 표시</b>로 읽을 것.
            for (int d = 0; d < DumpDiceTypes.Length; d++)
            {
                DiceType diceType = DumpDiceTypes[d];
                sb.AppendLine($"effect.kingSynergyMul[{diceType}] = {Num(DiceMetaDataProvider.GetKingSynergyDamageMultiplier(diceType))}");
            }

            for (int l = 0; l < DumpLevels.Length; l++)
            {
                int level = DumpLevels[l];

                // 4단계 조사가 "Fire 폭발 범위가 KingFire 소환에 반응하는 연결이 골든에
                // 한 줄도 없다"고 지목한 자리다. level >= 9 축만 여기서 잠긴다.
                sb.AppendLine($"effect.fireExplosionRangeMul[{level}] = {Num(DiceMetaDataProvider.GetFireExplosionRangeMultiplier(level))}");

                // 독 배수는 Poison 일 때만 KingPoison 소환을 본다. 두 타입을 나란히 떠서
                // 그 비대칭이 표에 남게 한다.
                sb.AppendLine($"effect.poisonDamageMul[{DiceType.Poison}][{level}] = {Num(DiceMetaDataProvider.GetPoisonDamageMultiplier(DiceType.Poison, level))}");
                sb.AppendLine($"effect.poisonDamageMul[{DiceType.KingPoison}][{level}] = {Num(DiceMetaDataProvider.GetPoisonDamageMultiplier(DiceType.KingPoison, level))}");

                // 둔화 지속은 Ice 가 level>=9, KingIce 가 소환+레벨을 본다. 역시 비대칭.
                sb.AppendLine($"effect.slowDuration[{DiceType.Ice}][{level}] = {Num(DiceMetaDataProvider.GetSlowDuration(DiceType.Ice, level))}");
                sb.AppendLine($"effect.slowDuration[{DiceType.KingIce}][{level}] = {Num(DiceMetaDataProvider.GetSlowDuration(DiceType.KingIce, level))}");

                // 아래 둘은 [stable] 의 dice.windPushChance / dice.timeCooldownReduce 와 같은
                // 함수의 <b>2인자 오버로드</b>다. 차이는 ElementUpgradeManager 곱 하나뿐이다.
                //
                // 주의 — <b>로비에서 이 두 줄은 [stable] 짝과 항상 정확히 같은 값이다.</b>
                // ElementUpgradeManager 는 Awake 에서 ResetAll 을 하고 PlayerPrefs 가 없어
                // 전 원소 레벨이 0 이고, GetTotalBonusMultiplier 가 1 + 0*0.1 = 1f 를
                // 돌려주기 때문이다. 매니저가 아예 없으면 곱하는 줄 자체를 건너뛰므로
                // 결과는 역시 같다. 즉 <b>곱하는 줄을 통째로 지워도 이 16줄은 안 바뀐다.</b>
                //
                // 그래서 이 줄들의 쓸모는 "같다"가 아니라 <b>"달라졌다"</b> 쪽에 있다.
                // 짝과 값이 어긋나 있으면 그 덤프는 원소 레벨이 0 이 아닌 상태에서 뜬 것이고,
                // 기준선으로 쓰면 안 된다 — state.elementLevel[*] 로 확인할 것.
                sb.AppendLine($"effect.windPushChance[{DiceType.Wind}][{level}] = {Num(DiceMetaDataProvider.GetWindPushChancePercent(DiceType.Wind, level))}");
                sb.AppendLine($"effect.timeCooldownReduce[{DiceType.Time}][{level}] = {Num(DiceMetaDataProvider.GetTimeCooldownReducePercent(DiceType.Time, level))}");
            }
        }

        /// <summary>
        /// <b>골든으로 못 잠그는 축</b>을 파일 안에 적어 둔다.
        ///
        /// 왜 코드가 아니라 산출물에 적는가 — 4단계 조사에서 가장 비싼 발견이
        /// "GoldenBaseline.EnvironmentSection 이 선언만 되고 참조가 0 이라 damage[*] 360줄이
        /// 죽은 기록이었다"는 것이었다. 파일만 보고는 무엇이 검증되고 무엇이 안 되는지
        /// 알 수 없었기 때문에 아무도 몰랐다. 그 실패를 반복하지 않으려면 <b>덮이지 않는
        /// 축이 산출물 자체에 이름으로 남아야 한다.</b>
        ///
        /// '#' 으로 시작하므로 GoldenBaseline 파서는 전부 건너뛴다 — 키 비교를 오염시키지 않는다.
        /// </summary>
        private static void DumpUnlockableAxes(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("## 못 잠그는 축  (이 파일로는 못 덮는다. 5.1 에서 다른 수단이 필요하다)");
            sb.AppendLine("# 1. [5.1-a2 에서 해소] Monster.TakeDamage 의 감쇄·증가 합성식 — 100/(100+armor),");
            sb.AppendLine("#    음수 방어 분기, 상태 피해증가 합산, CeilToInt. 산술을 OJ.Core 의");
            sb.AppendLine("#    IncomingDamageFormula 로 내리고(5.1-a) core.incoming.* 로 잠갔다(5.1-a2).");
            sb.AppendLine("#    Monster 인스턴스는 여전히 못 만든다 — TakeDamage 의 사망 분기가");
            sb.AppendLine("#    EquipmentManager.OnMonsterKilled(세이브 변경) / MonsterManager.UnregisterMonster");
            sb.AppendLine("#    (null 체크 없음 → 로비에서 NRE) / MonsterSpawner.PoolMonster(풀 오염)를 부른다.");
            sb.AppendLine("#    <b>남은 사각지대는 호출부다</b>: 어떤 값을 그 순수 함수에 넘기는가(6종 필드 수집,");
            sb.AppendLine("#    단축평가 순서, dmg<=0 게이트)는 여전히 골든 밖이다. 산술은 잠겼고 배선은 안 잠겼다.");
            sb.AppendLine("# 1b. UIBattleDiceDetailPanel.CalculateAppliedDamage — Monster 쪽과 식이 다르다");
            sb.AppendLine("#    (곱 결합 순서, Mathf.Max(1,·) 유무, 보너스 클램프 시점). core.incoming.* 는");
            sb.AppendLine("#    Monster 쪽만 잠근다. 둘의 발산은 여전히 아무 테스트도 안 본다.");
            sb.AppendLine("# 2. 상태 피해증가 6종의 write 값(10/20/10/15/10) — 각 *DiceEffect.cs 안의 리터럴이라");
            sb.AppendLine("#    이름이 없다. 유물 쪽 2차값만 state.relic.* 에 남는다.");
            sb.AppendLine("# 3. KingNormal 4연타의 70% / 10% 분할비 — AttackContent.PlayKingNormalMultiHit 이");
            sb.AppendLine("#    private 이고 Monster 를 요구한다. 타수·간격·범위는 hit.kingNormal.* 로 잠갔다.");
            sb.AppendLine("# 4. 왕다이스 '소환됨' 상태의 값 8종(왕시너지 / 크리확률 / 크리배수 /");
            sb.AppendLine("#    CrownResonance / Fire폭발범위 / Poison독배수 / KingIce둔화 / HasKingIce·KingPoison).");
            sb.AppendLine("#    DiceTypeStarManager 는 인자가 아니라 싱글톤이고, 덤퍼는 로비에서 돈다.");
            sb.AppendLine("#    state.summoned[*] 가 전부 0 이면 이 축은 이번 덤프에서 안 밟혔다는 뜻이다.");
            sb.AppendLine("# 5. RelicManager.ConsumeAttackDamageMultiplier / TryTriggerLastWall — 읽기가 아니라");
            sb.AppendLine("#    쓰기다. 뜨는 행위 자체가 유물 1회성 효과를 소모한다. 부를 수 없다.");
            sb.AppendLine("# 6. Thunder / KingThunder 의 MonsterManager null 체크 누락 — '값'이 아니라 '없는 방어'라");
            sb.AppendLine("#    골든 키로 표현되지 않는다. 테스트로 잡아야 한다.");
            sb.AppendLine("# 7. 크리티컬 적용식 Mathf.RoundToInt(damage * mul) 과 GetCooldown 의");
            sb.AppendLine("#    Mathf.Pow(1.2f, star-1) * 2f — 이름 있는 함수가 아니라 호출부 안의 식이다.");
            sb.AppendLine("#    cooldown[*] 과 state.equip/relic.cooldownReduce 를 나란히 두면 역산은 되지만,");
            sb.AppendLine("#    OJ.Core.Tests 가 재현하려면 순수 함수로 내려와야 한다.");
            sb.AppendLine("# 8. GetCooldown 의 Mathf.Max(0.05f, 1f - reduce) 하한 — 감소율 95% 이상인 세이브가");
            sb.AppendLine("#    있어야 밟힌다. 합성 인자를 못 넣는 구조라 이 덤프로는 영원히 안 밟힌다.");
            sb.AppendLine("# 9. DamageFormula 의 'scaled *= LevelDamageMultiplier; scaled *= KingSynergyMultiplier'");
            sb.AppendLine("#    접기 — core.dmgChain 이 KingSynergy 를 1f 로 고정해서 못 잡는다. IEEE754 에서");
            sb.AppendLine("#    x * 1f 는 비트가 보존되므로 이 접기는 그 격자에서 무연산이다. KingSynergy 축을");
            sb.AppendLine("#    {1f, 1.2f} 로 늘려 648줄로 키워도 잡히는 케이스가 0개라 늘리지 않았다.");
            sb.AppendLine("#    (elementMul * relicMul 쪽 접기는 core.dmgChain[125][3][1.3][1.4][1.2] 가 잡는다.)");
            sb.AppendLine("# 10. effect.* 중 로비에서 상수로 굳는 것들 — effect.kingSynergyMul[*] 15줄은 전부 1,");
            sb.AppendLine("#    effect.hasKingIce/KingPoisonDamageBonus 는 전부 False, effect.crit.* 는 0/2 다.");
            sb.AppendLine("#    이 줄들이 잡는 변이는 '소환 안 됨' 분기의 기본 반환값을 바꾸는 것뿐이고,");
            sb.AppendLine("#    임계값(≥9 / ≥12)이나 시너지 대상 타입 목록을 고쳐도 diff 가 안 난다.");
            sb.AppendLine("#    값으로 읽지 말고 state.summoned[*] 와 짝지어 '미검증 표시'로 읽을 것.");
            sb.AppendLine("# 11b. core.incoming 이 <b>원리적으로</b> 못 잡는 것 (구멍이 아니라 성질이다.");
            sb.AppendLine("#    격자를 넓혀도 안 잡히니 넓히지 말 것. 전부 Mono 실측으로 확인했다):");
            sb.AppendLine("#    (a) DefenseMultiplier 의 'armor >= 0f' 를 'armor > 0f' 로 바꾸는 변이.");
            sb.AppendLine("#        두 식은 armor==0 에서만 갈릴 수 있는데 거기서 양쪽 분기가 같은 1f 를");
            sb.AppendLine("#        낸다(100/100 과 2-100/100). int32 20억개 <b>전수</b> 비교로 갈리는 값");
            sb.AppendLine("#        0개를 확인했다. 'float armor = defense' 중간변수를 지우는 변이도 같다.");
            sb.AppendLine("#    (b) AppliedDamage 의 재괄호화 dmg * (defMul * incMul).");
            sb.AppendLine("#        이유는 '정밀도가 넉넉해서'가 아니라 <b>양쪽이 같은 실수를 한 번만 반올림</b>");
            sb.AppendLine("#        하기 때문이다: dmg 는 곱하기 전에 float(가수 24비트)로 승격되고 defMul/incMul");
            sb.AppendLine("#        도 float 지역변수다. 24x24=48비트라 dmg*defMul 도 defMul*incMul 도 중간");
            sb.AppendLine("#        타입(double, 가수 53비트) 안에서 <b>정확</b>하다. 그래서 두 순서 다");
            sb.AppendLine("#        round53(dmg·defMul·incMul) 이 되어 비트까지 같다. 중간 정밀도가 48비트");
            sb.AppendLine("#        이상이기만 하면 성립하므로 x87(64비트)이어도 결론은 같다.");
            sb.AppendLine("#        실측: 임의 float32 삼중항 4986만개에서 갈림 0 (같은 표에서 fold 는 1462만개");
            sb.AppendLine("#        갈린다). 반면 중간 변수로 접는 변이(fold)는 float 24비트로 실제로 깎여서");
            sb.AppendLine("#        격자에서 9점에 잡힌다 — 그 비대칭이 근거다.");
            sb.AppendLine("#    (c) 왕얼음 15 와 왕독 15 를 서로 바꾸거나 상수 하나로 합치는 변이. 값이 같다.");
            sb.AppendLine("#        '합치지 말라'는 규약은 골든이 아니라 사람이 지켜야 한다.");
            sb.AppendLine("#    (d) TotalBonusPercent 의 <b>인자 순서</b>. int 덧셈이라 순서를 어떻게 섞어도");
            sb.AppendLine("#        (오버플로가 나도) 결과가 같다. 시그니처 순서는 규약으로만 지켜진다.");
            sb.AppendLine("#    (e) <b>부호 있는 0</b>. Num() 의 \"R\" 표기가 -0f 와 0f 를 둘 다 \"0\" 으로 찍는다.");
            sb.AppendLine("#        그래서 WallHpBarRatioOnDamage 의 'ratio < 0' 을 'ratio <= 0' 으로 바꾸는");
            sb.AppendLine("#        변이는 -0f 를 0f 로 바꿀 뿐이라 이 파일에 흔적이 안 남는다. 격자를 넓혀");
            sb.AppendLine("#        -0f 를 만들면 잡히는 것이 아니라 RoundTripTextIsBitTight 가 깨진다");
            sb.AppendLine("#        (그 테스트는 비트로 비교한다). 잠그려면 표기를 비트로 바꿔야 한다.");
            sb.AppendLine("#    ※ 아래 둘은 <b>원래 구멍이었고 5.1-a2 에서 격자를 넓혀 메웠다</b> — 성질이 아니다.");
            sb.AppendLine("#      · dmg 의 int→float 승격: dmg 축이 2^24 아래에만 있어서 안 밟혔다. 16777217 추가.");
            sb.AppendLine("#      · WallHpBarRatioClamped 의 'totalHp > 0' 가드 모양: totalHp 가 전부 음수가");
            sb.AppendLine("#        아니어서 'totalHp != 0' 변이가 통과했다. [-20][-100] / [-1][-1] 추가.");
            sb.AppendLine("# 11. effect.windPushChance / effect.timeCooldownReduce 16줄은 원소 레벨이 0 인 한");
            sb.AppendLine("#    [stable] 의 dice.windPushChance / dice.timeCooldownReduce 와 값이 같다.");
            sb.AppendLine("#    ElementUpgradeManager 곱을 지우는 변이는 이 덤프로 안 잡힌다. 짝과 어긋나 있으면");
            sb.AppendLine("#    그건 검출이 아니라 '원소 레벨이 0 이 아닌 상태에서 떴다'는 경고다.");
        }

        private static string DescribeElements(ElementType[] elements)
        {
            if (elements == null || elements.Length == 0)
                return "<empty>";

            var sb = new StringBuilder();
            for (int i = 0; i < elements.Length; i++)
            {
                if (i > 0)
                    sb.Append('/');
                sb.Append(elements[i]);
            }

            return sb.ToString();
        }

        private static string DescribeMilestones(List<DiceMetaDataDatabase.DiceLevelMilestone> milestones)
        {
            if (milestones == null || milestones.Count == 0)
                return "<empty>";

            var sb = new StringBuilder();
            for (int i = 0; i < milestones.Count; i++)
            {
                if (i > 0)
                    sb.Append(" | ");
                sb.Append(milestones[i].level).Append(':').Append(OneLine(milestones[i].description));
            }

            return sb.ToString();
        }

        // 기준선은 한 줄에 키 하나다. 기획 텍스트에 줄바꿈이 섞이면 파일 구조가 깨지므로
        // 눈에 보이는 기호로 바꿔 둔다. 값이 바뀌면 여전히 diff 에 잡힌다.
        private static string OneLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "<empty>";

            return value.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
        }

        private static string Describe(List<PointRewardEntry> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return "<empty>";

            var sb = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(rewards[i].PointType).Append(':').Append(rewards[i].Amount);
            }

            return sb.ToString();
        }

        // 라운드트립 표기("R")로 적는다. 문화권과 정밀도 때문에 값이 달라 보이는 일을 막는다.
        private static string Num(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Num(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static int CountLines(StringBuilder sb)
        {
            int count = 0;
            for (int i = 0; i < sb.Length; i++)
            {
                if (sb[i] == '\n')
                    count++;
            }

            return count;
        }
    }
}
#endif
