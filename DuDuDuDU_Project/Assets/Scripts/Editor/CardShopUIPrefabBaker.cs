using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Dice;

namespace OJ.EditorTools
{
    /// <summary>
    /// 조합식 카드 상점 UI 둘(<see cref="UICardShopPanel"/> · <see cref="UIRecipeBookDialog"/>)을 프리팹으로 굽는다.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> GUID 가 유지되어 <c>DialogCatalog</c> 참조가 안 끊긴다.
    /// 굽고 나면 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 한 번 돌려 등재할 것 —
    /// 등재를 빠뜨리면 <b>카드 창이 안 뜨는 것</b>으로만 드러난다(<c>UIService</c> 가 에러를 남긴다).
    ///
    /// <b>재실행해도 안전하다</b> — 코드로 조립하므로 몇 번을 돌려도 같은 프리팹이 나온다.
    /// </summary>
    public static class CardShopUIPrefabBaker
    {
        private const string PanelPath = "Assets/Prefab/Refactory/BattleScene/UICardShopPanel.prefab";
        private const string BookPath = "Assets/Prefab/Refactory/BattleScene/UIRecipeBookDialog.prefab";
        private const string MythicListPath = "Assets/Prefab/Refactory/BattleScene/UIMythicListDialog.prefab";
        private const string CinematicPath = "Assets/Prefab/Refactory/BattleScene/UIMythicSummonCinematic.prefab";
        private const string HannaFontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        [MenuItem("OJ/개발/조합식 카드/UI 프리팹 굽기")]
        private static void Bake()
        {
            // 폰트는 BM HANNA 하나다. 없으면 멈춘다 — 폰트 없이 구우면 한글이 전부 네모로 저장된다.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HannaFontPath);
            if (font == null)
            {
                Debug.LogError("[굽기] 폰트가 없다: " + HannaFontPath + " — 프리팹을 만들지 않는다.");
                return;
            }

            // 갈색 글자(카드 종류·이름)는 외곽선이 없다. 없으면 기본 머티리얼로 굽고 알린다.
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(font));
            var plain = AssetDatabase.LoadAssetAtPath<Material>(
                (folder + "/" + font.name + " Material_NoneOutLine.mat").Replace('\\', '/'));
            if (plain == null)
                Debug.LogWarning("[굽기] 외곽선 없는 머티리얼을 못 찾았다. 갈색 글자에도 외곽선이 붙는다.");

            BakeOne("__CardShopBakeRoot", PanelPath,
                (parent) => UICardShopPanel.Create(parent, font, plain).gameObject);
            BakeOne("__RecipeBookBakeRoot", BookPath,
                (parent) => UIRecipeBookDialog.Create(parent, font, plain).gameObject);

            BakeOne("__MythicListBakeRoot", MythicListPath,
                (parent) => UIMythicListDialog.Create(parent, font, plain).gameObject);
            BakeOne("__MythicCinematicBakeRoot", CinematicPath,
                (parent) => UIMythicSummonCinematic.Create(parent, font, plain).gameObject);

            Debug.Log("[굽기] 조합식 카드 UI 넷을 구웠다." + System.Environment.NewLine +
                      "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려 등재할 것.");
        }

        private static void BakeOne(string tempName, string path, System.Func<Transform, GameObject> build)
        {
            var temp = new GameObject(tempName);
            try
            {
                GameObject root = build(temp.transform);
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
    }
}
