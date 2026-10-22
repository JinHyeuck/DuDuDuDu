namespace OJ.Core
{
    /// <summary>
    /// 패배한 뒤 "킹 썬더를 써 보세요" 안내를 띄울지. 엔진을 쓰지 않는다.
    ///
    /// 킹 다이스의 강함을 체감시키려는 흐름의 일부다 — 4스테이지부터는 킹 없이 버티기 어렵게 두고(<see cref="KingThunderGuide"/>),
    /// 지면 "킹을 만들어 다시 도전" 으로 이어 준다. <b>억지로 지게 만드는 연출이 아니라</b> 이미 진 유저에게 길을 알려 주는 것이다.
    /// 킹이 보드에 있는데도 졌으면 안내가 맞지 않으므로 띄우지 않는다(킹은 있는데 약한 문제는 안내로 풀리지 않는다).
    /// </summary>
    public static class KingHintRules
    {
        public static bool ShouldShow(bool isWin, bool isTutorial, bool isTower, int stageIndex, bool ownsKingThunder, bool kingOnBoard)
        {
            if (isWin || isTutorial || isTower)
                return false;

            return stageIndex >= KingThunderGuide.Stage && ownsKingThunder && !kingOnBoard;
        }
    }
}
