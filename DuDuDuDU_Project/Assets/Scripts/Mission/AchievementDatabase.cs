using System.Collections.Generic;
using UnityEngine;
using OJ.Core;
using OJ.Point;

namespace OJ.Mission
{
    /// <summary>
    /// 업적의 정본. 계열 17개가 단계별로 펼쳐져 한 목록에 들어 있다.
    /// (AGENTS 확정사항 3 — 밸런스 수치는 SO 로 올린다)
    ///
    /// <b>계열은 필드가 아니라 <see cref="AchievementDefinition.CounterKey"/> 로 묶인다.</b>
    /// 같은 카운터를 보는 것들이 곧 한 계열이고, <see cref="BuildSeries"/> 가 로딩 때
    /// 한 번 묶어 <b>요구 횟수 오름차순</b>으로 정렬한다. 화면은 그 정렬된 순서에서
    /// "아직 안 받은 가장 낮은 단계" 하나만 그린다(<c>MissionRules.VisibleTierIndex</c>).
    ///
    /// <b>언커먼·신화 보석 합성 계열은 없다.</b> 합성은 재료 4개를 한 등급 올리는 것이고,
    /// <c>GemDefinitionDatabase</c> 에 언커먼 보석이 하나도 없으며 신화는 재료가 될 수 없다
    /// (<c>EquipmentManager.TryMergeGems</c> 가 제외한다). 없는 행동의 업적을 만들면
    /// 영원히 0 인 줄이 목록에 남는다.
    /// </summary>
    [CreateAssetMenu(fileName = "AchievementDatabase", menuName = "OJ/Achievement Database")]
    public sealed class AchievementDatabase : ScriptableObject
    {
        [Tooltip("모든 단계를 한 목록에 적는다. 계열은 action + subKey 가 묶는다.")]
        [SerializeField] private List<AchievementDefinition> achievements = new List<AchievementDefinition>();

        private List<AchievementSeries> seriesCache;

        public IReadOnlyList<AchievementDefinition> Achievements => achievements;

        private void OnEnable()
        {
            if (achievements == null)
                achievements = new List<AchievementDefinition>();

            if (achievements.Count == 0)
                achievements.AddRange(BuildDefaults());

            seriesCache = null;
        }

        /// <summary>에디터 도구가 쓰는 채우기. 이미 값이 있으면 건드리지 않는다.</summary>
        public bool PopulateDefaults()
        {
            if (achievements == null)
                achievements = new List<AchievementDefinition>();

            if (achievements.Count > 0)
                return false;

            achievements.AddRange(BuildDefaults());
            seriesCache = null;
            return true;
        }

        /// <summary>계열 목록. 처음 물을 때 한 번 묶고 정렬한다.</summary>
        public IReadOnlyList<AchievementSeries> Series
        {
            get
            {
                if (seriesCache == null)
                    seriesCache = BuildSeries(achievements);

                return seriesCache;
            }
        }

        public List<string> Validate()
        {
            return Validate(achievements);
        }

        // ── 규칙 (static) ───────────────────────────────────────────────

        /// <summary>
        /// 같은 카운터 키끼리 묶고 <b>요구 횟수 오름차순</b>으로 정렬한다.
        ///
        /// <b>계열의 순서는 목록에 처음 나온 순서다.</b> 정렬하지 않는 이유는 그것이
        /// 화면의 줄 순서이고, 기획이 인스펙터에서 끌어 옮기는 것으로 바꿀 수 있어야 하기
        /// 때문이다. 단계 안쪽만 정렬한다.
        ///
        /// 정렬은 안정적이어야 한다 — 같은 요구 횟수가 둘이면 목록 순서를 지킨다.
        /// <c>List.Sort</c> 는 불안정하므로 쓰지 않고 삽입 정렬로 둔다(단계는 많아야 몇 개다).
        /// </summary>
        public static List<AchievementSeries> BuildSeries(IReadOnlyList<AchievementDefinition> source)
        {
            var result = new List<AchievementSeries>();
            if (source == null)
                return result;

            var index = new Dictionary<string, AchievementSeries>(System.StringComparer.Ordinal);

            for (int i = 0; i < source.Count; i++)
            {
                AchievementDefinition definition = source[i];
                if (definition == null)
                    continue;

                string key = definition.CounterKey;
                if (!index.TryGetValue(key, out AchievementSeries series))
                {
                    series = new AchievementSeries(key, new List<AchievementDefinition>());
                    index[key] = series;
                    result.Add(series);
                }

                InsertSorted(series.Tiers, definition);
            }

            return result;
        }

