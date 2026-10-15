using UnityEngine;

namespace OJ.Dev
{
    /// <summary>
    /// 테스트 모드 스위치 셋 — 연속 웨이브 · 자리 해금 · 공속 UP.
    ///
    /// <b>셋은 서로 독립이다.</b> 하나만 켜도, 둘만 켜도, 셋 다 켜도 각자의 규칙만 걸린다.
    /// 읽는 쪽이 서로의 값을 보지 않는다.
    ///
    /// <b>PlayerPrefs 에 남긴다.</b> 에디터에서 도메인 리로드가 나도(스크립트 수정) static 값은
    /// 날아가는데, 그때마다 로비에서 다시 켜는 것은 테스트를 막는 일이다.
    /// 세이브(<c>SaveService</c>)와 키가 섞이지 않게 <c>Dev.</c> 접두를 쓴다.
    ///
    /// <b>릴리스 빌드에서는 전부 false 로 고정된다</b>(<c>UNITY_EDITOR || DEV_DEFINE</c> 밖).
    /// 읽는 코드가 그대로 남아도 켜질 길이 없다.
    /// </summary>
    public static class TestModeFlags
    {
#if UNITY_EDITOR || DEV_DEFINE
        private const string ContinuousWaveKey = "Dev.TestMode.ContinuousWave";
        private const string SlotUnlockKey = "Dev.TestMode.SlotUnlock";
        private const string FireRateUpKey = "Dev.TestMode.FireRateUp";

        /// <summary>웨이브가 끝나면 곧바로 다음 웨이브, 웨이브 중에도 소환·머지·진화·강화.</summary>
        public static bool ContinuousWave
        {
            get => Read(ContinuousWaveKey);
            set => Write(ContinuousWaveKey, value);
        }

        /// <summary>첫 줄 7칸만 열려 있고 나머지는 SP 로 해금한다.</summary>
        public static bool SlotUnlock
        {
            get => Read(SlotUnlockKey);
            set => Write(SlotUnlockKey, value);
        }

        /// <summary>삭제 버튼 자리에 공속 UP 버튼이 뜬다.</summary>
        public static bool FireRateUp
        {
            get => Read(FireRateUpKey);
            set => Write(FireRateUpKey, value);
        }

        private static bool Read(string key)
        {
            try { return PlayerPrefs.GetInt(key, 0) != 0; }
            catch { return false; }
        }

        private static void Write(string key, bool value)
        {
            try { PlayerPrefs.SetInt(key, value ? 1 : 0); }
            catch { }
        }
#else
        public static bool ContinuousWave { get => false; set { } }
        public static bool SlotUnlock { get => false; set { } }
        public static bool FireRateUp { get => false; set { } }
#endif
    }
}
