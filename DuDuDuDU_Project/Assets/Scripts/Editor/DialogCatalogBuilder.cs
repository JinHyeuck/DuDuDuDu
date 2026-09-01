using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.UI;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="DialogCatalog"/> 를 프로젝트에서 훑어 채운다. (MIGRATION_BASELINE 10.3 / 12)
    ///
    /// <b>왜 손으로 안 적나.</b> 다이얼로그가 17개고 앞으로 늘어난다. 목록을 손으로 관리하면
    /// 새 다이얼로그를 만들면서 등재를 잊는 일이 반드시 생기고, 그 결과는 <b>런타임에
    /// "창이 안 열린다"</b> 로만 드러난다. 훑어서 채우면 그 사고가 성립하지 않는다.
    ///
    /// <b>덮어쓰지 않고 보고한다.</b> 이미 있는 항목은 그대로 두고, 빠진 것만 더하고,
    /// 프리팹이 없어진 항목은 <i>지우지 않고 문제로 보고</i>한다. 자동 도구가 조용히
    /// 지우면 사람이 일부러 넣은 예외까지 사라진다.
    /// </summary>
    public static class DialogCatalogBuilder
    {
        private const string CatalogPath = "Assets/ScriptableObject/DialogCatalog.asset";

        [MenuItem("OJ/개발/다이얼로그 카탈로그/훑어서 갱신")]
        private static void Rebuild()
        {
            DialogCatalog catalog = LoadOrCreate();
            var sb = new StringBuilder("[카탈로그] 갱신").AppendLine();

            List<GameObject> found = FindDialogPrefabs();
            sb.AppendLine("  프로젝트에서 찾은 다이얼로그 프리팹 " + found.Count + "개");

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("entries");

            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty e = list.GetArrayElementAtIndex(i);
                GameObject prefab = e.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                string typeName = e.FindPropertyRelative("typeName").stringValue;
                string key = !string.IsNullOrWhiteSpace(typeName) ? typeName.Trim()
                    : (prefab != null ? prefab.name : null);

                if (!string.IsNullOrEmpty(key))
                    known.Add(key);
            }

            int added = 0;
            foreach (GameObject prefab in found)
            {
                if (known.Contains(prefab.name))
                    continue;

                list.arraySize++;
                SerializedProperty e = list.GetArrayElementAtIndex(list.arraySize - 1);
                e.FindPropertyRelative("typeName").stringValue = string.Empty;
                e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                known.Add(prefab.name);
                added++;
                sb.AppendLine("    + " + prefab.name);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            sb.AppendLine("  더한 것 " + added + "개 / 전체 " + catalog.Entries.Count + "개");
            AppendValidation(sb, catalog);
            Debug.Log(sb.ToString());
        }

        [MenuItem("OJ/개발/다이얼로그 카탈로그/검사만")]
        private static void ValidateOnly()
        {
            DialogCatalog catalog = AssetDatabase.LoadAssetAtPath<DialogCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[카탈로그] 에셋이 없다: " + CatalogPath);
                return;
            }

            var sb = new StringBuilder("[카탈로그] 검사").AppendLine();
            sb.AppendLine("  등재 " + catalog.Entries.Count + "개");
            AppendValidation(sb, catalog);
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 등재 누락을 잡는다. <b>여기가 이 도구의 핵심이다</b> —
        /// 코드에 <c>DialogBase</c> 파생이 있는데 카탈로그에 없으면 그 창은 열 방법이 없다.
        /// </summary>
        private static void AppendValidation(StringBuilder sb, DialogCatalog catalog)
        {
            List<string> problems = catalog.Validate();

            var registered = new HashSet<string>(
                catalog.Entries.Select(DialogCatalog.KeyOf).Where(k => !string.IsNullOrEmpty(k)),
                StringComparer.Ordinal);

            foreach (Type type in TypeCache.GetTypesDerivedFrom<DialogBase>())
            {
                if (type.IsAbstract)
                    continue;

                if (!registered.Contains(type.Name))
                    problems.Add("코드에는 있는데 카탈로그에 없다: " + type.Name);
            }

            if (problems.Count == 0)
            {
                sb.AppendLine("  문제 없음.");
                return;
            }

            sb.AppendLine("  문제 " + problems.Count + "건:");
            foreach (string p in problems)
                sb.AppendLine("    !! " + p);
        }

        /// <summary>
        /// 루트에 <see cref="DialogBase"/> 파생이 붙은 프리팹만 고른다.
        /// 자식에 붙은 것은 다이얼로그 자체가 아니라 그 안의 부품이다.
        /// </summary>
        private static List<GameObject> FindDialogPrefabs()
        {
            var found = new List<GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                if (prefab.GetComponent<DialogBase>() != null)
                    found.Add(prefab);
            }

            return found.OrderBy(p => p.name, StringComparer.Ordinal).ToList();
        }

        private static DialogCatalog LoadOrCreate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DialogCatalog>(CatalogPath);
            if (catalog != null)
                return catalog;

            catalog = ScriptableObject.CreateInstance<DialogCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[카탈로그] 새로 만들었다: " + CatalogPath);
            return catalog;
        }
    }
}
