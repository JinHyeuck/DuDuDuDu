using System.Text;

namespace OJ.Equipment
{
    public static class UIEquipmentEffectTextFormatter
    {
        public static string BuildGemDescription(GemDefinition definition)
        {
            if (definition == null || definition.effects == null || definition.effects.Count == 0)
                return "효과 없음";

            StringBuilder sb = new StringBuilder(128);
            for (int i = 0; i < definition.effects.Count; i++)
            {
                if (i > 0)
                    sb.Append(" / ");

                sb.Append(BuildEffectText(definition.effects[i]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// 첫 효과를 이름과 수치로 나눈다("공격력 +4%" → "공격력", "+4%").
        /// 강화 팝업의 장착 보석 줄이 둘을 다른 색·자리에 그린다.
        /// </summary>
        public static void SplitFirstEffect(GemDefinition definition, out string label, out string value)
        {
            if (definition == null || definition.effects == null || definition.effects.Count == 0)
            {
                label = "효과 없음";
                value = string.Empty;
                return;
            }

            SplitEffectText(BuildEffectText(definition.effects[0]), out label, out value);
        }

        /// <summary>모든 효과를 나눠 줄바꿈으로 잇는다. 이름 줄과 수치 줄의 순서가 맞물린다.</summary>
        public static void SplitEffects(GemDefinition definition, out string labels, out string values)
        {
            if (definition == null || definition.effects == null || definition.effects.Count == 0)
            {
                labels = "효과 없음";
                values = string.Empty;
                return;
            }

            StringBuilder labelBuilder = new StringBuilder(64);
            StringBuilder valueBuilder = new StringBuilder(32);
            for (int i = 0; i < definition.effects.Count; i++)
            {
                if (i > 0)
                {
                    labelBuilder.Append('\n');
                    valueBuilder.Append('\n');
                }

                SplitEffectText(BuildEffectText(definition.effects[i]), out string label, out string value);
                labelBuilder.Append(label);
                valueBuilder.Append(value);
            }

            labels = labelBuilder.ToString();
            values = valueBuilder.ToString();
        }

        /// <summary>마지막 " +" / " -" 앞뒤로 자른다. 수치가 없는 문구는 이름만 남는다.</summary>
        public static void SplitEffectText(string text, out string label, out string value)
        {
            text = text ?? string.Empty;
            int index = System.Math.Max(text.LastIndexOf(" +", System.StringComparison.Ordinal),
                                        text.LastIndexOf(" -", System.StringComparison.Ordinal));
            if (index < 0)
            {
                label = text;
                value = string.Empty;
                return;
            }

            label = text.Substring(0, index);
            value = text.Substring(index + 1);
        }

        public static string BuildEffectText(GemEffect effect)
        {
            if (effect == null)
                return "효과 없음";

            string target = BuildTargetText(effect);
            switch (effect.statType)
            {
                case GemStatType.AttackPercent:
                    return $"{target}공격력 +{effect.percentValue * 100f:0.#}%";
                case GemStatType.AttackFlat:
                    return $"{target}공격력 +{effect.flatValue}";
                case GemStatType.CooldownReducePercent:
                    return $"{target}쿨다운 -{effect.percentValue * 100f:0.#}%";
                case GemStatType.FirstNWavesDamageFlat:
                    return $"처음 {effect.intParam}웨이브 피해 +{effect.flatValue}";
                case GemStatType.FireExplosionRangePercent:
                    return $"화염 범위 +{effect.percentValue * 100f:0.#}%";
                case GemStatType.WellHpOnKill:
                    return $"처치 시 Well HP +{effect.flatValue}";
                case GemStatType.FinalDamagePercent:
                    return $"{target}최종 피해 +{effect.percentValue * 100f:0.#}%";
                case GemStatType.FireExplosionTargetCountFlat:
                    return $"화염 추가 타겟 +{effect.flatValue}";
                case GemStatType.ThunderChainCountFlat:
                    return $"번개 체인 수 +{effect.flatValue}";
                case GemStatType.GoldOnKill:
                    return $"처치 시 골드 +{effect.flatValue}";
                default:
                    return effect.statType.ToString();
            }
        }

        private static string BuildTargetText(GemEffect effect)
        {
            if (effect.targetDiceType != DiceType.Max)
                return $"[{effect.targetDiceType}] ";
            if (effect.targetElementType != ElementType.Max)
                return $"[{effect.targetElementType}] ";
            return string.Empty;
        }
    }
}
