using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ;
using OJ.Dice;
using OJ.Stage;
using OJ.StageStar;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="DiceUnlockDatabase"/> 에셋을 만들고 검사한다.
    /// <c>TowerDatabaseAssetBuilder</c> 와 같은 자리의 도구다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 조정한 밸런스를
    /// 도구가 조용히 되돌리면, 값이 왜 바뀌었는지 아무도 못 찾는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    /// </summary>
    public static class DiceUnlockDatabaseAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/DiceUnlockDatabase.asset";
        private const string StageDatabasePath = "Assets/ScriptableObject/StageDatabase.asset";

        [MenuItem("OJ/개발/다이스 언락/데이터베이스 에셋 만들기")]
        private static void CreateOrValidate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DiceUnlockDatabase>(AssetPath);
            if (existing != null)
            {
                Report("[언락] 이미 있다: " + AssetPath, existing);
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

            var database = ScriptableObject.CreateInstance<DiceUnlockDatabase>();
            database.PopulateDefaults();

            AssetDatabase.CreateAsset(database, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report("[언락] 새로 만들었다: " + AssetPath + System.Environment.NewLine +
                   "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.", database);
        }

        [MenuItem("OJ/개발/다이스 언락/데이터베이스 검사")]
        private static void ValidateOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<DiceUnlockDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[언락] 에셋이 없다: " + AssetPath +
                               " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            Report("[언락] 검사: " + AssetPath, database);
        }

        /// <summary>
        /// 언락 표를 한 장으로 뽑는다. <b>세 컨텐츠에 고르게 퍼졌는지를 눈으로 보는 수단</b>이다 —
        /// 가격만 보면 균형이 맞아 보여도, 보상처가 한 컨텐츠에 몰려 있으면 나머지 둘은
        /// 다이스를 미끼로 쓰지 못한다.
        /// </summary>
        [MenuItem("OJ/개발/다이스 언락/언락 표 뽑기")]
        private static void DumpTable()
        {
            IReadOnlyList<DiceUnlockDefinition> definitions = LoadDefinitions(out bool fromAsset);
            if (!fromAsset)
                Debug.LogWarning("[언락] 에셋이 없어 코드 기본값으로 표를 뽑는다.");

            var sb = new StringBuilder();
            sb.AppendLine("다이스\t단계\t재화\t가격\t별\t스테이지\t층");

            int specialTotal = 0;
            int kingTotal = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                DiceUnlockDefinition d = definitions[i];
                if (d == null)
                    continue;

                DiceTier tier = DiceEvolution.GetTier(d.diceType);
                if (tier == DiceTier.King)
                    kingTotal += d.price;
                else if (tier == DiceTier.Special)
                    specialTotal += d.price;

                sb.Append(d.diceType).Append('\t')
                  .Append(tier).Append('\t')
                  .Append(DiceOwnershipManager.GetPriceCurrency(d.diceType)).Append('\t')
                  .Append(d.price).Append('\t')
                  .Append(Cell(d.starRequirement)).Append('\t')
                  .Append(Cell(d.stageRequirement)).Append('\t')
                  .Append(Cell(d.towerFloor)).AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("특수 합계\t" + specialTotal + "\tSpecialDiceCore");
            sb.AppendLine("킹 합계\t" + kingTotal + "\tMythicScroll");

            Debug.Log("[언락] 표" + System.Environment.NewLine + sb);
        }

        private static string Cell(int value)
        {
            return value > 0 ? value.ToString() : "-";
        }

        private static IReadOnlyList<DiceUnlockDefinition> LoadDefinitions(out bool fromAsset)
        {
            var database = AssetDatabase.LoadAssetAtPath<DiceUnlockDatabase>(AssetPath);
            if (database != null)
            {
                fromAsset = true;
                return database.Definitions;
            }

            fromAsset = false;
            return DiceUnlockDatabase.BuildDefaults();
        }

        /// <summary>
        /// <b>스테이지 수를 에셋에서 직접 읽는다.</b> Provider 를 타면 <c>StaticResource</c> 를
        /// 깨우는데, 에디터 도구는 씬이 열려 있지 않은 상태로도 돌아야 한다.
        /// 못 읽으면 0 을 주고, <c>Validate</c> 는 0 을 "그 검사는 건너뛴다"로 읽는다 —
        /// 스테이지 수를 몰라서 <b>멀쩡한 표를 문제로 부르는 것</b>이 더 나쁘다.
        /// </summary>
        private static int ReadStageCount()
        {
            var stages = AssetDatabase.LoadAssetAtPath<StageDatabase>(StageDatabasePath);
            return stages != null ? stages.StageCount : 0;
        }

        private static void Report(string header, DiceUnlockDatabase database)
        {
            int stageCount = ReadStageCount();
            int maxStarCount = stageCount * StageStarUtility.MaxStarsPerStage;

            List<string> problems = database.Validate(
                stageCount, maxStarCount, StageStarUtility.StarsPerReward);

            if (problems.Count == 0)
            {
                Debug.Log(header + System.Environment.NewLine +
                          "  항목 " + database.Definitions.Count + "개, 문제 없음." +
                          (stageCount > 0
                              ? "  (스테이지 " + stageCount + ", 최대 별 " + maxStarCount + ")"
                              : "  (StageDatabase 를 못 읽어 스테이지·별 범위 검사는 건너뛰었다)"),
                          database);
                return;
            }

            var sb = new StringBuilder(header);
            sb.AppendLine();
            sb.AppendLine("  문제 " + problems.Count + "건:");
            for (int i = 0; i < problems.Count; i++)
                sb.AppendLine("   - " + problems[i]);

            Debug.LogError(sb.ToString(), database);
        }
    }
}
