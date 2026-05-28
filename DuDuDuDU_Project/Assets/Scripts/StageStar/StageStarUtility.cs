using UnityEngine;

namespace OJ
{
    public static class StageStarUtility
    {
        public const int MaxStarsPerStage = 3;
        public const int StarsPerReward = 3;
        public const int DiaRewardAmount = 100;

        public static int GetStarCount(StageClearGrade grade)
        {
            return Mathf.Clamp((int)grade, 0, MaxStarsPerStage);
        }

        public static StageClearGrade GetGradeForStarCount(int starCount)
        {
            return (StageClearGrade)Mathf.Clamp(starCount, 0, MaxStarsPerStage);
        }

        public static string GetConditionText(StageClearGrade grade)
        {
            switch (grade)
            {
                case StageClearGrade.Minimum:
                    return "\uD074\uB9AC\uC5B4";
                case StageClearGrade.Half:
                    return "HP 50% \uC774\uC0C1 \uC0C1\uD0DC\uB85C \uD074\uB9AC\uC5B4";
                case StageClearGrade.Perfect:
                    return "HP 100% \uC0C1\uD0DC\uB85C \uD074\uB9AC\uC5B4";
                default:
                    return string.Empty;
            }
        }
    }
}
