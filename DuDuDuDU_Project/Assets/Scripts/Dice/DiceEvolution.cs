using System.Collections.Generic;

namespace OJ.Dice
{
    /// <summary>
    /// 다이스가 어느 단계에 있는가. 조합식을 걷어내고 남은 유일한 등급 개념이다.
    ///
    /// 예전에는 이 구분이 세 군데에 흩어져 있었다 — <c>isMythic</c> 플래그(킹만),
    /// <c>summonable</c>(특수·킹), 그리고 <c>DiceType</c> 의 숫자 구간(0/100/200).
    /// 셋이 같은 것을 다르게 말해서, 특수 다이스를 "소환 안 되는 것" 으로 부를 수밖에 없었다.
    /// 진화는 단계를 <b>올리는</b> 동작이라 단계 자체에 이름이 필요하다.
    /// </summary>
    public enum DiceTier
    {
        Base,
        Special,
        King,
    }

    /// <summary>
    /// 진화·교환의 규칙표. (조합식 폐기)
    ///
    /// <b>왜 조합식을 버렸는가.</b> 예전 구조는 <c>DiceMetaDataDatabase</c> 의
    /// <c>recipeMaterials</c> 에 "★2 재료 2종을 2개씩" 같은 식을 적어 두고, 보드에서 그
    /// 재료가 다 모였을 때만 상위 다이스를 만들어 줬다. 재료가 <b>두 종류</b>라 유저는
    /// 원하는 상위 다이스 하나를 얻으려고 서로 다른 두 계통을 동시에 키워야 했고,
    /// 그것이 게임을 시작하기 전에 넘어야 하는 허들이 됐다.
    ///
    /// 이제는 <b>한 줄기다.</b> 기본 다이스를 4성까지 머지하면 그 다이스 하나만으로
    /// 재화를 내고 다음 단계로 올라간다. 재료 조합이 없으므로 배선도 1:1 이다.
    ///
    /// <code>
    ///   Normal  → Tornado    → KingNormal
    ///   Fire    → ArmorBreak → KingFire
    ///   Ice     → Wind       → KingIce
    ///   Thunder → Time       → KingThunder
    ///   Poison  → Stun       → KingPoison
    /// </code>
    ///
    /// <b>이 배선은 새로 지어낸 것이 아니다.</b> 킹 5종의 옛 조합식이 이미
    /// "기본 4성 + 대응 특수 1개" 였고, 그 짝이 곧 위 표다. 즉 킹으로 가는 길은
    /// 한 글자도 바뀌지 않았고, 사라진 것은 특수 다이스를 <b>만드는</b> 쪽의
    /// 2종 조합뿐이다. 덕분에 원소도 계통마다 하나로 떨어진다 —
    /// <see cref="DiceMetaDataProvider.GetBaseElementType"/> 참조.
    /// </summary>
    public static class DiceEvolution
    {
        /// <summary>
        /// 진화(상위 단계로) 비용. 재화는 <see cref="PointType.BattleEnhanceStone"/> 하나다.
        ///
        /// <b>비용 네 개는 전부 여기 있다.</b> 흩뿌려 두면 수급이 바뀔 때 한쪽만 고치게 된다.
        /// 지금 수급은 웨이브 클리어당 +1(<c>GameManager.HandleWaveCompleted</c>)이고
        /// 스테이지가 15웨이브라 한 판 예산이 15 안팎이다. 같은 재화를 속성강화
        /// (<c>ElementUpgradeManager.GetNextUpgradeCost</c>, 1·2·3…)가 함께 쓴다.
        /// 즉 <b>지금 값은 수급 대비 비싸다</b> — 현상금 시스템이 들어와 수급이 늘면
        /// 이 네 상수만 만지면 된다.
        /// </summary>
        public const int BaseEvolveCost = 10;

        /// <summary>기본 다이스를 같은 성급의 다른 기본 다이스로 바꾼다.</summary>
        public const int BaseExchangeCost = 5;

        /// <summary>특수 다이스를 대응하는 킹으로 올린다.</summary>
        public const int SpecialEvolveCost = 20;

        /// <summary>특수 다이스를 다른 특수 다이스로 바꾼다.</summary>
        public const int SpecialExchangeCost = 10;

        /// <summary>
        /// 진화가 열리는 성급. 기본 다이스는 <b>여기까지 머지해야</b> 진화할 수 있다.
        /// 특수 다이스는 성급 개념이 없어(<c>showStarUI = false</c>, 항상 1) 이 조건을 안 본다.
        /// </summary>
        public const int EvolveRequiredStar = MergeSystem.MaxStar;

        private static readonly DiceType[] baseTypes =
        {
            DiceType.Normal,
            DiceType.Fire,
            DiceType.Ice,
            DiceType.Thunder,
            DiceType.Poison,
        };

        private static readonly DiceType[] specialTypes =
        {
            DiceType.Tornado,
            DiceType.ArmorBreak,
            DiceType.Wind,
            DiceType.Time,
            DiceType.Stun,
        };

