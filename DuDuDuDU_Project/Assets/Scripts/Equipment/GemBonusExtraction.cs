using System.Collections.Generic;
using OJ.Core;

namespace OJ.Equipment
{
    /// <summary>
    /// <c>GemEffect</c>(Assembly-CSharp) → <see cref="GemEffectInput"/>(OJ.Core) 변환과,
    /// <b>enum 코드가 밀리면 컴파일을 세우는 잠금</b>. (MIGRATION_BASELINE 5.2)
    ///
    /// 왜 이 파일이 따로 있는가: OJ.Core 는 <c>noEngineReferences</c> 이전에 <b>Assembly-CSharp 을
    /// 참조할 수 없다</b> — enum 도 마찬가지다. 그래서 <see cref="GemBonusFormula"/> 는 DiceType /
    /// ElementType / GemStatType 을 int 상수로 <b>베껴</b> 들고 있다. 베낀 것은 언젠가 갈라진다.
    ///
    /// 실제로 갈라진 적이 있다. ef30864 가 <c>DiceType.Max</c> 를 11 → 205 로 밀었을 때
    /// 에셋을 100(=Tornado)으로 리맵해 보석 효과 100개 중 71개가 조용히 죽었다(f0cccdb / 3a6f5bd).
    /// <b>컴파일도 콘솔도 조용했다.</b> 같은 종류의 다음 사고를 컴파일러가 잡게 하려고
    /// 아래 <c>Guard*</c> 상수를 둔다 — enum 값이 바뀌는 순간 이 파일이 <b>CS0020(상수 0 으로 나눔)</b>
    /// 으로 터진다. 값을 고칠 곳은 이 파일이 아니라 <see cref="GemBonusFormula"/> 의 상수다.
    ///
    /// <b>여기서 하지 않는 일:</b> 효과를 모으지 않는다. 장착 슬롯을 훑어 효과를 펼치는 것은
    /// <c>EquipmentManager.EnumerateActiveEffects</c> 의 상태 순회이고, 그것은 순수화 대상이 아니다.
    /// 이 파일은 <b>이미 손에 든 효과 목록</b>을 기본형으로 옮겨 담을 뿐이다.
    /// </summary>
    public static class GemBonusExtraction
    {
        // ── 컴파일 타임 잠금 ──────────────────────────────────────────────────────────
        //
        // 전부 상수식이라 컴파일러가 접는다. 좌우가 같으면 1/1, 다르면 1/0 이 되어
        // CS0020 으로 <b>컴파일이 멈춘다.</b> 런타임 검사가 아니라서 아무도 부르지 않아도 산다
        // (그것이 요점이다 — 죽은 검사는 검사가 아니다).
        //
        // 줄이 지루하게 많은 것은 의도다. Max 하나만 잠그면 중간 값이 밀리는 사고를 놓친다.

        private const int GuardDiceNormal = 1 / (((int)DiceType.Normal == GemBonusFormula.DiceTypeNormal) ? 1 : 0);
        private const int GuardDiceFire = 1 / (((int)DiceType.Fire == GemBonusFormula.DiceTypeFire) ? 1 : 0);
        private const int GuardDiceIce = 1 / (((int)DiceType.Ice == GemBonusFormula.DiceTypeIce) ? 1 : 0);
        private const int GuardDiceThunder = 1 / (((int)DiceType.Thunder == GemBonusFormula.DiceTypeThunder) ? 1 : 0);
        private const int GuardDicePoison = 1 / (((int)DiceType.Poison == GemBonusFormula.DiceTypePoison) ? 1 : 0);
        private const int GuardDiceTornado = 1 / (((int)DiceType.Tornado == GemBonusFormula.DiceTypeTornado) ? 1 : 0);
        private const int GuardDiceStun = 1 / (((int)DiceType.Stun == GemBonusFormula.DiceTypeStun) ? 1 : 0);
        private const int GuardDiceArmorBreak = 1 / (((int)DiceType.ArmorBreak == GemBonusFormula.DiceTypeArmorBreak) ? 1 : 0);
        private const int GuardDiceWind = 1 / (((int)DiceType.Wind == GemBonusFormula.DiceTypeWind) ? 1 : 0);
        private const int GuardDiceTime = 1 / (((int)DiceType.Time == GemBonusFormula.DiceTypeTime) ? 1 : 0);
        private const int GuardDiceKingNormal = 1 / (((int)DiceType.KingNormal == GemBonusFormula.DiceTypeKingNormal) ? 1 : 0);
        private const int GuardDiceKingFire = 1 / (((int)DiceType.KingFire == GemBonusFormula.DiceTypeKingFire) ? 1 : 0);
        private const int GuardDiceKingIce = 1 / (((int)DiceType.KingIce == GemBonusFormula.DiceTypeKingIce) ? 1 : 0);
        private const int GuardDiceKingThunder = 1 / (((int)DiceType.KingThunder == GemBonusFormula.DiceTypeKingThunder) ? 1 : 0);
        private const int GuardDiceKingPoison = 1 / (((int)DiceType.KingPoison == GemBonusFormula.DiceTypeKingPoison) ? 1 : 0);

