using System;
using System.IO;
using System.Text;

namespace OJ.Core
{
    /// <summary>
    /// 세이브 파일 읽기·쓰기. (MIGRATION_BASELINE 7.4)
    ///
    /// <b>왜 그냥 <c>File.WriteAllText</c> 가 아닌가.</b> 그건 파일을 <i>먼저 비우고</i> 쓴다.
    /// 그 사이에 앱이 죽으면(모바일에서는 OS 가 백그라운드 앱을 언제든 죽인다) 0 바이트 파일이
    /// 남는다. 세이브가 통째로 날아가는 가장 흔한 경로다.
    ///
    /// <b>여기서 하는 것.</b>
    /// <list type="number">
    /// <item><c>.writing</c> 에 전부 쓴다. 본 파일은 아직 멀쩡하다.</item>
    /// <item><c>Flush(true)</c> 로 디스크까지 밀어 넣는다. 이걸 빼면 OS 캐시에만 있는 상태로
    /// 이름만 바뀌어, 전원이 끊기면 <b>이름은 새것이고 내용은 빈</b> 파일이 남는다.</item>
    /// <item><c>File.Replace</c> 로 갈아 끼우면서 이전 것을 <c>.bak</c> 으로 남긴다.
    /// 이 교체는 원자적이라 중간 상태가 없다 — 옛 파일이거나 새 파일이거나 둘 중 하나다.</item>
    /// </list>
    ///
    /// <b>한계.</b> 디렉터리 엔트리 자체의 fsync 는 하지 않는다(.NET 에 API 가 없다).
    /// NTFS·ext4 는 이름 바꾸기를 저널에 남기므로 실질적으로 안전하지만 보장은 아니다.
    ///
    /// 이 클래스는 <c>System.IO</c> 만 쓴다. 그래서 에디터 없이 헤드리스로 전부 검증된다.
    /// </summary>
    public static class SaveFile
    {
        /// <summary>쓰는 중인 임시 파일. 이게 남아 있으면 지난번 쓰기가 중단된 것이다.</summary>
        public const string WritingSuffix = ".writing";

        /// <summary>직전 세이브. 본 파일이 깨졌을 때 여기서 살린다.</summary>
        public const string BackupSuffix = ".bak";

        // BOM 없는 UTF-8. BOM 이 붙으면 JSON 파서에 따라 첫 글자에서 걸린다.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string WritingPathOf(string path) => path + WritingSuffix;

        public static string BackupPathOf(string path) => path + BackupSuffix;

        /// <summary>
        /// 상태를 파일에 쓴다. 성공하면 <paramref name="path"/> 는 완전한 새 내용이고
        /// <c>.bak</c> 은 직전 내용이다. 도중에 죽으면 <paramref name="path"/> 는 손대지 않은
        /// 옛 내용 그대로다.
        /// </summary>
        public static void Save(string path, SaveState state)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("경로가 비어 있다.", nameof(path));

            WriteText(path, SaveSerializer.Serialize(state));
        }

        /// <summary>
        /// 이미 만들어 둔 텍스트를 원자적으로 쓴다. <see cref="Save"/> 의 본체이자,
        /// 깨진 내용을 일부러 심는 테스트가 쓰는 입구다.
        /// </summary>
        public static void WriteText(string path, string contents)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string writingPath = WritingPathOf(path);
            byte[] bytes = Utf8NoBom.GetBytes(contents);

            // FileShare.None — 다른 프로세스가 반쯤 쓰인 것을 읽지 못하게 한다.
            using (var stream = new FileStream(writingPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);

                // true 여야 한다. 인자 없는 Flush() 는 .NET 버퍼만 비우고 OS 캐시에 남긴다.
                stream.Flush(true);
            }

            if (!File.Exists(path))
            {
                // 첫 저장이다. Replace 는 대상이 있어야 하므로 그냥 옮긴다.
                File.Move(writingPath, path);
                return;
            }

