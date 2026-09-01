using System.Linq;
using UnityEditor;
using UnityEngine;
using OJ.UI;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// <c>StaticResource</c> 프리팹의 빈 슬롯을 프로젝트에서 찾아 채운다.
    ///
    /// <b>왜 자동인가.</b> 이런 배선은 "에셋 하나를 슬롯에 끌어다 놓기"인데, 사람이 하면
    /// 빠뜨리고 그 결과는 <b>런타임에 조용한 null</b> 로만 드러난다. 프로젝트에 후보가
    /// 하나뿐인 종류의 참조는 기계가 잇는 편이 정확하다.
    ///
    /// <b>안전 규칙 세 가지.</b>
    /// <list type="number">
    /// <item><b>비어 있을 때만 채운다.</b> 이미 꽂힌 것을 갈아 끼우지 않는다 —
    ///   일부러 다른 것을 넣었을 수 있다.</item>
    /// <item><b>후보가 정확히 하나일 때만 채운다.</b> 둘 이상이면 어느 것이 맞는지
    ///   기계가 알 수 없으므로 손대지 않고 알린다.</item>
    /// <item><b>한 일을 반드시 로그로 남긴다.</b> 에셋을 조용히 바꾸는 도구는
    ///   나중에 "왜 바뀌었지"를 만든다.</item>
    /// </list>
    /// </summary>
    public static class StaticResourceAutoWire
    {
        // 경로를 박아 두지 않는다. 처음에 Assets/Prefab/ 로 짐작했다가 실제 위치가
        // Assets/Resources/ 여서 도구가 통째로 조용히 아무 일도 하지 않았다.
        // 컴포넌트로 찾으면 프리팹을 옮겨도 따라간다.

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            // 컴파일 직후마다 돈다. 비어 있지 않으면 아무 일도 하지 않으므로
            // 평소에는 로그조차 남지 않는다.
            Wire(quiet: true);
        }

        [MenuItem("OJ/개발/StaticResource 빈 슬롯 채우기")]
        private static void WireFromMenu() => Wire(quiet: false);

        private static void Wire(bool quiet)
        {
            GameObject prefab = FindStaticResourcePrefab();
            if (prefab == null)
            {
                // 조용히 넘어가지 않는다. 도구가 대상을 못 찾으면 아무 일도 하지 않는데,
                // 그건 "배선이 이미 다 됐다"와 로그상 구별되지 않는다. 실제로 그 침묵 때문에
                // 경로 오류를 한 번 놓쳤다.
                Debug.LogWarning("[배선] StaticResource 컴포넌트를 가진 프리팹을 못 찾았다.");
                return;
            }

            var resource = prefab.GetComponent<StaticResource>();

            bool changed = false;
            changed |= TryFill<DialogCatalog>(resource, "DialogCatalog", quiet);

            if (!changed)
            {
                if (!quiet)
                    Debug.Log("[배선] 채울 빈 슬롯이 없다.");

                return;
            }

            EditorUtility.SetDirty(resource);
            PrefabUtility.SavePrefabAsset(prefab);
            AssetDatabase.SaveAssets();
        }

        private static GameObject FindStaticResourcePrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (candidate != null && candidate.GetComponent<StaticResource>() != null)
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// 슬롯이 비어 있고 프로젝트에 후보가 하나뿐이면 채운다.
        /// </summary>
        private static bool TryFill<T>(StaticResource resource, string fieldName, bool quiet)
            where T : Object
        {
            var so = new SerializedObject(resource);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                if (!quiet)
                    Debug.LogError("[배선] StaticResource 에 " + fieldName + " 필드가 없다.");

                return false;
            }

            if (prop.objectReferenceValue != null)
                return false;

            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            if (guids.Length == 0)
            {
                // 이것도 조용히 넘기지 않는다. 슬롯이 비었는데 채울 것도 없다는 뜻이라
                // 그 자체가 알아야 할 사실이다.
                Debug.LogWarning("[배선] " + typeof(T).Name + " 에셋이 프로젝트에 없어 " +
                                 fieldName + " 슬롯을 채우지 못했다.");
                return false;
            }

            if (guids.Length > 1)
            {
                // 어느 것이 맞는지 기계가 알 수 없다. 아무거나 꽂으면 "왜 엉뚱한 목록이
                // 쓰이지"를 나중에 쫓게 된다.
                Debug.LogWarning("[배선] " + typeof(T).Name + " 에셋이 " + guids.Length +
                                 "개라 자동으로 못 고른다. 직접 꽂을 것:" + System.Environment.NewLine +
                                 string.Join(System.Environment.NewLine,
                                     guids.Select(g => "  " + AssetDatabase.GUIDToAssetPath(g))));
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            prop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<T>(path);
            so.ApplyModifiedProperties();

            Debug.Log("[배선] StaticResource." + fieldName + " <- " + path);
            return true;
        }
    }
}