        private static readonly DiceType[] kingTypes =
        {
            DiceType.KingNormal,
            DiceType.KingFire,
            DiceType.KingIce,
            DiceType.KingThunder,
            DiceType.KingPoison,
        };

        /// <summary>
        /// 진화 배선. 위 세 배열과 <b>순서로 짝지어져 있다</b> — 배열 하나만 고치면 어긋난다.
        /// 그래서 순서에 기대지 않고 표를 따로 적는다.
        /// </summary>
        private static readonly Dictionary<DiceType, DiceType> evolveMap = new Dictionary<DiceType, DiceType>
        {
            { DiceType.Normal, DiceType.Tornado },
            { DiceType.Fire, DiceType.ArmorBreak },
            { DiceType.Ice, DiceType.Wind },
            { DiceType.Thunder, DiceType.Time },
            { DiceType.Poison, DiceType.Stun },

            { DiceType.Tornado, DiceType.KingNormal },
            { DiceType.ArmorBreak, DiceType.KingFire },
            { DiceType.Wind, DiceType.KingIce },
            { DiceType.Time, DiceType.KingThunder },
            { DiceType.Stun, DiceType.KingPoison },
        };

        public static IReadOnlyList<DiceType> BaseTypes => baseTypes;
        public static IReadOnlyList<DiceType> SpecialTypes => specialTypes;
        public static IReadOnlyList<DiceType> KingTypes => kingTypes;

        public static DiceTier GetTier(DiceType diceType)
        {
            // 숫자 구간으로 판정한다. Define.cs 의 DiceType 이 0/100/200 으로 끊어 놓은 것이
            // 원래 이 뜻이었고, 다른 판정 근거(isMythic·summonable)는 에셋에서 오므로
            // 에셋이 비면 조용히 답이 바뀐다. 단계는 그래선 안 된다.
            int value = (int)diceType;
            if (value >= (int)DiceType.KingNormal)
                return DiceTier.King;
            if (value >= (int)DiceType.Tornado)
                return DiceTier.Special;
            return DiceTier.Base;
        }

        /// <summary>
        /// 이 다이스가 지금 진화할 수 있는가. 성급 조건까지 본다.
        /// 킹은 최종 단계라 언제나 false 다.
        /// </summary>
        public static bool CanEvolve(DiceType diceType, int star)
        {
            if (!evolveMap.ContainsKey(diceType))
                return false;

            if (GetTier(diceType) == DiceTier.Base && star < EvolveRequiredStar)
                return false;

            return true;
        }

        public static bool TryGetEvolveTarget(DiceType diceType, out DiceType target)
        {
            return evolveMap.TryGetValue(diceType, out target);
        }

        /// <summary>
        /// 진화 비용. 진화할 수 없는 다이스면 0 — 호출부는 <see cref="CanEvolve"/> 로 먼저 거른다.
        /// </summary>
        public static int GetEvolveCost(DiceType diceType)
        {
            switch (GetTier(diceType))
            {
                case DiceTier.Base:
                    return BaseEvolveCost;
                case DiceTier.Special:
                    return SpecialEvolveCost;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 교환 비용. 킹은 교환 대상이 아니다 — 다섯 종이 전부 최종이라
        /// "다른 것으로 바꾼다"가 등가 교환이 아니라 계통 갈아타기가 되고,
        /// 그러면 재료로 쓴 4성 머지 전부가 재화 10개로 되사지는 셈이 된다.
        /// </summary>
        public static int GetExchangeCost(DiceType diceType)
        {
            switch (GetTier(diceType))
            {
                case DiceTier.Base:
                    return BaseExchangeCost;
                case DiceTier.Special:
                    return SpecialExchangeCost;
                default:
                    return 0;
            }
        }

        public static bool CanExchange(DiceType diceType)
        {
            return GetTier(diceType) != DiceTier.King;
        }

        /// <summary>
        /// 같은 단계의 <b>다른</b> 다이스를 하나 고른다. 자기 자신은 후보에서 빠지므로
        /// 재화를 내고 제자리에 남는 일이 없다.
        ///
        /// 난수는 호출부가 넘긴다. <c>UnityEngine.Random</c> 을 여기서 직접 부르면
        /// 이 클래스가 엔진에 묶여 테스트에서 두드릴 수 없게 된다.
        /// </summary>
        public static bool TryGetExchangeCandidates(DiceType diceType, List<DiceType> buffer)
        {
            if (buffer == null)
                return false;

            buffer.Clear();

            DiceType[] pool;
            switch (GetTier(diceType))
            {
                case DiceTier.Base:
                    pool = baseTypes;
                    break;
                case DiceTier.Special:
                    pool = specialTypes;
                    break;
                default:
                    return false;
            }

            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] == diceType)
                    continue;
                buffer.Add(pool[i]);
            }

            return buffer.Count > 0;
        }
    }
}