        private static void InsertSorted(List<AchievementDefinition> tiers, AchievementDefinition definition)
        {
            int at = tiers.Count;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].requiredCount > definition.requiredCount)
                {
                    at = i;
                    break;
                }
            }

            tiers.Insert(at, definition);
        }

        /// <summary>
        /// 목록이 성한지 본다.
        ///
        /// <b>같은 계열에 같은 요구 횟수가 둘인 것을 사고로 본다.</b> 둘 중 하나를 받아도
        /// 다른 하나가 같은 숫자로 다시 뜨는데, 화면상 아무것도 안 바뀐 것처럼 보인다.
        /// </summary>
        public static List<string> Validate(IReadOnlyList<AchievementDefinition> source)
        {
            var problems = new List<string>();

            if (source == null || source.Count == 0)
            {
                problems.Add("업적 목록이 비었다.");
                return problems;
            }

            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < source.Count; i++)
            {
                AchievementDefinition definition = source[i];
                if (definition == null)
                {
                    problems.Add(i + "번 업적이 비어 있다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.id))
                    problems.Add(i + "번 업적의 id 가 비었다.");
                else if (!seenIds.Add(definition.id))
                    problems.Add("업적 id 가 중복이다: " + definition.id);

                if (definition.requiredCount <= 0)
                    problems.Add(definition.id + " 의 요구 횟수가 0 이하다.");

                if (!MissionRules.IsValidSubKey(definition.subKey))
                {
                    problems.Add(definition.id + " 의 subKey 에 '" +
                                 MissionRules.SubKeySeparator + "' 가 들어 있다.");
                }

                if (definition.rewards == null || definition.rewards.Count == 0)
                    problems.Add(definition.id + " 에 보상이 없다.");
            }

            List<AchievementSeries> series = BuildSeries(source);
            for (int i = 0; i < series.Count; i++)
            {
                List<AchievementDefinition> tiers = series[i].Tiers;
                for (int t = 1; t < tiers.Count; t++)
                {
                    if (tiers[t].requiredCount == tiers[t - 1].requiredCount)
                    {
                        problems.Add(series[i].CounterKey + " 계열에 요구 횟수 " +
                                     tiers[t].requiredCount + " 가 둘이다.");
                    }
                }
            }

            return problems;
        }

        /// <summary>
        /// 기본 목록. <b>수치는 초안이다</b> — 인스펙터에서 조정하라고 SO 로 올린 것이고,
        /// 여기 값은 "에셋이 비었을 때 게임이 돌아가게" 하는 몫이다.
        /// </summary>
        public static List<AchievementDefinition> BuildDefaults()
        {
            var list = new List<AchievementDefinition>();

            AddSeries(list, "ach_play", MissionAction.GamePlay, string.Empty, "게임 플레이 {0}회",
                new[] { 10, 50, 200, 1000 }, PointType.Gold, new[] { 10000, 50000, 200000, 1000000 });

            AddSeries(list, "ach_pinball", MissionAction.PinballShot, string.Empty, "핀볼 발사 {0}회",
                new[] { 10, 100, 1000, 10000 }, PointType.PinballTicket, new[] { 1, 5, 20, 50 });

            AddSeries(list, "ach_iap", MissionAction.IapPurchase, string.Empty, "인앱 결제 {0}회",
                new[] { 1, 5, 20 }, PointType.FreeGem, new[] { 100, 500, 2000 });

            AddSeries(list, "ach_dice_normal", MissionAction.DiceLevelUp, MissionSubKeys.DiceNormal,
                "일반 다이스 레벨업 {0}회",
                new[] { 10, 50, 200 }, PointType.NormalScroll, new[] { 10, 50, 200 });

            AddSeries(list, "ach_dice_rare", MissionAction.DiceLevelUp, MissionSubKeys.DiceRare,
                "레어 다이스 레벨업 {0}회",
                new[] { 10, 50, 200 }, PointType.RareStone, new[] { 10, 50, 200 });

            AddSeries(list, "ach_dice_mythic", MissionAction.DiceLevelUp, MissionSubKeys.DiceMythic,
                "신화 다이스 레벨업 {0}회",
                new[] { 5, 25, 100 }, PointType.MythicStone, new[] { 10, 50, 200 });

            AddSeries(list, "ach_equipment", MissionAction.EquipmentLevelUp, string.Empty,
                "장비 레벨업 {0}회",
                new[] { 10, 50, 200 }, PointType.WeaponScroll, new[] { 10, 50, 200 });

            AddSeries(list, "ach_gem_draw", MissionAction.GemDraw, string.Empty, "보석 뽑기 {0}회",
                new[] { 10, 100, 1000 }, PointType.FreeGem, new[] { 50, 300, 1500 });

            // 합성은 재료 등급으로 센다. 언커먼은 보석 데이터에 없고 신화는 재료가 못 된다.
            AddSeries(list, "ach_merge_common", MissionAction.GemMerge,
                MissionSubKeys.ForRarity(Rarity.Common), "커먼 보석 합성 {0}회",
                new[] { 5, 25, 100 }, PointType.FreeGem, new[] { 30, 150, 600 });

            AddSeries(list, "ach_merge_normal", MissionAction.GemMerge,
                MissionSubKeys.ForRarity(Rarity.Normal), "노멀 보석 합성 {0}회",
                new[] { 5, 25, 100 }, PointType.FreeGem, new[] { 30, 150, 600 });

            AddSeries(list, "ach_merge_rare", MissionAction.GemMerge,
                MissionSubKeys.ForRarity(Rarity.Rare), "레어 보석 합성 {0}회",
                new[] { 5, 25, 100 }, PointType.FreeGem, new[] { 50, 250, 1000 });

            AddSeries(list, "ach_merge_epic", MissionAction.GemMerge,
                MissionSubKeys.ForRarity(Rarity.Epic), "에픽 보석 합성 {0}회",
                new[] { 5, 25, 100 }, PointType.FreeGem, new[] { 80, 400, 1600 });

            AddSeries(list, "ach_ad", MissionAction.AdWatch, string.Empty, "광고 시청 {0}회",
                new[] { 10, 50, 200 }, PointType.FreeGem, new[] { 50, 250, 1000 });

            AddSeries(list, "ach_shop", MissionAction.ShopPurchase, string.Empty, "상품 구매 {0}회",
                new[] { 5, 25, 100 }, PointType.FreeGem, new[] { 50, 250, 1000 });

            AddSeries(list, "ach_kill", MissionAction.MonsterKill, string.Empty, "적 처치 {0}회",
                new[] { 1000, 10000, 100000 }, PointType.Gold, new[] { 20000, 200000, 2000000 });

            AddSeries(list, "ach_bounty", MissionAction.BountyKill, string.Empty, "현상금 처치 {0}회",
                new[] { 10, 50, 200 }, PointType.Gold, new[] { 30000, 150000, 600000 });

            AddSeries(list, "ach_dice_merge", MissionAction.DiceMerge, string.Empty, "다이스 머지 {0}회",
                new[] { 100, 1000, 10000 }, PointType.Gold, new[] { 20000, 200000, 2000000 });

            return list;
        }

        private static void AddSeries(
            List<AchievementDefinition> list, string idPrefix, MissionAction action, string subKey,
            string titleFormat, int[] counts, PointType rewardType, int[] rewardAmounts)
        {
            for (int i = 0; i < counts.Length; i++)
            {
                list.Add(new AchievementDefinition
                {
                    // id 에 요구 횟수를 넣는다. 단계를 중간에 끼워 넣어도 뒤 단계의 id 가
                    // 밀리지 않아, 이미 받은 사람이 다시 받게 되는 일이 없다.
                    id = idPrefix + "_" + counts[i],
                    action = action,
                    subKey = subKey,
                    requiredCount = counts[i],
                    titleFormat = titleFormat,
                    rewards = new List<MissionReward>
                    {
                        new MissionReward(rewardType, rewardAmounts[i]),
                    },
                });
            }
        }
    }
}
