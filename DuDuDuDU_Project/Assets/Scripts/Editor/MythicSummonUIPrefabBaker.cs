using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Dice;

namespace OJ.EditorTools
{
    /// <summary>
    /// 킹(신화) 다이스 소환 연출 창(<see cref="UIMythicSummonCinematic"/>)을 프리팹으로 굽는다.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> GUID 가 유지되어 <c>DialogCatalog</c> 참조가 안 끊긴다.
    /// 굽고 나면 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 한 번 돌려 등재할 것 —
    /// 등재를 빠뜨리면 킹이 나와도 연출 없이 바로 드러나는 것으로만 드러난다(<c>MergeSystem</c> 의 폴백).
    /// 코드로 조립하므로 몇 번을 돌려도 같은 프리팹이 나온다.
    /// </summary>
    public static class MythicSummonUIPrefabBaker
    {
        private const string CinematicPath = "Assets/Prefab/Refactory/BattleScene/UIMythicSummonCinematic.prefab";
        private const string HannaFontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        [MenuItem("OJ/개발/킹 소환 연출/UI 프리팹 굽기")]
        private static void Bake()
        {
            // 폰트는 BM HANNA 하나다. 없으면 멈춘다 — 폰트 없이 구우면 한글이 전부 네모로 저장된다.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HannaFontPath);
            if (font == null)
            {
                Debug.LogError("[굽기] 폰트가 없다: " + HannaFontPath + " — 프리팹을 만들지 않는다.");
                return;
            }

            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(font));
            var plain = AssetDatabase.LoadAssetAtPath<Material>(
                (folder + "/" + font.name + " Material_NoneOutLine.mat").Replace('\\', '/'));

            var temp = new GameObject("__MythicCinematicBakeRoot");
            try
            {
                GameObject root = UIMythicSummonCinematic.Create(temp.transform, font, plain).gameObject;
                root.transform.SetParent(null, false);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CinematicPath));

                bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(CinematicPath) != null;
                PrefabUtility.SaveAsPrefabAsset(root, CinematicPath, out bool ok);
                Object.DestroyImmediate(root);

                if (!ok)
                {
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + CinematicPath);
                    return;
                }

                Debug.Log("[굽기] " + (existed ? "덮어썼다" : "새로 만들었다") + ": " + CinematicPath + System.Environment.NewLine +
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
    }
}
