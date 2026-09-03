
namespace OJ.Core
{
    /// <summary>
    /// 장착 보석 효과 하나를 <b>기본형 6개로만</b> 옮겨 담은 것. <c>GemEffect</c> 의 사본이다.
    ///
    /// OJ.Core 는 Assembly-CSharp 타입을 볼 수 없다 — <c>GemStatType</c> / <c>DiceType</c> /
    /// <c>ElementType</c> 같은 enum 도 마찬가지다. 그래서 세 축을 전부 <b>int 코드</b>로 내렸다.
    /// 코드값은 <see cref="GemBonusFormula"/> 의 상수에 적혀 있고, 실제 enum 과 어긋나지
    /// 않는지는 <c>Assets/Scripts/Equipment/GemBonusExtraction.cs</c> 가 <b>컴파일 타임에</b> 막는다.
    ///
    /// <b>구조체인 것이 의도다.</b> 원본 루프는 <c>if (effect == null) continue;</c> 로 null 을
    /// 건너뛰는데, null 은 "값"이 아니라 참조의 성질이라 기본형으로 내릴 수 없다. 그 필터는
    /// 추출쪽(<c>GemBonusExtraction.ToInputs</c>)이 들고 있다 — 거기서 null 을 빼고 담으므로
    /// 여기 들어온 배열에는 null 자리가 아예 없다. 순수 함수쪽에 <c>IsValid</c> 같은 플래그를
    /// 만들어 되살리지 마라. 그러면 같은 필터가 두 군데로 갈라진다.
    /// </summary>
    public readonly struct GemEffectInput
    {
        /// <summary><c>GemEffect.statType</c>. <see cref="GemBonusFormula.StatAttackPercent"/> 계열 코드.</summary>
        public readonly int StatType;

        /// <summary><c>GemEffect.targetDiceType</c>. <see cref="GemBonusFormula.DiceTypeMax"/> 가 "전부".</summary>
        public readonly int TargetDiceType;

        /// <summary><c>GemEffect.targetElementType</c>. <see cref="GemBonusFormula.ElementTypeMax"/> 가 "전부".</summary>
        public readonly int TargetElementType;

        /// <summary><c>GemEffect.percentValue</c>.</summary>
        public readonly float PercentValue;

        /// <summary><c>GemEffect.flatValue</c>.</summary>
        public readonly int FlatValue;

        /// <summary><c>GemEffect.intParam</c>. FirstNWavesDamageFlat 에서만 웨이브 한계로 쓰인다.</summary>
        public readonly int IntParam;

        public GemEffectInput(
            int statType,
            int targetDiceType,
            int targetElementType,
            float percentValue,
            int flatValue,
            int intParam)
        {
            StatType = statType;
            TargetDiceType = targetDiceType;
            TargetElementType = targetElementType;
            PercentValue = percentValue;
            FlatValue = flatValue;
            IntParam = intParam;
        }
    }