            try
            {
                // 세 번째 인자가 백업 경로다. 교체 직전의 본 파일이 여기로 간다.
                // ignoreMetadataErrors: true — 다른 볼륨이나 네트워크 드라이브에서 ACL 복사가
                // 실패하면 교체 자체가 취소되는데, 세이브 파일에는 지킬 메타데이터가 없다.
                File.Replace(writingPath, path, BackupPathOf(path), true);
            }
            catch (PlatformNotSupportedException)
            {
                // Windows 에디터에서는 여기로 오지 않는다. 안드로이드·iOS 실기에서만 걸릴 수 있는
                // 경로라 <b>내가 확인할 수 없는 곳</b>이다. 그래서 던지지 않고 물러선다 —
                // 여기서 예외가 올라가면 실기에서만 저장이 통째로 안 되고, 그건 출시를 막는다.
                ReplaceByMove(path);
            }
            catch (IOException)
            {
                // 같은 이유. File.Replace 는 두 파일이 다른 볼륨에 있으면 IOException 을 낸다.
                ReplaceByMove(path);
            }
        }

        /// <summary>
        /// <c>File.Replace</c> 를 못 쓸 때의 대체 경로. <b>원자적이지 않다.</b>
        ///
        /// 그래도 안전한 이유: 중간에 죽으면 본 파일이 없고 <c>.bak</c> 만 있는 상태가 되는데,
        /// 그건 <see cref="Load"/> 가 이미 다루는 상태다(백업으로 읽고 <see cref="SaveSource.Backup"/>
        /// 을 돌려준다). 즉 <b>가장 나쁜 결과가 "직전 저장분으로 되돌아감"</b>이지 소실이 아니다.
        ///
        /// 순서를 바꾸면(먼저 본 파일을 지우면) 그 성질이 깨진다. 지금 순서를 유지할 것.
        /// </summary>
        internal static void ReplaceByMove(string path)
        {
            string backupPath = BackupPathOf(path);
            DeleteIfExists(backupPath);

            // 1) 본 파일 -> 백업. 이 시점에 좋은 데이터는 백업에 있다.
            File.Move(path, backupPath);

            // 2) 새 내용 -> 본 파일.
            File.Move(WritingPathOf(path), path);
        }

        /// <summary>
        /// 세이브를 읽는다. 본 파일이 깨져 있으면 <c>.bak</c> 으로 물러선다.
        ///
        /// <b>절대 예외를 밖으로 던지지 않는다.</b> 대신 <see cref="SaveLoadResult.Source"/> 로
        /// 무슨 일이 있었는지 알린다. 호출부가 판단해야 하는 갈림길이기 때문이다 —
        /// <see cref="SaveSource.None"/> 은 새 게임을 시작해도 되지만
        /// <see cref="SaveSource.Unreadable"/> 은 <b>덮어쓰면 안 된다.</b>
        /// </summary>
        public static SaveLoadResult Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("경로가 비어 있다.", nameof(path));

            bool primaryExists = File.Exists(path);
            bool backupExists = File.Exists(BackupPathOf(path));

            if (!primaryExists && !backupExists)
                return new SaveLoadResult { State = null, Source = SaveSource.None };

            string primaryError = null;
            if (primaryExists)
            {
                SaveLoadResult ok = TryReadOne(path, SaveSource.Primary, out primaryError);
                if (ok != null)
                    return ok;
            }

            if (backupExists)
            {
                string backupError;
                SaveLoadResult ok = TryReadOne(BackupPathOf(path), SaveSource.Backup, out backupError);
                if (ok != null)
                {
                    ok.Message = primaryExists
                        ? "세이브 본 파일을 못 읽어 백업으로 되돌렸다. 마지막 저장분은 잃었다. 사유: " + primaryError
                        : "세이브 본 파일이 없어 백업에서 읽었다.";
                    return ok;
                }

                return new SaveLoadResult
                {
                    State = null,
                    Source = SaveSource.Unreadable,
                    Message = "본 파일과 백업 둘 다 못 읽었다. 본 파일: " +
                              (primaryError ?? "없음") + " / 백업: " + backupError,
                };
            }

            return new SaveLoadResult
            {
                State = null,
                Source = SaveSource.Unreadable,
                Message = "세이브를 못 읽었고 백업도 없다. 사유: " + primaryError,
            };
        }

        /// <summary>읽혔으면 결과를, 아니면 null 과 사유를 준다.</summary>
        private static SaveLoadResult TryReadOne(string path, SaveSource source, out string error)
        {
            string json;
            try
            {
                json = File.ReadAllText(path, Utf8NoBom);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // 파일 자체를 못 여는 경우다. 형식 문제와 구별해서 남긴다 —
                // 이쪽은 파일을 지운다고 해결되지 않는다.
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }

            SaveState state;
            try
            {
                state = SaveSerializer.Deserialize(json);
            }
            catch (SaveFormatException ex)
            {
                error = ex.Message;
                return null;
            }

            string upgradeError;
            if (!SaveStateMigration.TryUpgrade(state, out upgradeError))
            {
                error = upgradeError;
                return null;
            }

            error = null;
            return new SaveLoadResult { State = state, Source = source };
        }

        /// <summary>
        /// 세이브를 전부 지운다. 개발용 초기화(7.6)가 쓴다.
        /// <c>.writing</c> 찌꺼기까지 같이 치운다.
        /// </summary>
        public static void Delete(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("경로가 비어 있다.", nameof(path));

            DeleteIfExists(path);
            DeleteIfExists(BackupPathOf(path));
            DeleteIfExists(WritingPathOf(path));
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
