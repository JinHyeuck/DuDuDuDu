using OJ.Core;

namespace OJ.Dice
{
    /// <summary>
    /// 승급 효과·미션의 표시 문구. <b>문구의 정본은 계획서 부록 A</b>이고 수치는 <c>PromotionEffectRules</c> 와
    /// 같아야 한다 — 효과 수치를 고치면 여기 문장도 같이 고칠 것(문자열은 컴파일러가 검사하지 않는다).
    ///
    /// 승급 단계마다 최종 대미지 ×1.2 는 모든 다이스 공통이라 효과 문장에 적지 않고 <see cref="CommonLine"/> 으로 따로 보인다.
    /// </summary>
    public static class PromotionText
    {
        /// <summary>모든 승급이 공통으로 주는 것.</summary>
        public const string CommonLine = "승급할 때마다 최종 대미지 ×1.2";

        /// <summary>그 다이스의 그 단계(1~3) 승급 효과 문장. 없는 단계면 빈 문자열.</summary>
        public static string Describe(DiceType diceType, int tier)
        {
            switch (diceType)
            {
                // ── 기본 5종 (T1 만) ──
                case DiceType.Normal:
                    return tier == 1 ? "공격이 대상을 관통해 뒤의 적 1명에게 50% 피해" : string.Empty;
                case DiceType.Fire:
                    return tier == 1 ? "폭발에 맞은 적이 5마리 이상이면 폭발 피해 30% 증가" : string.Empty;
                case DiceType.Ice:
                    return tier == 1 ? "둔화된 적에게 명중하면 20% 확률로 0.5초 빙결" : string.Empty;
                case DiceType.Poison:
                    return tier == 1 ? "이미 중독된 적에게 다시 명중하면 중독 틱이 즉시 1회 발동하고 지속시간 갱신" : string.Empty;
                case DiceType.Thunder:
                    return tier == 1 ? "전이가 끝나면 번개가 처음 대상에게 되돌아와 50% 피해" : string.Empty;

                // ── 레어 5종 ──
                case DiceType.Tornado:
                    return Pick(tier,
                        "흡입 중 중심의 적이 받는 피해 20% 증가",
                        "흡입이 끝난 적에게 1.5초 둔화",
                        "흡입한 적이 5마리 이상이면 쿨타임 30% 단축");
                case DiceType.Stun:
                    return Pick(tier,
                        "기절한 적이 죽으면 주변 적 1명 기절",
                        "기절 지속시간 50% 증가",
                        "기절 중인 적 처치 시 자신의 쿨타임 즉시 초기화");
                case DiceType.ArmorBreak:
                    return Pick(tier,
                        "방깎이 걸린 적에게 명중하면 20% 확률로 방어 무시 추가 일격(피해 50%)",
                        "방깎 상태의 적이 죽으면 주변 적에게 방깎 전이",
                        "보스에게 방깎 효과 2배");
                case DiceType.Wind:
                    return Pick(tier,
                        "밀려난 적 주변의 적 2명도 절반 거리로 함께 밀림",
                        "밀치기 범위(벽 앞 띠 길이) 50% 확대",
                        "밀치기 직후 짧은 기절");
                case DiceType.Time:
                    return Pick(tier,
                        "쿨타임을 당길 대상으로 가장 느린 다이스를 우선",
                        "당겨진 다이스의 다음 공격 대미지 30% 증가",
                        "공격 시 50% 확률로 보드 모든 다이스의 남은 쿨타임 50% 단축");

                // ── 킹 5종 ──
                case DiceType.KingNormal:
                    return Pick(tier,
                        "4연타의 마지막 타가 관통",
                        "처치되면 남은 타가 다음 적에게 이어짐",
                        "크리티컬이 발생하면 4연타가 한 번 더");
                case DiceType.KingFire:
                    return Pick(tier,
                        "폭발로 맞춘 적 1마리당 다음 폭발 피해 +3% (최대 +30%, 웨이브 동안 유지)",
                        "폭발 중심에 맞은 적은 폭발 피해 50% 증가",
                        "웨이브당 1회, 화면 전체 적에게 같은 피해");
                case DiceType.KingIce:
                    return Pick(tier,
                        "둔화된 적이 죽으면 주변 적에게 둔화 전이",
                        "연쇄 타격 대상 3명 → 5명",
                        "빙결된 보스가 정지가 풀린 뒤 일정 시간 받는 피해 증가");
                case DiceType.KingPoison:
                    return Pick(tier,
                        "중독된 적이 죽으면 주변 적의 중독이 갱신되고 남은 시간 연장",
                        "중독과 둔화가 동시에 걸린 적은 중독 피해 30% 증가",
                        "중독된 적이 일정 수 이상이면 모두 둔화");
                case DiceType.KingThunder:
                    return Pick(tier,
                        "전이 대상에게 감전 표식, 표식을 가진 적이 죽으면 번개 재전이",
                        "표식 대상에게 번개 피해 20% 증가",
                        "5번째 공격마다 전이 대상 +3");
            }

            return string.Empty;
        }

        private static string Pick(int tier, string t1, string t2, string t3)
        {
            switch (tier)
            {
                case 1: return t1;
                case 2: return t2;
                case 3: return t3;
                default: return string.Empty;
            }
        }

        /// <summary>미션 A(보유형) 이름. 목표값은 "가진 값 / 하한" 으로 같이 보인다.</summary>
        public static string OwnedName(PromotionOwned kind)
        {
            switch (kind)
            {
                case PromotionOwned.EquipmentLevels: return "장비 레벨 합";
                case PromotionOwned.RelicLevels: return "유물 레벨 합";
                case PromotionOwned.GemScore: return "보석 점수";
                default: return kind.ToString();
            }
        }

        /// <summary>미션 이름. 화면 목록의 한 줄.</summary>
        public static string MissionName(PromotionMission mission)
        {
            switch (mission)
            {
                case PromotionMission.PinballShots: return "핀볼 발수 소모";
                case PromotionMission.SynergyTickets: return "시너지 티켓 소모";
                case PromotionMission.Sweeps: return "소탕 횟수";
                case PromotionMission.TowerChallenges: return "무한의 탑 도전";
                case PromotionMission.StageChallenges: return "스테이지 도전";
                case PromotionMission.MonsterKills: return "몬스터 처치";
                case PromotionMission.BossKills: return "보스 처치";
                case PromotionMission.EventCurrency: return "이벤트 재화 소모";
                default: return mission.ToString();
            }
        }
    }
}
