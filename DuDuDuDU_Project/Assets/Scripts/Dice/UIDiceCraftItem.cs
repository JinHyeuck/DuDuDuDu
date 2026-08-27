using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIDiceCraftItem : MonoBehaviour
    {
        [SerializeField] private Button craftButton;
        [SerializeField] private TMP_Text craftButtonText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image bgImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text recipeText;
        [SerializeField] private TMP_Text progressText;
        [Header("Material Slots")]
        [SerializeField] private Image[] materialIcons;
        [SerializeField] private TMP_Text[] materialCountTexts;
        [SerializeField] private TMP_Text[] materialStateTexts;

        private DiceType mythicType;
        private System.Func<DiceType, bool> craftCallback;
        private System.Func<DiceType, int, int> materialCountProvider;
        private System.Func<DiceType, int> baseEquivalentProvider;
        private System.Func<DiceType, int> percentProvider;
        private readonly StringBuilder lineBuilder = new StringBuilder(256);
        public DiceType MythicType => mythicType;

        private void Awake()
        {
            if (craftButton != null)
                craftButton.onClick.AddListener(HandleCraftClick);
        }

        private void OnDestroy()
        {
            if (craftButton != null)
                craftButton.onClick.RemoveListener(HandleCraftClick);
        }

        public void Bind(
            DiceType type,
            System.Func<DiceType, bool> onCraft,
            System.Func<DiceType, int, int> countProvider,
            System.Func<DiceType, int> baseProvider,
            System.Func<DiceType, int> getPercent)
        {
            mythicType = type;
            craftCallback = onCraft;
            materialCountProvider = countProvider;
            baseEquivalentProvider = baseProvider;
            percentProvider = getPercent;
            Refresh();
        }

        public void Refresh()
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(mythicType);
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            int percent = percentProvider != null ? percentProvider(mythicType) : 0;
            gameObject.SetActive(percent > 0);
            if (percent <= 0)
                return;

            if (bgImage != null)
                bgImage.color = DiceMetaDataProvider.GetColor(mythicType);

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(mythicType);

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : mythicType.ToString());

            bool canCraft = BuildRecipeTexts(recipe, percent);

            if (craftButton != null)
                craftButton.interactable = canCraft;

            if (craftButtonText != null)
                craftButtonText.SetText(canCraft ? "소환" : "재료 부족");
        }

        private bool BuildRecipeTexts(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe, int percent)
        {
            if (recipe == null || recipe.Count == 0)
            {
                if (recipeText != null)
                    recipeText.SetText("조합식 없음");
                if (progressText != null)
                    progressText.SetText(string.Empty);
                ClearMaterialSlots();
                return false;
            }

            bool canCraft = true;
            lineBuilder.Clear();
            int readyCount = 0;
            int totalRequiredBase = 0;
            int totalOwnedBaseForRecipe = 0;
            Dictionary<DiceType, int> requiredBaseByType = new Dictionary<DiceType, int>();

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                int have = materialCountProvider != null ? materialCountProvider(req.diceType, req.star) : 0;
                bool ok = have >= req.count;
                if (!ok)
                    canCraft = false;
                else
                    readyCount++;

                bool showStarUI = DiceMetaDataProvider.ShowStarUI(req.diceType);
                if (showStarUI)
                {
                    lineBuilder.Append(req.star);
                    lineBuilder.Append("★ ");
                }
                lineBuilder.Append(req.diceType);
                lineBuilder.Append(" x");
                lineBuilder.Append(req.count);
                lineBuilder.Append(" (");
                lineBuilder.Append(have);
                lineBuilder.Append("/");
                lineBuilder.Append(req.count);
                lineBuilder.Append(")");
                if (i < recipe.Count - 1)
                    lineBuilder.Append('\n');

                int requiredBase = req.count * GetBaseUnitFromStar(req.star);
                totalRequiredBase += requiredBase;
                requiredBaseByType.TryGetValue(req.diceType, out int baseSum);
                requiredBaseByType[req.diceType] = baseSum + requiredBase;

                SetMaterialSlot(i, req, have, ok);
            }

            foreach (var pair in requiredBaseByType)
            {
                int haveBase = baseEquivalentProvider != null ? baseEquivalentProvider(pair.Key) : 0;
                totalOwnedBaseForRecipe += Mathf.Min(haveBase, pair.Value);
            }

            int calculatedPercent = totalRequiredBase > 0
                ? Mathf.Clamp(Mathf.RoundToInt((totalOwnedBaseForRecipe * 100f) / totalRequiredBase), 0, 100)
                : 0;

            if (recipeText != null)
                recipeText.SetText(lineBuilder.ToString());
            if (progressText != null)
                progressText.SetText("{0}/{1}  {2}%", readyCount, recipe.Count, Mathf.Max(percent, calculatedPercent));
            HideExtraMaterialSlots(recipe.Count);

            return canCraft;
        }

        private void SetMaterialSlot(int index, DiceMetaDataDatabase.DiceRecipeMaterial req, int have, bool ok)
        {
            if (materialIcons == null || index < 0 || index >= materialIcons.Length)
                return;

            Image icon = materialIcons[index];
            if (icon == null)
                return;

            icon.gameObject.SetActive(true);
            icon.sprite = DiceMetaDataProvider.GetIcon(req.diceType);
            icon.color = Color.white;

            if (materialCountTexts != null && index < materialCountTexts.Length && materialCountTexts[index] != null)
            {
                materialCountTexts[index].gameObject.SetActive(true);
                bool showStarUI = DiceMetaDataProvider.ShowStarUI(req.diceType);
                if (showStarUI)
                    materialCountTexts[index].SetText("{0}★ x{1}", req.star, req.count);
                else
                    materialCountTexts[index].SetText("x{0}", req.count);
            }

            if (materialStateTexts != null && index < materialStateTexts.Length && materialStateTexts[index] != null)
            {
                materialStateTexts[index].gameObject.SetActive(true);
                materialStateTexts[index].SetText("{0}/{1}", have, req.count);
                materialStateTexts[index].color = ok ? new Color(0.2f, 0.9f, 0.4f) : new Color(1f, 0.35f, 0.35f);
            }
        }

        private void HideExtraMaterialSlots(int usedCount)
        {
            if (materialIcons == null)
                return;

            for (int i = usedCount; i < materialIcons.Length; i++)
            {
                if (materialIcons[i] != null)
                    materialIcons[i].gameObject.SetActive(false);

                if (materialCountTexts != null && i < materialCountTexts.Length && materialCountTexts[i] != null)
                    materialCountTexts[i].gameObject.SetActive(false);

                if (materialStateTexts != null && i < materialStateTexts.Length && materialStateTexts[i] != null)
                    materialStateTexts[i].gameObject.SetActive(false);
            }
        }

        private void ClearMaterialSlots()
        {
            HideExtraMaterialSlots(0);
        }

        private static int GetBaseUnitFromStar(int star)
        {
            int s = Mathf.Max(1, star);
            return 1 << (s - 1);
        }

        private void HandleCraftClick()
        {
            craftCallback?.Invoke(mythicType);
        }
    }
}
