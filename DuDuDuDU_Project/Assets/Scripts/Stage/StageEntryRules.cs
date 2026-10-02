namespace OJ.Stage
{
    /// <summary>
    /// 본편 스테이지 입장료.
    ///
    /// <b>원래 화면에만 있고 코드에는 없었다.</b> 로비의 입장 버튼이 "고기 x5" 를 달고
    /// 있었는데 <c>OnClickEnterStage</c> 는 바로 씬을 로드했다 — <b>표시된 값과 실제
    /// 동작이 달랐다.</b> 그 상태로 두면 밸런스 계산(<c>SweepEconomy</c>)이 전제한
    /// 고기 소비가 실제보다 적고, 시즌 패스처럼 "쓴 만큼" 을 세는 것이 전부 어긋난다.
    ///
    /// <b>소탕 1회와 같은 5 다.</b> 소탕은 전투를 생략하고 결과만 받는 것이라
    /// (<c>SweepRules.StaminaCostPerSweep</c>) 값이 다르면 둘 중 싼 쪽만 쓰게 된다.
    /// 소탕이 더 싸면 아무도 직접 하지 않고, 비싸면 소탕을 쓸 이유가 없다.
    /// </summary>
    public static class StageEntryRules
    {
        /// <summary>스테이지 1회 입장에 드는 고기.</summary>
        public const int StaminaCost = SweepRules.StaminaCostPerSweep;

        /// <summary>들어갈 수 있는가. 모자라면 입장 자체가 막힌다.</summary>
        public static bool CanEnter(int ownedStamina)
        {
            return ownedStamina >= StaminaCost;
        }
    }
}
