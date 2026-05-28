using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIStageStarConditionRow : MonoBehaviour
    {
        [SerializeField] private Image starIcon;
        [SerializeField] private GameObject achievedRoot;
        [SerializeField] private TMP_Text conditionText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Range(0f, 1f)] private float achievedAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.45f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

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

            if (canvasGroup != null)
                canvasGroup.alpha = achieved ? achievedAlpha : lockedAlpha;
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
