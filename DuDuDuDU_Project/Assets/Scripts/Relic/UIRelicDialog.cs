using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.UI;

namespace OJ.Relic
{
    public class UIRelicDialog : DialogBase
    {
        [Header("List")]
        [SerializeField] private Transform relicRoot;
        [SerializeField] private UIRelicElement relicElementPrefab;
        [SerializeField] private List<UIRelicElement> relicElements = new List<UIRelicElement>();

        [Header("Detail Popup")]
        [SerializeField] private GameObject detailPopupRoot;
        [SerializeField] private Button detailPopupCloseButton;
        [SerializeField] private Button detailPreviousButton;
        [SerializeField] private Button detailNextButton;
        [SerializeField] private UIRelicElement detailRelicElement;
        [SerializeField, HideInInspector] private Image detailBackgroundImage;
        [SerializeField, HideInInspector] private Image detailIconImage;
        [SerializeField, HideInInspector] private TMP_Text detailNameText;
        [SerializeField, HideInInspector] private TMP_Text detailLevelText;
        [SerializeField] private TMP_Text detailEffectText;
        [SerializeField] private TMP_Text detailExampleText;
        [SerializeField, HideInInspector] private TMP_Text detailUnknownText;

        [Header("Summon")]
        [SerializeField] private Button summonButton;
        [SerializeField] private TMP_Text summonButtonText;
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private TMP_Text ticketCostText;


        private RelicDefinition selectedDefinition;
        private bool deferRelicRefresh;
        private Coroutine landingAnimationCoroutine;
        private UIRelicElement landingAnimationElement;

        protected override void OnLoad()
        {
            BuildElementsIfNeeded();
            CreateDetailPopupIfNeeded();

            if (summonButton != null)
                summonButton.onClick.AddListener(HandleSummonClick);

            if (detailPopupCloseButton != null)
                detailPopupCloseButton.onClick.AddListener(HideDetailPopup);

            if (detailPreviousButton != null)
                detailPreviousButton.onClick.AddListener(SelectPreviousRelic);

            if (detailNextButton != null)
                detailNextButton.onClick.AddListener(SelectNextRelic);

            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.OnRelicChanged += HandleRelicChanged;
                RelicManager.Instance.OnSummonCountChanged += HandleSummonCountChanged;
            }

            SetDetailPopupVisible(false);
        }

        protected override void OnUnload()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(HandleSummonClick);

            if (detailPopupCloseButton != null)
                detailPopupCloseButton.onClick.RemoveListener(HideDetailPopup);

            if (detailPreviousButton != null)
                detailPreviousButton.onClick.RemoveListener(SelectPreviousRelic);

