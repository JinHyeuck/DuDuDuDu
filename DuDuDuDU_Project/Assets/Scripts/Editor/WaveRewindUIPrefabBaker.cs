using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Rewind;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// 웨이브 되돌리기의 에디터 도구 둘 — 버튼 프리팹 굽기, 설정 에셋 만들기.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> GUID 가 유지되어 <c>DialogCatalog</c> 참조가 안 끊긴다.
    /// 굽고 나면 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 한 번 돌려 등재할 것 —
    /// 등재를 빠뜨리면 <b>버튼이 안 뜨는 것</b>으로만 드러난다.
    /// </summary>
    public static class WaveRewindUIPrefabBaker
    {
        private const string ButtonPath =
            "Assets/Prefab/Refactory/BattleScene/UIWaveRewindButton.prefab";

        private const string SettingsPath =
            "Assets/ScriptableObject/WaveRewindSettings.asset";

        [MenuItem("OJ/개발/되돌리기/UI 프리팹 굽기")]
        private static void BakeButton()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 한글이 전부 네모로 저장된다. 그 상태로 저장하는 것이
                // 최악이라 여기서 멈춘다. ContributionUIPrefabBaker 와 같은 판단이다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            var temp = new GameObject("__WaveRewindBakeRoot");
            try
            {
                GameObject root = UIWaveRewindButton.Create(temp.transform, font).gameObject;
                root.transform.SetParent(null, false);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ButtonPath));

                bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath) != null;
                PrefabUtility.SaveAsPrefabAsset(root, ButtonPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + ButtonPath);
                    return;
                }

                Debug.Log("[굽기] " + (existed ? "덮어썼다" : "새로 만들었다") + ": " + ButtonPath +
                          System.Environment.NewLine +
                          "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려 등재할 것.");
            }
            finally
            {
                if (temp != null)
                    Object.DestroyImmediate(temp);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 되돌리기 설정 에셋을 만들고 <c>StaticResource</c> 에 꽂는다.
        ///
        /// <b>왜 <c>StaticResourceAutoWire</c> 에 안 넣었나.</b> 그 도구는 슬롯이 비었는데
        /// 후보가 없으면 <b>경고를 낸다</b> — 컴파일마다 돈다. 그런데 이 에셋은
        /// <b>없는 것이 정상</b>이다(코드 기본값이 곧 출시 값이라 Provider 가 조용히 내려간다).
        /// 정상 상태에 매번 노란 줄을 띄우면 진짜 사고가 그 밑에 묻힌다.
        /// 그래서 자동 배선 목록 대신, 만들 때 여기서 직접 잇는다.
        /// </summary>
        [MenuItem("OJ/개발/되돌리기/설정 에셋 만들기")]
        private static void CreateSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<WaveRewindSettings>(SettingsPath);
            if (existing == null)
            {
                WaveRewindSettings created = ScriptableObject.CreateInstance<WaveRewindSettings>();
                created.PopulateDefaults();

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SettingsPath));
                AssetDatabase.CreateAsset(created, SettingsPath);
                AssetDatabase.SaveAssets();

                existing = created;
                Debug.Log("[되돌리기] 설정 에셋을 만들었다: " + SettingsPath);
            }
            else
            {
                // 이미 있으면 값을 건드리지 않는다. 밸런스를 조정해 둔 것을 도구가
                // 기본값으로 되돌리면, 그 손실은 되돌릴 방법이 없다.
                Debug.Log("[되돌리기] 설정 에셋이 이미 있다. 값은 건드리지 않는다: " + SettingsPath);
            }

            WireIntoStaticResource(existing);
        }

        private static void WireIntoStaticResource(WaveRewindSettings settings)
        {
            GameObject prefab = FindStaticResourcePrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[되돌리기] StaticResource 프리팹을 못 찾아 배선을 건너뛴다. " +
                                 "에셋은 만들어져 있으니 손으로 꽂아도 된다.");
                return;
            }

            var resource = prefab.GetComponent<StaticResource>();
            var so = new SerializedObject(resource);
            SerializedProperty prop = so.FindProperty("WaveRewindSettings");
            if (prop == null)
            {
                Debug.LogError("[되돌리기] StaticResource 에 WaveRewindSettings 필드가 없다.");
                return;
            }

            if (prop.objectReferenceValue == settings)
            {
                Debug.Log("[되돌리기] 이미 꽂혀 있다.");
                return;
            }

            prop.objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(resource);
            PrefabUtility.SavePrefabAsset(prefab);
            AssetDatabase.SaveAssets();

            Debug.Log("[되돌리기] StaticResource.WaveRewindSettings 에 꽂았다.");
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
        /// 한글이 들어 있는 TMP 폰트를 고른다. 이름이 아니라 <b>실제 글리프 보유</b>로 고르고
        /// 경로로 정렬해 실행마다 같은 것이 뽑히게 한다(멱등성).
        /// <c>ContributionUIPrefabBaker</c>·<c>TowerUIPrefabBaker</c> 와 같은 판정이다.
        /// </summary>
        private static TMP_FontAsset FindKoreanFont()
        {
            const int Sample = '가';

            return AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, System.StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>)
                .FirstOrDefault(f => f != null && f.HasCharacter(Sample));
        }
    }
}
