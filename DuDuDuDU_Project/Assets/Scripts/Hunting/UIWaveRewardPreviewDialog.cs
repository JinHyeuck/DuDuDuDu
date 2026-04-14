using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIWaveRewardPreviewDialog : IDialog
    {
        [Header("UI")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text detailText;

        [Header("Animation")]
        [SerializeField] private Vector2 hiddenOffset = new Vector2(0f, -28f);
        [SerializeField] private float fadeInDuration = 0.18f;
        [SerializeField] private float holdDuration = 0.9f;
        [SerializeField] private float fadeOutDuration = 0.22f;

        private CanvasGroup canvasGroup;
        private Coroutine playRoutine;
        private Vector2 shownPosition;
        private bool hasShownPosition;

        protected override void OnLoad()
        {
            ResolveReferences();
            RefreshGoldIcon();
            ApplyHiddenState();
        }

        public void ShowGoldGain(int gainedGold, int accumulatedGold, int totalGold)
        {
            if (gainedGold <= 0)
                return;

            ResolveReferences();
            RefreshGoldIcon();

            if (amountText != null)
                amountText.SetText("+{0}", gainedGold);

            // if (detailText != null)
            //     detailText.SetText("클리어 시 누적 골드 {0}/{1}", accumulatedGold, totalGold);

            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            ApplyHiddenState();

            Enter();
            playRoutine = StartCoroutine(CoPlay());
        }

        protected override void OnExit()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            ApplyHiddenState();
        }

        private void ResolveReferences()
        {
            if (panelRect == null)
                panelRect = dialogView != null ? dialogView.transform as RectTransform : null;

            if (dialogView != null && canvasGroup == null)
            {
                canvasGroup = dialogView.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = dialogView.AddComponent<CanvasGroup>();
            }

            if (panelRect != null && hasShownPosition == false)
            {
                shownPosition = panelRect.anchoredPosition;
                hasShownPosition = true;
            }
        }

        private void RefreshGoldIcon()
        {
            if (iconImage == null || StaticResource.Instance == null || StaticResource.Instance.PointMetadataDatabase == null)
                return;

            PointMetadataDatabase.PointMetadata metadata = StaticResource.Instance.PointMetadataDatabase.Get(PointType.Gold);
            if (metadata != null)
                iconImage.sprite = metadata.icon;
        }

        private IEnumerator CoPlay()
        {
            yield return Animate(0f, 1f, hiddenOffset, Vector2.zero, fadeInDuration);
            yield return CoWaitRealtime(holdDuration);
            yield return Animate(1f, 0f, Vector2.zero, hiddenOffset, fadeOutDuration);

            playRoutine = null;
            Exit();
        }

        private IEnumerator Animate(float fromAlpha, float toAlpha, Vector2 fromOffset, Vector2 toOffset, float duration)
        {
            if (panelRect == null || canvasGroup == null)
                yield break;

            if (duration <= 0f)
            {
                canvasGroup.alpha = toAlpha;
                panelRect.anchoredPosition = shownPosition + toOffset;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
                panelRect.anchoredPosition = shownPosition + Vector2.Lerp(fromOffset, toOffset, eased);
                yield return null;
            }

            canvasGroup.alpha = toAlpha;
            panelRect.anchoredPosition = shownPosition + toOffset;
        }

        private IEnumerator CoWaitRealtime(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void ApplyHiddenState()
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (panelRect != null)
                panelRect.anchoredPosition = shownPosition + hiddenOffset;
        }
    }
}
