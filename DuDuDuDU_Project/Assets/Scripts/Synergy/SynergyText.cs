using OJ.Core;

namespace OJ.Synergy
{
    /// <summary>
    /// 시너지 문구. <b>UI 를 참조하지 않는 별도 클래스다</b> — 뷰 안에 두면 헤드리스 러너가
    /// UnityEngine.UI 를 못 불러 영영 테스트 밖에 남는다(<c>OJ.Shop.ShopText</c> 와 같은 이유).
    /// </summary>
    public static class SynergyText
    {
        public static string CategoryName(SynergyCategory category)
        {
            switch (category)
            {
                case SynergyCategory.Dice: return "다이스";
                case SynergyCategory.Combat: return "전투";
                case SynergyCategory.Action: return "행동";
                case SynergyCategory.Unified: return "통합";
                default: return category.ToString();
            }
        }

        /// <summary>속성 이름. 사용자 시안이 번개 계열을 "전기속성" 이라 불러 그 말을 따른다.</summary>
        public static string ElementName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Normal: return "노멀";
                case ElementType.Fire: return "불";
                case ElementType.Water: return "얼음";
                case ElementType.Light: return "전기";
                case ElementType.Dark: return "독";
                default: return element.ToString();
            }
        }

        public static string DiceName(DiceType dice)
        {
            switch (dice)
            {
                case DiceType.Normal: return "노멀";
                case DiceType.Fire: return "불";
                case DiceType.Ice: return "얼음";
                case DiceType.Thunder: return "번개";
                case DiceType.Poison: return "독";
                case DiceType.Tornado: return "토네이도";
                case DiceType.Stun: return "스턴";
                case DiceType.ArmorBreak: return "아머브레이크";
                case DiceType.Wind: return "윈드";
                case DiceType.Time: return "타임";
                case DiceType.KingNormal: return "킹 노멀";
                case DiceType.KingFire: return "킹 불";
                case DiceType.KingIce: return "킹 얼음";
                case DiceType.KingThunder: return "킹 번개";
                case DiceType.KingPoison: return "킹 독";
                default: return dice.ToString();
            }
        }

        public static string TargetName(SynergyTargetKind kind, int target)
        {
            switch (kind)
            {
                case SynergyTargetKind.Element: return ElementName((ElementType)target);
                case SynergyTargetKind.Dice: return DiceName((DiceType)target);
                default: return string.Empty;
            }
        }

        /// <summary>줄 하나의 문구. 빈 줄이면 빈 문자열.</summary>
        public static string Describe(SynergyLine line, bool condition)
        {
            SynergyOption option = SynergyRules.Resolve(line, condition);
            if (option == null)
                return string.Empty;

            return SynergyRules.Describe(option, line.Value, TargetName(option.Target, line.Target));
        }

        /// <summary>옵션의 범위 문구. 확률표에 쓴다. 예: "SP 300~120 소모 시".</summary>
        public static string DescribeRange(SynergyOption option)
        {
            string range = option.Worst == option.Best ? option.Worst.ToString() : option.Worst + "~" + option.Best;
            string target = option.Target == SynergyTargetKind.Element ? "(속성)"
                : option.Target == SynergyTargetKind.Dice ? "(다이스)"
                : string.Empty;

            return option.Format
                .Replace("{v}", range)
                .Replace("{t}", target)
                .Replace("{s}", option.Seconds.ToString("0.#"))
                .Replace("{c}", option.MaxStacks.ToString())
                .Replace("{n}", option.Hits.ToString());
        }

        /// <summary>
        /// 대상을 저장용 이름으로. enum 이름으로 저장해야 enum 번호가 바뀌어도 안 깨진다
        /// (<c>SaveState</c> 클래스 주석).
        /// </summary>
        public static string TargetToSave(SynergyTargetKind kind, int target)
        {
            switch (kind)
            {
                case SynergyTargetKind.Element: return ((ElementType)target).ToString();
                case SynergyTargetKind.Dice: return ((DiceType)target).ToString();
                default: return string.Empty;
            }
        }

        public static int TargetFromSave(SynergyTargetKind kind, string saved)
        {
            switch (kind)
            {
                case SynergyTargetKind.Element:
                    return System.Enum.TryParse(saved, out ElementType element) ? (int)element : 0;
                case SynergyTargetKind.Dice:
                    return System.Enum.TryParse(saved, out DiceType dice) ? (int)dice : 0;
                default:
                    return -1;
            }
        }
    }
}