    /// <summary>
    /// 장착 보석 보너스의 <b>합산과 매칭 규칙</b>. <c>EquipmentManager</c> 의 348~430 / 616~694 줄에서
    /// 글자 그대로 내려온 것이다. (MIGRATION_BASELINE 5.2)
    ///
    /// <b>여기 들어올 수 없는 것 — 그래서 함수가 효과 목록을 인자로 받는다:</b>
    ///  - <c>EnumerateActiveEffects</c>. 이름은 열거지만 하는 일은 <b>상태 순회</b>다 —
    ///    장착 슬롯 6종 × 슬롯 5칸을 훑고, 칸마다 <c>IsSlotUnlocked</c>(장비 레벨 조회)를 묻고,
    ///    <c>gemDefinitionMap</c> 에서 보석 정의를 찾는다. 순수 함수가 될 수 없다.
    ///    그래서 이 파일의 함수는 전부 <b>이미 펼쳐진 효과 배열</b>을 받는다.
    ///  - <c>DiceMetaDataProvider.GetBaseElementType</c>. Assembly-CSharp 타입이라 참조 불가라서
    ///    <see cref="BaseDiceType"/> 로 <b>int 스위치를 그대로 옮겨 적었다.</b> 사본이므로
    ///    한쪽만 고치면 조용히 갈라진다 — 그 사고는 골든 core.gemBonus.baseDice[*] 가 잡는다.
    ///
    /// <b>배열 순서가 곧 값이다.</b> <see cref="SumPercent"/> 는 float 덧셈이고 float 덧셈은
    /// 결합법칙을 지키지 않는다. 원본의 순서는 <c>EquipmentType</c> 열거 순 → 슬롯 인덱스 순 →
    /// 정의의 effects 인덱스 순이다. 배열을 만들 때 그 순서를 바꾸면 값이 갈릴 수 있다.
    /// 골든 core.gemBonus.sumPercent 의 <b>18/19번 집합</b>이 그 쌍이다 — (0.1, 0.1, 0.5) 와
    /// 그 역순이고 합이 1 ulp 다르다(0.7 vs 0.700000048). 3/4번 쌍은 모양만 역순이고
    /// (0.1, 0.2, 0.3) 은 어느 순서로 더해도 0.6 이라 <b>순서 변경을 검출하지 못한다.</b>
    ///
    /// <b>이 파일을 고치기 전에:</b> 목표는 개선이 아니라 현행 동작 고정이다.
    /// <c>Mathf</c> 를 <c>System.Math</c> 로 바꾸지 마라(Math 는 double 로 올린다).
    /// <c>OJMath.Max(0f, x)</c> 는 <c>a &gt; b ? a : b</c> 라서 <c>Max(0f, -0f)</c> 가 <b>-0f</b> 이고
    /// <c>Max(0f, NaN)</c> 이 <b>NaN</b> 이다. <c>Math.Max</c> 와 답이 다르다.
    /// </summary>
    public static class GemBonusFormula
    {
        // ── DiceType 코드 (Assets/Scripts/Define/Define.cs 의 DiceType) ────────────────
        //
        // 값이 띄엄띄엄한 것이 중요하다. Max 는 열거의 마지막이라 <b>205</b> 이지 11 도 100 도 아니다.
        // 이 사실을 몰라서 실제로 사고가 났다 — 아래 IsTargetMatched 주석을 볼 것.

        public const int DiceTypeNormal = 0;
        public const int DiceTypeFire = 1;
        public const int DiceTypeIce = 2;
        public const int DiceTypeThunder = 3;
        public const int DiceTypePoison = 4;

        public const int DiceTypeTornado = 100;
        public const int DiceTypeStun = 101;
        public const int DiceTypeArmorBreak = 102;
        public const int DiceTypeWind = 103;
        public const int DiceTypeTime = 104;

        public const int DiceTypeKingNormal = 200;
        public const int DiceTypeKingFire = 201;
        public const int DiceTypeKingIce = 202;
        public const int DiceTypeKingThunder = 203;
        public const int DiceTypeKingPoison = 204;

        /// <summary>"모든 다이스". 열거 마지막이라 <b>205</b> 다.</summary>
        public const int DiceTypeMax = 205;

        // ── ElementType 코드 ──────────────────────────────────────────────────────────
        //
        // 이름이 DiceType 과 갈린다: Ice → Water, Thunder → Light, Poison → Dark.
        // 그 뒤틀린 대응이 ElementTypeOf 에 그대로 들어 있다.

        public const int ElementTypeNormal = 0;
        public const int ElementTypeFire = 1;
        public const int ElementTypeWater = 2;
        public const int ElementTypeLight = 3;
        public const int ElementTypeDark = 4;

        /// <summary>"모든 원소". 열거 마지막이라 <b>5</b> 다.</summary>
        public const int ElementTypeMax = 5;

        // ── GemStatType 코드 ──────────────────────────────────────────────────────────

        public const int StatAttackPercent = 0;
        public const int StatAttackFlat = 1;
        public const int StatCooldownReducePercent = 2;
        public const int StatFirstNWavesDamageFlat = 3;
        public const int StatFireExplosionRangePercent = 4;
        public const int StatWellHpOnKill = 5;
        public const int StatFinalDamagePercent = 6;
        public const int StatFireExplosionTargetCountFlat = 7;
        public const int StatThunderChainCountFlat = 8;
        public const int StatGoldOnKill = 9;

