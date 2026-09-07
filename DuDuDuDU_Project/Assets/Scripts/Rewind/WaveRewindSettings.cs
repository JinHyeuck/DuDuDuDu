using UnityEngine;

namespace OJ.Rewind
{
    /// <summary>
    /// 웨이브 되돌리기의 밸런스 값. (AGENTS 확정 결정 3 — 수치는 SO 가 정본)
    ///
    /// <b>이 에셋이 기능의 제거 손잡이다.</b> 되돌리기는 "위기를 여러 번 겪게 해서
    /// 더 강해지고 싶게 만든다" 는 가설 위에 서 있는데, 그 가설은 <b>틀릴 수 있다</b> —
    /// 되돌리기를 많이 쓴 유저가 "충분히 했다" 는 포만감을 느껴 다음 판을 안 켜게 되면
    /// 기능이 정확히 반대로 작동한 것이다.
    ///
    /// 그때 코드를 지우지 않는다. <see cref="rewindEnabled"/> 를 끄면 버튼도 팝업도
    /// 통째로 사라지고, 다시 켜 보고 싶을 때 되살릴 것이 남는다. 판정 근거는
    /// <c>RunHistoryManager</c> 에 쌓이는 되돌리기 기록이다(3단계).
    ///
    /// <b><c>enabled</c> 라고 이름 짓지 않는다.</b> 그 이름은 <c>Behaviour</c> 의 것이라
    /// 읽는 사람이 "컴포넌트를 껐다" 로 오해한다. 여기서 끄는 것은 게임 기능이다.
    /// </summary>
    [CreateAssetMenu(
        menuName = "OJ/전투/웨이브 되돌리기 설정",
        fileName = "WaveRewindSettings")]
    public sealed class WaveRewindSettings : ScriptableObject
    {
        [Header("통째 스위치")]
        [Tooltip("끄면 되돌리기 버튼과 사망 시 제안이 전부 사라진다. 기능을 뺄 때 여기만 끈다.")]
        public bool rewindEnabled = true;

        [Header("판당 횟수")]
        [Tooltip("공짜로 쓸 수 있는 횟수. 0 이면 무료 경로가 없다.")]
        public int freeCountPerRun = 1;

        [Tooltip("리워드 광고를 보고 쓸 수 있는 횟수. 광고 서비스가 없으면 이 값과 무관하게 안 뜬다.")]
        public int adCountPerRun = 1;

        [Header("발동 경로")]
        [Tooltip("웨이브 중 아무 때나 누를 수 있는 버튼.")]
        public bool allowDuringWave = true;

        [Tooltip("벽이 0 이 되는 순간 뜨는 제안.")]
        public bool allowOnDeath = true;

        [Header("연출")]
        [Tooltip("몬스터가 소환 위치로 거슬러 올라가고 벽이 차오르는 되감기 연출. " +
                 "끄면 되돌리기가 즉시 끝난다 — 동작은 완전히 같다.")]
        public bool playRewindPresentation = true;

        [Tooltip("되감기 최대 길이(초). 웨이브가 길면 그만큼 더 빨리 감긴다. 0 이면 상한 없음.")]
        public float maxRewindSeconds = 2f;

        /// <summary>
        /// 에셋이 없을 때 쓰는 코드 기본값. 다른 Provider 들과 같은 규약이다.
        ///
        /// <b>필드 선언의 초기값과 같은 값을 다시 쓴다.</b> 새 인스턴스는 이미 그 값을
        /// 갖고 태어나지만, 여기서 한 번 더 못박아야 <c>CreateInstance</c> 로 만든 것과
        /// 에디터에서 만든 것이 같다는 사실이 코드에 남는다.
        /// </summary>
        public void PopulateDefaults()
        {
            rewindEnabled = true;
            freeCountPerRun = 1;
            adCountPerRun = 1;
            allowDuringWave = true;
            allowOnDeath = true;
            playRewindPresentation = true;
            maxRewindSeconds = 2f;
        }
    }
}
