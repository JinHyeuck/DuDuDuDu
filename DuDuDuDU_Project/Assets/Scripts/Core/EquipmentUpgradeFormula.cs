
namespace OJ.Core
{
    /// <summary>
    /// 장비 6종의 <b>강화 규칙표와 그 산출식</b>. EquipmentManager 의 GetRule /
    /// GetUpgradeCost / GetEquipmentAttack / GetSlotUnlockLevel 에서 글자 그대로
    /// 내려온 것이다. (MIGRATION_BASELINE 5.2)
    ///
    /// <b>전부 정수 산술이다.</b> 이 파일에는 float 가 한 줄도 없고, 그래서
    /// AGENTS.md 의 "부동소수: Unity는 Mono" 절이 말하는 확장 정밀도 함정이 없다 —
    /// Mono 든 CoreCLR 이든 같은 답을 낸다. 대신 <c>OJMath.Max</c> 두 개가
    /// <b>int 오버로드</b>로 묶여 있어야 한다는 조건이 붙는다.
    /// <c>OJMath.Max(0, x)</c> 의 0 을 <c>0f</c> 로 쓰면 float 오버로드로 넘어가면서
    /// 2^24 위에서 값이 깎이고, 반환 타입도 달라진다. 리터럴에 f 를 붙이지 마라.
    ///
    /// <b>enum 을 못 들고 온다.</b> OJ.Core 는 Assembly-CSharp 을 참조할 수 없어서
    /// <c>EquipmentType</c> 을 볼 수 없다. 그래서 규칙표는 <b>int 인덱스</b>로 받고,
    /// enum → 인덱스 변환은 호출부(EquipmentManager.ToRuleIndex)가 한다.
    /// 그 변환을 <c>(int)equipmentType</c> 캐스트로 쓰지 않은 것은 일부러다 —
    /// 캐스트로 쓰면 enum 순서를 바꾸는 순간 무기 골드가 조용히 투구 골드가 된다.
    /// 호출부는 <b>이름 대 이름</b>으로 switch 해서 아래 상수로 옮긴다.
    ///
    /// <b>합계(GetTotalEquipmentAttack)는 여기 없다.</b> 그것은 6종을
    /// <c>Enum.GetValues(typeof(EquipmentType))</c> 로 순회하며 더하는 것이라
    /// enum 을 봐야 하고, 더해지는 항 하나하나가 <see cref="Attack"/> 다.
    /// 즉 잠글 값은 전부 여기 있고 밖에 남은 것은 순회뿐이다. int 덧셈은
    /// unchecked 에서도 결합·교환법칙이 성립하므로 순회 순서는 결과에 영향이 없다.
    /// </summary>
    public static class EquipmentUpgradeFormula
    {
        // ── 규칙표 인덱스 ────────────────────────────────────────────────────────────
        //
        // 현재 EquipmentType 의 선언 순서와 같은 값이지만, <b>그것에 기대지 않는다.</b>
        // 호출부가 이름으로 매핑하므로 enum 을 재배열해도 여기 값은 그대로 유효하다.
        public const int WeaponIndex = 0;
        public const int HelmetIndex = 1;
        public const int ArmorIndex = 2;
        public const int RingIndex = 3;
        public const int ShoesIndex = 4;
        public const int NecklaceIndex = 5;

        /// <summary>
        /// 규칙표에 없는 장비. 원본 <c>GetRule</c> 의 <c>default:</c> 자리다.
        ///
        /// 지금은 <c>EquipmentType</c> 이 6값뿐이라 <b>호출부에서 도달하지 않는다.</b>
        /// 그래도 남긴 이유는 나중에 enum 이 늘었을 때 새 장비가 조용히
        /// <see cref="WeaponIndex"/> 의 값을 쓰는 것보다 default 로 떨어지는 것이 낫기
        /// 때문이다. 값은 원본 default 리터럴 그대로다.
        /// </summary>
        public const int UnknownIndex = -1;

        /// <summary>
        /// 강화 규칙 6필드. EquipmentManager 안에 있던 private struct 를 그대로 옮겼다.
        /// 필드 이름·순서까지 원본과 같게 뒀다 — 이름이 바뀌면 골든 키가 바뀌고,
        /// 그러면 "값이 바뀐 것"과 구분이 안 된다.
        /// </summary>
        public struct EquipmentUpgradeRule
        {
            public int baseGold;
            public int goldPerLevel;
            public int baseScroll;
            public int scrollPerLevel;
            public int baseAttack;
            public int attackPerLevel;
        }

