namespace OJ.Core
{
    /// <summary>
    /// "지금 강화할 수 있는가" 의 규칙. 엔진을 쓰지 않는다.
    ///
    /// 레드닷이 이것을 본다(하단 탭 · 장비 칸 · 다이스 칸). <b>한 곳에 두는 이유:</b> 칸마다 비슷한 식을 따로 두면 장비 쪽만 만렙을
    /// 놓치거나 다이스 쪽만 두루마리를 놓친다 — 뉴비가 "강화할 수 있는데 표시가 없는" 화면에서 강화를 못 하고 나간다.
    /// </summary>
    public static class UpgradeAffordability
    {
        /// <summary>만렙이 아니고, 골드와 두루마리가 둘 다 비용 이상이면 참. 비용이 0 이하여도 만렙이면 거짓이다.</summary>
        public static bool CanUpgrade(bool isMaxLevel, int ownedGold, int ownedScroll, int costGold, int costScroll)
        {
            if (isMaxLevel)
                return false;

            return ownedGold >= costGold && ownedScroll >= costScroll;
        }
    }
}
