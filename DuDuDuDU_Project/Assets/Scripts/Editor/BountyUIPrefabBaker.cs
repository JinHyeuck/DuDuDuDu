using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Bounty;
using OJ.Hunting;

namespace OJ.EditorTools
{
    /// <summary>
    /// 현상금 UI 둘(<see cref="UIBountyBanner"/>·<see cref="UIBountySelectDialog"/>)과
    /// 보스·현상금 경고 띠(<see cref="UIBattleWarning"/>)를 프리팹으로 굽는다.
    /// 시안은 <c>Art/Layout/Wanted_Layout*.png</c> · <c>Warning_Layout*.png</c>, 수치는
    /// <c>Docs/WantedUIArtPort.md</c>.
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

        private const string WarningPath =
            "Assets/Prefab/Refactory/BattleScene/UIBattleWarning.prefab";

        /// <summary>
        /// 경고 띠로 대체된 옛 현상금 알림. 남아 있으면 카탈로그가 클래스 없는 프리팹을
        /// 들고 있게 되므로 굽기가 치운다(휴지통으로 — 되돌릴 수 있게).
        /// </summary>
        private const string ObsoleteCalloutPath =
            "Assets/Prefab/Refactory/BattleScene/UIBountyCallout.prefab";

        [MenuItem("OJ/개발/현상금/UI 프리팹 굽기")]
        private static void Bake()
        {
            // 폰트는 BM HANNA 하나다. PSD 글자 레이어는 NotoSansKR-Black 이지만 이 게임의 폰트가
            // 아니다(사용자 결정, 2026-10-01) — PSD 에서는 크기·색·외곽선만 가져온다.
            // 없으면 멈춘다: 폰트 없이 구우면 한글이 전부 네모로 저장된다.
            TMP_FontAsset hanna = LoadFont(HannaFontPath);
            if (hanna == null)
                return;

            // 색 글자(갈색 등)는 외곽선이 없다. 없으면 기본(외곽선) 머티리얼로 굽고 알린다 —
            // 멈출 만한 일은 아니지만 모르고 지나가면 시안과 다르게 나온다.
            Material plain = FindPlainMaterial(hanna);
            if (plain == null)
                Debug.LogWarning("[굽기] '" + hanna.name + " Material_NoneOutLine' 을 못 찾았다. 색 글자에도 외곽선이 붙는다.");

            BakeOne("__BountyBannerBakeRoot", BannerPath, hanna,
                (parent, f) => UIBountyBanner.Create(parent, f, plain).gameObject);

            BakeOne("__BountySelectBakeRoot", SelectPath, hanna,
                (parent, f) => UIBountySelectDialog.Create(parent, f, plain).gameObject);

            BakeOne("__BattleWarningBakeRoot", WarningPath, hanna,
                (parent, f) => UIBattleWarning.Create(parent, f).gameObject);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ObsoleteCalloutPath) != null)
            {
                bool trashed = AssetDatabase.MoveAssetToTrash(ObsoleteCalloutPath);
                Debug.Log("[굽기] 옛 현상금 알림 프리팹을 " + (trashed ? "휴지통으로 옮겼다: " : "치우지 못했다: ") +
                          ObsoleteCalloutPath);
            }

            Debug.Log("[굽기] 현상금 UI 두 개와 경고 띠를 구웠다." + System.Environment.NewLine +
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
        /// 폰트 옆의 외곽선 없는 머티리얼(<c>{폰트} Material_NoneOutLine</c>). 폰트마다 하나씩 있다.
        /// </summary>
        private static Material FindPlainMaterial(TMP_FontAsset font)
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(font));
            string path = folder + "/" + font.name + " Material_NoneOutLine.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path.Replace('\\', '/'));
        }

        private const string HannaFontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        private static TMP_FontAsset LoadFont(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
                Debug.LogError("[굽기] 폰트가 없다: " + path + " — 프리팹을 만들지 않는다.");
            return font;
        }
    }
}