            if (detailNextButton != null)
                detailNextButton.onClick.RemoveListener(SelectNextRelic);

            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.OnRelicChanged -= HandleRelicChanged;
                RelicManager.Instance.OnSummonCountChanged -= HandleSummonCountChanged;
            }

            StopLandingAnimation();
        }

        protected override void OnEnter()
        {
            BuildElementsIfNeeded();
            selectedDefinition = null;
            deferRelicRefresh = false;
            SetDetailPopupVisible(false);

            RefreshAll();
        }

        protected override void OnExit()
        {
            deferRelicRefresh = false;
            StopLandingAnimation();
        }


        private void BuildElementsIfNeeded()
        {
            if (RelicManager.Instance == null)
                return;

            IReadOnlyList<RelicDefinition> definitions = RelicManager.Instance.GetDefinitions();
            if (definitions == null)
                return;

            if (relicElementPrefab != null && relicRoot != null)
            {
                while (relicElements.Count < definitions.Count)
                {
                    UIRelicElement element = Instantiate(relicElementPrefab, relicRoot);
                    relicElements.Add(element);
                }
            }

            for (int i = 0; i < relicElements.Count; i++)
            {
                UIRelicElement element = relicElements[i];
                if (element == null)
                    continue;

                if (i < definitions.Count)
                    element.Bind(definitions[i], HandleRelicClicked);
                else
                    element.gameObject.SetActive(false);
            }
        }

        private void HandleRelicClicked(RelicDefinition definition)
        {
            selectedDefinition = definition;
            SetDetailPopupVisible(true);
            RefreshDetail();
            RefreshSelection();
        }

        /// <summary>
        /// 카탈로그에서 꺼내 띄운다. (10.4)
        ///
        /// 예전에는 씬 인스턴스를 <c>[SerializeField]</c> 로 가리키고, 비면 씬을 뒤져 때웠다.
        /// 두 경로 다 <b>실패가 조용하다</b> — 참조가 <c>None</c> 이면 아무 로그 없이 안 열린다.
        /// 게다가 그 탐색은 자기 하위에 딸려 들어온 인스턴스를 골라내야 했는데,
        /// 프리팹을 꺼내 쓰면 그 자리 문제 자체가 없다.
        ///
        /// <c>Show</c> 가 아니라 <c>Get</c> 인 이유는 <c>Open</c> 이 결과를 넣은 뒤에
        /// 스스로 <c>Enter</c> 를 부르기 때문이다. 확인이 뽑기보다 앞서는 순서도 그대로다 —
        /// <b>못 열면 소모도 없어야 한다.</b>
        /// </summary>
        private void HandleSummonClick()
        {
            if (RelicManager.Instance == null)
                return;

            UIRelicSummonDialog summonDialog = GameContainer.UI?.Get<UIRelicSummonDialog>();
            if (summonDialog == null)
                return;

            deferRelicRefresh = true;
            if (!RelicManager.Instance.TrySummon(out RelicSummonResult result))
            {
                deferRelicRefresh = false;
                RefreshSummonCost();
                return;
            }

            summonDialog.Load_Element();
            summonDialog.Open(result, HandleSummonDialogClosed);
        }

        private void HandleRelicChanged()
        {
            if (deferRelicRefresh)
                return;

            RefreshAll();
        }

        private void HandleSummonCountChanged()
        {
            if (deferRelicRefresh)
                return;

            RefreshSummonCost();
        }

        private void HandleSummonDialogClosed(RelicSummonResult result)
        {
            deferRelicRefresh = false;

            if (!isEnter || dialogView == null || !dialogView.activeInHierarchy)
                return;

            selectedDefinition = result != null ? result.Definition : null;
            RefreshAll();
            PlaySummonedRelicLanding(result);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < relicElements.Count; i++)
            {
                if (relicElements[i] != null)
                {
                    relicElements[i].Refresh();
                    relicElements[i].VisibleName(false);
                }
            }

            RefreshSelection();
            RefreshDetail();
            RefreshSummonCost();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < relicElements.Count; i++)
            {
                UIRelicElement element = relicElements[i];
                if (element == null || element.Definition == null)
                    continue;

                element.SetSelected(selectedDefinition != null && element.Definition.relicId == selectedDefinition.relicId);
            }
        }

        private void PlaySummonedRelicLanding(RelicSummonResult result)
        {
            if (result == null || result.Definition == null)
                return;

            UIRelicElement targetElement = GetRelicElement(result.Definition.relicId);
            if (targetElement == null)
                return;

            StopLandingAnimation();
            landingAnimationCoroutine = StartCoroutine(CoPlaySummonedRelicLanding(result.Definition, targetElement));
        }

        private IEnumerator CoPlaySummonedRelicLanding(RelicDefinition definition, UIRelicElement targetElement)
        {
            RectTransform rootRt = dialogView != null ? dialogView.GetComponent<RectTransform>() : null;
            RectTransform targetRt = targetElement != null ? targetElement.GetComponent<RectTransform>() : null;
            if (rootRt == null || targetRt == null || relicElementPrefab == null)
            {
                targetElement?.PlayReceiveAnimation();
                landingAnimationCoroutine = null;
                yield break;
            }

            yield return null;

            landingAnimationElement = Instantiate(relicElementPrefab, dialogView.transform);
            landingAnimationElement.name = "SummonedRelicLanding";
            landingAnimationElement.Bind(definition, null);
            landingAnimationElement.SetSelected(false);
            landingAnimationElement.VisibleName(false);

            RectTransform landingRt = landingAnimationElement.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = landingAnimationElement.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;

            Vector2 startPosition = Vector2.zero;
            Vector2 endPosition = GetLocalCenter(rootRt, targetRt);
            SetAnchor(landingRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(160f, 190f), startPosition);
            landingRt.localScale = Vector3.one * 2.1f;
            landingRt.SetAsLastSibling();

            const float moveDuration = 0.46f;
            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / moveDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                landingRt.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
                landingRt.localScale = Vector3.LerpUnclamped(Vector3.one * 2.1f, Vector3.one, eased);
                canvasGroup.alpha = t < 0.84f ? 1f : Mathf.Lerp(1f, 0.25f, (t - 0.84f) / 0.16f);
                yield return null;
            }

            Destroy(landingAnimationElement.gameObject);
            landingAnimationElement = null;
            targetElement.PlayReceiveAnimation();
            landingAnimationCoroutine = null;
        }

        private UIRelicElement GetRelicElement(RelicId relicId)
        {
            for (int i = 0; i < relicElements.Count; i++)
            {
                UIRelicElement element = relicElements[i];
                if (element != null && element.Definition != null && element.Definition.relicId == relicId)
                    return element;
            }

            return null;
        }

        private Vector2 GetLocalCenter(RectTransform rootRt, RectTransform targetRt)
        {
            Vector3[] corners = new Vector3[4];
            targetRt.GetWorldCorners(corners);
            Vector3 centerWorld = (corners[0] + corners[2]) * 0.5f;

            Camera uiCamera = GetUICamera();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, centerWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRt, screenPoint, uiCamera, out Vector2 localPoint);
            return localPoint;
        }

        private Camera GetUICamera()
        {
            Canvas canvas = dialogView != null ? dialogView.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        private void StopLandingAnimation()
        {
            if (landingAnimationCoroutine != null)
            {
                StopCoroutine(landingAnimationCoroutine);
                landingAnimationCoroutine = null;
            }

            if (landingAnimationElement != null)
            {
                Destroy(landingAnimationElement.gameObject);
                landingAnimationElement = null;
            }
        }

        private void RefreshDetail()
        {
            if (selectedDefinition == null || RelicManager.Instance == null)
            {
                ClearDetail();
                RefreshNavigationButtons();
                return;
            }

            int level = RelicManager.Instance.GetLevel(selectedDefinition.relicId);
            int displayLevel = Mathf.Max(1, level);
            bool owned = level > 0;

            RefreshDetailRelicElement(selectedDefinition);

            if (detailEffectText != null)
            {
                detailEffectText.gameObject.SetActive(owned);
                detailEffectText.SetText(owned ? RelicManager.Instance.GetEffectText(selectedDefinition.relicId, displayLevel) : string.Empty);
            }

            if (detailExampleText != null)
            {
                string example = owned ? RelicManager.Instance.GetExampleText(selectedDefinition.relicId) : string.Empty;
                detailExampleText.gameObject.SetActive(!string.IsNullOrEmpty(example));
                detailExampleText.SetText(example);
            }

            RefreshNavigationButtons();
        }

        private void RefreshDetailRelicElement(RelicDefinition definition)
        {
            if (detailRelicElement == null)
                return;

            detailRelicElement.Bind(definition, null);
            detailRelicElement.SetSelected(false);
        }

        private void RefreshSummonCost()
        {
            if (RelicManager.Instance == null)
                return;

            RelicSummonCost cost = RelicManager.Instance.GetCurrentSummonCost();
            bool canSummon = RelicManager.Instance.CanSummon();

            if (summonButton != null)
                summonButton.interactable = canSummon;

            if (summonButtonText != null)
                summonButtonText.SetText("유물 뽑기");

            if (goldCostText != null)
                goldCostText.SetText("x{0}", cost.goldCost);

            if (ticketCostText != null)
                ticketCostText.SetText("x{0}", cost.ticketCost);
        }

        private void HideDetailPopup()
        {
            selectedDefinition = null;
            SetDetailPopupVisible(false);
            RefreshSelection();
            ClearDetail();
        }

        private void SelectPreviousRelic()
        {
            SelectAdjacentRelic(-1);
        }

        private void SelectNextRelic()
        {
            SelectAdjacentRelic(1);
        }

        private void SelectAdjacentRelic(int direction)
        {
            if (RelicManager.Instance == null || selectedDefinition == null)
                return;

            IReadOnlyList<RelicDefinition> definitions = RelicManager.Instance.GetDefinitions();
            if (definitions == null || definitions.Count == 0)
                return;

            int currentIndex = -1;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].relicId == selectedDefinition.relicId)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
                return;

            int nextIndex = (currentIndex + direction + definitions.Count) % definitions.Count;
            selectedDefinition = definitions[nextIndex];
            RefreshDetail();
            RefreshSelection();
        }

        private void SetDetailPopupVisible(bool visible)
        {
            if (detailPopupRoot != null)
                detailPopupRoot.SetActive(visible);
        }

        private void RefreshNavigationButtons()
        {
            bool canNavigate = false;
            if (RelicManager.Instance != null && selectedDefinition != null)
            {
                IReadOnlyList<RelicDefinition> definitions = RelicManager.Instance.GetDefinitions();
                canNavigate = definitions != null && definitions.Count > 1;
            }

            if (detailPreviousButton != null)
                detailPreviousButton.gameObject.SetActive(canNavigate);

            if (detailNextButton != null)
                detailNextButton.gameObject.SetActive(canNavigate);
        }

        private void ClearDetail()
        {
            if (detailRelicElement != null)
                detailRelicElement.Bind(null, null);

            if (detailUnknownText != null)
                detailUnknownText.gameObject.SetActive(false);

            if (detailNameText != null)
                detailNameText.SetText(string.Empty);

            if (detailLevelText != null)
                detailLevelText.SetText(string.Empty);

            if (detailEffectText != null)
            {
                detailEffectText.SetText(string.Empty);
                detailEffectText.gameObject.SetActive(false);
            }

            if (detailExampleText != null)
            {
                detailExampleText.SetText(string.Empty);
                detailExampleText.gameObject.SetActive(false);
            }
        }

        private void CreateDetailPopupIfNeeded()
        {
            if (dialogView == null)
                return;

            HideLegacyDetailObjects();

            if (detailPopupRoot == null)
            {
                detailPopupRoot = CreateRectObject("DetailPopupRoot", dialogView.transform);
                SetStretch(detailPopupRoot.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
                detailPopupRoot.transform.SetAsLastSibling();

                Image dimImage = detailPopupRoot.AddComponent<Image>();
                dimImage.color = new Color(0f, 0f, 0f, 0.68f);
                detailPopupCloseButton = detailPopupRoot.AddComponent<Button>();

                detailEffectText = CreateRuntimeText("DetailEffectText", detailPopupRoot.transform, "효과", 42f, TextAlignmentOptions.Center, Color.white);
                SetAnchor(detailEffectText.rectTransform, new Vector2(0.10f, 0.22f), new Vector2(0.90f, 0.32f), Vector2.zero, Vector2.zero);

                detailExampleText = CreateRuntimeText("DetailExampleText", detailPopupRoot.transform, "예시", 24f, TextAlignmentOptions.Center, new Color(0.78f, 0.9f, 1f, 1f));
                SetAnchor(detailExampleText.rectTransform, new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.22f), Vector2.zero, Vector2.zero);

                detailPreviousButton = CreateRuntimeButton("PreviousButton", detailPopupRoot.transform, "<", new Vector2(86f, 120f), new Vector2(0.18f, 0.50f), new Color(1f, 1f, 1f, 0f));
                TMP_Text previousText = detailPreviousButton.GetComponentInChildren<TMP_Text>();
                if (previousText != null)
                    previousText.fontSize = 72f;

                detailNextButton = CreateRuntimeButton("NextButton", detailPopupRoot.transform, ">", new Vector2(86f, 120f), new Vector2(0.82f, 0.50f), new Color(1f, 1f, 1f, 0f));
                TMP_Text nextText = detailNextButton.GetComponentInChildren<TMP_Text>();
                if (nextText != null)
                    nextText.fontSize = 72f;
            }

            CreateDetailRelicElementIfNeeded();

            detailPopupRoot.SetActive(false);
        }

        private void CreateDetailRelicElementIfNeeded()
        {
            if (detailRelicElement != null || detailPopupRoot == null || relicElementPrefab == null)
                return;

            detailRelicElement = Instantiate(relicElementPrefab, detailPopupRoot.transform);
            detailRelicElement.name = "DetailRelicElement";
            SetupDetailRelicElementTransform();
            detailRelicElement.Bind(null, null);
        }

        private void SetupDetailRelicElementTransform()
        {
            if (detailRelicElement == null)
                return;

            RectTransform rt = detailRelicElement.GetComponent<RectTransform>();
            if (rt == null)
                return;

            SetAnchor(rt, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.56f), new Vector2(160f, 190f), Vector2.zero);
            rt.localScale = Vector3.one * 2.35f;
        }

        private void HideLegacyDetailObjects()
        {
            if (detailBackgroundImage != null)
                detailBackgroundImage.gameObject.SetActive(false);

            if (detailNameText != null)
                detailNameText.gameObject.SetActive(false);

            if (detailLevelText != null)
                detailLevelText.gameObject.SetActive(false);

            if (detailEffectText != null)
                detailEffectText.gameObject.SetActive(false);

            if (detailExampleText != null)
                detailExampleText.gameObject.SetActive(false);
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateRuntimeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static TMP_Text CreateRuntimeText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        private static Button CreateRuntimeButton(string name, Transform parent, string label, Vector2 size, Vector2 anchor, Color color)
        {
            GameObject go = CreateRectObject(name, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            SetAnchor(rt, anchor, anchor, size, Vector2.zero);

            Image image = go.AddComponent<Image>();
            image.color = color;
            Button button = go.AddComponent<Button>();

            TMP_Text text = CreateRuntimeText("Text", go.transform, label, 34f, TextAlignmentOptions.Center, Color.white);
            SetStretch(text.rectTransform, 0f, 0f, 1f, 1f);
            return button;
        }

        private static void SetStretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            rt.localScale = Vector3.one;
        }

        private static void SetAnchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPosition;
            rt.localScale = Vector3.one;
        }
    }
}
