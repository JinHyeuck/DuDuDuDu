#if UNITY_EDITOR || DEV_DEFINE
using System;
using OJ.Core;
using UnityEngine;
using OJ.DI;
using OJ.Hunting;
using OJ.SceneFlow;

namespace OJ.Save
{
    /// <summary>
    /// 개발용 세이브 초기화. (MIGRATION_BASELINE 7.6)
    ///
    /// <b>왜 필요한가.</b> 지금 진행도를 지우려면 PlayerPrefs 키 10개를 알고 있어야 한다
    /// (<c>OJ.Point.*</c> 처럼 접두어가 붙는 것도 있어 이름을 다 못 적는다). 실제로 "처음부터"
    /// 상태를 재현하려다 일부만 지워, <b>세이브가 반쯤 남은 적 없는 상태</b>로 테스트하게 되는
    /// 일이 생긴다. 그건 실제 유저에게 절대 안 생기는 상태라 거기서 나온 버그는 시간 낭비다.
    ///
    /// <b>지우기만 하면 안 지워진다.</b> 매니저들이 지우기 전 값을 메모리에 들고 있어서, 다음 저장
    /// (거래·씬 이동·앱 일시정지)이 그 값을 파일에 도로 쓴다. 실제로 "세이브 전부 지우기" 를
    /// 눌러도 초기화가 안 되는 것이 그것이었다. 그래서 지운 뒤에는
    /// <list type="number">
    /// <item>이 세션의 저장을 막고(<see cref="SaveService.BlockWritesUntilRestart"/>),</item>
    /// <item>앱을 다시 시작한다 — 에디터는 플레이를 껐다가 <b>타이틀 씬부터 다시 켜고</b>,
    ///       빌드는 앱을 종료한다(다시 켜면 첫 설치와 같다).</item>
    /// </list>
    /// 메모리를 기본값으로 되돌리는 길은 택하지 않았다. 매니저 생성자가 하는 초기 지급과
    /// 정확히 같은 상태를 다시 만들어야 하는데, 하나라도 어긋나면 "실제로는 없는 상태" 가 된다.
    /// 새로 시작하는 것이 첫 설치를 재현하는 유일하게 확실한 방법이다.
    ///
    /// 에디터와 DEV_DEFINE 빌드에만 존재한다. 릴리스 빌드에는 컴파일되지 않는다.
    /// </summary>
    public static class SaveResetCheat
    {
        /// <summary>저장된 것을 전부 지우고 앱을 다시 시작한다. 플레이 중이 아니면 지우기만 한다.</summary>
        public static void WipeAll()
        {
            // 지우기 <b>전에</b> 막는다. 지우고 나서 막으면 그 사이 저장(예: 일시정지 플러시)이
            // 빈 자리에 옛 값을 다시 쓸 수 있다.
            if (Application.isPlaying)
                GameContainer.SaveService?.BlockWritesUntilRestart();

            SaveFile.Delete(SavePaths.SaveFilePath);

            // 키를 하나씩 지우지 않고 통째로 지운다. 접두어가 붙는 키(OJ.Point.*,
            // OJ.Bullet.Level.*)는 열거할 방법이 없어서 목록으로는 다 못 지운다.
            // 이 프로젝트는 PlayerPrefs 를 세이브 말고 다른 용도로 쓰지 않는다(전수 확인).
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.LogWarning("[Dev] 세이브를 전부 지웠다. 파일: " + SavePaths.SaveFilePath);

            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            RestartPlayFromTitle();
#else
            // 기기에서는 같은 프로세스 안에서 처음부터 다시 시작할 방법이 없다(정적 상태·컨테이너).
            // 종료하고 다시 켜는 것이 첫 실행이다.
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        /// <summary>
        /// 플레이를 끄고, 에디터로 돌아오면 타이틀 씬을 열어 다시 플레이한다.
        /// 다시 켤 때 도메인이 새로 올라오므로 정적 상태까지 첫 실행과 같다.
        /// </summary>
        private static void RestartPlayFromTitle()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            UnityEditor.EditorApplication.isPlaying = false;
            Debug.LogWarning("[Dev] 플레이를 다시 시작한다(타이틀부터).");
        }

        private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change != UnityEditor.PlayModeStateChange.EnteredEditMode)
                return;

            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            string titlePath = FindTitleScenePath();
            if (!string.IsNullOrEmpty(titlePath)
                && UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(titlePath);
            }

            UnityEditor.EditorApplication.isPlaying = true;
        }

        private static string FindTitleScenePath()
        {
            string titleName = SceneCatalog.NameOf(SceneId.Title);
            foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(scene.path) == titleName)
                    return scene.path;
            }

            Debug.LogError("[Dev] 빌드 세팅에서 타이틀 씬(" + titleName + ")을 못 찾았다. 지금 씬으로 다시 켠다.");
            return null;
        }

        [UnityEditor.MenuItem("OJ/개발/세이브 전부 지우기")]
        private static void WipeAllMenu()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog(
                    "세이브 초기화",
                    "저장된 진행도를 전부 지운다. 되돌릴 수 없다.\n플레이 중이면 타이틀부터 다시 시작한다.\n\n경로: " + SavePaths.SaveFilePath,
                    "지운다", "취소"))
            {
                return;
            }

            WipeAll();
        }
#endif
    }
}
#endif