        /// <summary>
        /// 쿨다운 감소율 상한. <c>EquipmentManager.GetCooldownReductionPercent</c> 의 리터럴 0.8f 다.
        /// <b>이 값이 실효 상한이다</b> — 아래 <see cref="CooldownReductionPercent"/> 주석의 이중 캡 설명을 볼 것.
        /// </summary>
        public const float CooldownReductionCap = 0.8f;

        // ── 다이스 → 기본 원소 접기 ───────────────────────────────────────────────────

        /// <summary>
        /// <c>DiceMetaDataProvider.GetBaseElementType</c> 의 int 판이다. 스위치를 그대로 옮겼다.
        ///
        /// <b>치역이 {0,1,2,3,4} ∪ {입력 그대로}</b> 인 것이 매칭 규칙의 전부를 결정한다.
        /// 합성 5종(100~104)과 킹 5종(200~204)은 전부 기본 5종으로 접히므로,
        /// 이 함수가 100 이나 200 을 돌려주는 입력은 <b>기본 5종·합성·킹 어디에도 없다.</b>
        /// (default 로 빠지는 정의되지 않은 코드 — 예컨대 7 — 만 자기 자신으로 나온다.)
        /// 그래서 <c>targetDiceType == 100</c> 인 효과는 영원히 매칭되지 않는다.
        /// </summary>
        public static int BaseDiceType(int diceType)
        {
            switch (diceType)
            {
                case DiceTypeKingNormal:
                    return DiceTypeNormal;
                case DiceTypeKingFire:
                    return DiceTypeFire;
                case DiceTypeKingIce:
                    return DiceTypeIce;
                case DiceTypeKingPoison:
                    return DiceTypePoison;
                case DiceTypeKingThunder:
                    return DiceTypeThunder;
                case DiceTypeTornado:
                    return DiceTypeNormal;
                case DiceTypeArmorBreak:
                    return DiceTypeFire;
                case DiceTypeWind:
                    return DiceTypeIce;
                // Stun → Poison, Time → Thunder 로 고쳤다(조합식 폐기 · 진화 배선 정리).
                // 예전 값은 Stun → Thunder, Time → Normal 이었는데, 같은 리포의 킹 조합식이
                // KingPoison ← Stun · KingThunder ← Time 이라 두 표가 서로 다른 계통을
                // 가리키고 있었다. 이제 진화 배선(OJ.Dice.DiceEvolution)이 정본이고
                // 이 표와 DiceMetaDataProvider.GetBaseElementType 이 그것을 따른다.
                //
                // 골든 기준선도 같이 움직인다 — core.gemBonus 의 네 줄
                // (baseDice/element[101], baseDice/element[104])이 바뀐 값으로 갱신돼 있다.
                // match 격자는 안 바뀐다: GemMatchDice 축에 101·104 가 없다.
                case DiceTypeStun:
                    return DiceTypePoison;
                case DiceTypeTime:
                    return DiceTypeThunder;
                default:
                    return diceType;
            }
        }

        /// <summary>
        /// <c>EquipmentManager.ToElementType</c> 의 int 판이다.
        ///
        /// <b>안에서 <see cref="BaseDiceType"/> 를 한 번 더 부른다.</b> 호출부
        /// (<see cref="IsTargetMatched"/>)가 이미 접어서 넘기므로 두 번째 접기는 항상 항등이지만,
        /// 원본이 그렇게 쓰여 있으므로 그대로 뒀다. 빼도 지금 값은 같다 — 그래도 빼지 마라.
        /// 나중에 접기 표가 항등이 아니게 되는 날(예: Tornado 를 Tornado 로 두는 날)
        /// 두 함수의 답이 갈리고, 그 차이는 조용하다.
        ///
        /// 접히지 않는 코드(default)는 <see cref="ElementTypeMax"/> 를 낸다 —
        /// 그러면 "원소 지정" 효과는 매칭되지 않고 "전부(Max)" 효과만 붙는다.
        /// </summary>
        public static int ElementTypeOf(int diceType)
        {
            switch (BaseDiceType(diceType))
            {
                case DiceTypeNormal:
                    return ElementTypeNormal;
                case DiceTypeFire:
                    return ElementTypeFire;
                case DiceTypeIce:
                    return ElementTypeWater;
                case DiceTypeThunder:
                    return ElementTypeLight;
                case DiceTypePoison:
                    return ElementTypeDark;
                default:
                    return ElementTypeMax;
            }
        }

