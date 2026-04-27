using System.Text;

namespace OJ
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
