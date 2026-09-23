using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.IdleReward;

namespace OJ.EditorTools
{
    /// <summary>
    /// 로비에 고기 축제 위젯을 심는다.
    ///
    /// <b>방치보상 버튼을 대체하지 않는다.</b> 버튼이 하나 늘어나는 것이 맞다 —
    /// 방치보상 팝업은 자동전투 보상으로 남고, 고기는 팝업 없이 여기서 바로 받는다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구가 <c>[InitializeOnLoad]</c> 로 돌면 에디터를
    /// 켤 때마다 LobbyScene 을 덮어써서 손으로 한 작업이 조용히 사라진다
    /// (MIGRATION_BASELINE 1.1).
    ///
    /// <b>두 번 눌러도 안전하다.</b> 이미 있으면 아무것도 하지 않는다 — 자리를 씬에서
    /// 옮겨 뒀어도 그대로 남는다.
    /// </summary>
    public static class MeatFestivalLobbyInstaller
    {
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";

        /// <summary>붙일 부모. 소탕 버튼과 같은 줄이다(<c>Canvas/Content/Stage</c>).</summary>
        private const string ParentName = "Stage";

        /// <summary>
        /// 처음 놓이는 자리. 소탕 버튼이 (-167, -532) 에 324x142 로 있으므로 그 오른쪽이다.
        /// <b>마음에 안 들면 씬에서 옮기면 된다</b> — 이 도구는 이미 있는 것을 옮기지 않는다.
        /// </summary>
        private static readonly Vector2 AnchoredPosition = new Vector2(330f, -532f);

        [MenuItem("OJ/개발/고기축제/로비 위젯 설치")]
        private static void Install()
        {
            TMP_FontAsset font = FindKoreanFont();
            if (font == null)
            {
                Debug.LogError("[고기축제] 한글 TMP 폰트를 못 찾았다. 위젯을 만들지 않는다.");
                return;
            }

            Scene lobbyScene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasAlreadyLoaded = lobbyScene.IsValid() && lobbyScene.isLoaded;
            if (!wasAlreadyLoaded)
                lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            try
            {
                if (FindInScene(lobbyScene) != null)
                {
                    Debug.Log("[고기축제] 로비에 위젯이 이미 있다. 그대로 둔다 — " +
                              "자리를 바꾸려면 씬에서 옮길 것.");
                    return;
                }

                Transform parent = FindParent(lobbyScene);
                if (parent == null)
                {
                    Debug.LogError($"[고기축제] LobbyScene 에서 '{ParentName}' 를 못 찾아 위젯을 심지 못했다.");
                    return;
                }

                UIMeatFestivalLobbyButton widget = UIMeatFestivalLobbyButton.Create(parent, font);
                var rect = widget.GetComponent<RectTransform>();
                rect.anchoredPosition = AnchoredPosition;
                rect.SetAsLastSibling();

                Undo.RegisterCreatedObjectUndo(widget.gameObject, "고기축제 위젯 설치");
                EditorSceneManager.MarkSceneDirty(lobbyScene);
                EditorSceneManager.SaveScene(lobbyScene);
                Debug.Log($"[고기축제] LobbyScene 의 {ParentName} 아래에 위젯을 심었다. 폰트: {font.name}");
            }
            finally
            {
                if (!wasAlreadyLoaded && lobbyScene.IsValid() && lobbyScene.isLoaded)
                    EditorSceneManager.CloseScene(lobbyScene, true);
            }
        }

        private static UIMeatFestivalLobbyButton FindInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = roots[i].GetComponentInChildren<UIMeatFestivalLobbyButton>(true);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindParent(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j].name == ParentName)
                        return all[j];
                }
            }

            return null;
        }

        /// <summary>
        /// 한글이 들어 있는 TMP 폰트를 고른다. 이름이 아니라 <b>실제 글리프 보유</b>로 고르고,
        /// 경로로 정렬해 같은 도구를 두 번 돌려도 같은 폰트가 뽑히게 한다
        /// (<c>IdleRewardPrefabBaker</c> 와 같은 방식).
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
