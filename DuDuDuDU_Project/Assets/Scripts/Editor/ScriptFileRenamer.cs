using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.UI;

namespace OJ.EditorTools
{
    /// <summary>
    /// 스크립트 파일 이름을 클래스 이름에 맞춘다. (MIGRATION_BASELINE 10.2)
    ///
    /// <b>왜 도구여야 하나.</b> 파일을 탐색기나 <c>git mv</c> 로 옮기면 Unity 는 그것을
    /// "지우고 새로 만들었다"로 보고 <b>새 GUID 를 발급한다.</b> 스크립트의 GUID 는
    /// 프리팹·씬이 컴포넌트를 가리키는 유일한 열쇠라, 바뀌는 순간 그 스크립트를 쓰던
    /// 모든 프리팹이 <c>Missing (Mono Script)</c> 가 된다. 되돌릴 수 없다.
    ///
    /// <c>AssetDatabase.RenameAsset</c> 은 <c>.meta</c> 를 같이 옮겨 GUID 를 유지한다.
    /// 그래서 이 작업은 반드시 에디터 안에서 해야 한다.
    ///
    /// <b>클래스 이름을 바꾸는 것 자체는 안전하다.</b> 프리팹은 클래스 이름이 아니라
    /// GUID + fileID 로 참조하기 때문이다. 위험한 것은 오직 파일 이동·이름 변경이다.
    /// </summary>
    public static class ScriptFileRenamer
    {
        /// <summary>
        /// 바꿀 목록. <b>지금 필요한 것만 적는다</b> — 프로젝트 전체를 훑어
        /// "파일명과 클래스명이 다른 것"을 자동으로 고치게 만들면, 의도적으로 여러 타입을
        /// 한 파일에 둔 곳(예: <c>Define.cs</c>)까지 건드린다.
        /// </summary>
        private static readonly (string from, string to)[] Renames =
        {
            ("Assets/Scripts/Interface/IDialog.cs", "DialogBase"),
        };

        // 되돌리는 수단은 git 이다. .meta 도 추적되므로 GUID 까지 그대로 복원된다.
        [MenuItem("OJ/개발/스크립트 파일명을 클래스명에 맞춘다 (GUID 유지)")]
        private static void Apply() => Run(apply: true);

        private static void Run(bool apply)
        {
            var sb = new StringBuilder();
            sb.AppendLine(apply ? "[파일명] 변경" : "[파일명] 미리보기 — 바꾸지 않는다");

            var done = new List<string>();
            foreach ((string from, string to) in Renames)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(from);
                if (script == null)
                {
                    // 이미 바꾼 뒤 다시 돌렸을 때 여기로 온다. 사고가 아니다.
                    sb.AppendLine("  건너뜀 " + from + "   (없다 — 이미 바꿨을 수 있다)");
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(from);
                sb.AppendLine("  " + (apply ? "변경 " : "변경 예정 ") + from + "  ->  " + to + ".cs");
                sb.AppendLine("    GUID " + guid + " (유지된다)");

                if (!apply)
                    continue;

                string error = AssetDatabase.RenameAsset(from, to);
                if (!string.IsNullOrEmpty(error))
                {
                    sb.AppendLine("    !! 실패: " + error);
                    continue;
                }

                done.Add(to);
            }

            if (apply && done.Count > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                sb.AppendLine("  바꾼 것 " + done.Count + "개");
            }

            Debug.Log(sb.ToString());
        }
    }
}
