using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using OJ.Stage;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="UISweepCountDialog"/> 를 프리팹으로 굽는다.
    ///
    /// <b>왜 코드로 짓고 구워내나.</b> 이 프로젝트의 다이얼로그는 카탈로그가 든 프리팹이고
    /// (<c>DialogCatalog</c>), 프리팹을 손으로 만들면 위치·색·크기를 전부 옮겨 적어야 한다.
    /// <c>IdleRewardPrefabBaker</c> 가 쓴 방법 그대로 — 정확한 값을 아는 코드를 에디터에서
    /// 한 번 돌려 결과를 저장하면 옮겨 적다 생기는 오차가 없다.
    ///
    /// <b>델리게이트는 저장되지 않는다.</b> <c>onClick.AddListener</c> 로 붙인 것은 프리팹에
    /// 남지 않으므로 굽는 시점에 붙이지 않고 런타임 <c>OnLoad</c> 에서 붙인다.
    /// 여기서 저장되는 것은 <b>계층·값·참조</b>뿐이다.
    ///
    /// 다시 돌려도 안전하다. 같은 경로에 덮어쓰므로 GUID 가 유지되고, 카탈로그와 씬의
    /// 참조가 끊기지 않는다.
    /// </summary>
    public static class SweepCountDialogPrefabBaker
    {
        private const string PrefabPath = "Assets/Prefab/Refactory/LobbyScene/UISweepCountDialog.prefab";

        [MenuItem("OJ/개발/소탕/횟수 선택 창 프리팹 굽기")]
        private static void Bake()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                // 폰트 없이 구우면 모든 텍스트가 기본 폰트(라틴 전용)로 저장되고 한글이
                // 네모로 뜬다. 그 상태로 저장하는 것이 최악이라 여기서 멈춘다.
                Debug.LogError("[굽기] 한글 TMP 폰트를 못 찾았다. 프리팹을 만들지 않는다.");
                return;
            }

            if (!VerifyGlyphs(font))
                return;

            // 임시 부모에 짓는다. 씬을 건드리지 않으려고 캔버스 없이 만든다 —
            // 레이아웃은 전부 앵커·크기로 정해져 있어 캔버스가 없어도 값은 같다.
            var temp = new GameObject("__SweepCountBakeRoot");
            try
            {
                UISweepCountDialog dialog = UISweepCountDialog.Create(temp.transform, font);
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
        /// 폰트가 이 창이 쓸 글자를 전부 갖고 있는지 본다. 하나라도 없으면 굽지 않는다.
        ///
        /// <b>왜 굽기를 막나.</b> 프로젝트 UI 폰트는 Static 이고 폴백이 비어 있어서,
        /// 아틀라스에 없는 글자가 <b>예외도 로그도 없이 빈칸</b>으로 나온다. 실제로 이 창의
        /// "소탕" 에서 '탕' 이 그렇게 사라졌고, 그건 화면을 눈으로 봐야만 드러났다.
        /// 여기서 멈추면 같은 일이 <b>굽는 순간</b> 드러난다.
        ///
        /// 고치는 법은 로그에 적어 준다 — <c>BMHANNAProOTF_CharacterSet.txt</c> 에 글자를
        /// 더하고 Font Asset Creator 로 아틀라스를 다시 구우면 된다.
        /// </summary>
        private static bool VerifyGlyphs(TMP_FontAsset font)
        {
            var missing = UISweepCountDialog.RequiredGlyphs
                .Distinct()
                .Where(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c))
                .ToArray();

            if (missing.Length == 0)
                return true;

            string listed = string.Join(" ", missing.Select(c => $"'{c}'(U+{(int)c:X4})"));
            Debug.LogError(
                $"[굽기] 폰트 '{font.name}' 에 없는 글자가 {missing.Length}개 있다: {listed}" +
                System.Environment.NewLine +
                "  이대로 구우면 그 자리가 빈칸으로 나온다. 프리팹을 만들지 않는다." +
                System.Environment.NewLine +
                "  BMHANNAProOTF_CharacterSet.txt 에 더한 뒤 Font Asset Creator 로 아틀라스를 다시 구울 것.");
            return false;
        }

        /// <summary>
        /// 한글이 들어 있는 TMP 폰트를 고른다. <c>IdleRewardPrefabBaker</c> 와 같은 방식이다 —
        /// 이름이 아니라 <b>실제 한글 글리프 보유</b>로 고르고, 경로로 정렬해 같은 도구를
        /// 두 번 돌려도 같은 폰트가 뽑히게 한다.
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
