#if UNITY_EDITOR || DEV_DEFINE
using System;
using OJ.Core;
using UnityEngine;
using OJ.Hunting;

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
    /// <b>지금은 PlayerPrefs 와 파일 양쪽을 지운다.</b> 7단계는 스키마까지만이라 아직 파일에
    /// 쓰는 코드가 없다(실배선은 8.7). 그래도 파일까지 지워 두는 것은, 배선이 붙는 순간
    /// 이 치트를 고쳐야 한다는 것을 잊게 되면 <b>지운 줄 알았는데 파일이 남아</b> 옛 진행도가
    /// 되살아나기 때문이다. 그때 원인을 찾기가 아주 어렵다.
    ///
    /// 에디터와 DEV_DEFINE 빌드에만 존재한다. 릴리스 빌드에는 컴파일되지 않는다.
    /// </summary>
    public static class SaveResetCheat
    {
        /// <summary>
        /// 저장된 것을 전부 지운다. <b>지운 뒤 앱을 다시 시작해야 한다</b> —
        /// 매니저들이 메모리에 들고 있는 값은 그대로라, 지금 상태로 저장하면 도로 쓰인다.
        /// </summary>
        public static void WipeAll()
        {
            SaveFile.Delete(SavePaths.SaveFilePath);

            // 키를 하나씩 지우지 않고 통째로 지운다. 접두어가 붙는 키(OJ.Point.*,
            // OJ.Bullet.Level.*)는 열거할 방법이 없어서 목록으로는 다 못 지운다.
            // 이 프로젝트는 PlayerPrefs 를 세이브 말고 다른 용도로 쓰지 않는다(전수 확인).
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            Debug.LogWarning(
                "[Dev] 세이브를 전부 지웠다. 파일: " + SavePaths.SaveFilePath + Environment.NewLine +
                "메모리에 남은 값이 다시 저장되지 않도록 지금 바로 앱(플레이)을 재시작할 것.");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("OJ/개발/세이브 전부 지우기")]
        private static void WipeAllMenu()
        {
            if (!UnityEditor.EditorUtility.DisplayDialog(
                    "세이브 초기화",
                    "저장된 진행도를 전부 지운다. 되돌릴 수 없다.\n\n경로: " + SavePaths.SaveFilePath,
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
