using OJ.Core;
using OJ.Dice;
using OJ.Relic;

namespace OJ.Rewind
{
    /// <summary>
    /// 웨이브가 시작되기 <b>직전</b>의 판 상태 전부. 되돌리기가 이걸로 되감는다.
    ///
    /// <b>왜 매니저마다 흩어 두지 않고 한 덩이인가.</b> 되돌리기의 실패 모드는
    /// "일부만 되감긴 판" 이고, 그건 화면에서 고장으로 보이지 않는다 —
    /// SP 만 안 돌아왔다거나 유물 한 개가 안 살아났다거나 하는 식이라
    /// <b>유저는 자기가 손해 본 줄도 모른다.</b> 한 덩이로 두면 뜨는 곳과
    /// 되돌리는 곳이 한 화면에 보이고, 필드를 늘릴 때 짝을 빠뜨리기 어려워진다.
    ///
    /// <b>여기 없는 것은 일부러 없다.</b> 그 목록과 이유는
    /// <see cref="WaveRewindManager.Capture"/> 주석에 있다.
    /// </summary>
    public sealed class WaveRewindSnapshot
    {
        /// <summary>보드 한 칸. 주사위가 들고 있는 것 중 되살릴 값은 이 셋뿐이다.</summary>
        public struct BoardDice
        {
            public int SlotIndex;
            public DiceType Type;
            public int Star;
        }

        /// <summary>
        /// 벽 HP · 웨이브 번호 · 현상금 진행 · <b>SP · 소환 비용 · 강화석</b>까지 전부 여기 있다.
        ///
        /// 뒤의 셋은 따로 있었다 — SP 묶음은 <c>UIDiceSummonSystem</c> 의 손으로 쓴 구조체가,
        /// 강화석은 아래에 있던 <c>int</c> 하나가 들었다. 둘 다 <c>RunState</c> 로 들어가면서
        /// <b>필드 누락이 테스트에 잡히게 됐다</b>(<c>RunStateSnapshotTests</c> 가 리플렉션으로
        /// 전 필드를 훑는다). 손으로 쓴 구조체는 늘어난 필드를 모른다.
        /// </summary>
        public RunState.WaveSnapshot Run;

        /// <summary>판에 한 번뿐인 유물 발동 표시. 안 되돌리면 유물을 태워 먹는다.</summary>
        public RelicManager.RunFlags RelicFlags;

        /// <summary>속성 강화 레벨. 인덱스가 곧 <c>(int)ElementType</c> 다.</summary>
        public int[] ElementLevels;

        /// <summary>보드 위 주사위. 빈 칸은 담지 않으므로 길이가 슬롯 수와 다르다.</summary>
        public BoardDice[] Board;
    }
}
