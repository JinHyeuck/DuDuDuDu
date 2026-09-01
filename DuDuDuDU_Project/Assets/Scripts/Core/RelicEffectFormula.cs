
namespace OJ.Core
{
    /// <summary>
    /// 유물 효과 게터의 <b>산술 부분</b>만 모았다. (MIGRATION_BASELINE 5.3)
    ///
    /// <b>여기 없는 것 — 전부 RelicManager 에 남는다:</b>
    ///  - <c>RelicId</c> / <c>DiceType</c> 조회. enum 은 Assembly-CSharp 타입이라 OJ.Core 가 못 본다
    ///  - <c>ConsumeAttackDamageMultiplier</c> / <c>TryTriggerLastWall</c> / <c>RollSummonStar</c> /
    ///    <c>TrySpawnTwinDice</c> — <b>이름은 게터인데 상태를 바꾸거나 난수를 소모한다.</b>
    ///    순수 함수에 넣으면 "계산했다"가 곧 "1회성 효과를 썼다"가 된다
    ///  - <c>IsBoardFull()</c>(UIBoard), <c>HasCrownResonance</c>(DiceTypeStarManager),
    ///    <c>IsLastWallCooldownActive</c>(GameManager) — 다른 매니저를 본다. bool 로 받는다
    /// </summary>
    public static class RelicEffectFormula
    {
        /// <summary>쿨감 상한. RelicManager 가 0~80 으로 자른다.
        /// 그 뒤 DiceMetaDataProvider 가 다시 <c>Max(0.05f, 1-r)</c> 로 자르므로 <b>이중 캡</b>이고
        /// 실효 상한은 여기 80 이다. 값을 바꾸면 실제로 밸런스가 움직인다.</summary>
        public const float CooldownReductionMaxPercent = 80f;

        /// <summary>회오리 피해증가 지속시간 하한. 0 이면 즉시 만료라 효과가 없다.</summary>
        public const float TornadoBonusMinDuration = 0.1f;

        /// <summary>
        /// 쿨감 합산. QuickHands 는 항상, LastWall 은 벽 부활 쿨다운 중일 때만 더해진다.
        /// 더한 뒤에 자른다 — 자르고 더하면 두 유물을 같이 낀 경우가 달라진다.
        /// </summary>
        public static float CooldownReductionPercent(float quickHandsPercent, float lastWallPercent, bool lastWallActive)
        {
            float percent = quickHandsPercent;
            if (lastWallActive)
                percent += lastWallPercent;

            return OJMath.Clamp(percent, 0f, CooldownReductionMaxPercent);
        }

        /// <summary>
        /// 피해 배수. 조건이 맞는 유물의 퍼센트를 <b>먼저 int 가 아니라 float 로 합산</b>하고
        /// 마지막에 한 번만 0.01f 를 곱한다. 각각 곱해서 더하면 값이 갈린다.
        /// </summary>
        public static float DamageMultiplier(
            float fullBoardPercent, bool fullBoardApplies,
            float crownResonancePercent, bool crownResonanceApplies)
        {
            float bonusPercent = 0f;
            if (fullBoardApplies)
                bonusPercent += fullBoardPercent;
            if (crownResonanceApplies)
                bonusPercent += crownResonancePercent;

            return 1f + bonusPercent * 0.01f;
        }

        /// <summary>퍼센트 값을 정수로. <c>OJMath.RoundToInt</c> 는 은행가 반올림이다 —
        /// <c>(int)(x + 0.5f)</c> 로 바꾸면 .5 경계에서 어긋난다.</summary>
        public static int PercentToInt(float percent)
        {
            return OJMath.RoundToInt(percent);
        }

        /// <summary>지속시간 하한을 건다. 0 이하가 들어오면 효과가 즉시 사라진다.</summary>
        public static float DurationWithFloor(float duration)
        {
            return OJMath.Max(TornadoBonusMinDuration, duration);
        }

        /// <summary>유물 레벨 상한. Database 가 없을 때의 기본값 20 은 호출부가 정한다.</summary>
        public static int ClampLevel(int level, int maxLevel)
        {
            return OJMath.Clamp(level, 0, OJMath.Max(1, maxLevel));
        }
    }
}
