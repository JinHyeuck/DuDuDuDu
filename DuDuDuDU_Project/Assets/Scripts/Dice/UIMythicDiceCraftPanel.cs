using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace OJ
{
    public class UIMythicDiceCraftPanel : IDialog
    {
        [Header("UI")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private UIMythicDiceCraftItem itemPrefab;
        [SerializeField] private TMP_Text stateText;

        [Header("Behavior")]
        [SerializeField] private bool autoToggleByGameState = true;
        [SerializeField] private float refreshInterval = 0.2f;

        private readonly List<UIMythicDiceCraftItem> items = new List<UIMythicDiceCraftItem>();
        private readonly List<UIDice> consumeBuffer = new List<UIDice>();
        private float nextRefreshTime;

        private void Update()
        {
            if (autoToggleByGameState)
                SyncVisibilityWithState();

            if (!isEnter)
                return;

            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
            RefreshAll();
        }

        protected override void OnLoad()
        {
            BuildIfNeeded();
            RefreshStateText();
        }

        protected override void OnEnter()
        {
            BuildIfNeeded();
            RefreshAll();
        }

        private void SyncVisibilityWithState()
        {
            bool shouldOpen = GameManager.Instance != null && GameManager.Instance.inGameState == InGameState.Setting;
            if (shouldOpen == isEnter)
                return;

            if (shouldOpen)
                Enter();
            else
                Exit();
        }

        private void BuildIfNeeded()
        {
            if (itemPrefab == null || listRoot == null || items.Count > 0)
                return;

            List<DiceType> mythics = DiceMetaDataProvider.GetMythicTypes();
            for (int i = 0; i < mythics.Count; i++)
            {
                UIMythicDiceCraftItem item = Instantiate(itemPrefab, listRoot);
                item.Bind(mythics[i], TryCraftAndRefresh, GetMaterialCount, GetBaseEquivalentCount, GetRecipeProgressPercent);
                items.Add(item);
            }
        }

        public void RefreshAll()
        {
            RefreshStateText();

            for (int i = 0; i < items.Count; i++)
                items[i].Refresh();

            SortItemsByPercentDesc();
        }

        private bool TryCraftAndRefresh(DiceType mythicType)
        {
            bool crafted = TryCraft(mythicType);
            RefreshAll();
            return crafted;
        }

        private void RefreshStateText()
        {
            if (stateText == null)
                return;

            InGameState state = GameManager.Instance != null ? GameManager.Instance.inGameState : InGameState.None;
            stateText.SetText($"State: {state}");
        }

        private int GetMaterialCount(DiceType type, int star)
        {
            if (DiceTypeStarManager.Instance == null)
                return 0;

            return DiceTypeStarManager.Instance.GetTypeStarCount(type, star);
        }

        private int GetBaseEquivalentCount(DiceType type)
        {
            if (DiceTypeStarManager.Instance == null)
                return 0;

            return DiceTypeStarManager.Instance.GetTypeBaseEquivalent(type);
        }

        private int GetRecipeProgressPercent(DiceType mythicType)
        {
            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (DiceTypeStarManager.Instance == null || recipe == null || recipe.Count == 0)
                return 0;

            return DiceTypeStarManager.Instance.GetRecipeProgressPercent(recipe);
        }

        private void SortItemsByPercentDesc()
        {
            items.Sort((a, b) =>
            {
                int pa = GetRecipeProgressPercent(a.MythicType);
                int pb = GetRecipeProgressPercent(b.MythicType);
                return pb.CompareTo(pa);
            });

            for (int i = 0; i < items.Count; i++)
                items[i].transform.SetSiblingIndex(i);
        }

        private bool TryCraft(DiceType mythicType)
        {
            if (DiceMetaDataProvider.IsSummonable(mythicType))
                return false;

            if (UIBoard.Instance == null || UIBoard.Instance.diceMap == null)
                return false;

            int slotIndex = GetFirstEmptySlot();
            if (slotIndex < 0)
                return false;

            IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe = DiceMetaDataProvider.GetRecipeMaterials(mythicType);
            if (recipe == null || recipe.Count == 0)
                return false;

            if (DiceTypeStarManager.Instance == null || !DiceTypeStarManager.Instance.CanCraft(recipe))
                return false;

            consumeBuffer.Clear();
            UIDice[] map = UIBoard.Instance.diceMap;
            bool[] used = new bool[map.Length];

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                int found = 0;
                for (int idx = 0; idx < map.Length; idx++)
                {
                    if (used[idx])
                        continue;

                    UIDice dice = map[idx];
                    if (dice == null)
                        continue;

                    if (dice.Type != req.diceType || dice.Star != req.star)
                        continue;

                    used[idx] = true;
                    consumeBuffer.Add(dice);
                    found++;
                    if (found >= req.count)
                        break;
                }

                if (found < req.count)
                {
                    consumeBuffer.Clear();
                    return false;
                }
            }

            for (int i = 0; i < consumeBuffer.Count; i++)
            {
                UIDice dice = consumeBuffer[i];
                if (dice == null)
                    continue;

                DiceTypeStarManager.Instance.OnDiceRemove(dice.Type, dice.Star);
                Destroy(dice.gameObject);
            }

            const int mythicStar = 1;
            DiceTypeStarManager.Instance.OnDiceSpawn(mythicType, mythicStar);
            UIBoard.Instance.SpawnDice(mythicType, mythicStar, slotIndex);
            return true;
        }

        private int GetFirstEmptySlot()
        {
            UIDice[] map = UIBoard.Instance.diceMap;
            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] == null)
                    return i;
            }

            return -1;
        }
    }

}
