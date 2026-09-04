using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Bounty;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="BountyDatabase"/> 에셋을 만들고 검사한다.
    /// <c>RelicDatabaseAssetBuilder</c> 와 같은 자리의 도구다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 조정한 밸런스를
    /// 도구가 조용히 되돌리면, 값이 왜 바뀌었는지 아무도 못 찾는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    /// </summary>
    public static class BountyDatabaseAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/BountyDatabase.asset";

        [MenuItem("OJ/개발/현상금/데이터베이스 에셋 만들기")]
        private static void CreateOrValidate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<BountyDatabase>(AssetPath);
            if (existing != null)
            {
                Report("[현상금] 이미 있다: " + AssetPath, existing);
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

            var database = ScriptableObject.CreateInstance<BountyDatabase>();
            database.PopulateDefaults();

            AssetDatabase.CreateAsset(database, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report("[현상금] 새로 만들었다: " + AssetPath + System.Environment.NewLine +
                   "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.", database);
        }

        [MenuItem("OJ/개발/현상금/데이터베이스 검사")]
        private static void ValidateOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<BountyDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[현상금] 에셋이 없다: " + AssetPath +
                               " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            Report("[현상금] 검사: " + AssetPath, database);
        }

        private static void Report(string header, BountyDatabase database)
        {
            var sb = new StringBuilder(header).AppendLine();
            sb.AppendLine("  등급 " + database.Definitions.Count + "개");

            foreach (BountyDefinition d in database.Definitions)
            {
                if (d == null)
                    continue;

                sb.AppendLine("    " + d.grade + " " + d.displayName +
                              " — 기준 " + (d.referenceWaveRatio * 100f).ToString("0") + "% 웨이브 x" +
                              d.hpMultiplier.ToString("0.##") +
                              " / " + d.rewardKind + " +" + d.rewardAmount);
            }

            System.Collections.Generic.List<string> problems = database.Validate();
            if (problems.Count == 0)
            {
                sb.Append("  문제 없음.");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine("  문제 " + problems.Count + "건:");
            foreach (string p in problems)
                sb.AppendLine("    !! " + p);

            Debug.LogError(sb.ToString());
        }
    }
}
