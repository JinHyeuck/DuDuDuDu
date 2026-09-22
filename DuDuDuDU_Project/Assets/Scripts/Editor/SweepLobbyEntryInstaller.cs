using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using OJ.Stage;

namespace OJ.EditorTools
{
    /// <summary>
    /// 로비의 소탕 버튼에 <see cref="UISweepLobbyButton"/> 을 얹고 배선한다.
    ///
    /// <b>버튼을 새로 만들지 않는다.</b> <c>Canvas/Content/Stage/Sweep</c> 은 이미 씬에 있다 —
    /// 없는 것은 컴포넌트와 <c>onClick</c> 뿐이었다. 그래서 이 도구는 프리팹을 굽지 않고
    /// 있는 오브젝트에 얹기만 한다. 아트가 손으로 잡아 둔 위치·스프라이트를 건드리지 않으려는 것이다.
    ///
    /// <b>메뉴로만 돈다.</b> 씬을 건드리는 도구가 <c>[InitializeOnLoad]</c> 로 돌면 에디터를
    /// 켤 때마다 LobbyScene 을 덮어써서 손으로 한 작업이 조용히 사라진다
    /// (<c>TowerLobbyEntryInstaller</c> 주석, MIGRATION_BASELINE 1.1).
    ///
    /// <b>두 번 눌러도 안전하다.</b> 이미 붙어 있으면 비어 있는 참조만 채우고 끝낸다.
    /// </summary>
    public static class SweepLobbyEntryInstaller
    {
        private const string LobbyScenePath = "Assets/Scenes/LobbyScene.unity";
        private const string SweepObjectName = "Sweep";
        private const string SweepParentName = "Stage";

        [MenuItem("OJ/개발/소탕/로비 버튼 배선")]
        private static void Install()
        {
            Scene lobbyScene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool wasAlreadyLoaded = lobbyScene.IsValid() && lobbyScene.isLoaded;
            if (!wasAlreadyLoaded)
                lobbyScene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);

            try
            {
                GameObject sweep = FindSweepObject(lobbyScene);
                if (sweep == null)
                {
                    Debug.LogError(
                        $"[소탕] LobbyScene 에서 {SweepParentName}/{SweepObjectName} 을 못 찾았다. " +
                        "이름이 바뀌었는지 확인할 것.");
                    return;
                }

                if (Wire(sweep))
                    EditorSceneManager.MarkSceneDirty(lobbyScene);

                if (lobbyScene.isDirty)
                    EditorSceneManager.SaveScene(lobbyScene);
            }
            finally
            {
                if (!wasAlreadyLoaded && lobbyScene.IsValid() && lobbyScene.isLoaded)
                    EditorSceneManager.CloseScene(lobbyScene, true);
            }
        }

        /// <summary>
        /// 컴포넌트를 얹고 비어 있는 참조를 채운다. 무언가 바꿨으면 true.
        ///
        /// <b>이미 채워진 참조는 덮지 않는다.</b> 손으로 다른 칸을 물려 뒀을 수 있고,
        /// 그것을 이 도구가 매번 되돌리면 씬 작업이 도구와 싸우게 된다.
        /// </summary>
        private static bool Wire(GameObject sweep)
        {
            bool changed = false;

            var entry = sweep.GetComponent<UISweepLobbyButton>();
            if (entry == null)
            {
                entry = Undo.AddComponent<UISweepLobbyButton>(sweep);
                Debug.Log("[소탕] UISweepLobbyButton 을 얹었다.");
                changed = true;
            }
            else
            {
                Debug.Log("[소탕] UISweepLobbyButton 이 이미 있다. 빈 참조만 채운다.");
            }

            var serialized = new SerializedObject(entry);

            SerializedProperty buttonProperty = serialized.FindProperty("button");
            if (buttonProperty != null && buttonProperty.objectReferenceValue == null)
            {
                var button = sweep.GetComponent<Button>();
                if (button == null)
                {
                    Debug.LogError("[소탕] Sweep 오브젝트에 Button 이 없다. 배선할 수 없다.");
                }
                else
                {
                    buttonProperty.objectReferenceValue = button;
                    Debug.Log("[소탕] button 참조를 채웠다.");
                    changed = true;
                }
            }

            SerializedProperty stateTextProperty = serialized.FindProperty("stateText");
            if (stateTextProperty != null && stateTextProperty.objectReferenceValue == null)
            {
                TMP_Text stateText = FindStateText(sweep);
                if (stateText == null)
                {
                    Debug.LogWarning(
                        "[소탕] 상태를 적을 TMP_Text 를 못 찾았다. 칸 없이도 동작하므로 그대로 둔다 — " +
                        "필요하면 인스펙터에서 물릴 것.");
                }
                else
                {
                    stateTextProperty.objectReferenceValue = stateText;
                    Debug.Log($"[소탕] stateText 에 '{stateText.gameObject.name}' 를 물렸다.");
                    changed = true;
                }
            }

            if (changed)
                serialized.ApplyModifiedProperties();

            return changed;
        }

        /// <summary>
        /// 상태를 적을 칸. <b>"Sweep" 라벨은 건너뛴다.</b>
        ///
        /// 이 버튼 밑에는 TMP_Text 가 둘 있는데 하나는 "Sweep" 이라고 적힌 제목이다.
        /// 그것을 물리면 버튼 이름이 "3 스테이지" 로 덮여 버린다.
        /// </summary>
        private static TMP_Text FindStateText(GameObject sweep)
        {
            TMP_Text[] texts = sweep.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string text = texts[i].text;
                if (string.IsNullOrEmpty(text) ||
                    !text.Trim().Equals(SweepObjectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return texts[i];
                }
            }

            return null;
        }

        private static GameObject FindSweepObject(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < all.Length; j++)
                {
                    Transform candidate = all[j];
                    if (candidate.name != SweepObjectName)
                        continue;

                    // 같은 이름이 다른 데 또 있을 수 있으므로 부모까지 본다.
                    if (candidate.parent != null && candidate.parent.name == SweepParentName)
                        return candidate.gameObject;
                }
            }

            return null;
        }
    }
}
