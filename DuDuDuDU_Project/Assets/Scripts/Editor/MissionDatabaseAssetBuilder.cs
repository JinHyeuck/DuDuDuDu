using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Mission;

namespace OJ.EditorTools
{
    /// <summary>
    /// 미션·업적 데이터베이스 에셋을 만들고 검사한다.
    /// <c>BountyDatabaseAssetBuilder</c> 와 같은 자리의 도구다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 조정한 수치를
    /// 도구가 조용히 되돌리면, 값이 왜 바뀌었는지 아무도 못 찾는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    /// </summary>
    public static class MissionDatabaseAssetBuilder
    {
        private const string Folder = "Assets/ScriptableObject";
        private const string DailyPath = Folder + "/DailyMissionDatabase.asset";
        private const string AchievementPath = Folder + "/AchievementDatabase.asset";

        [MenuItem("OJ/개발/미션/데이터베이스 에셋 만들기")]
        private static void CreateOrValidate()
        {
            System.IO.Directory.CreateDirectory(Folder);

            bool created = false;
            created |= EnsureDaily();
            created |= EnsureAchievement();

            if (created)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[미션] StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.");
            }

            ValidateOnly();
        }

        [MenuItem("OJ/개발/미션/데이터베이스 검사")]
        private static void ValidateOnly()
        {
            ReportDaily();
            ReportAchievement();
        }

        private static bool EnsureDaily()
        {
            if (AssetDatabase.LoadAssetAtPath<DailyMissionDatabase>(DailyPath) != null)
            {
                Debug.Log("[미션] 일일 미션 에셋이 이미 있다: " + DailyPath);
                return false;
            }

            var database = ScriptableObject.CreateInstance<DailyMissionDatabase>();
            database.PopulateDefaults();
            AssetDatabase.CreateAsset(database, DailyPath);

            Debug.Log("[미션] 일일 미션 에셋을 새로 만들었다: " + DailyPath);
            return true;
        }

        private static bool EnsureAchievement()
        {
            if (AssetDatabase.LoadAssetAtPath<AchievementDatabase>(AchievementPath) != null)
            {
                Debug.Log("[미션] 업적 에셋이 이미 있다: " + AchievementPath);
                return false;
            }

            var database = ScriptableObject.CreateInstance<AchievementDatabase>();
            database.PopulateDefaults();
            AssetDatabase.CreateAsset(database, AchievementPath);

            Debug.Log("[미션] 업적 에셋을 새로 만들었다: " + AchievementPath);
            return true;
        }

        private static void ReportDaily()
        {
            var database = AssetDatabase.LoadAssetAtPath<DailyMissionDatabase>(DailyPath);
            if (database == null)
            {
                Debug.LogError("[미션] 일일 미션 에셋이 없다: " + DailyPath +
                               " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            var sb = new StringBuilder("[미션] 일일 미션 검사: " + DailyPath).AppendLine();
            sb.AppendLine("  미션 " + database.Missions.Count + "개 (켜진 것 " +
                          database.GetActiveMissions().Count + "개)");

            foreach (DailyMissionDefinition d in database.Missions)
            {
                if (d == null)
                    continue;

                sb.AppendLine("    " + (d.enabled ? "O " : "X ") + d.id + " — " + d.DisplayTitle +
                              " / 카운터 " + d.CounterKey +
                              " / 보상 " + DescribeRewards(d.rewards));
            }

            sb.AppendLine("  추가 보상 문턱: " + string.Join(", ", database.GetTierThresholds()));

            Flush(sb, database.Validate());
        }

        private static void ReportAchievement()
        {
            var database = AssetDatabase.LoadAssetAtPath<AchievementDatabase>(AchievementPath);
            if (database == null)
            {
                Debug.LogError("[미션] 업적 에셋이 없다: " + AchievementPath +
                               " — 지금은 코드 기본값으로 돌아가고 있다.");
                return;
            }

            var sb = new StringBuilder("[미션] 업적 검사: " + AchievementPath).AppendLine();
            sb.AppendLine("  단계 " + database.Achievements.Count + "개 / 계열 " +
                          database.Series.Count + "개");

            foreach (AchievementSeries series in database.Series)
            {
                var counts = new List<string>();
                foreach (AchievementDefinition tier in series.Tiers)
                    counts.Add(tier.requiredCount.ToString());

                sb.AppendLine("    " + series.CounterKey + " — " + string.Join(" → ", counts));
            }

            Flush(sb, database.Validate());
        }

        private static string DescribeRewards(List<MissionReward> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return "없음";

            var parts = new List<string>(rewards.Count);
            for (int i = 0; i < rewards.Count; i++)
                parts.Add(rewards[i].pointType + " x" + rewards[i].amount);

            return string.Join(", ", parts);
        }

        private static void Flush(StringBuilder sb, List<string> problems)
        {
            if (problems.Count == 0)
            {
                sb.Append("  문제 없음.");
                Debug.Log(sb.ToString());
                return;
            }

            sb.AppendLine("  문제 " + problems.Count + "건:");
            foreach (string p in problems)
                sb.AppendLine("    !! " + p);

            Debug.LogError(sb.ToString());
        }
    }
}
