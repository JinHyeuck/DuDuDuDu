using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using OJ.Dice;
using OJ.Equipment;
using OJ.Lobby;
using OJ.Relic;
using OJ.UI;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// 씬에 상주하는 팝업 인스턴스를 걷어낸다. (MIGRATION_BASELINE 10.4)
    ///
    /// <b>왜 지우나.</b> 팝업은 이제 <c>UIService</c> 가 카탈로그에서 만들어 띄운다.
    /// 씬 인스턴스가 남아 있으면 <b>같은 창이 둘</b>이 된다. 대부분은 꺼진 채로 있어서
    /// 눈에 안 띄지만, 스스로 뜨는 것이 있었다 — 조합 진행도 창(<c>UIDiceCraftProgressDialog</c>)이
    /// <c>Update</c> 에서 <c>inGameState</c> 를 보고 자기가 <c>Enter</c> 했고, 그래서 같은 창이
    /// 진짜로 두 개 떴다. 그 창은 진화 개편에서 조합식과 함께 사라졌지만, <b>이 도구가
    /// 필요한 이유는 그대로다</b> — 스스로 뜨는 창이 지금 없다는 것과 앞으로도 없다는 것은
    /// 다른 말이고, 씬에 남은 팝업 인스턴스는 어차피 카탈로그가 만든 것과 둘이 된다.
    ///
    /// <b>페이지도 지운다. 다만 지우기 전에 자리를 기록한다.</b> 로비 탭 내용물
    /// (장비·주사위·유물)은 팝업 루트가 아니라 <c>Content</c> 영역 안에 들어가야 하는데,
    /// 그 자리를 코드가 알 방법이 없다. 그래서 지우기 직전에 <b>부모를
    /// <c>LobbyLayoutController.pageRoot</c> 에 넣어 둔다.</b>
    /// 이 순서가 뒤바뀌면 페이지가 어디에 붙어야 하는지 영영 알 수 없게 된다.
    /// </summary>
    public static class ScenePopupCleaner
    {
        /// <summary>
        /// 팝업이 아닌 것들. <b>이름이 아니라 역할로 고른 목록이다.</b>
        /// 계층 위치가 <c>Canvas/LobbyLayoutController/Content</c> 안이라는 것이 그 근거다.
        ///
        /// 이들도 지우지만, 지우기 전에 부모를 <c>pageRoot</c> 로 기록한다.
        /// </summary>
        private static readonly HashSet<string> Pages = new HashSet<string>(StringComparer.Ordinal)
        {
            "UIRelicDialog",
            "UIDiceGrowthPage",
            "UIEquipmentPage",
        };

        // 되돌리는 수단은 git 이다. 미리보기 단계를 따로 두지 않고 한 번에 하되,
        // 무엇을 지웠는지 경로까지 로그로 남긴다.
        [MenuItem("OJ/개발/씬 팝업 인스턴스 제거")]
        private static void Apply() => Run(apply: true);

        private static void Run(bool apply)
        {
            var sb = new StringBuilder();
            sb.AppendLine(apply ? "[팝업정리] 제거" : "[팝업정리] 미리보기 — 아무것도 지우지 않는다");

            string activePath = SceneManager.GetActiveScene().path;
            int total = 0;

            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine("  " + scene.name);

                // 먼저 전부 모은 뒤에 지운다. 순회 중에 파괴하면 컬렉션이 흔들린다.
                var targets = new List<DialogBase>();
                Transform pageParent = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (DialogBase dialog in root.GetComponentsInChildren<DialogBase>(includeInactive: true))
                    {
                        if (Pages.Contains(dialog.GetType().Name) && pageParent == null)
                            pageParent = dialog.transform.parent;

                        targets.Add(dialog);
                    }
                }

                // 페이지가 있던 자리를 컨트롤러에 넣어 둔다. 지운 뒤에는 알 수 없다.
                if (pageParent != null)
                    sb.AppendLine("    페이지 자리: " + Path(pageParent));

                if (apply && pageParent != null)
                    WirePageRoot(scene, pageParent, sb);

                foreach (DialogBase dialog in targets)
                {
                    if (dialog == null)
                        continue;

                    sb.AppendLine("    " + (apply ? "제거 " : "제거 예정 ") + dialog.GetType().Name +
                                  "   " + Path(dialog.transform));

                    if (apply)
                        UnityEngine.Object.DestroyImmediate(dialog.gameObject);
                }

                if (apply && targets.Count > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sb.AppendLine("    (저장함)");
                }

                total += targets.Count;
            }

            // 작업 중이던 씬으로 되돌린다. 도구가 열어 둔 씬을 남겨 두면
            // 다음에 플레이했을 때 엉뚱한 씬이 돈다.
            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

            sb.AppendLine();
            sb.AppendLine("  " + (apply ? "지운 것 " : "지울 것 ") + total + "개");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// <c>LobbyLayoutController.pageRoot</c> 에 페이지가 있던 부모를 넣는다.
        ///
        /// 이 배선이 없으면 페이지가 <c>null</c> 부모로 만들어져 팝업 루트에 붙고,
        /// 로비 화면을 통째로 덮는다. <b>지우기 전에 해야 한다.</b>
        /// </summary>
        private static void WirePageRoot(Scene scene, Transform pageParent, StringBuilder sb)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var controller = root.GetComponentInChildren<LobbyLayoutController>(true);
                if (controller == null)
                    continue;

                var so = new SerializedObject(controller);
                SerializedProperty prop = so.FindProperty("pageRoot");
                if (prop == null)
                {
                    sb.AppendLine("    !! LobbyLayoutController 에 pageRoot 필드가 없다.");
                    return;
                }

                prop.objectReferenceValue = pageParent as RectTransform;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
                sb.AppendLine("    배선 LobbyLayoutController.pageRoot <- " + Path(pageParent));
                return;
            }
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
                sb.Insert(0, p.name + "/");

            return sb.ToString();
        }
    }
}