        // ── 매칭 규칙 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// <c>EquipmentManager.IsTargetMatched</c> 의 int 판. 효과 하나가 이 다이스에 붙는가.
        ///
        /// 규칙은 네 줄이고 <b>순서가 곧 규칙</b>이다:
        /// <code>
        ///   1) diceType == DiceTypeMax                      → 무조건 true (조기 반환)
        ///   2) targetDiceType 이 Max 도 아니고 접은 값도 아니면 → false
        ///   3) targetElementType == ElementTypeMax           → true
        ///   4) targetElementType == 접은 값의 원소            → 그 비교 결과
        /// </code>
        ///
        /// <b>1) 이 왜 중요한가 — 실제로 난 사고.</b> ef30864("특수 다이스 추가") 이전에는
        /// <c>DiceType.Max == 11</c> 이었고 에셋의 <c>targetDiceType: 11</c> 이 "모든 다이스"를 뜻했다.
        /// 그 커밋이 Max 를 11 → <b>205</b> 로 밀면서 에셋을 11 → <b>100</b> 으로 리맵했는데,
        /// 100 은 Max 가 아니라 <b>Tornado</b> 다. 2) 에서 <c>BaseDiceType</c> 의 치역과 비교되므로
        /// 100 은 절대 같아질 수 없고, 효과 100개 중 71개가 통째로 죽어 있었다(f0cccdb / 3a6f5bd).
        /// 그중 19개(WellHpOnKill 10 + GoldOnKill 9)만 우연히 살아 있었는데, 그 게터들이
        /// <c>diceType = DiceTypeMax</c> 로 물어서 <b>1) 의 조기 반환에 걸렸기 때문</b>이다.
        /// 컴파일도 콘솔도 조용했고 "보석을 껴도 데미지가 그대로"로만 드러났다.
        ///
        /// 그래서 이 규칙이 순수 함수로 내려와 있어야 한다. 골든 core.gemBonus.match 격자는
        /// targetDiceType 에 <b>100 과 200 을 일부러 넣는다</b> — 둘 다 "절대 매칭되지 않는 코드"이고,
        /// 그 사실이 값으로 박혀 있어야 다음번 리맵이 조용히 지나가지 못한다.
        /// </summary>
        public static bool IsTargetMatched(int targetDiceType, int targetElementType, int diceType)
        {
            if (diceType == DiceTypeMax)
                return true;

            int baseType = BaseDiceType(diceType);

            if (targetDiceType != DiceTypeMax && targetDiceType != baseType)
                return false;

            if (targetElementType == ElementTypeMax)
                return true;

            int elementType = ElementTypeOf(baseType);
            return targetElementType == elementType;
        }

        // ── 합산 ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// <c>EquipmentManager.SumPercent</c>. 매칭된 효과의 <c>percentValue</c> 를 더한다.
        ///
        /// <b>클램프가 두 겹인 것이 사양이다.</b> 항마다 <c>OJMath.Max(0f, ·)</c> 를 걸고 합계에도
        /// 한 번 더 건다. 항마다 거는 것은 <b>음수 효과 하나가 다른 효과를 깎지 못하게</b> 하고,
        /// 합계 쪽은 (항이 전부 0 이상이라) 지금은 아무것도 하지 않는다. 그래도 지우지 마라 —
        /// 항쪽 클램프를 빼는 변이와 합계쪽을 빼는 변이는 <b>서로 다른 줄에서 갈린다.</b>
        /// 골든은 음수 percentValue 를 섞은 효과 집합을 들고 그 차이를 밟는다.
        ///
        /// <paramref name="effects"/> 의 <b>순서가 값에 들어간다</b>(float 덧셈은 결합적이 아니다).
        /// 클래스 주석을 볼 것.
        ///
        /// 원본은 <c>EnumerateActiveEffects</c> 가 매칭을 먼저 하고 그 다음 statType 을 봤다.
        /// 여기서도 같은 차례로 두었다 — 둘 다 부작용 없는 술어라 값은 순서와 무관하지만,
        /// 원본과 나란히 놓고 읽을 수 있는 편이 낫다.
        /// </summary>
        public static float SumPercent(GemEffectInput[] effects, int statType, int diceType)
        {
            float sum = 0f;

            int count = effects == null ? 0 : effects.Length;
            for (int i = 0; i < count; i++)
            {
                if (!IsTargetMatched(effects[i].TargetDiceType, effects[i].TargetElementType, diceType))
                    continue;
                if (effects[i].StatType != statType)
                    continue;

                sum += OJMath.Max(0f, effects[i].PercentValue);
            }

            return OJMath.Max(0f, sum);
        }

