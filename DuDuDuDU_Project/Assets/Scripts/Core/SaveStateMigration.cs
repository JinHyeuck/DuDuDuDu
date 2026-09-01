using System;

namespace OJ.Core
{
    /// <summary>세이브를 읽은 결과가 어디서 왔는지.</summary>
    public enum SaveSource
    {
        /// <summary>세이브가 없다. 새 게임이다.</summary>
        None = 0,

        /// <summary>정상 파일에서 읽었다.</summary>
        Primary = 1,

        /// <summary>본 파일이 깨져 <c>.bak</c> 에서 읽었다. 마지막 저장분은 잃었다.</summary>
        Backup = 2,

        /// <summary>
        /// 본 파일도 백업도 못 읽었다.
        ///
        /// <b>이때 덮어쓰면 안 된다.</b> 디스크 오류·권한 문제·다운그레이드처럼 고칠 수 있는
        /// 원인일 수 있는데, 새 세이브로 덮으면 그 순간 복구 불가능해진다.
        /// </summary>
        Unreadable = 3,
    }

    /// <summary>세이브 로드 결과.</summary>
    public sealed class SaveLoadResult
    {
        /// <summary>읽어 낸 상태. <see cref="SaveSource.Unreadable"/> 이면 null 이다.</summary>
        public SaveState State { get; set; }

        public SaveSource Source { get; set; }

        /// <summary>사람이 읽을 사유. 정상 경로면 null.</summary>
        public string Message { get; set; }

        /// <summary>이 결과로 게임을 시작해도 되는가.</summary>
        public bool IsUsable => State != null;
    }

    /// <summary>
    /// 세이브 버전 정책. (MIGRATION_BASELINE 7.2)
    ///
    /// 지금은 버전이 1 하나뿐이라 올릴 것이 없다. 그래도 이 파일을 따로 두는 이유는
    /// <b>내려가는 쪽</b>이 이미 결정돼야 하기 때문이다 — 유저가 앱을 롤백하면 옛 빌드가
    /// 새 세이브를 읽게 된다. 그때 "모르는 필드는 무시"로 읽어 버리면 <b>그대로 저장하는 순간
    /// 새 빌드가 쓴 값이 전부 사라진다.</b> 그건 조용히 일어나고 되돌릴 수 없다.
    /// 그래서 미래 버전은 읽기를 거부한다.
    /// </summary>
    public static class SaveStateMigration
    {
        /// <summary>
        /// 읽어 낸 상태를 현재 버전으로 맞춘다.
        /// </summary>
        /// <returns>맞출 수 있으면 true. false 면 <paramref name="error"/> 에 사유가 들어간다.</returns>
        public static bool TryUpgrade(SaveState state, out string error)
        {
            if (state == null)
            {
                error = "상태가 null 이다.";
                return false;
            }

            if (state.Version > SaveState.CurrentVersion)
            {
                error = string.Format(
                    "세이브 버전 {0} 은 이 빌드(버전 {1})보다 새롭다. 읽지 않는다 — " +
                    "읽으면 모르는 필드가 지워진 채로 다시 저장된다.",
                    state.Version, SaveState.CurrentVersion);
                return false;
            }

            if (state.Version < 1)
            {
                error = "세이브 버전이 " + state.Version + " 이다. 1 이상이어야 한다.";
                return false;
            }

            // 버전 1 뿐이라 올릴 단계가 없다. 버전이 늘면 여기 if 를 순서대로 쌓는다
            // (1→2, 2→3 …). switch 가 아니라 연쇄여야 여러 버전을 건너뛴 세이브도 올라온다.

            state.Version = SaveState.CurrentVersion;
            error = null;
            return true;
        }
    }
}
