using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OJ
{
    public class UIRelicDialog : IDialog
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
        [SerializeField] private UIRelicSummonDialog summonDialog;

        [SerializeField] private LobbyLayoutController lobbyLayoutController;

        private RelicDefinition selectedDefinition;

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
                RelicManager.Instance.OnRelicChanged += RefreshAll;
                RelicManager.Instance.OnSummonCountChanged += RefreshSummonCost;
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
                RelicManager.Instance.OnRelicChanged -= RefreshAll;
                RelicManager.Instance.OnSummonCountChanged -= RefreshSummonCost;
            }
        }

        protected override void OnEnter()
        {
            BuildElementsIfNeeded();
            selectedDefinition = null;
            SetDetailPopupVisible(false);

            RefreshAll();
        }

        public override void BackKeyCall()
        {
            lobbyLayoutController?.ShowTab(LobbyTab.Home);
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

        private void HandleSummonClick()
        {
            if (RelicManager.Instance == null)
                return;

            ResolveSummonDialogIfNeeded();
            if (summonDialog == null)
            {
                Debug.LogWarning("UIRelicDialog: Scene에 UIRelicSummonDialog가 배치되어 있지 않습니다.");
                return;
            }

            if (!RelicManager.Instance.TrySummon(out RelicSummonResult result))
                return;

            selectedDefinition = result.Definition;
            RefreshAll();
            summonDialog.Load_Element();
            summonDialog.Open(result);
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

        private void ResolveSummonDialogIfNeeded()
        {
            if (summonDialog != null && !summonDialog.transform.IsChildOf(transform))
                return;

            if (summonDialog != null)
            {
                summonDialog.gameObject.SetActive(false);
                summonDialog = null;
            }

#if UNITY_2023_1_OR_NEWER
            UIRelicSummonDialog[] dialogs = Object.FindObjectsByType<UIRelicSummonDialog>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            UIRelicSummonDialog[] dialogs = Object.FindObjectsOfType<UIRelicSummonDialog>(true);
#endif

            for (int i = 0; i < dialogs.Length; i++)
            {
                UIRelicSummonDialog dialog = dialogs[i];
                if (dialog == null || dialog.transform.IsChildOf(transform))
                    continue;

                summonDialog = dialog;
                return;
            }
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
