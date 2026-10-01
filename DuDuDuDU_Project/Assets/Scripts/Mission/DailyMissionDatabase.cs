using System.Collections.Generic;
using UnityEngine;
using OJ.Core;
using OJ.Point;

namespace OJ.Mission
{
    /// <summary>
    /// 일일 미션의 정본. 목록 11줄과 추가 보상 4칸이 여기 다 있다.
    /// (AGENTS 확정사항 3 — 밸런스 수치는 SO 로 올린다)
    ///
    /// <b>매일 같은 11개가 뜬다. 뽑지 않는다.</b> 풀에서 랜덤으로 고르면 "오늘은 결제 미션이
    /// 떠서 10개를 못 채운다" 가 생기고, 그건 유저가 어떻게 해 볼 수 없는 실패다.
    /// 고정이면 추가 보상 문턱(3·5·7·10)이 항상 같은 난이도를 뜻한다.
    ///
    /// <b>검사와 기본값이 <c>static</c> 인 이유</b>는 <c>DiceUnlockDatabase</c> 와 같다 —
    /// 헤드리스 러너에서 <c>ScriptableObject.CreateInstance</c> 가 돌지 않으므로, 규칙을
    /// SO 인스턴스에 매달면 에디터를 열어야만 확인된다.
    /// </summary>
    [CreateAssetMenu(fileName = "DailyMissionDatabase", menuName = "OJ/Daily Mission Database")]
    public sealed class DailyMissionDatabase : ScriptableObject
    {
        [Tooltip("매일 뜨는 목록. 순서가 곧 화면 순서다.")]
        [SerializeField] private List<DailyMissionDefinition> missions = new List<DailyMissionDefinition>();

        [Tooltip("오늘 n개를 깨면 주는 추가 보상. 요구 개수 오름차순으로 적는다.")]
        [SerializeField] private List<DailyMissionTier> tiers = new List<DailyMissionTier>();

        public IReadOnlyList<DailyMissionDefinition> Missions => missions;

        public IReadOnlyList<DailyMissionTier> Tiers => tiers;

        private void OnEnable()
        {
            if (missions == null)
                missions = new List<DailyMissionDefinition>();
            if (tiers == null)
                tiers = new List<DailyMissionTier>();

            // 비었을 때만 채운다. 로드마다 덮으면 인스펙터에서 고친 수치가 조용히 사라진다.
            if (missions.Count == 0)
                missions.AddRange(BuildDefaultMissions());
            if (tiers.Count == 0)
                tiers.AddRange(BuildDefaultTiers());
        }

        /// <summary>에디터 도구가 쓰는 채우기. 이미 값이 있으면 건드리지 않는다.</summary>
        public bool PopulateDefaults()
        {
            bool changed = false;

            if (missions == null)
                missions = new List<DailyMissionDefinition>();
            if (tiers == null)
                tiers = new List<DailyMissionTier>();

            if (missions.Count == 0)
            {
                missions.AddRange(BuildDefaultMissions());
                changed = true;
            }

            if (tiers.Count == 0)
            {
                tiers.AddRange(BuildDefaultTiers());
                changed = true;
            }

            return changed;
        }

        /// <summary>오늘 뜨는 미션만. 꺼 둔 것은 빠진다.</summary>
        public List<DailyMissionDefinition> GetActiveMissions()
        {
            return GetActiveMissions(missions);
        }

        /// <summary>추가 보상 문턱만 뽑은 것. 게이지 계산(<c>MissionRules</c>)에 그대로 넘긴다.</summary>
        public List<int> GetTierThresholds()
        {
            return GetTierThresholds(tiers);
        }

        public List<string> Validate()
        {
            return Validate(missions, tiers);
        }

        // ── 규칙 (static) ───────────────────────────────────────────────

        public static List<DailyMissionDefinition> GetActiveMissions(
            IReadOnlyList<DailyMissionDefinition> source)
        {
            var list = new List<DailyMissionDefinition>();
            if (source == null)
                return list;

            for (int i = 0; i < source.Count; i++)
            {
                DailyMissionDefinition definition = source[i];
                if (definition != null && definition.enabled)
                    list.Add(definition);
            }

            return list;
        }

        public static List<int> GetTierThresholds(IReadOnlyList<DailyMissionTier> source)
        {
            var list = new List<int>();
            if (source == null)
                return list;

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    list.Add(source[i].requiredClearCount);
            }

            return list;
        }

