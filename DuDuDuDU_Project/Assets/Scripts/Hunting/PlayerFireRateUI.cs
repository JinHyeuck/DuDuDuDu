using UnityEngine;

namespace OJ
{
    public class PlayerFireRateUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private Transform followTarget;
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private RectTransform indicatorRect;
        [SerializeField] private RectTransform minPointRect;
        [SerializeField] private RectTransform maxPointRect;
        [SerializeField] private Canvas targetCanvas;
        [SerializeField] private Camera targetCamera;

        [Header("Follow")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, -1.1f, 0f);
        [SerializeField] private Vector2 uiOffset = Vector2.zero;
        [SerializeField] private bool hideOutsideWave = true;

        private RectTransform canvasRect;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (rootRect == null)
                rootRect = transform as RectTransform;

            if (playerController == null)
                playerController = PlayerController.Instance;

            if (followTarget == null && playerController != null)
                followTarget = playerController.transform;

            if (targetCanvas == null)
                targetCanvas = GetComponentInParent<Canvas>();

            if (targetCanvas != null)
                canvasRect = targetCanvas.transform as RectTransform;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void LateUpdate()
        {
            if (!TryResolveReferences())
                return;

            bool visible = !hideOutsideWave
                || (GameManager.Instance != null && GameManager.Instance.inGameState == InGameState.Wave);

            SetVisible(visible);
            if (!visible)
                return;

            UpdateFollowPosition();
            UpdateIndicatorPosition();
        }

        private bool TryResolveReferences()
        {
            if (playerController == null)
                playerController = PlayerController.Instance;

            if (followTarget == null && playerController != null)
                followTarget = playerController.transform;

            if (targetCanvas == null)
                targetCanvas = GetComponentInParent<Canvas>();

            if (canvasRect == null && targetCanvas != null)
                canvasRect = targetCanvas.transform as RectTransform;

            if (targetCamera == null)
            {
                if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    targetCamera = targetCanvas.worldCamera;

                if (targetCamera == null)
                    targetCamera = Camera.main;
            }

            return playerController != null
                && followTarget != null
                && rootRect != null
                && indicatorRect != null
                && minPointRect != null
                && maxPointRect != null
                && targetCanvas != null
                && canvasRect != null;
        }

        private void UpdateFollowPosition()
        {
            Vector3 worldPos = followTarget.position + worldOffset;
            Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(targetCamera, worldPos);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCamera,
                out Vector2 localPoint))
            {
                rootRect.anchoredPosition = localPoint + uiOffset;
            }
        }

        private void UpdateIndicatorPosition()
        {
            Vector2 anchoredPos = indicatorRect.anchoredPosition;
            float progress = playerController.GetFireCycleProgress01();
            anchoredPos.x = Mathf.Lerp(minPointRect.anchoredPosition.x, maxPointRect.anchoredPosition.x, progress);
            indicatorRect.anchoredPosition = anchoredPos;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void OnValidate()
        {
            if (rootRect == null)
                rootRect = transform as RectTransform;
        }
    }
}
