using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIRelicSummonDialog : IDialog
    {
        [SerializeField] private Button tapButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text unknownText;
        [SerializeField] private TMP_Text guideText;

        private RelicSummonResult result;
        private int currentStepIndex;
        private bool revealed;

        protected override void OnLoad()
        {
            if (tapButton != null)
                tapButton.onClick.AddListener(HandleTap);
        }

        protected override void OnUnload()
        {
            if (tapButton != null)
                tapButton.onClick.RemoveListener(HandleTap);
        }

        public void Open(RelicSummonResult summonResult)
        {
            result = summonResult;
            currentStepIndex = 0;
            revealed = false;
            Enter();
            ApplyMysteryState(Rarity.Normal);
        }

        private void HandleTap()
        {
            if (result == null || result.Definition == null)
            {
                Exit();
                return;
            }

            int targetStep = GetRarityStepIndex(result.Definition.rarity);
            if (currentStepIndex < targetStep)
            {
                currentStepIndex++;
                ApplyMysteryState(GetRarityByStepIndex(currentStepIndex));
                return;
            }

            if (!revealed)
            {
                revealed = true;
                ApplyResultState();
                return;
            }

            Exit();
        }

        private void ApplyMysteryState(Rarity rarity)
        {
            if (RelicManager.Instance != null && backgroundImage != null)
                backgroundImage.sprite = RelicManager.Instance.GetBackground(rarity);

            if (iconImage != null)
                iconImage.gameObject.SetActive(false);

            if (nameText != null)
                nameText.gameObject.SetActive(false);

            if (levelText != null)
                levelText.gameObject.SetActive(false);

            if (effectText != null)
                effectText.gameObject.SetActive(false);

            if (unknownText != null)
            {
                unknownText.gameObject.SetActive(true);
                unknownText.SetText("?");
            }

            if (guideText != null)
                guideText.SetText("탭하여 확인");
        }

        private void ApplyResultState()
        {
            RelicDefinition definition = result.Definition;
            if (definition == null)
                return;

            if (RelicManager.Instance != null && backgroundImage != null)
                backgroundImage.sprite = RelicManager.Instance.GetBackground(definition.rarity);

            if (iconImage != null)
            {
                iconImage.gameObject.SetActive(true);
                iconImage.sprite = definition.icon;
            }

            if (nameText != null)
            {
                nameText.gameObject.SetActive(true);
                nameText.SetText(definition.displayName);
            }

            if (levelText != null)
            {
                levelText.gameObject.SetActive(true);
                levelText.SetText("Lv.{0}", result.NewLevel);
            }

            if (effectText != null)
            {
                effectText.gameObject.SetActive(true);
                effectText.SetText(RelicManager.Instance != null
                    ? RelicManager.Instance.GetEffectText(definition.relicId, result.NewLevel)
                    : definition.description);
            }

            if (unknownText != null)
                unknownText.gameObject.SetActive(false);

            if (guideText != null)
                guideText.SetText("탭하여 닫기");
        }

        private static int GetRarityStepIndex(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Rare:
                    return 1;
                case Rarity.Epic:
                    return 2;
                case Rarity.Mythic:
                    return 3;
                default:
                    return 0;
            }
        }

        private static Rarity GetRarityByStepIndex(int stepIndex)
        {
            switch (stepIndex)
            {
                case 1:
                    return Rarity.Rare;
                case 2:
                    return Rarity.Epic;
                case 3:
                    return Rarity.Mythic;
                default:
                    return Rarity.Normal;
            }
        }
    }
}
