using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIRelicDialog : IDialog
    {
        [Header("List")]
        [SerializeField] private Transform relicRoot;
        [SerializeField] private UIRelicElement relicElementPrefab;
        [SerializeField] private List<UIRelicElement> relicElements = new List<UIRelicElement>();

        [Header("Detail")]
        [SerializeField] private Image detailBackgroundImage;
        [SerializeField] private Image detailIconImage;
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailLevelText;
        [SerializeField] private TMP_Text detailEffectText;
        [SerializeField] private TMP_Text detailExampleText;

        [Header("Summon")]
        [SerializeField] private Button summonButton;
        [SerializeField] private TMP_Text summonButtonText;
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private TMP_Text ticketCostText;
        [SerializeField] private UIRelicSummonDialog summonDialog;

        private RelicDefinition selectedDefinition;

        protected override void OnLoad()
        {
            BuildElementsIfNeeded();

            if (summonButton != null)
                summonButton.onClick.AddListener(HandleSummonClick);

            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.OnRelicChanged += RefreshAll;
                RelicManager.Instance.OnSummonCountChanged += RefreshSummonCost;
            }
        }

        protected override void OnUnload()
        {
            if (summonButton != null)
                summonButton.onClick.RemoveListener(HandleSummonClick);

            if (RelicManager.Instance != null)
            {
                RelicManager.Instance.OnRelicChanged -= RefreshAll;
                RelicManager.Instance.OnSummonCountChanged -= RefreshSummonCost;
            }
        }

        protected override void OnEnter()
        {
            BuildElementsIfNeeded();
            if (selectedDefinition == null)
                SelectFirstDefinition();

            RefreshAll();
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

        private void SelectFirstDefinition()
        {
            if (RelicManager.Instance == null)
                return;

            IReadOnlyList<RelicDefinition> definitions = RelicManager.Instance.GetDefinitions();
            if (definitions != null && definitions.Count > 0)
                selectedDefinition = definitions[0];
        }

        private void HandleRelicClicked(RelicDefinition definition)
        {
            selectedDefinition = definition;
            RefreshDetail();
            RefreshSelection();
        }

        private void HandleSummonClick()
        {
            if (RelicManager.Instance == null)
                return;

            if (!RelicManager.Instance.TrySummon(out RelicSummonResult result))
                return;

            selectedDefinition = result.Definition;
            RefreshAll();
            if (summonDialog != null)
                summonDialog.Open(result);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < relicElements.Count; i++)
            {
                if (relicElements[i] != null)
                    relicElements[i].Refresh();
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
                return;

            int level = RelicManager.Instance.GetLevel(selectedDefinition.relicId);
            int displayLevel = Mathf.Max(1, level);
            bool owned = level > 0;

            if (detailBackgroundImage != null)
                detailBackgroundImage.sprite = RelicManager.Instance.GetBackground(selectedDefinition.rarity);

            if (detailIconImage != null)
            {
                detailIconImage.gameObject.SetActive(owned);
                detailIconImage.sprite = selectedDefinition.icon;
            }

            if (detailNameText != null)
                detailNameText.SetText(owned ? selectedDefinition.displayName : "???");

            if (detailLevelText != null)
            {
                if (owned)
                    detailLevelText.SetText("Lv.{0}", level);
                else
                    detailLevelText.SetText("미획득");
            }

            if (detailEffectText != null)
                detailEffectText.SetText(RelicManager.Instance.GetEffectText(selectedDefinition.relicId, displayLevel));

            if (detailExampleText != null)
                detailExampleText.SetText(RelicManager.Instance.GetExampleText(selectedDefinition.relicId));
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
    }
}
