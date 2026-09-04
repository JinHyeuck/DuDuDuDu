using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Dice;
using OJ.Equipment;
using OJ.Relic;
using OJ.Utils;

namespace OJ.CombatPower
{
    /// <summary>
    /// Displays the current permanent combat power and a short-lived change notification.
    /// All hierarchy and references are authored in UICombatPowerDisplay.prefab.
    /// </summary>
    public class UICombatPowerDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text powerText;
        [SerializeField] private TMP_Text deltaText;
        [SerializeField] private Image deltaArrowImage;
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private CanvasGroup toastCanvasGroup;

        [Header("Colors")]
        [SerializeField] private Color increaseColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color decreaseColor = new Color(1f, 0.3f, 0.3f, 1f);

        [Header("Timing")]
        [SerializeField] private float countDuration = 0.35f;
        [SerializeField] private float fadeInDuration = 0.08f;
        [SerializeField] private float deltaVisibleSeconds = 0.75f;
        [SerializeField] private float fadeOutDuration = 0.22f;

        private long displayedPower;
        private bool initialized;
        private Coroutine animationRoutine;
        private Coroutine deltaRoutine;

        private void OnEnable()
        {
            Subscribe();
            Refresh(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopRunningCoroutines();
        }

        public void Refresh(bool animate = true)
        {
            long nextPower = CombatPowerCalculator.Current;
            if (!initialized)
            {
                initialized = true;
                displayedPower = nextPower;
                SetPowerText(nextPower);
                HideToast();
                return;
            }

            long previousPower = displayedPower;
            long delta = nextPower - previousPower;
            if (delta == 0L)
                return;

            if (animationRoutine != null)
                StopCoroutine(animationRoutine);

            if (animate)
                animationRoutine = StartCoroutine(CoAnimatePower(previousPower, nextPower));
            else
            {
                displayedPower = nextPower;
                SetPowerText(nextPower);
                animationRoutine = null;
            }

            ShowDelta(delta);
        }

        private IEnumerator CoAnimatePower(long from, long to)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, countDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                SetPowerText(from + (long)((to - from) * (double)t));
                yield return null;
            }

            displayedPower = to;
            SetPowerText(to);
            animationRoutine = null;
        }

        private void ShowDelta(long delta)
        {
            if (deltaText == null || toastRoot == null || toastCanvasGroup == null)
                return;

            bool increased = delta > 0L;
            Color deltaColor = increased ? increaseColor : decreaseColor;
            deltaText.color = deltaColor;
            deltaText.SetText(FormatNatural(System.Math.Abs(delta)));

            if (deltaArrowImage != null)
            {
                deltaArrowImage.color = deltaColor;
                deltaArrowImage.rectTransform.localEulerAngles = increased
                    ? Vector3.zero
                    : new Vector3(0f, 0f, 180f);
            }

            if (deltaRoutine != null)
                StopCoroutine(deltaRoutine);
            deltaRoutine = StartCoroutine(CoShowToastBriefly());
        }

        private IEnumerator CoShowToastBriefly()
        {
            toastRoot.SetActive(true);
            SetToastAlpha(0f);
            yield return FadeToast(0f, 1f, fadeInDuration);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, deltaVisibleSeconds));
            yield return FadeToast(1f, 0f, fadeOutDuration);
            toastRoot.SetActive(false);
            deltaRoutine = null;
        }

        private IEnumerator FadeToast(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetToastAlpha(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetToastAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetToastAlpha(to);
        }

        private void Subscribe()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged += HandleDiceLevelChanged;
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged += HandleEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged += HandleGemChanged;
            }
            if (RelicManager.Instance != null)
                RelicManager.Instance.OnRelicChanged += HandleRelicChanged;
        }

        private void Unsubscribe()
        {
            // isAlive 였던 자리다. MonoSingleton 시절에는 Instance 게터가 <b>없으면 만들어 냈기</b>
            // 때문에, 구독을 푸는 것뿐인데 매니저가 새로 생기는 일을 막으려고 isAlive 를 썼다.
            // 8.3a 에서 DiceLevelManager 가 순수 클래스가 되면서 Instance 는 그냥 읽기가 됐고,
            // 부작용이 없으므로 평범한 null 검사가 정확히 같은 의미가 된다.
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= HandleDiceLevelChanged;

            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged -= HandleEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged -= HandleGemChanged;
            }
            if (RelicManager.Instance != null)
                RelicManager.Instance.OnRelicChanged -= HandleRelicChanged;
        }

        private void HandleDiceLevelChanged(DiceType diceType, int level) => Refresh();
        private void HandleEquipmentChanged(EquipmentType equipmentType) => Refresh();
        private void HandleGemChanged() => Refresh();
        private void HandleRelicChanged() => Refresh();

        private void SetPowerText(long value)
        {
            if (powerText != null)
                powerText.SetText(FormatNatural(value));
        }

        private void SetToastAlpha(float alpha)
        {
            if (toastCanvasGroup == null)
                return;

            toastCanvasGroup.alpha = Mathf.Clamp01(alpha);
            toastCanvasGroup.interactable = false;
            toastCanvasGroup.blocksRaycasts = false;
        }

        private void HideToast()
        {
            SetToastAlpha(0f);
            if (toastRoot != null)
                toastRoot.SetActive(false);
        }

        private void StopRunningCoroutines()
        {
            if (animationRoutine != null)
                StopCoroutine(animationRoutine);
            if (deltaRoutine != null)
                StopCoroutine(deltaRoutine);
            animationRoutine = null;
            deltaRoutine = null;
            HideToast();
        }

        private static string FormatNatural(long value)
        {
            return System.Math.Max(0L, value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
