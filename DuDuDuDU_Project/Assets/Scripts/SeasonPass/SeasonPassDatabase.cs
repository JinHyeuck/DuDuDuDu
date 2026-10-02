using System.Collections.Generic;
using UnityEngine;
using OJ.Point;

namespace OJ.SeasonPass
{
    /// <summary>
    /// 시즌 패스의 정본. 시즌마다 보상표가 하나씩 들어간다.
    /// (AGENTS 확정사항 3 — 밸런스 수치는 SO 로 올린다)
    ///
    /// <b>달마다 새 보상이므로 목록이 계속 는다.</b> 그래서 "이번 달 시즌이 목록에 없는"
    /// 상태가 반드시 온다 — 운영이 다음 달 표를 안 넣어 둔 경우다. 그때 패스가 화면에서
    /// 통째로 사라지면 유저는 컨텐츠가 없어진 줄 안다. <see cref="FallbackSeason"/> 이
    /// 그 자리를 메우고, <see cref="Validate"/> 가 "다음 달 표가 없다" 를 미리 말한다.
    ///
    /// <b>검사와 기본값이 <c>static</c> 인 이유</b>는 <c>DiceUnlockDatabase</c> 와 같다 —
    /// 헤드리스 러너에서 <c>ScriptableObject.CreateInstance</c> 가 돌지 않으므로, 규칙을
    /// SO 인스턴스에 매달면 에디터를 열어야만 확인된다.
    /// </summary>
    [CreateAssetMenu(fileName = "SeasonPassDatabase", menuName = "OJ/Season Pass Database")]
    public sealed class SeasonPassDatabase : ScriptableObject
    {
        /// <summary>
        /// 한 레벨을 올리는 데 드는 포인트(= 쓴 고기). 화면의 "0 / 1000" 의 분모다.
        ///
        /// <b>소탕 1회가 고기 5 다</b>(<c>SweepRules.StaminaCostPerSweep</c>).
        /// 200 으로 두면 레벨 하나에 소탕 40회이고, 하루 소탕 상한이 72회
        /// (<c>SweepEconomy.DailySweepClears</c>)라 하루에 한 레벨 남짓 오른다.
        /// 한 달이면 30레벨 안팎 — 그것이 아래 기본 시즌의 길이다.
        /// </summary>
        [Tooltip("한 레벨에 필요한 포인트(쓴 고기). 소탕 1회 = 고기 5.")]
        [SerializeField, Min(1)] private int pointsPerLevel = 200;

        [Tooltip("시즌 목록. id 는 yyyy-MM 이고 그 달에 그 시즌이 돈다.")]
        [SerializeField] private List<SeasonPassSeason> seasons = new List<SeasonPassSeason>();

        [Tooltip("그 달의 시즌이 목록에 없을 때 대신 도는 표. 비우면 패스가 화면에서 사라진다.")]
        [SerializeField] private SeasonPassSeason fallbackSeason = new SeasonPassSeason();

        public int PointsPerLevel => Mathf.Max(1, pointsPerLevel);

        public IReadOnlyList<SeasonPassSeason> Seasons => seasons;

        public SeasonPassSeason FallbackSeason => fallbackSeason;

        private void OnEnable()
        {
            if (seasons == null)
                seasons = new List<SeasonPassSeason>();

            if (fallbackSeason == null || fallbackSeason.MaxLevel == 0)
                fallbackSeason = BuildDefaultSeason("기본", "시즌 (기본 보상)");

            // 비었을 때만 채운다. 로드마다 덮으면 인스펙터에서 고친 수치가 조용히 사라진다.
            if (seasons.Count == 0)
                seasons.AddRange(BuildDefaultSeasons());
        }