        /// <summary>
        /// <c>EquipmentManager.SumFlat</c>. 매칭된 효과의 <c>flatValue</c> 를 더한다.
        ///
        /// <b>int 덧셈이라 unchecked 로 넘친다.</b> 큰 flatValue 두 개가 넘치면 합계가 음수가 되고,
        /// 마지막 <c>OJMath.Max(0, sum)</c> 이 그것을 0 으로 접는다. 그것이 현행 동작이고,
        /// 골든이 <c>int.MaxValue</c> 근처 표본으로 그 접힘을 박제한다. long 으로 올리지 마라.
        /// </summary>
        public static int SumFlat(GemEffectInput[] effects, int statType, int diceType)
        {
            int sum = 0;

            int count = effects == null ? 0 : effects.Length;
            for (int i = 0; i < count; i++)
            {
                if (!IsTargetMatched(effects[i].TargetDiceType, effects[i].TargetElementType, diceType))
                    continue;
                if (effects[i].StatType != statType)
                    continue;

                sum += OJMath.Max(0, effects[i].FlatValue);
            }

            return OJMath.Max(0, sum);
        }

        // ── 캡이 걸리는 합산 ──────────────────────────────────────────────────────────

        /// <summary>
        /// <c>EquipmentManager.GetCooldownReductionPercent</c>.
        /// <c>OJMath.Clamp(SumPercent(...), 0f, 0.8f)</c> 그대로다.
        ///
        /// <b>캡이 두 겹이고, 무는 것은 여기 0.8 이다.</b> 호출부
        /// (<c>DiceMetaDataProvider.GetCooldown</c> :481-482)는 이렇게 쓴다:
        /// <code>
        ///   float reducePercent = EquipmentManager.Instance.GetCooldownReductionPercent(diceType);
        ///   cooldown *= OJMath.Max(0.05f, 1f - reducePercent);
        /// </code>
        /// 즉 저쪽은 "쿨다운을 5% 밑으로는 못 내린다" = 감소율 0.95 상한이다. 그런데 이 함수가
        /// 이미 0.8 로 잘라서 넘기므로 <c>1f - reducePercent</c> 는 [0.2, 1] 을 벗어나지 못하고
        /// <b>저쪽 하한 0.05f 는 한 번도 물리지 않는 죽은 가지다.</b>
        /// <b>실효 상한은 0.8 이다.</b>
        ///
        /// 두 값 중 하나만 만지는 변경은 조용히 무의미하다 — 0.05f 를 0.5f 로 바꿔도 지금은
        /// 아무 일도 안 일어나고, 여기 0.8f 를 0.99f 로 올리는 순간 갑자기 저쪽이 살아난다.
        /// 감소율 상한을 조정할 일이 생기면 <b>두 줄을 같이</b> 봐야 한다.
        ///
        /// (참고: <c>RelicManager</c> 쪽 감소율은 이 합산을 타지 않는다. :487-488 에서 따로
        /// 곱해지고 거기에는 0.8 캡이 없어 0.05f 하한이 실제로 물 수 있다.)
        /// </summary>
        public static float CooldownReductionPercent(GemEffectInput[] effects, int diceType)
        {
            return OJMath.Clamp(SumPercent(effects, StatCooldownReducePercent, diceType), 0f, CooldownReductionCap);
        }

