using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Stage;

namespace OJ.StageStar
{
    public class UIStageStarConditionRow : MonoBehaviour
    {
        [SerializeField] private Image starIcon;
        [SerializeField] private GameObject achievedRoot;
        [SerializeField] private TMP_Text conditionText;

        public void Bind(StageClearGrade requiredGrade, StageClearGrade bestGrade, string overrideConditionText = null)
        {
            bool achieved = bestGrade >= requiredGrade;

            if (conditionText != null)
                conditionText.SetText(string.IsNullOrEmpty(overrideConditionText)
                    ? StageStarUtility.GetConditionText(requiredGrade)
                    : overrideConditionText);

            if (starIcon != null)
                starIcon.gameObject.SetActive(achieved);

            if (achievedRoot != null)
                achievedRoot.SetActive(achieved);
        }
    }
}