        // 이 한 줄이 71개를 죽였던 그 값이다. 205 가 아니게 되는 순간 컴파일이 멈춘다.
        private const int GuardDiceMax = 1 / (((int)DiceType.Max == GemBonusFormula.DiceTypeMax) ? 1 : 0);

        private const int GuardElementNormal = 1 / (((int)ElementType.Normal == GemBonusFormula.ElementTypeNormal) ? 1 : 0);
        private const int GuardElementFire = 1 / (((int)ElementType.Fire == GemBonusFormula.ElementTypeFire) ? 1 : 0);
        private const int GuardElementWater = 1 / (((int)ElementType.Water == GemBonusFormula.ElementTypeWater) ? 1 : 0);
        private const int GuardElementLight = 1 / (((int)ElementType.Light == GemBonusFormula.ElementTypeLight) ? 1 : 0);
        private const int GuardElementDark = 1 / (((int)ElementType.Dark == GemBonusFormula.ElementTypeDark) ? 1 : 0);
        private const int GuardElementMax = 1 / (((int)ElementType.Max == GemBonusFormula.ElementTypeMax) ? 1 : 0);

        private const int GuardStatAttackPercent = 1 / (((int)GemStatType.AttackPercent == GemBonusFormula.StatAttackPercent) ? 1 : 0);
        private const int GuardStatAttackFlat = 1 / (((int)GemStatType.AttackFlat == GemBonusFormula.StatAttackFlat) ? 1 : 0);
        private const int GuardStatCooldown = 1 / (((int)GemStatType.CooldownReducePercent == GemBonusFormula.StatCooldownReducePercent) ? 1 : 0);
        private const int GuardStatFirstNWaves = 1 / (((int)GemStatType.FirstNWavesDamageFlat == GemBonusFormula.StatFirstNWavesDamageFlat) ? 1 : 0);
        private const int GuardStatFireRange = 1 / (((int)GemStatType.FireExplosionRangePercent == GemBonusFormula.StatFireExplosionRangePercent) ? 1 : 0);
        private const int GuardStatWellHp = 1 / (((int)GemStatType.WellHpOnKill == GemBonusFormula.StatWellHpOnKill) ? 1 : 0);
        private const int GuardStatFinalDamage = 1 / (((int)GemStatType.FinalDamagePercent == GemBonusFormula.StatFinalDamagePercent) ? 1 : 0);
        private const int GuardStatFireTargets = 1 / (((int)GemStatType.FireExplosionTargetCountFlat == GemBonusFormula.StatFireExplosionTargetCountFlat) ? 1 : 0);
        private const int GuardStatThunderChain = 1 / (((int)GemStatType.ThunderChainCountFlat == GemBonusFormula.StatThunderChainCountFlat) ? 1 : 0);
        private const int GuardStatGoldOnKill = 1 / (((int)GemStatType.GoldOnKill == GemBonusFormula.StatGoldOnKill) ? 1 : 0);

        // ── 변환 ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 효과 하나를 기본형으로 옮긴다. enum → int 는 <b>단순 캐스트</b>여야 한다.
        /// switch 로 다시 매핑하지 마라 — 그 순간 위 Guard 상수가 지키는 축이 하나 늘고
        /// 매핑표가 조용히 갈라질 자리가 생긴다.
        /// </summary>
        public static GemEffectInput ToInput(GemEffect effect)
        {
            return new GemEffectInput(
                (int)effect.statType,
                (int)effect.targetDiceType,
                (int)effect.targetElementType,
                effect.percentValue,
                effect.flatValue,
                effect.intParam);
        }

        /// <summary>
        /// 효과 목록을 기본형 배열로 옮긴다.
        ///
        /// <b>null 효과를 여기서 뺀다.</b> 원본 합산 루프는 <c>if (effect == null) continue;</c> 로
        /// 건너뛰는데, null 은 값이 아니라 참조의 성질이라 <see cref="GemEffectInput"/> 으로는
        /// 표현할 수 없다. 그래서 그 필터가 이쪽에 남았다. 순수 함수쪽에 되살리지 마라.
        ///
        /// <b>순서를 바꾸지 마라.</b> <see cref="GemBonusFormula.SumPercent"/> 는 float 덧셈이고
        /// float 덧셈은 결합적이 아니다. 넘어온 차례 그대로 담는다 — 정렬·중복 제거·병합
        /// 어느 것도 여기서 하면 안 된다.
        /// </summary>
        public static GemEffectInput[] ToInputs(IEnumerable<GemEffect> effects)
        {
            var result = new List<GemEffectInput>();
            if (effects == null)
                return result.ToArray();

            foreach (GemEffect effect in effects)
            {
                if (effect == null)
                    continue;

                result.Add(ToInput(effect));
            }

            return result.ToArray();
        }
    }
}
