using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Tutorial;

namespace OJ.EditorTools
{
    /// <summary>
    /// 튜토리얼 오버레이 프리팹을 굽는다. <c>WaveRewindUIPrefabBaker</c> 와 같은 자리의 도구다.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> GUID 가 유지되어 <c>DialogCatalog</c> 참조가 안 끊기므로
    /// 몇 번을 눌러도 안전하다. 처음 구웠으면 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을
    /// 한 번 돌려 등재할 것 — 등재를 빠뜨리면 튜토리얼이 "오버레이를 열지 못했다" 로그를 남기고
    /// <b>가르치지 않은 채 평범한 판으로</b> 흘러간다.
    /// </summary>
    public static class TutorialUIPrefabBaker
    {
        private const string PrefabPath =
            "Assets/Prefab/Refactory/BattleScene/UITutorialOverlay.prefab";

        private const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        // 손가락 아트가 아직 없다. Unity 내장 UI 스프라이트로 자리를 잡는다 —
        // 빌드에 항상 들어 있는 것이라 경로가 깨질 일이 없다.
        private const string RingSpritePath = "UI/Skin/Knob.psd";
        private const string ArrowSpritePath = "UI/Skin/DropdownArrow.psd";

        [MenuItem("OJ/개발/튜토리얼/UI 프리팹 굽기")]
        private static void Bake()
        {
            // 폰트는 BM HANNA 하나다(IngamePsdTextApplier 주석). 없으면 한글이 네모로 저장되므로 멈춘다.
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                Debug.LogError("[굽기] BM HANNA 폰트를 못 찾았다: " + FontPath + " — 프리팹을 만들지 않는다.");
                return;
            }

            Sprite ring = AssetDatabase.GetBuiltinExtraResource<Sprite>(RingSpritePath);
            Sprite arrow = AssetDatabase.GetBuiltinExtraResource<Sprite>(ArrowSpritePath);
            if (ring == null || arrow == null)
                Debug.LogWarning("[굽기] 내장 UI 스프라이트를 못 찾았다. 손가락이 흰 사각형으로 보인다.");

            var temp = new GameObject("__TutorialOverlayBakeRoot");
            try
            {
                GameObject root = UITutorialOverlay.Create(temp.transform, font, ring, arrow).gameObject;
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
    }
}
