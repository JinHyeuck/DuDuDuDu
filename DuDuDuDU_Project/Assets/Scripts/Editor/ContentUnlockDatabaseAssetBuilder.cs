using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.ContentUnlock;
using OJ.Core;
using OJ.Stage;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="ContentUnlockDatabase"/> 에셋을 만들고 검사한다. <c>DiceUnlockDatabaseAssetBuilder</c> 와 같은 자리다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 조정한 순서를 도구가
    /// 조용히 되돌리면 왜 바뀌었는지 아무도 못 찾는다. 기본표로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    /// </summary>
    public static class ContentUnlockDatabaseAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/ContentUnlockDatabase.asset";
        private const string StageDatabasePath = "Assets/ScriptableObject/StageDatabase.asset";

        [MenuItem("OJ/개발/컨텐츠 해금/데이터베이스 에셋 만들기")]
        private static void CreateOrValidate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ContentUnlockDatabase>(AssetPath);
            if (existing != null)
            {
                Report("[해금] 이미 있다: " + AssetPath, existing);
                return;
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

            var database = ScriptableObject.CreateInstance<ContentUnlockDatabase>();
            database.PopulateDefaults();

            AssetDatabase.CreateAsset(database, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Report("[해금] 새로 만들었다: " + AssetPath + System.Environment.NewLine +
                   "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.", database);
        }

        [MenuItem("OJ/개발/컨텐츠 해금/데이터베이스 검사")]
        private static void ValidateOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentUnlockDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[해금] 에셋이 없다: " + AssetPath + " — 지금은 코드 기본표로 돌아가고 있다.");
                return;
            }

            Report("[해금] 검사: " + AssetPath, database);
        }

        /// <summary>
        /// 스테이지 수를 에셋에서 직접 읽는다 — Provider 를 타면 <c>StaticResource</c> 를 깨운다.
        /// 못 읽으면 0 이고, 검사는 0 을 "범위 검사 건너뜀" 으로 읽는다.
        /// </summary>
        private static int ReadStageCount()
        {
            var stages = AssetDatabase.LoadAssetAtPath<StageDatabase>(StageDatabasePath);
            return stages != null ? stages.StageCount : 0;
        }

        private static void Report(string header, ContentUnlockDatabase database)
        {
            List<string> problems = database.Validate(ReadStageCount());

            var sb = new StringBuilder(header);
            sb.AppendLine();
            sb.AppendLine("  클리어\t컨텐츠\t방식");
            foreach (ContentUnlockDefinition d in database.Definitions)
            {
                if (d != null)
                    sb.AppendLine("  " + d.requiredClear + "\t" + d.displayName + "\t" + d.presentation);
            }

            if (problems.Count == 0)
            {
                sb.Append("  문제 없음.");
                Debug.Log(sb.ToString(), database);
                return;
            }

            sb.AppendLine("  문제 " + problems.Count + "건:");
            foreach (string problem in problems)
                sb.AppendLine("   - " + problem);

            Debug.LogError(sb.ToString(), database);
        }
    }
}