        /// <summary>
        /// <c>EquipmentManager.GetFirstNWavesDamageFlatBonus</c>. 초반 N 웨이브 한정 고정 피해.
        ///
        /// 한계 판정이 <b><c>waveIndex &lt;= limit</c> 포함</b>이다(코드는 그 여집합인
        /// <c>waveIndex &gt; limit</c> 로 걸러 낸다). <c>intParam == 3</c> 이면 웨이브 3 <b>까지</b>
        /// 붙고 4 부터 안 붙는다. 부등호를 <c>&lt;</c> 로 바꾸면 마지막 웨이브 한 칸이 조용히 사라진다 —
        /// 골든이 waveIndex 를 limit 과 <b>같게 / 하나 크게</b> 둘 다 밟는다.
        ///
        /// 게이트가 셋이고 <b>겹친다</b>:
        ///   <c>waveIndex &lt;= 0</c> 조기 반환 · <c>limit = OJMath.Max(0, intParam)</c> ·
        ///   <c>limit &lt;= 0</c> 건너뜀.
        /// 셋째는 이미 둘째가 음수를 0 으로 접은 뒤라 <c>limit == 0</c> 일 때만 걸리고,
        /// 그때는 <c>waveIndex &gt; limit</c> 도 어차피 참이다. <b>완전히 중복이다.</b>
        /// 그래도 지웠다 되살리는 왕복을 만들지 마라 — 원본 그대로 두는 것이 이 파일의 목적이다.
        /// </summary>
        public static int FirstNWavesDamageFlatBonus(GemEffectInput[] effects, int diceType, int waveIndex)
        {
            if (waveIndex <= 0)
                return 0;

            int sum = 0;

            int count = effects == null ? 0 : effects.Length;
            for (int i = 0; i < count; i++)
            {
                if (!IsTargetMatched(effects[i].TargetDiceType, effects[i].TargetElementType, diceType))
                    continue;
                if (effects[i].StatType != StatFirstNWavesDamageFlat)
                    continue;

                int limit = OJMath.Max(0, effects[i].IntParam);
                if (limit <= 0 || waveIndex > limit)
                    continue;

                sum += OJMath.Max(0, effects[i].FlatValue);
            }

            return OJMath.Max(0, sum);
        }

        // ── 나머지 게터의 대응표 (위임할 때 볼 것) ────────────────────────────────────
        //
        // EquipmentManager 의 public 게터 중 위 네 개로 덮이지 않는 것들은 전부
        // "SumPercent / SumFlat 을 부르고 결과에 OJMath.Max 를 한 번 더 거는" 모양이다.
        // 그 바깥 클램프는 SumPercent/SumFlat 이 이미 같은 클램프로 끝나므로 <b>항상 항등</b>이라
        // 여기 래퍼를 따로 만들지 않았다. 위임할 때 이 표대로 부르면 된다.
        //
        //   GetAttackPercentBonus(d)            → OJMath.Max(0f, SumPercent(e, StatAttackPercent, d))
        //   GetAttackFlatBonus(d)               → OJMath.Max(0,  SumFlat(e, StatAttackFlat, d))
        //   GetFireExplosionRangeBonus(d)       → OJMath.Max(0f, SumPercent(e, StatFireExplosionRangePercent, d))
        //   GetFinalDamagePercentBonus(d)       → OJMath.Max(0f, SumPercent(e, StatFinalDamagePercent, d))
        //   GetFireExplosionExtraTargetCount(d) → OJMath.Max(0,  SumFlat(e, StatFireExplosionTargetCountFlat, d))
        //   GetThunderChainExtraCount(d)        → OJMath.Max(0,  SumFlat(e, StatThunderChainCountFlat, d))
        //   GetWellHpOnKill()                   → SumFlat(e, StatWellHpOnKill, DiceTypeMax)
        //   GetGoldOnKill()                     → SumFlat(e, StatGoldOnKill,  DiceTypeMax)
        //
        // 마지막 둘은 인자가 없는 게터인데 <b>내부적으로 DiceTypeMax 로 묻는다</b> —
        // 그래서 IsTargetMatched 첫 줄의 조기 반환에 걸려 targetDiceType 이 무엇이든 매칭된다.
        // 위의 리맵 사고에서 이 둘만 살아남은 이유가 정확히 그것이다. 위임할 때 diceType 을
        // 빼먹고 0(Normal)으로 부르면 <b>19개가 다시 죽는다.</b>
    }
}
