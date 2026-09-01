using System.IO;
using UnityEngine;

namespace OJ.Save
{
    /// <summary>
    /// 세이브 파일이 있는 곳. (MIGRATION_BASELINE 7.4)
    ///
    /// <c>OJ.Core</c> 의 <c>SaveFile</c> 은 경로를 받기만 하고 어디인지는 모른다 —
    /// <c>Application.persistentDataPath</c> 가 엔진 API 라 거기서 못 쓰기 때문이고,
    /// 덕분에 저장 로직 전체가 에디터 없이 테스트된다. 경로를 아는 곳은 여기 하나다.
    ///
    /// <b><c>persistentDataPath</c> 인 이유.</b> Android 는 앱 전용 폴더라 백업 대상이고,
    /// iOS 는 <c>Library/Application Support</c> 로 가서 iCloud 백업에 들어간다.
    /// <c>dataPath</c>(설치 폴더)는 플랫폼에 따라 읽기 전용이라 쓰면 안 된다.
    /// </summary>
    public static class SavePaths
    {
        /// <summary>세이브 파일 이름. 확장자를 남긴 것은 사람이 열어 볼 수 있게 하기 위함이다.</summary>
        public const string FileName = "save.json";

        /// <summary>통합 세이브 파일의 전체 경로.</summary>
        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, FileName);
    }
}
