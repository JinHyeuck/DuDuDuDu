using OJ.Core;

namespace OJ.Save
{
    /// <summary>
    /// 통합 세이브의 한 조각을 소유하는 것. (MIGRATION_BASELINE 8.7)
    ///
    /// <b>왜 매퍼가 아니라 인터페이스인가.</b> 바깥에 <c>SaveMapper</c> 를 두면 그것이
    /// 매니저들의 내부 상태를 알아야 한다 — 필드를 열거나 접근자를 새로 뚫어야 하고,
    /// 그러면 "저장 때문에 생긴 public"이 늘어나 다른 코드가 그걸 쓰기 시작한다.
    ///
    /// 각자 자기 몫을 읽고 쓰면 그 문제가 없다. 그리고 매니저를 새로 만든 사람이
    /// 인터페이스를 붙이지 않으면 <b>인스톨러에서 등록이 빠진 것이 눈에 보인다</b> —
    /// 저장이 조용히 누락되는 것보다 낫다.
    /// </summary>
    public interface ISaveStateOwner
    {
        /// <summary>
        /// 소유한 영구 상태를 <paramref name="state"/> 에 쓴다.
        ///
        /// <b>남의 몫은 건드리지 말 것.</b> 이 메서드는 파일 전체를 만드는 과정의 한 조각이고,
        /// 다른 조각을 덮으면 그쪽 진행도가 사라진다.
        /// </summary>
        void WriteTo(SaveState state);

        /// <summary>
        /// 영구 상태를 <paramref name="state"/> 에서 읽어 온다.
        ///
        /// <b>기존 로드 경로와 같은 검증·클램프를 해야 한다.</b> 손상된 세이브가 그대로
        /// 메모리에 들어오면 다음 저장에서 그 값이 굳는다.
        /// </summary>
        void ReadFrom(SaveState state);
    }
}
