using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OJ.EditorTools
{
    /// <summary>
    /// 씬의 <see cref="CanvasScaler"/> 를 하나의 규격으로 맞춘다. (MIGRATION_BASELINE 9.2)
    ///
    /// <b>규격은 1080x1920 / ScaleWithScreenSize / 너비 기준(match 0)</b> 이다.
    /// 세로 모바일 게임이고, 로비·타이틀·배틀 메인 캔버스가 이미 그 값이라 그것을 정본으로 삼는다.
    ///
    /// <b>왜 도구인가.</b> 씬 파일을 직접 고치는 것은 이 리포의 금지 사항이다 —
    /// YAML 을 손으로 만지면 fileID 나 프리팹 오버라이드가 조용히 깨지고, 그 손상은
    /// 한참 뒤에 다른 증상으로 나타난다. <c>AssetDatabase</c> 를 거치면 그 위험이 없다.
    ///
    /// <b>왜 미리보기와 적용을 나눴나.</b> 씬 저장은 되돌리기 어렵다. 무엇이 바뀌는지
    /// 먼저 눈으로 보고 나서 적용하는 편이 안전하다.
    /// </summary>
    public static class CanvasStandardizer
    {
        private static readonly Vector2 Reference = new Vector2(1080f, 1920f);
        private const CanvasScaler.ScaleMode Mode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        private const CanvasScaler.ScreenMatchMode MatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        private const float Match = 0f;

        // 미리보기와 적용을 나누지 않는다. <b>되돌리는 수단은 git 이다.</b>
        // 씬·프리팹·에셋이 전부 추적되므로 결과가 틀리면 git checkout 한 번이면 된다.
        // 두 단계로 나누면 사람 클릭만 두 배가 되고 안전은 별로 늘지 않는다.
        // 대신 무엇을 바꿨는지 전·후 값을 빠짐없이 로그로 남긴다.
        [MenuItem("OJ/개발/캔버스 규격 1080x1920 으로 통일")]
        private static void Apply() => Run(apply: true);

        private static void Run(bool apply)
        {
            var sb = new StringBuilder();
            sb.AppendLine(apply ? "[캔버스] 규격 통일 적용" : "[캔버스] 규격 통일 미리보기 — 아무것도 바꾸지 않는다");
            sb.AppendLine("  규격: " + Reference.x + "x" + Reference.y + " / " + Mode + " / match " + Match);

            int changed = 0;
            string activePath = SceneManager.GetActiveScene().path;

            foreach (string path in BuildScenePaths())
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine();
                sb.AppendLine("  " + scene.name);

                int changedHere = 0;
                foreach (CanvasScaler scaler in FindScalers(scene))
                {
                    string before = Describe(scaler);
                    if (IsStandard(scaler))
                    {
                        sb.AppendLine("    OK " + Path(scaler.transform) + "   " + before);
                        continue;
                    }

                    changedHere++;
                    sb.AppendLine("    -> " + Path(scaler.transform));
                    sb.AppendLine("       전: " + before);

                    if (apply)
                    {
                        Undo.RecordObject(scaler, "캔버스 규격 통일");
                        MakeStandard(scaler);
                        EditorUtility.SetDirty(scaler);
                        sb.AppendLine("       후: " + Describe(scaler));
                    }
                    else
                    {
                        sb.AppendLine("       후: " + Reference.x + "x" + Reference.y + " / " + Mode +
                                      " / " + MatchMode + " / match " + Match);
                    }
                }

                if (apply && changedHere > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sb.AppendLine("    (저장함)");
                }

                changed += changedHere;
            }

            // 열어 둔 씬을 되돌린다. 도구가 작업 중이던 씬을 바꿔 놓으면
            // 다음에 플레이 버튼을 눌렀을 때 엉뚱한 씬이 돈다.
            if (!string.IsNullOrEmpty(activePath))
                EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single);

            sb.AppendLine();
            sb.AppendLine(changed == 0
                ? "  바꿀 것이 없다. 전부 규격에 맞다."
                : "  " + (apply ? "바꾼 것 " : "바꿔야 할 것 ") + changed + "개");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 빌드 세팅에 들어 있고 켜져 있는 씬만 본다.
        ///
        /// 빌드에 없는 씬(예: <c>UITutorial</c>)은 게임에서 열리지 않으므로 규격을 맞출
        /// 이유가 없다. 오히려 손대면 <b>왜 바뀌었는지 모를 diff</b> 만 남는다.
        /// </summary>
        private static List<string> BuildScenePaths()
        {
            var paths = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                    paths.Add(scene.path);
            }

            return paths;
        }

        private static List<CanvasScaler> FindScalers(Scene scene)
        {
            var found = new List<CanvasScaler>();
            foreach (GameObject root in scene.GetRootGameObjects())
                found.AddRange(root.GetComponentsInChildren<CanvasScaler>(includeInactive: true));

            return found;
        }

        private static bool IsStandard(CanvasScaler s)
        {
            return s.uiScaleMode == Mode
                   && s.screenMatchMode == MatchMode
                   && Mathf.Approximately(s.referenceResolution.x, Reference.x)
                   && Mathf.Approximately(s.referenceResolution.y, Reference.y)
                   && Mathf.Approximately(s.matchWidthOrHeight, Match);
        }

        private static void MakeStandard(CanvasScaler s)
        {
            s.uiScaleMode = Mode;
            s.screenMatchMode = MatchMode;
            s.referenceResolution = Reference;
            s.matchWidthOrHeight = Match;
        }

        private static string Describe(CanvasScaler s)
        {
            return string.Format("{0} / ref {1}x{2} / {3} / match {4}",
                s.uiScaleMode, s.referenceResolution.x, s.referenceResolution.y,
                s.screenMatchMode, s.matchWidthOrHeight);
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