        /// <summary>
        /// 목록이 성한지 본다. 에디터 도구와 테스트가 같이 쓴다.
        ///
        /// <b>"문턱이 켜진 미션 수보다 크다" 를 사고로 본다.</b> 10개 문턱인데 달성 가능한
        /// 미션이 9개면 그 보상은 <b>영원히 안 열리고</b>, 화면에는 멀쩡히 보인다 —
        /// 유저는 자기가 뭘 덜 했는지 찾느라 시간을 쓴다. 지금 광고·결제를 꺼 두면
        /// 바로 이 상태가 되므로, 끄는 순간 이 검사가 말해 줘야 한다.
        /// </summary>
        public static List<string> Validate(
            IReadOnlyList<DailyMissionDefinition> missions,
            IReadOnlyList<DailyMissionTier> tiers)
        {
            var problems = new List<string>();
            var seenIds = new HashSet<string>(System.StringComparer.Ordinal);

            int activeCount = 0;

            if (missions == null || missions.Count == 0)
            {
                problems.Add("일일 미션 목록이 비었다.");
            }
            else
            {
                for (int i = 0; i < missions.Count; i++)
                {
                    DailyMissionDefinition definition = missions[i];
                    if (definition == null)
                    {
                        problems.Add(i + "번 미션이 비어 있다.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(definition.id))
                        problems.Add(i + "번 미션의 id 가 비었다.");
                    else if (!seenIds.Add(definition.id))
                        problems.Add("미션 id 가 중복이다: " + definition.id);

                    if (definition.requiredCount <= 0)
                        problems.Add(definition.id + " 의 요구 횟수가 0 이하다.");

                    if (!MissionRules.IsValidSubKey(definition.subKey))
                    {
                        problems.Add(definition.id + " 의 subKey 에 '" +
                                     MissionRules.SubKeySeparator + "' 가 들어 있다.");
                    }

                    // 보상이 없으면 받기 버튼이 아무 일도 하지 않는다. 화면상 성공과
                    // 구분되지 않으므로 데이터 사고로 본다.
                    if (definition.rewards == null || definition.rewards.Count == 0)
                        problems.Add(definition.id + " 에 보상이 없다.");

                    if (definition.enabled)
                        activeCount++;
                }
            }

            if (tiers == null || tiers.Count == 0)
            {
                problems.Add("추가 보상 문턱이 비었다.");
                return problems;
            }

            int previous = 0;
            for (int i = 0; i < tiers.Count; i++)
            {
                DailyMissionTier tier = tiers[i];
                if (tier == null)
                {
                    problems.Add(i + "번 추가 보상이 비어 있다.");
                    continue;
                }

                if (tier.requiredClearCount <= previous)
                {
                    problems.Add("추가 보상 문턱이 오름차순이 아니다: " +
                                 previous + " 다음에 " + tier.requiredClearCount);
                }

                previous = tier.requiredClearCount;

                if (tier.rewards == null || tier.rewards.Count == 0)
                    problems.Add(tier.requiredClearCount + "개 문턱에 보상이 없다.");

                if (tier.requiredClearCount > activeCount)
                {
                    problems.Add(tier.requiredClearCount + "개 문턱은 열 수 없다 — " +
                                 "켜져 있는 미션이 " + activeCount + "개뿐이다.");
                }
            }

            return problems;
        }

        /// <summary>
        /// 기본 목록 11줄. <b>수치는 초안이다</b> — 인스펙터에서 조정하라고 SO 로 올린 것이고,
        /// 여기 값은 "에셋이 비었을 때 게임이 돌아가게" 하는 몫이다.
        /// </summary>
        public static List<DailyMissionDefinition> BuildDefaultMissions()
        {
            return new List<DailyMissionDefinition>
            {
                Make("daily_login", MissionAction.Login, 1, "로그인 {0}회",
                    new MissionReward(PointType.FreeGem, 50)),

                Make("daily_play", MissionAction.GamePlay, 3, "게임 플레이 {0}회",
                    new MissionReward(PointType.Gold, 30000)),

                Make("daily_pinball", MissionAction.PinballShot, 10, "핀볼 발사 {0}회",
                    new MissionReward(PointType.FreeGem, 30)),

                Make("daily_iap", MissionAction.IapPurchase, 1, "인앱 결제 {0}회",
                    new MissionReward(PointType.FreeGem, 100)),

                Make("daily_dice_level", MissionAction.DiceLevelUp, 3, "다이스 레벨업 {0}회",
                    new MissionReward(PointType.NormalScroll, 5)),

                Make("daily_equipment_level", MissionAction.EquipmentLevelUp, 3, "장비 레벨업 {0}회",
                    new MissionReward(PointType.WeaponScroll, 5)),

                Make("daily_gem_draw", MissionAction.GemDraw, 10, "보석 뽑기 {0}회",
                    new MissionReward(PointType.Gold, 20000)),

                Make("daily_gem_merge", MissionAction.GemMerge, 3, "보석 합성 {0}회",
                    new MissionReward(PointType.FreeGem, 30)),

                Make("daily_ad", MissionAction.AdWatch, 2, "광고 시청 {0}회",
                    new MissionReward(PointType.FreeGem, 50)),

                Make("daily_kill", MissionAction.MonsterKill, 300, "적 처치 {0}회",
                    new MissionReward(PointType.Gold, 30000)),

                Make("daily_shop", MissionAction.ShopPurchase, 3, "상품 구매 {0}회",
                    new MissionReward(PointType.FreeGem, 30)),
            };
        }

        /// <summary>기본 추가 보상 4칸. 문턱은 기획대로 3·5·7·10 이다.</summary>
        public static List<DailyMissionTier> BuildDefaultTiers()
        {
            return new List<DailyMissionTier>
            {
                MakeTier(3, new MissionReward(PointType.FreeGem, 100)),
                MakeTier(5, new MissionReward(PointType.RelicTicket, 1)),
                MakeTier(7, new MissionReward(PointType.PinballTicket, 3)),
                MakeTier(10, new MissionReward(PointType.FreeGem, 300)),
            };
        }

        private static DailyMissionDefinition Make(
            string id, MissionAction action, int requiredCount, string titleFormat,
            params MissionReward[] rewards)
        {
            return new DailyMissionDefinition
            {
                id = id,
                action = action,
                subKey = string.Empty,
                requiredCount = requiredCount,
                titleFormat = titleFormat,
                enabled = true,
                rewards = new List<MissionReward>(rewards),
            };
        }

        private static DailyMissionTier MakeTier(int requiredClearCount, params MissionReward[] rewards)
        {
            return new DailyMissionTier
            {
                requiredClearCount = requiredClearCount,
                rewards = new List<MissionReward>(rewards),
            };
        }
    }
}
