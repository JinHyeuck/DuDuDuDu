using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Bounty;

namespace OJ.EditorTools
{
    /// <summary>
    /// 현상금 UI 두 개(<see cref="UIBountyBanner"/>·<see cref="UIBountySelectDialog"/>)를
    /// 프리팹으로 굽는다.
    ///
    /// <b>왜 손으로 안 짜나.</b> 선택 창은 3x2 칸 여섯 개에 칸마다 글자 넷이라 손으로 놓으면
    /// 좌표 서른 개를 인스펙터에 옮겨 적게 된다. 값을 아는 것은 코드이고, 옮겨 적는 순간
    /// 오차가 생긴다 — <c>UIBattleDiceDetailPanel</c> 이 같은 이유로 이 방식이다.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> GUID 가 유지되어 <c>DialogCatalog</c> 참조가 안 끊긴다.
    /// 굽고 나면 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 한 번 돌려 등재할 것 —
    /// 등재를 빠뜨리면 <b>창이 안 열리는 것</b>으로만 드러난다.
    /// </summary>
    public static class BountyUIPrefabBaker
    {
        private const string BannerPath =
            "Assets/Prefab/Refactory/BattleScene/UIBountyBanner.prefab";

        private const string SelectPath =
            "Assets/Prefab/Refactory/BattleScene/UIBountySelectDialog.prefab";

        [MenuItem("OJ/개발/현상금/UI 프리팹 굽기")]
        private static void Bake()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 한글이 전부 네모로 저장된다. 그 상태로 저장하는 것이
                // 최악이라 여기서 멈춘다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            BakeOne("__BountyBannerBakeRoot", BannerPath, font,
                (parent, f) => UIBountyBanner.Create(parent, f).gameObject);

            BakeOne("__BountySelectBakeRoot", SelectPath, font,
                (parent, f) => UIBountySelectDialog.Create(parent, f).gameObject);

            Debug.Log("[굽기] 현상금 UI 두 개를 구웠다." + System.Environment.NewLine +
                      "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려 등재할 것.");
        }

        private static void BakeOne(
            string tempName,
            string path,
            TMP_FontAsset font,
            System.Func<Transform, TMP_FontAsset, GameObject> build)
        {
            var temp = new GameObject(tempName);
            try
            {
                GameObject root = build(temp.transform, font);
                root.transform.SetParent(null, false);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));

                bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + path);
                    return;
                }

                Debug.Log("[굽기] " + (existed ? "덮어썼다" : "새로 만들었다") + ": " + path);
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
        /// 한글이 들어 있는 TMP 폰트를 고른다. 이름이 아니라 <b>실제 글리프 보유</b>로 고르고
        /// 경로로 정렬해 실행마다 같은 것이 뽑히게 한다(멱등성).
        /// <c>UIBattleDiceDetailPanelBaker</c> 와 같은 판정이다.
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
