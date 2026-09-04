using System.Text;
using OJ.DI;

namespace OJ.Dice
{
    /// <summary>
    /// 다이스의 효과를 사람이 읽는 한 줄로 만든다. <b>숫자는 전부 실제 공식에서 뽑는다.</b>
    ///
    /// <b>왜 이 파일이 따로 있는가.</b> 같은 문구를 두 화면이 필요로 한다 —
    /// 전투 중의 작은 상세창(<see cref="UIBattleDiceDetailPanel"/>)과 로비의 성장 화면
    /// (<see cref="UIDiceGrowthDetailPanel"/>). 둘이 각자 적으면 한쪽만 고쳐지고, 그때
    /// 갈라지는 것은 문장이 아니라 <b>숫자</b>다. 이 코드베이스는 그 사고를 이미 겪었다:
    /// 옛 전투 상세창이 연쇄 타격 수를 자기 자리에서 다시 계산했고, 그 복사본이 유물
    /// 보정을 빼먹어 '피뢰침 고리'를 껴도 화면 숫자가 그대로였다.
    ///
    /// <b>그래서 공식을 베끼지 않고 부른다.</b> 여기 있는 것은 조립뿐이고 값은 전부
    /// <see cref="DiceMetaDataProvider"/>·<see cref="OJ.Hunting.AttackContent"/> 가 준다.
    ///
    /// <b>킹은 대부분 접지 않는다.</b> 자세한 이유는 <see cref="AppendBaseTrait"/> 참조.
    ///
    /// <b>에셋의 description 과 역할이 다르다.</b> 그쪽은 "이 다이스가 무엇을 하는가"를
    /// 문장으로 말하고 <b>수치를 담지 않는다</b> — 담으면 밸런스를 만질 때마다 조용히
    /// 낡는다(실제로 킹 4종의 설명이 그렇게 낡아 있었다). 수치는 여기가 맡는다.
    /// </summary>
    public static class DiceTraitText
    {
        /// <summary>
        /// 전투 중 카드에 들어갈 <b>짧은</b> 한 줄. 폭이 좁아 이름과 수치만 남긴다.
        /// </summary>
        public static string Short(DiceType diceType, int level, IBattleRefs battle)
        {
            return Build(diceType, level, battle, verbose: false);
        }

        /// <summary>
        /// 로비 성장 화면에 들어갈 <b>자세한</b> 줄. 여러 효과가 있으면 줄을 나눈다.
        /// </summary>
        public static string Detailed(DiceType diceType, int level, IBattleRefs battle)
        {
            return Build(diceType, level, battle, verbose: true);
        }

