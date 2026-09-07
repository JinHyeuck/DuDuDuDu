using UnityEngine;
using OJ.Utils;

namespace OJ.Rewind
{
    /// <summary>
    /// <see cref="WaveRewindSettings"/> 로 가는 유일한 창구.
    /// <c>BountyDatabaseProvider</c>·<c>StageDatabaseProvider</c> 와 같은 형태다 —
    /// 이 프로젝트에서 SO 를 얻는 방법이 넷이 되면 어느 것이 정본인지 알 수 없게 된다.
    ///
    /// <b>폴백을 캐시에 넣지 않는다.</b> 넣으면 나중에 <c>StaticResource</c> 가 살아나도
    /// 영영 코드 기본값을 쓴다. 다른 두 Provider 의 주석이 같은 함정을 적어 두었다.
    ///
    /// <b>여기서는 에셋이 없어도 울지 않는다.</b> 다른 Provider 들은 에셋이 없으면
    /// <c>LogError</c> 를 내는데, 그것들은 없으면 게임 내용이 비는 데이터베이스다.
    /// 이쪽은 <b>밸런스 손잡이 다섯 개</b>고 코드 기본값이 곧 출시 값이라, 에셋이 없는
    /// 상태가 정상 동작이다. 없다고 매번 빨간 줄을 내면 진짜 사고가 그 밑에 묻힌다.
    /// </summary>
    public static class WaveRewindSettingsProvider
    {
        private static WaveRewindSettings settings;
        private static WaveRewindSettings fallbackSettings;

        public static WaveRewindSettings Settings
        {
            get
            {
                if (settings != null)
                    return settings;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.WaveRewindSettings != null)
                {
                    settings = resource.WaveRewindSettings;
                    return settings;
                }

                if (fallbackSettings == null)
                {
                    fallbackSettings = ScriptableObject.CreateInstance<WaveRewindSettings>();
                    fallbackSettings.PopulateDefaults();
                }

                return fallbackSettings;
            }
        }

        /// <summary>
        /// 지금 돌려주는 것이 <b>진짜 에셋</b>인가. 진단(F9)과 에디터 도구가 쓴다.
        /// getter 를 먼저 밟아 1단 시도를 강제한다 — 안 그러면 아무도 안 건드린 상태에서
        /// 물었을 때 언제나 "폴백" 이라고 답하는, 호출 순서에 좌우되는 판정이 된다.
        /// </summary>
        public static bool HasRealSettings
        {
            get
            {
                _ = Settings;
                return settings != null;
            }
        }
    }
}
