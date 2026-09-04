using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Equipment;
using OJ.StageStar;

namespace OJ.EditorTools
{
    /// <summary>
    /// 비어 있는 <c>[SerializeField]</c> 참조를 찾아 보고한다. (MIGRATION_BASELINE 10 게이트)
    ///
    /// <b>왜 필요한가.</b> 씬에서 오브젝트를 지우면 그것을 가리키던 참조는 <c>Missing</c> 이
    /// 아니라 <b><c>None</c></b> 이 된다. Missing 은 인스펙터에 빨갛게 뜨지만 None 은
    /// 처음부터 안 채운 것과 구별되지 않는다. 그리고 이 코드베이스는 그런 참조를
    /// <c>?.</c> 나 <c>if (x != null)</c> 로 감싸는 습관이 있어서, 끊긴 배선이
    /// <b>아무 로그 없이 기능만 죽이는</b> 형태로 나타난다.
    ///
    /// 실제로 10.4 에서 그 일이 났다. 페이지를 프리팹 생성으로 바꾸자
    /// <c>UIEquipmentPage.lobbyLayoutController</c> 가 None 이 되어 백키가 조용히 죽었고,
    /// 코드·컴파일·테스트 어느 쪽에도 흔적이 없었다.
    ///
    /// <b>None 이 전부 사고는 아니다.</b> 선택 항목도 많다. 그래서 이 도구는 고치지 않고
    /// <b>목록만 준다</b> — 판단은 사람이 한다.
    /// </summary>
    public static class NoneReferenceReport
    {
        [MenuItem("OJ/개발/빈 참조 보고서")]
        private static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[빈참조] 비어 있는 [SerializeField] 참조 (None)");
            sb.AppendLine("  프로젝트 타입만 본다. Unity 내장 타입(Button, Text ...)은 선택 항목인 경우가 많아 제외한다.");

            int total = 0;
            total += ScanPrefabs(sb);
            total += ScanScenes(sb);

            sb.AppendLine();
            sb.AppendLine("  합계 " + total + "건");
            sb.AppendLine("  전부 사고는 아니다. 기능이 죽었는지는 각각 판단할 것.");
            Debug.Log(sb.ToString());
        }

        private static int ScanPrefabs(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 프리팹 ---");

            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                count += ScanRoot(sb, prefab, path);
            }

            return count;
        }

        private static int ScanScenes(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 씬 ---");

            string activePath = SceneManager.GetActiveScene().path;
            int count = 0;

            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                    count += ScanRoot(sb, root, scene.name);
            }

            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

            return count;
        }

        private static int ScanRoot(StringBuilder sb, GameObject root, string where)
        {
            int count = 0;
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;

                Type type = behaviour.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("OJ", StringComparison.Ordinal))
                    continue;

                var so = new SerializedObject(behaviour);
                SerializedProperty prop = so.GetIterator();
                bool enterChildren = true;

                while (prop.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    if (prop.objectReferenceValue != null)
                        continue;

                    // 프로젝트 타입만 본다. Unity 내장 컴포넌트 슬롯은 비어 있는 것이
                    // 정상인 경우가 많아 섞이면 목록이 쓸모없어진다.
                    string fieldType = prop.type;
                    if (!IsProjectType(behaviour, prop.propertyPath))
                        continue;

                    sb.AppendFormat("  {0}   {1}.{2}   ({3})",
                        where, type.Name, prop.propertyPath, fieldType).AppendLine();
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 이 필드가 프로젝트 타입을 가리키는가.
        ///
        /// <c>SerializedProperty.type</c> 은 <c>PPtr&lt;$UIStageStarDialog&gt;</c> 같은 문자열이라
        /// 그 안의 이름을 꺼내 프로젝트 어셈블리에 있는지 본다. 리플렉션으로 필드를 다시
        /// 찾는 것보다 짧고, 배열 원소처럼 경로가 복잡한 경우에도 흔들리지 않는다.
        /// </summary>
        private static bool IsProjectType(MonoBehaviour behaviour, string propertyPath)
        {
            var so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty(propertyPath);
            if (prop == null)
                return false;

            string raw = prop.type;
            int start = raw.IndexOf('$');
            int end = raw.LastIndexOf('>');
            if (start < 0 || end < 0 || end <= start)
                return false;

            string name = raw.Substring(start + 1, end - start - 1);
            foreach (Type candidate in TypeCache.GetTypesDerivedFrom<UnityEngine.Object>())
            {
                if (candidate.Name != name)
                    continue;

                return candidate.Namespace != null &&
                       candidate.Namespace.StartsWith("OJ", StringComparison.Ordinal);
            }

            return false;
        }
    }
}
