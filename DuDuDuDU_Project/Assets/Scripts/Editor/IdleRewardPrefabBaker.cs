using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.IdleReward;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="UIIdleRewardDialog"/> 를 프리팹으로 굽는다. (MIGRATION_BASELINE 10.5)
    ///
    /// <b>왜 구워내나.</b> 이 창만 UI 를 코드로 지어서 나머지 16개와 구조가 달랐다.
    /// 손으로 다시 만들면 위치·색·크기를 전부 옮겨 적어야 하는데, 이미 정확한 값을 아는
    /// 코드가 있다. 그 코드를 <b>에디터에서 한 번 돌려</b> 결과를 프리팹으로 저장하면
    /// 옮겨 적는 과정에서 생길 오차가 없다.
    ///
    /// <b>델리게이트는 저장되지 않는다.</b> <c>onClick.AddListener</c> 로 붙인 것은
    /// 프리팹에 남지 않으므로, 구울 때 붙이지 않고 런타임 <c>OnLoad</c> 에서 붙인다.
    /// 여기서 저장되는 것은 <b>계층·값·참조</b>뿐이다.
    ///
    /// 다시 돌려도 안전하다. 같은 경로에 덮어쓰므로 GUID 가 유지되고, 카탈로그와 씬의
    /// 참조가 끊기지 않는다.
    /// </summary>
    public static class IdleRewardPrefabBaker
    {
        private const string PrefabPath = "Assets/Prefab/Refactory/LobbyScene/UIIdleRewardDialog.prefab";

        [MenuItem("OJ/개발/방치보상 창 프리팹 굽기")]
        private static void Bake()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 모든 텍스트가 기본 폰트(라틴 전용)로 저장되고,
                // 한글이 네모로 뜬다. 그 상태로 저장하는 것이 최악이라 여기서 멈춘다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            // 임시 부모에 짓는다. 씬을 건드리지 않으려고 캔버스 없이 만든다 —
            // 레이아웃은 전부 앵커·크기로 정해져 있어 캔버스가 없어도 값은 같다.
            var temp = new GameObject("__IdleRewardBakeRoot");
            try
            {
                UIIdleRewardDialog dialog = UIIdleRewardDialog.Create(temp.transform, font);
                GameObject root = dialog.gameObject;
                root.transform.SetParent(null, false);

                System.IO.Directory.CreateDirectory(
                    System.IO.Path.GetDirectoryName(PrefabPath));

                bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + PrefabPath);
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[굽기] " + (existed ? "덮어썼다" : "새로 만들었다") + ": " + PrefabPath +
                          System.Environment.NewLine +
                          "  폰트: " + font.name + System.Environment.NewLine +
                          "  카탈로그 갱신(OJ/개발/다이얼로그 카탈로그/훑어서 갱신)을 이어서 돌릴 것.");
            }
            finally
            {
                if (temp != null)
                    Object.DestroyImmediate(temp);
            }
        }

        /// <summary>
        /// 한글이 들어 있는 TMP 폰트를 고른다.
        ///
        /// 이름으로 찾지 않는다 — 폰트를 갈아 끼우면 이름이 바뀌는데 그때 조용히
        /// 라틴 전용 폰트가 선택되면 한글이 전부 네모가 된다. <b>실제로 한글 글리프를
        /// 갖고 있는지</b>로 고르는 편이 이름보다 정확하다.
        ///
        /// <b>경로로 정렬한 뒤 고른다.</b> <c>FindAssets</c> 의 반환 순서는 보장되지
        /// 않아서, 한글 폰트가 둘 이상이면 실행마다 다른 것이 뽑힐 수 있다. 그러면
        /// 같은 도구를 두 번 돌렸을 때 프리팹이 달라진다 — 12단계 게이트가 요구하는
        /// 멱등성이 깨지는 자리다. 정렬은 그것을 막는 한 줄이다.
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
