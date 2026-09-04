using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Dice;

namespace OJ.EditorTools
{
    /// <summary>
    /// 전투 다이스 상세창(<see cref="UIBattleDiceDetailPanel"/>)을 프리팹으로 굽는다.
    ///
    /// <b>왜 다시 굽나.</b> 진화 개편에서 이 창의 내용물이 통째로 바뀌었다 — 전체 화면
    /// 상세창이 아이콘·성급·특성·쿨타임·속성만 담은 작은 카드가 됐고, 딤드가 빠지고,
    /// 진화·교환 버튼 두 개가 새로 생겼다. 옛 프리팹에는 그 버튼이 아예 없어서
    /// <c>[SerializeField]</c> 가 전부 <c>None</c> 으로 남는다.
    ///
    /// <b>같은 경로에 덮어쓴다.</b> 그래야 GUID 가 유지되고
    /// <c>DialogCatalog</c> 가 물고 있는 참조가 안 끊긴다. 경로를 바꾸면 카탈로그가
    /// 죽은 GUID 를 가리키고, 그 실패는 <b>"창이 안 뜬다"로만</b> 드러난다.
    ///
    /// 구운 뒤 <c>Tools/diff_prefab.py</c> 로 두 판을 비교해 멱등성을 확인할 수 있다
    /// (Unity 가 저장마다 fileID 를 새로 발급하므로 <c>git diff</c> 로는 못 본다).
    /// </summary>
    public static class UIBattleDiceDetailPanelBaker
    {
        private const string PrefabPath =
            "Assets/Prefab/Refactory/BattleScene/UIBattleDiceDetailPanel.prefab";

        [MenuItem("OJ/개발/전투 다이스 상세창 프리팹 굽기")]
        private static void Bake()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 모든 글자가 기본 폰트(라틴 전용)로 저장되고 한글이
                // 네모가 된다. 그 상태로 저장하는 것이 최악이라 여기서 멈춘다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            var temp = new GameObject("__BattleDiceDetailBakeRoot");
            try
            {
                UIBattleDiceDetailPanel panel = UIBattleDiceDetailPanel.Create(temp.transform, font);
                GameObject root = panel.gameObject;
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
                          "  기존 프리팹을 덮었으므로 GUID 는 그대로다 — 카탈로그 참조는 살아 있다.");
            }
            finally
            {
                if (temp != null)
                    Object.DestroyImmediate(temp);
            }
        }

        /// <summary>
        /// 한글이 들어 있는 TMP 폰트를 고른다. 이름이 아니라 <b>실제 글리프 보유</b>로 고른다 —
        /// 이름으로 찾으면 폰트를 갈아 끼웠을 때 조용히 라틴 전용이 선택된다.
        /// 경로로 정렬해 실행마다 같은 것이 뽑히게 한다(멱등성).
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