        /// <summary>
        /// 7케이스 × 6필드 = 42리터럴. EquipmentManager.GetRule(715~734) 을 통째로 옮겼다.
        ///
        /// <b>표를 압축하지 마라.</b> 조사에서 나온 사실이 두 가지 있는데, 둘 다
        /// "지금 값이 같다"는 것이지 "같아야 한다"는 것이 아니다:
        ///
        ///  * 6종 전부 <c>baseAttack = 2</c>, <c>attackPerLevel = 3</c>, <c>scrollPerLevel = 1</c> 이다.
        ///    실제로 다른 것은 골드 2열과 <c>baseScroll</c> 뿐이다. 공통 3열을 상수로 빼면
        ///    장비 하나만 공격력 곡선을 다르게 하고 싶어지는 날 <b>표가 아니라 코드를</b>
        ///    고쳐야 한다. 밸런스 수치를 SO 로 올리는 것이 이 리팩토링의 결정 3번이므로,
        ///    그때까지는 표 모양 그대로 들고 있는 편이 옮기기 쉽다.
        ///  * <c>Necklace</c> 행과 <c>default</c> 행은 <b>6필드가 전부 같다.</b> 즉 Necklace
        ///    케이스를 지워도 어떤 입력으로도 값이 갈리지 않는다 — 골든으로 못 잡는
        ///    등가 변이다. 그래서 이 두 행을 합치지 말라는 것은 테스트가 아니라 사람이
        ///    지켜야 한다. 합치는 순간 "목걸이 값을 조정했더니 미지의 장비까지 따라 움직이는"
        ///    결합이 생긴다.
        /// </summary>
        public static EquipmentUpgradeRule Rule(int equipmentTypeIndex)
        {
            switch (equipmentTypeIndex)
            {
                case WeaponIndex:
                    return new EquipmentUpgradeRule { baseGold = 120, goldPerLevel = 52, baseScroll = 3, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case HelmetIndex:
                    return new EquipmentUpgradeRule { baseGold = 95, goldPerLevel = 48, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case ArmorIndex:
                    return new EquipmentUpgradeRule { baseGold = 100, goldPerLevel = 48, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case RingIndex:
                    return new EquipmentUpgradeRule { baseGold = 110, goldPerLevel = 50, baseScroll = 3, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case ShoesIndex:
                    return new EquipmentUpgradeRule { baseGold = 90, goldPerLevel = 46, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                case NecklaceIndex:
                    return new EquipmentUpgradeRule { baseGold = 100, goldPerLevel = 50, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
                default:
                    return new EquipmentUpgradeRule { baseGold = 100, goldPerLevel = 50, baseScroll = 2, scrollPerLevel = 1, baseAttack = 2, attackPerLevel = 3 };
            }
        }

        // ── 강화 비용 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// EquipmentManager.GetUpgradeCost 의 골드 항.
        /// <c>OJMath.Max(0, baseGold + ((OJMath.Max(1, currentLevel) - 1) * goldPerLevel))</c>
        ///
        /// <b>하한이 두 겹이다.</b> 안쪽 <c>OJMath.Max(1, ·)</c> 는 레벨을, 바깥
        /// <c>OJMath.Max(0, ·)</c> 는 비용을 자른다.
        ///
        /// <b>바깥 하한은 "표가 전부 양수라 안 물린다"가 아니다.</b> 곱셈항
        /// <c>(level - 1) * goldPerLevel</c> 이 <b>int 를 넘치면</b> 합이 음수가 되고
        /// 그때 바깥 하한이 실제로 0 으로 자른다. Mono 에서 확인한 첫 지점은
        /// Weapon 이 <c>level = 41,297,762</c>(raw = -2,147,483,604 → 0), default 행이
        /// <c>level = 42,949,672</c> 다. 두루마리·공격력도 같은 이유로 각각
        /// <c>level = 2,147,483,646</c> / <c>level = 715,827,883</c> 에서 물린다.
        /// 그래서 이 하한을 지우는 것은 등가 변형이 아니라 관측 가능한 사양 변경이다 —
        /// <c>EquipmentUpgradeFormulaTests.OuterZeroClampBitesWhenTheLevelTermOverflows</c>
        /// 가 그 지점을 직접 밟는다. 표가 SO 로 올라가 음수 값이 들어올 수 있게 되면
        /// 오버플로 없이도 물리게 된다.
        ///
        /// 원본은 골드와 두루마리를 한 함수에서 계산하면서 <c>level</c> 을 한 번만
        /// 구했다. 여기서는 함수를 둘로 갈랐고 각자 <c>OJMath.Max(1, ·)</c> 를 다시 한다 —
        /// 정수라 두 번 계산해도 같은 값이다(float 였다면 이런 분리가 위험했을 것이다).
        /// </summary>
        public static int UpgradeGoldCost(int baseGold, int goldPerLevel, int currentLevel)
        {
            int level = OJMath.Max(1, currentLevel);
            return OJMath.Max(0, baseGold + ((level - 1) * goldPerLevel));
        }

        /// <summary>
        /// EquipmentManager.GetUpgradeCost 의 두루마리 항.
        /// 골드 항과 <b>같은 모양</b>이지만 합치지 마라 — 두 항은 표에서 서로 다른 열을
        /// 받고, 한쪽만 곡선이 바뀌는 날 합쳐 둔 함수가 양쪽을 같이 움직인다.
        /// </summary>
        public static int UpgradeScrollCost(int baseScroll, int scrollPerLevel, int currentLevel)
        {
            int level = OJMath.Max(1, currentLevel);
            return OJMath.Max(0, baseScroll + ((level - 1) * scrollPerLevel));
        }

        /// <summary>규칙표에서 골드 비용까지 한 번에. 호출부가 표를 두 번 뽑지 않게 하는 편의다.</summary>
        public static int UpgradeGoldCostOf(int equipmentTypeIndex, int currentLevel)
        {
            EquipmentUpgradeRule rule = Rule(equipmentTypeIndex);
            return UpgradeGoldCost(rule.baseGold, rule.goldPerLevel, currentLevel);
        }

        /// <summary>규칙표에서 두루마리 비용까지 한 번에.</summary>
        public static int UpgradeScrollCostOf(int equipmentTypeIndex, int currentLevel)
        {
            EquipmentUpgradeRule rule = Rule(equipmentTypeIndex);
            return UpgradeScrollCost(rule.baseScroll, rule.scrollPerLevel, currentLevel);
        }

        // ── 장비 공격력 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// EquipmentManager.GetEquipmentAttack 의 산출식.
        /// <code>
        ///   if (level &lt;= 1) return 0;
        ///   return OJMath.Max(0, baseAttack + ((level - 1) * attackPerLevel));
        /// </code>
        ///
        /// <b>레벨 1 은 0 이지 baseAttack 이 아니다.</b> 조기 반환을 빼고
        /// <c>OJMath.Max(0, baseAttack + ((level - 1) * attackPerLevel))</c> 만 남기면
        /// 레벨 1 에서 0 대신 2 가 나온다 — 장비 6종이 전부 그러므로 전투력이 통째로
        /// 12 만큼 뛴다. 골든 core.equipUpgrade.attack[*][1] 이 그 변이를 잡는다.
        ///
        /// 조기 반환 조건이 <c>&lt;= 1</c> 이지 <c>&lt; 1</c> 이 아닌 것도 그 자리다.
        /// 레벨이 0 이나 음수로 들어오는 경로는 지금 없다(GetLevel 이 <c>OJMath.Max(1, ·)</c> 로
        /// 이미 자른다). 그래도 조건을 <c>&lt; 1</c> 로 느슨하게 하면 레벨 1 이 2 를 내므로
        /// 위와 같은 사고가 된다.
        /// </summary>
        public static int Attack(int baseAttack, int attackPerLevel, int level)
        {
            if (level <= 1)
                return 0;

            return OJMath.Max(0, baseAttack + ((level - 1) * attackPerLevel));
        }

        /// <summary>규칙표에서 공격력까지 한 번에.</summary>
        public static int AttackOf(int equipmentTypeIndex, int level)
        {
            EquipmentUpgradeRule rule = Rule(equipmentTypeIndex);
            return Attack(rule.baseAttack, rule.attackPerLevel, level);
        }

        // ── 보석 슬롯 해금 ───────────────────────────────────────────────────────────

        /// <summary>
        /// EquipmentManager.GetSlotUnlockLevel. <c>Define.MaxEquipmentSlot</c> 과
        /// <c>Define.EquipmentSlotUnlockLevels</c> 를 <b>인자로</b> 받는다 — Define 은
        /// Assembly-CSharp 이라 여기서 볼 수 없다.
        ///
        /// 분기가 셋이고 <b>지금 하나는 죽어 있다</b>:
        ///   1) 슬롯 범위 밖            → <c>int.MaxValue</c>  (어떤 레벨로도 못 연다는 뜻)
        ///   2) 표에 있는 슬롯          → 표 값
        ///   3) 표에 없는 슬롯          → <c>(slotIndex * 10) + 1</c>
        /// 현재 <c>MaxEquipmentSlot = 5</c> 이고 표 길이도 5 라 3번은 도달하지 않는다.
        /// 표가 짧아지거나 MaxEquipmentSlot 이 늘면 그때 살아난다. 골든은 두 인자를
        /// 키에 박아 넣어서 <b>그 조합까지</b> 밟는다.
        ///
        /// <b><paramref name="unlockLevels"/> 는 방어 복사를 하지 않는다.</b> 원본이
        /// <c>Define.EquipmentSlotUnlockLevels</c> 를 그대로 인덱싱하고, 그 필드는
        /// <c>static readonly int[]</c> 라 <b>참조만 못 바꾸지 내용은 누구나 바꿀 수 있다.</b>
        /// 여기서 복사하면 그 사실이 가려진다 — 지금은 현행 동작 고정이 목적이므로
        /// 원본과 같이 그대로 읽는다. 고치는 것은 표를 SO 로 올리는 단계의 일이다.
        ///
        /// null 을 <c>int.MaxValue</c> 로 처리하지 않는 것에 주의. 표가 null 이면
        /// 2번을 건너뛰고 <b>3번으로 내려간다</b> — 즉 슬롯이 열린다. 그것이 현행 동작이다.
        /// </summary>
        public static int SlotUnlockLevel(int slotIndex, int maxEquipmentSlot, int[] unlockLevels)
        {
            if (slotIndex < 0 || slotIndex >= maxEquipmentSlot)
                return int.MaxValue;

            if (unlockLevels != null && slotIndex < unlockLevels.Length)
                return unlockLevels[slotIndex];

            return (slotIndex * 10) + 1;
        }
    }
}
