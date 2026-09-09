using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Shop;

namespace OJ.EditorTools
{
    /// <summary>
    /// 상점 UI 프리팹과 <see cref="ShopDatabase"/> 에셋을 만든다.
    ///
    /// <b>왜 손으로 안 짜나.</b> 섹션 일곱 개에 머리글 일곱 + 항목 템플릿 넷이고, 항목마다
    /// 글자·아이콘·버튼이 서넛씩 붙는다. 손으로 놓으면 좌표와 색을 인스펙터에 백 번 옮겨
    /// 적게 되고, 옮겨 적는 순간 섹션마다 여백이 달라진다 —
    /// <c>TowerUIPrefabBaker</c>·<c>BountyUIPrefabBaker</c> 가 같은 이유로 이 방식이다.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> GUID 가 유지되어 <c>DialogCatalog</c> 참조가 안 끊긴다.
    /// 굽고 나면 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 한 번 돌려 등재할 것 —
    /// 등재를 빠뜨리면 <b>상점 탭이 빈 채로 열리는 것</b>으로만 드러난다.
    /// </summary>
    public static class ShopUIPrefabBaker
    {
        private const string PrefabPath = "Assets/Prefab/Refactory/LobbyScene/UIShopPage.prefab";
        private const string DatabasePath = "Assets/ScriptableObject/ShopDatabase.asset";

        [MenuItem("OJ/개발/상점/UI 프리팹 굽기")]
        private static void Bake()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 섹션 제목 일곱 개가 전부 네모로 저장된다. 그 상태로
                // 저장하는 것이 최악이라 여기서 멈춘다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            var temp = new GameObject("__ShopBakeRoot");
            try
            {
                GameObject root = UIShopPage.Create(temp.transform, font).gameObject;
                root.transform.SetParent(null, false);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));

                bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + PrefabPath);
                    return;
                }

                Debug.Log("[굽기] " + (existed ? "덮어썼다" : "새로 만들었다") + ": " + PrefabPath +
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
        /// 가격표 에셋을 만든다.
        ///
        /// <b>이미 있으면 값을 건드리지 않는다.</b> 조정해 둔 수치를 도구가 기본값으로
        /// 되돌리면 그 손실은 되돌릴 방법이 없다 — <c>WaveRewindUIPrefabBaker</c> 와 같은 판단이다.
        /// </summary>
        [MenuItem("OJ/개발/상점/데이터베이스 에셋 만들기")]
        private static void CreateDatabase()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShopDatabase>(DatabasePath);
            if (existing != null)
            {
                Debug.Log("[상점] 데이터베이스 에셋이 이미 있다. 값은 건드리지 않는다: " + DatabasePath);
                ReportValidation(existing);
                return;
            }

            ShopDatabase created = ScriptableObject.CreateInstance<ShopDatabase>();
            created.PopulateDefaults();

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(DatabasePath));
            AssetDatabase.CreateAsset(created, DatabasePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[상점] 데이터베이스 에셋을 만들었다: " + DatabasePath + System.Environment.NewLine +
                      "  다음: OJ/개발/StaticResource 빈 슬롯 채우기 가 자동으로 잇는다.");

            ReportValidation(created);
        }

        /// <summary>
        /// 에셋을 기본값으로 <b>되돌린다</b>. 위 "만들기" 와 달리 이미 있는 값을 덮는다.
        ///
        /// 별도 메뉴로 뺀 이유는 <b>덮어쓰기가 되돌릴 수 없기 때문</b>이다. 조정해 둔 수치가
        /// 있으면 사라진다. 구조가 바뀌어(필드 추가·삭제) 옛 에셋이 빈 칸투성이일 때만 쓴다.
        /// </summary>
        [MenuItem("OJ/개발/상점/데이터베이스 기본값으로 되돌리기")]
        private static void ResetDatabase()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ShopDatabase>(DatabasePath);
            if (asset == null)
            {
                Debug.LogWarning("[상점] 데이터베이스 에셋이 없다. 먼저 만들 것: " + DatabasePath);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "상점 데이터베이스 되돌리기",
                    "지금 에셋의 값을 전부 기본값으로 덮는다. 되돌릴 수 없다. 계속할까?",
                    "덮어쓴다", "취소"))
            {
                return;
            }

            asset.PopulateDefaults();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[상점] 데이터베이스를 기본값으로 되돌렸다: " + DatabasePath);
            ReportValidation(asset);
        }

        [MenuItem("OJ/개발/상점/데이터베이스 검사")]
        private static void ValidateDatabase()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ShopDatabase>(DatabasePath);
            if (asset == null)
            {
                Debug.LogWarning("[상점] 데이터베이스 에셋이 없다: " + DatabasePath);
                return;
            }

            ReportValidation(asset);
        }

        private static void ReportValidation(ShopDatabase database)
        {
            List<string> problems = database.Validate();
            if (problems.Count == 0)
            {
                Debug.Log("[상점] 데이터베이스 검사 통과.");
                return;
            }

            Debug.LogWarning("[상점] 데이터베이스 문제 " + problems.Count + "건:" +
                             System.Environment.NewLine + "  " +
                             string.Join(System.Environment.NewLine + "  ", problems));
        }

        /// <summary>
        /// 한글이 들어 있는 TMP 폰트를 고른다. 이름이 아니라 <b>실제 글리프 보유</b>로 고르고
        /// 경로로 정렬해 실행마다 같은 것이 뽑히게 한다(멱등성).
        /// <c>TowerUIPrefabBaker</c> 와 같은 판정이다.
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
