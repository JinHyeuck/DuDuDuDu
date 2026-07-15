using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIRelicElement : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text unknownText;
        [SerializeField] private GameObject selectedFrame;

        private RelicDefinition definition;
        private System.Action<RelicDefinition> clickCallback;
        private Coroutine receiveAnimationCoroutine;

        public RelicDefinition Definition => definition;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        public void Bind(RelicDefinition relicDefinition, System.Action<RelicDefinition> onClick)
        {
            definition = relicDefinition;
            clickCallback = onClick;
            Refresh();
        }

        public void Refresh()
        {
            if (definition == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            int level = RelicManager.Instance != null ? RelicManager.Instance.GetLevel(definition.relicId) : 0;
            bool owned = level > 0;

            if (backgroundImage != null && RelicManager.Instance != null)
                backgroundImage.sprite = RelicManager.Instance.GetBackground(definition.rarity);

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(owned);
                iconImage.sprite = definition.icon;
            }

            if (nameText != null)
            {
                nameText.gameObject.SetActive(owned);
                nameText.SetText(definition.displayName);
            }

            if (levelText != null)
            {
                levelText.gameObject.SetActive(owned);
                levelText.SetText("Lv.{0}", level);
            }

            if (unknownText != null)
            {
                unknownText.gameObject.SetActive(!owned);
                unknownText.SetText("?");
            }
        }

        public void VisibleName(bool visible)
        {
            if (nameText != null)
                nameText.gameObject.SetActive(visible);
        }

        public void SetSelected(bool selected)
        {
            if (selectedFrame != null)
                selectedFrame.SetActive(selected);
        }

        public void PlayReceiveAnimation()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (receiveAnimationCoroutine != null)
                StopCoroutine(receiveAnimationCoroutine);

            receiveAnimationCoroutine = StartCoroutine(CoPlayReceiveAnimation());
        }

        private void HandleClick()
        {
            if (definition != null)
                clickCallback?.Invoke(definition);
        }

        private IEnumerator CoPlayReceiveAnimation()
        {
            Transform cachedTransform = transform;
            Vector3 originScale = cachedTransform.localScale;
            Vector3 punchScale = originScale * 1.16f;

            yield return CoScale(cachedTransform, originScale, punchScale, 0.12f);
            yield return CoScale(cachedTransform, punchScale, originScale, 0.16f);
            yield return CoScale(cachedTransform, originScale, originScale * 1.07f, 0.08f);
            yield return CoScale(cachedTransform, originScale * 1.07f, originScale, 0.10f);

            cachedTransform.localScale = originScale;
            receiveAnimationCoroutine = null;
        }

        private static IEnumerator CoScale(Transform target, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                target.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            target.localScale = to;
        }
    }
}
