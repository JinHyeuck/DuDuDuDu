namespace OJ.Core
{
    /// <summary>
    /// 다이스 마일스톤이 열리는 레벨. <b>효과 코드의 `level >= N` 은 이 상수만 쓴다.</b>
    ///
    /// 예전에는 3 / 6 / 9 / 12 가 효과 파일마다 리터럴로 박혀 있었다. 레벨 상한이 30 으로 늘고
    /// 마일스톤이 10 / 20 / 25 로 옮겨 가면서(2026-11, 다이스 승급 개편) 그 리터럴 서른 곳을
    /// 한 번에 바꿔야 했다 — 다음에 또 옮길 때 같은 일을 겪지 않도록 한 곳으로 모았다.
    ///
    /// 옛 Lv3 마일스톤은 없다. 그 값(최종 대미지 +10% 등)은 <b>레벨과 무관한 기본 수치</b>로
    /// 합쳤고, 옛 6 / 9 / 12 가 각각 <see cref="First"/> / <see cref="Second"/> / <see cref="Third"/> 로 간다.
    /// <see cref="Final"/> 은 기본 5종에만 새로 붙는 Lv30 마일스톤이다.
    /// </summary>
    public static class DiceMilestoneLevels
    {
        /// <summary>옛 Lv6 자리.</summary>
        public const int First = 10;

        /// <summary>옛 Lv9 자리.</summary>
        public const int Second = 20;

        /// <summary>옛 Lv12 자리.</summary>
        public const int Third = 25;

        /// <summary>기본 5종 전용 마지막 마일스톤.</summary>
        public const int Final = 30;
    }
}