        private static string Build(DiceType diceType, int level, IBattleRefs battle, bool verbose)
        {
            var builder = new StringBuilder();

            switch (diceType)
            {
                case DiceType.Tornado:
                    builder.Append(verbose
                        ? "흡입: 주변의 적을 명중 지점으로 끌어당긴다"
                        : "주변 적 끌어당김");
                    break;

                case DiceType.Stun:
                    Append(builder, verbose, "기절",
                        "{0:0.#}% 확률로 적을 멈춰 세운다",
                        "{0:0.#}%",
                        DiceMetaDataProvider.GetStunChancePercent(level));
                    break;

                case DiceType.ArmorBreak:
                    Append(builder, verbose, "방어 감소",
                        "적의 방어력을 {0}% 낮춘다 (4.0초)",
                        "-{0}% (4.0초)",
                        DiceMetaDataProvider.GetArmorBreakPercent(level));
                    break;

                case DiceType.Wind:
                    // 바람은 총알을 쏘지 않고 벽 앞의 띠를 통째로 친다. 그래서 '대상 수'가
                    // 곧 동시 타격 수다 — 다른 다이스의 '추가 대상'과 뜻이 다르니 문구도 다르다.
                    if (verbose)
                    {
                        builder.AppendFormat("동시 타격: 벽 앞의 적 최대 {0}명",
                            DiceMetaDataProvider.GetWindTargetCount(level));
                        builder.AppendLine();
                        builder.AppendFormat("밀쳐내기: {0:0.#}% 확률",
                            DiceMetaDataProvider.GetWindPushChancePercent(diceType, level));
                    }
                    else
                    {
                        builder.AppendFormat("{0}명 타격 · 밀쳐내기 {1:0.#}%",
                            DiceMetaDataProvider.GetWindTargetCount(level),
                            DiceMetaDataProvider.GetWindPushChancePercent(diceType, level));
                    }
                    break;

                case DiceType.Time:
                    if (verbose)
                    {
                        builder.AppendFormat("쿨타임 당기기: 다른 다이스 {0}개의 남은 쿨타임 {1:0.#}% 감소",
                            DiceMetaDataProvider.GetTimeTargetCount(level),
                            DiceMetaDataProvider.GetTimeCooldownReducePercent(diceType, level));
                    }
                    else
                    {
                        builder.AppendFormat("쿨타임 -{0:0.#}% · 랜덤 {1}개",
                            DiceMetaDataProvider.GetTimeCooldownReducePercent(diceType, level),
                            DiceMetaDataProvider.GetTimeTargetCount(level));
                    }
                    break;

                // ── 킹 5종 ────────────────────────────────────────────────────────
                //
                // <b>계통 기본형으로 접으면 안 되는 넷이 있다.</b> 접기(AppendBaseTrait)는
                // "효과의 종류가 같고 세기만 다르다"가 성립할 때만 옳은데, 킹은 기본형에
                // 없는 동작을 하나씩 더 갖고 있다 — 킹노말의 3연타, 킹파이어의 강화 폭발,
                // 킹아이스의 기절, 킹포이즌의 둔화 동시 부여. 접힌 채로 두면 킹노말이
                // "단일 대상"이라고 말한다.
                //
                // 킹썬더만 아래 접기로 내려보낸다. 그쪽은 정말로 연쇄 하나뿐이고,
                // 전용 가산까지 GetThunderChainCount 가 이미 알고 있다.
                case DiceType.KingNormal:
                    builder.Append(verbose
                        ? "연타: 적과 그 주변 최대 3명을 0.2초 간격으로 세 번 더 타격"
                        : "주변 3명 3연타");
                    break;

                case DiceType.KingFire:
                    builder.Append(verbose
                        ? "강화 폭발: 일반 폭발보다 넓은 범위의 적에게 같은 피해"
                        : "강화 폭발");
                    break;

                case DiceType.KingIce:
                    if (verbose)
                    {
                        builder.Append("연쇄 타격: 적과 그 주변 최대 3명");
                        builder.AppendLine();
                        builder.AppendFormat("둔화: 적의 이동 속도를 {0:0.#}초 동안 낮춘다",
                            DiceMetaDataProvider.GetSlowDuration(diceType, level));
                        if (level >= 9)
                        {
                            builder.AppendLine();
                            builder.Append("빙결: 명중한 적을 1.0초 동안 멈춰 세운다");
                        }
                    }
                    else
                    {
                        builder.AppendFormat("주변 3명 · 둔화 {0:0.#}초",
                            DiceMetaDataProvider.GetSlowDuration(diceType, level));
                    }
                    break;

                case DiceType.KingPoison:
                    if (verbose)
                    {
                        builder.AppendFormat(
                            "중독: 0.5초마다 남은 체력의 10% x {0:0.##} 피해 ({1:0.#}초)",
                            DiceMetaDataProvider.GetPoisonDamageMultiplier(diceType, level),
                            DiceMetaDataProvider.GetPoisonDuration(diceType));
                        builder.AppendLine();
                        builder.AppendFormat("둔화: 적의 이동 속도를 {0:0.#}초 동안 낮춘다",
                            DiceMetaDataProvider.GetSlowDuration(diceType, level));
                    }
                    else
                    {
                        builder.AppendFormat("중독 {0:0.#}초 · 둔화 {1:0.#}초",
                            DiceMetaDataProvider.GetPoisonDuration(diceType),
                            DiceMetaDataProvider.GetSlowDuration(diceType, level));
                    }
                    break;

                default:
                    AppendBaseTrait(builder, diceType, level, battle, verbose);
                    break;
            }

            return builder.ToString();
        }