        /// <summary>에디터 도구가 쓰는 채우기. 이미 값이 있으면 건드리지 않는다.</summary>
        public bool PopulateDefaults()
        {
            bool changed = false;

            if (seasons == null)
                seasons = new List<SeasonPassSeason>();

            if (seasons.Count == 0)
            {
                seasons.AddRange(BuildDefaultSeasons());
                changed = true;
            }

            if (fallbackSeason == null || fallbackSeason.MaxLevel == 0)
            {
                fallbackSeason = BuildDefaultSeason("기본", "시즌 (기본 보상)");
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// 그 시즌 id 의 표. 없으면 <see cref="FallbackSeason"/>.
        /// <b>절대 null 을 주지 않는다</b> — 화면이 null 을 받으면 빈 창을 띄우게 되고,
        /// 그건 "컨텐츠가 사라졌다" 로 읽힌다.
        /// </summary>
        public SeasonPassSeason GetSeason(string seasonId)
        {
            return GetSeason(seasons, fallbackSeason, seasonId);
        }

        public List<string> Validate()
        {
            return Validate(seasons, fallbackSeason, pointsPerLevel);
        }

        // ── 규칙 (static) ───────────────────────────────────────────────

        public static SeasonPassSeason GetSeason(
            IReadOnlyList<SeasonPassSeason> seasons, SeasonPassSeason fallback, string seasonId)
        {
            if (seasons != null && !string.IsNullOrEmpty(seasonId))
            {
                for (int i = 0; i < seasons.Count; i++)
                {
                    if (seasons[i] != null && seasons[i].id == seasonId && seasons[i].MaxLevel > 0)
                        return seasons[i];
                }
            }

            return fallback;
        }

        /// <summary>
        /// 목록이 성한지 본다.
        ///
        /// <b>"다음 달 표가 없다" 를 문제로 본다.</b> 시즌은 달이 바뀌는 순간 교체되는데,
        /// 그때가 되어서야 없다는 것을 알면 이미 유저가 기본 시즌을 보고 있다.
        /// </summary>
        public static List<string> Validate(
            IReadOnlyList<SeasonPassSeason> seasons, SeasonPassSeason fallback, int pointsPerLevel)
        {
            var problems = new List<string>();

            if (pointsPerLevel <= 0)
                problems.Add("레벨당 포인트가 0 이하다.");

            if (fallback == null || fallback.MaxLevel == 0)
                problems.Add("기본 시즌이 비었다. 그 달의 표가 없으면 패스가 화면에서 사라진다.");

            if (seasons == null || seasons.Count == 0)
            {
                problems.Add("시즌 목록이 비었다.");
                return problems;
            }

            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < seasons.Count; i++)
            {
                SeasonPassSeason season = seasons[i];
                if (season == null)
                {
                    problems.Add(i + "번 시즌이 비어 있다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(season.id))
                    problems.Add(i + "번 시즌의 id 가 비었다.");
                else if (!seen.Add(season.id))
                    problems.Add("시즌 id 가 중복이다: " + season.id);

                if (season.MaxLevel == 0)
                {
                    problems.Add(season.id + " 에 레벨이 하나도 없다.");
                    continue;
                }

                for (int level = 1; level <= season.MaxLevel; level++)
                {
                    SeasonPassLevel data = season.GetLevel(level);
                    bool hasFree = data.freeRewards != null && data.freeRewards.Count > 0;
                    bool hasPremium = data.premiumRewards != null && data.premiumRewards.Count > 0;

                    // 양쪽 다 비면 그 레벨은 올라도 아무 일이 없다. 화면에는 빈 줄만 남는다.
                    if (!hasFree && !hasPremium)
                        problems.Add(season.id + " 의 " + level + "레벨에 보상이 하나도 없다.");
                }
            }

            return problems;
        }

        /// <summary>
        /// 지금 달과 다음 달의 표가 있는지. 에디터 검사가 쓴다 —
        /// 운영이 다음 달 표를 넣는 것을 잊는 것이 이 컨텐츠의 유일한 운영 실수다.
        /// </summary>
        public static List<string> CheckUpcoming(
            IReadOnlyList<SeasonPassSeason> seasons, System.DateTime localNow)
        {
            var problems = new List<string>();

            string thisMonth = OJ.Core.SeasonPassRules.SeasonId(localNow);
            string nextMonth = OJ.Core.SeasonPassRules.SeasonId(localNow.AddMonths(1));

            if (GetSeason(seasons, null, thisMonth) == null)
                problems.Add("이번 달(" + thisMonth + ") 시즌 표가 없다. 기본 시즌으로 돈다.");

            if (GetSeason(seasons, null, nextMonth) == null)
                problems.Add("다음 달(" + nextMonth + ") 시즌 표가 없다. 달이 바뀌기 전에 넣을 것.");

            return problems;
        }

        // ── 기본값 ─────────────────────────────────────────────────────

        /// <summary>
        /// 기본 시즌 둘(이번 달·다음 달). <b>수치는 초안이다</b> — 인스펙터에서 조정하라고
        /// SO 로 올린 것이고, 여기 값은 "에셋이 비었을 때 게임이 돌아가게" 하는 몫이다.
        /// </summary>
        public static List<SeasonPassSeason> BuildDefaultSeasons()
        {
            System.DateTime now = System.DateTime.Now;

            return new List<SeasonPassSeason>
            {
                BuildDefaultSeason(OJ.Core.SeasonPassRules.SeasonId(now), "시즌 1"),
                BuildDefaultSeason(OJ.Core.SeasonPassRules.SeasonId(now.AddMonths(1)), "시즌 2"),
            };
        }

        /// <summary>
        /// 30레벨짜리 한 시즌. 구성품은 기획서 7장의 목록 그대로다
        /// (소환권·강화권·골드·레어석·신화석).
        ///
        /// <b>유료 트랙이 무료보다 두껍다.</b> 5레벨마다 큰 것이 오고, 마지막 레벨에
        /// 신화석이 몰려 있다 — "안 사면 손해" 가 눈에 보여야 한다는 7.1 의 요구다.
        /// </summary>
        public static SeasonPassSeason BuildDefaultSeason(string id, string displayName)
        {
            var season = new SeasonPassSeason
            {
                id = id,
                displayName = displayName,
                levels = new List<SeasonPassLevel>(),
            };

            for (int level = 1; level <= 30; level++)
            {
                var data = new SeasonPassLevel();

                // 무료 트랙 — 매 레벨 골드, 5레벨마다 소환권.
                data.freeRewards.Add(new SeasonPassReward(PointType.Gold, 10000 * level));
                if (level % 5 == 0)
                    data.freeRewards.Add(new SeasonPassReward(PointType.NormalScroll, 2));

                // 유료 트랙 — 매 레벨 무료젬, 5레벨마다 레어석, 10레벨마다 신화석.
                data.premiumRewards.Add(new SeasonPassReward(PointType.FreeGem, 50 * level));
                if (level % 5 == 0)
                    data.premiumRewards.Add(new SeasonPassReward(PointType.RareStone, 10));
                if (level % 10 == 0)
                    data.premiumRewards.Add(new SeasonPassReward(PointType.MythicStone, 5));

                season.levels.Add(data);
            }

            return season;
        }
    }
}
