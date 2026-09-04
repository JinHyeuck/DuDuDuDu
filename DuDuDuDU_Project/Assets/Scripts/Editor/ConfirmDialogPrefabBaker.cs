using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.UI;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="UIConfirmDialog"/> 를 프리팹으로 굽는다.
    ///
    /// <b>왜 손으로 안 만드나.</b> 10.5 와 같은 이유다 — 위치·색·크기를 인스펙터에 옮겨
    /// 적으면 오차가 생기고, 정확한 값을 아는 것은 코드다. 한 번 돌려 저장한다.
    ///
    /// <b>다시 돌려도 안전하다.</b> 같은 경로에 덮어쓰므로 GUID 가 유지되고,
    /// 카탈로그가 그 GUID 로 물고 있으므로 참조가 끊기지 않는다.
    ///
    /// 구운 뒤 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 이어서 돌릴 것.
    /// </summary>
    public static class ConfirmDialogPrefabBaker
    {
        private const string PrefabPath = "Assets/Prefab/Refactory/UIConfirmDialog.prefab";

        [MenuItem("OJ/개발/확인 창 프리팹 굽기")]
        private static void Bake()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 모든 글자가 기본 폰트(라틴 전용)로 저장되고 한글이
                // 네모가 된다. 그 상태로 저장하는 것이 최악이라 여기서 멈춘다. (10.7)
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            var temp = new GameObject("__ConfirmDialogBakeRoot");
            try
            {
                UIConfirmDialog dialog = UIConfirmDialog.Create(temp.transform, font);
                GameObject root = dialog.gameObject;
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
        /// 한글이 들어 있는 TMP 폰트를 고른다. 이름이 아니라 <b>실제 글리프 보유</b>로 고른다 —
        /// 이름으로 찾으면 폰트를 갈아 끼웠을 때 조용히 라틴 전용이 선택된다. (10.5)
        /// 경로로 정렬해 실행마다 같은 것이 뽑히게 한다(12.1 멱등성).
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
