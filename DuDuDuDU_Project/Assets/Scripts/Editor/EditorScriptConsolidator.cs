using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// 흩어진 <c>Editor/</c> 폴더의 스크립트를 한 곳으로 모은다. (MIGRATION_BASELINE 11.2 / 11.3)
    ///
    /// <b>왜 모아야 하나.</b> asmdef 를 걸면 Unity 의 <c>Editor</c> 특수 폴더 규칙이 더 이상
    /// 적용되지 않는다. 그 폴더 안의 스크립트도 asmdef 가 정한 어셈블리로 들어가고,
    /// 그러면 <b>에디터 전용 코드가 런타임 어셈블리에 섞여 플레이어 빌드가 깨진다</b>
    /// (<c>UnityEditor</c> 는 빌드에 없다). 에디터 코드를 한 폴더로 모아 거기에만
    /// Editor 플랫폼 asmdef 를 두면 그 문제가 사라진다.
    ///
    /// <b>왜 도구인가.</b> 파일을 탐색기로 옮기면 Unity 가 새 GUID 를 발급한다.
    /// 에디터 스크립트는 프리팹이 참조하지 않아 겉보기엔 안전해 보이지만,
    /// <c>[CustomEditor]</c> · <c>[CustomPropertyDrawer]</c> 는 대상 타입으로 연결되고
    /// 다른 에디터 코드가 서로를 참조한다. GUID 를 지키는 편이 언제나 옳다.
    /// </summary>
    public static class EditorScriptConsolidator
    {
        private const string Destination = "Assets/Scripts/Editor";

        [MenuItem("OJ/개발/에디터 스크립트를 한 폴더로 모은다 (GUID 유지)")]
        private static void Consolidate()
        {
            var sb = new StringBuilder("[에디터정리] 흩어진 Editor 스크립트를 " + Destination + " 로 모은다").AppendLine();

            List<string> targets = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("/Editor/"))
                .Where(p => !p.StartsWith(Destination + "/", System.StringComparison.Ordinal))
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .ToList();

            if (targets.Count == 0)
            {
                sb.AppendLine("  옮길 것이 없다. 이미 모여 있다.");
                Debug.Log(sb.ToString());
                return;
            }

            int moved = 0;
            foreach (string path in targets)
            {
                string name = System.IO.Path.GetFileName(path);
                string target = Destination + "/" + name;

                if (AssetDatabase.LoadAssetAtPath<MonoScript>(target) != null)
                {
                    // 같은 이름이 이미 있으면 덮어쓰지 않는다. 어느 쪽이 맞는지
                    // 기계가 판단할 수 없고, 잘못 고르면 코드가 사라진다.
                    sb.AppendLine("  !! 이름 충돌로 건너뜀: " + path);
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(path);
                string error = AssetDatabase.MoveAsset(path, target);
                if (!string.IsNullOrEmpty(error))
                {
                    sb.AppendLine("  !! 실패: " + path + "  (" + error + ")");
                    continue;
                }

                moved++;
                sb.AppendLine("  " + path + "  ->  " + target);
                sb.AppendLine("     GUID " + guid + " (유지)");
            }

            // 비게 된 Editor 폴더를 치운다. 빈 폴더가 남으면 다음 사람이
            // "여기 뭐가 있었나" 를 다시 확인하게 된다.
            foreach (string folder in targets
                         .Select(System.IO.Path.GetDirectoryName)
                         .Select(d => d.Replace('\\', '/'))
                         .Distinct())
            {
                if (AssetDatabase.IsValidFolder(folder) &&
                    AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length == 0)
                {
                    AssetDatabase.DeleteAsset(folder);
                    sb.AppendLine("  빈 폴더 제거: " + folder);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("  옮긴 것 " + moved + "개");
            Debug.Log(sb.ToString());
        }
    }
}