        /// <summary>
        /// 기본 5종, 그리고 <b>접어도 되는 킹 하나</b>(킹썬더).
        ///
        /// 접기가 옳은 조건은 "효과의 종류가 같고 세기만 다를 것"이다. 킹썬더는 정말로
        /// 연쇄 하나뿐이고 그 세기를 <see cref="GetThunderChainCount"/> 가 이미 안다.
        /// 나머지 킹 넷은 기본형에 없는 동작을 갖고 있어 위에서 따로 적는다 —
        /// 접었다가 킹노말이 "단일 대상"이라고 말하는 일이 실제로 있었다.
        /// </summary>
        private static void AppendBaseTrait(
            StringBuilder builder, DiceType diceType, int level, IBattleRefs battle, bool verbose)
        {
            DiceType baseType = DiceMetaDataProvider.GetBaseElementType(diceType);

            switch (baseType)
            {
                case DiceType.Fire:
                    builder.Append(verbose
                        ? "폭발: 명중 지점 주변의 적에게 같은 피해"
                        : "주변 폭발");
                    break;

                case DiceType.Ice:
                    Append(builder, verbose, "둔화",
                        "적의 이동 속도를 {0:0.#}초 동안 낮춘다",
                        "{0:0.#}초",
                        DiceMetaDataProvider.GetSlowDuration(diceType, level));
                    break;

                case DiceType.Thunder:
                    Append(builder, verbose, "연쇄",
                        "번개가 주변 {0}명에게 추가로 전이된다",
                        "+{0}명",
                        GetThunderChainCount(diceType, level, battle));
                    break;

                case DiceType.Poison:
                    // 중독은 '남은 체력의 10%'라 절대 수치를 여기서 말할 수 없다.
                    // 지속 시간과 배수만 말하고, 실제 틱 피해는 전투 화면이 몬스터를
                    // 알고 있을 때만 계산할 수 있다.
                    if (verbose)
                    {
                        builder.AppendFormat(
                            "중독: 0.5초마다 남은 체력의 10% x {0:0.##} 피해 ({1:0.#}초)",
                            DiceMetaDataProvider.GetPoisonDamageMultiplier(diceType, level),
                            DiceMetaDataProvider.GetPoisonDuration(diceType));
                    }
                    else
                    {
                        builder.AppendFormat("중독 {0:0.#}초",
                            DiceMetaDataProvider.GetPoisonDuration(diceType));
                    }
                    break;

                default:
                    builder.Append(verbose ? "단일 대상을 공격한다" : "단일 대상");
                    break;
            }
        }

        /// <summary>
        /// 연쇄 타격 수. <b>전투가 살아 있으면 전투와 같은 함수를 부른다</b> —
        /// 그쪽만이 장비·유물 보정을 더한다. 로비에서는 그 보정이 붙지 않는 것이 맞다
        /// (전투 밖에서는 유물 자체가 없다).
        /// </summary>
        private static int GetThunderChainCount(DiceType diceType, int level, IBattleRefs battle)
        {
            int count = battle != null && battle.IsActive
                ? battle.Attack.GetThunderTargetCount(DiceType.Thunder)
                : DiceMetaDataProvider.GetThunderTargetCount(level);

            // 킹 썬더 전용 가산. 효과 쪽(KingThunderDiceEffect)에 상수로 박혀 있어
            // 옮길 자리가 없다 — 두 곳이 갈리지 않게 값만 여기 적고 근거를 남긴다.
            if (diceType == DiceType.KingThunder)
            {
                count += 2;
                if (level >= 3)
                    count += 2;
            }

            return count;
        }

        private static void Append(
            StringBuilder builder, bool verbose, string label,
            string verboseFormat, string shortFormat, object value)
        {
            if (verbose)
            {
                builder.Append(label).Append(": ");
                builder.AppendFormat(verboseFormat, value);
            }
            else
            {
                builder.Append(label).Append(' ');
                builder.AppendFormat(shortFormat, value);
            }
        }
    }
}
