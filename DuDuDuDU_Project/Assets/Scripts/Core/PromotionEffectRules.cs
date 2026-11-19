namespace OJ.Core
{
    /// <summary>
    /// 승급 효과의 <b>수치와 순수 규칙</b>. 전투 코드(<c>Hunting</c>)는 여기 상수만 읽는다 —
    /// 수치를 고칠 때 효과 파일 열다섯 개를 뒤지지 않게 한 곳에 모았다.
    ///
    /// 단계(T1~T3) 효과는 <c>PromotionManager.TierOf(dice) &gt;= N</c> 으로 열린다. 레벨 마일스톤과
    /// 별개 축이다(<see cref="DiceMilestoneLevels"/>). 문구·수치는 계획서 부록 A 가 정본이고,
    /// 계획서가 "최종 다듬기"로 남긴 값(<c>TimeBoardChance</c> 등)은 잠정이다.
    /// </summary>
    public static class PromotionEffectRules
    {
        /// <summary>단계마다 곱해지는 비율. T1 1.2 / T2 1.44 / T3 1.728.</summary>
        public const float DamagePerTier = 1.2f;

        /// <summary>승급 단계에 따른 최종 대미지 배수. 단계 0 이면 1.</summary>
        public static float DamageMultiplier(int tier)
        {
            float multiplier = 1f;
            for (int i = 0; i < tier && i < 3; i++)
                multiplier *= DamagePerTier;

            return multiplier;
        }

        // ── 기본 5종 (T1) ───────────────────────────────────────────────
        public const float NormalPierceRatio = 0.5f;
        public const float NormalPierceRadius = 1.4f;

        public const int FireBigExplosionMinTargets = 5;
        public const float FireBigExplosionMultiplier = 1.3f;

        public const float IceFreezeChance = 0.2f;
        public const float IceFreezeSeconds = 0.5f;

        public const float ThunderReturnRatio = 0.5f;

        // ── 기본 5종 Lv30 마일스톤 ──────────────────────────────────────
        /// <summary>파이어 Lv30: 폭발(번진 타) 최종 피해.</summary>
        public const float FireExplosionFinalMultiplier = 1.3f;

        /// <summary>아이스 Lv30: 둔화된 적이 받는 피해 증가(%).</summary>
        public const int IceSlowedDamageTakenPercent = 10;

        /// <summary>썬더 Lv30: 전이 피해.</summary>
        public const float ThunderTransferMultiplier = 1.3f;

        // ── 레어 5종 ───────────────────────────────────────────────────
        public const int TornadoCenterDamageTakenPercent = 20;
        public const float TornadoSlowAfterSeconds = 1.5f;
        public const int TornadoMassPullCount = 5;
        public const float TornadoMassPullCooldownCut = 0.3f;

        public const float StunChainRadius = 1.6f;
        public const float StunDurationMultiplierT2 = 1.5f;

        public const float ArmorBreakExtraChance = 0.2f;
        public const float ArmorBreakExtraRatio = 0.5f;
        public const float ArmorBreakChainRadius = 1.6f;
        public const int ArmorBreakBossMultiplier = 2;

        public const int WindSplashCount = 2;
        public const float WindSplashDistanceRatio = 0.5f;
        public const float WindSplashRadius = 1.2f;
        public const float WindRangeMultiplierT2 = 1.5f;
        public const float WindStunSecondsT3 = 0.3f;

        public const float TimeNextShotMultiplier = 1.3f;
        /// <summary>타임 T3 잠정값(계획서 "잠정"): 공격 시 이 확률로 보드 전체 남은 쿨타임을 줄인다.</summary>
        public const float TimeBoardChance = 0.5f;
        public const float TimeBoardRemainingRatio = 0.5f;

        // ── 킹 5종 ─────────────────────────────────────────────────────
        public const int KingNormalCritExtraRounds = 1;
        public const float KingNormalPierceRadius = 2.5f;
        public const float KingNormalRetargetRadius = 3f;

        public const int KingFireStackPercentPerTarget = 3;
        public const int KingFireStackMaxPercent = 30;
        public const float KingFireCenterMultiplier = 1.5f;

        public const float KingIceSpreadRadius = 1.6f;
        public const int KingIceChainTargetsT2 = 5;
        public const int KingIceChainTargetsBase = 3;
        public const int KingIceBossDamageTakenPercent = 20;
        public const float KingIceBossBonusSeconds = 4f;

        public const float KingPoisonSpreadRadius = 1.6f;
        public const float KingPoisonSlowedMultiplier = 1.3f;
        /// <summary>킹포이즌 T3: 중독된 적이 이만큼 이상이면 모두 둔화. 계획서의 "일정 수"를 잠정 10 으로 둔다.</summary>
        public const int KingPoisonSlowAllThreshold = 10;

        public const float KingThunderMarkSeconds = 6f;
        public const int KingThunderMarkDamageTakenPercent = 20;
        public const int KingThunderEveryNth = 5;
        public const int KingThunderBonusTargets = 3;

        /// <summary>킹파이어 T1 스택. 맞춘 적 수만큼 +3% 씩 쌓고 +30% 에서 멈춘다.</summary>
        public static int KingFireStackAfter(int currentPercent, int hitCount)
        {
            if (hitCount <= 0)
                return currentPercent < 0 ? 0 : currentPercent;

            long next = (long)(currentPercent < 0 ? 0 : currentPercent) + (long)hitCount * KingFireStackPercentPerTarget;
            return next > KingFireStackMaxPercent ? KingFireStackMaxPercent : (int)next;
        }

        /// <summary>n 번째마다 켜지는 효과인가(1부터 센다). 0 이하의 n 은 항상 false.</summary>
        public static bool IsEveryNth(int count, int n)
        {
            return n > 0 && count > 0 && count % n == 0;
        }
    }
}
