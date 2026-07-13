using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace OJ
{
    public class UIBoard : MonoBehaviour
    {
        public static UIBoard Instance;

        [Header("Board Settings")]
        public GridLayoutGroup grid;
        public GameObject slotPrefab;
        public UIDice dicePrefab;
        [SerializeField] private UIBattleDiceDetailPanel battleDiceDetailPanel;
        public int rows = 6;
        public int cols = 4;

        private UIDice selectedDice;
        private List<GameObject> slots = new();
        public UIDice[] diceMap;

        public int ShotIndex = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start()
        {
            CreateBoard();
        }

        private void CreateBoard()
        {
            int total = rows * cols;
            diceMap = new UIDice[total];

            for (int i = 0; i < total; i++)
            {
                var slot = Instantiate(slotPrefab, grid.transform);
                slots.Add(slot);
            }

            RelicManager.Instance?.TryApplyStageStartDice();
        }

        public void SpawnDice(DiceType type, int star, int slotIndex)
        {
            if (diceMap[slotIndex] != null) return;

            var dice = Instantiate(dicePrefab, slots[slotIndex].transform);
            dice.Init(type, star, slotIndex);
            diceMap[slotIndex] = dice;
        }

        public void ClearDice(int slotIndex)
        {
            if (diceMap[slotIndex] != null)
            {
                Destroy(diceMap[slotIndex].gameObject);
                diceMap[slotIndex] = null;
            }
        }

        public void OnDiceClicked(UIDice dice)
        {
            if (selectedDice == null)
            {
                selectedDice = dice;
                Highlight(dice, true);
            }
            else
            {
                if (selectedDice == dice)
                {
                    Highlight(dice, false);
                    selectedDice = null;
                    return;
                }

                MergeSystem.Instance.TryMerge(selectedDice, dice);
                Highlight(selectedDice, false);
                selectedDice = null;
            }
        }

        public void OpenBattleDiceDetail(UIDice dice)
        {
            if (dice == null)
                return;

            if (battleDiceDetailPanel == null)
            {
                UIBattleDiceDetailPanel[] panels = Resources.FindObjectsOfTypeAll<UIBattleDiceDetailPanel>();
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] != null && panels[i].gameObject.scene.IsValid())
                    {
                        battleDiceDetailPanel = panels[i];
                        break;
                    }
                }
            }

            if (battleDiceDetailPanel != null)
                battleDiceDetailPanel.Open(dice);
        }

        private void Highlight(UIDice dice, bool on)
        {
        }

        public UIDice GetDice(int slotIndex) => diceMap[slotIndex];

        public int GetSlotIndexFromObject(GameObject hitObj)
        {
            if (hitObj == null)
                return -1;

            Transform hitTransform = hitObj.transform;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                    continue;

                Transform slotTransform = slots[i].transform;
                if (hitTransform == slotTransform || hitTransform.IsChildOf(slotTransform))
                    return i;
            }

            return -1;
        }

        public bool TryMoveDiceToSlot(UIDice dice, int toSlotIndex)
        {
            if (dice == null || diceMap == null)
                return false;

            if (toSlotIndex < 0 || toSlotIndex >= diceMap.Length)
                return false;

            int fromSlotIndex = dice.SlotIndex;
            if (fromSlotIndex < 0 || fromSlotIndex >= diceMap.Length)
                return false;

            if (diceMap[fromSlotIndex] != dice)
                return false;

            if (fromSlotIndex == toSlotIndex)
            {
                dice.transform.SetParent(slots[toSlotIndex].transform);
                dice.transform.localPosition = Vector3.zero;
                dice.transform.localScale = Vector3.one;
                return true;
            }

            if (diceMap[toSlotIndex] != null)
                return false;

            diceMap[fromSlotIndex] = null;
            diceMap[toSlotIndex] = dice;
            dice.SetSlotIndex(toSlotIndex);

            dice.transform.SetParent(slots[toSlotIndex].transform);
            dice.transform.localPosition = Vector3.zero;
            dice.transform.localScale = Vector3.one;
            return true;
        }

        public bool TrySwapDice(UIDice a, UIDice b)
        {
            if (a == null || b == null || a == b || diceMap == null)
                return false;

            int aIndex = a.SlotIndex;
            int bIndex = b.SlotIndex;

            if (aIndex < 0 || aIndex >= diceMap.Length || bIndex < 0 || bIndex >= diceMap.Length)
                return false;

            if (diceMap[aIndex] != a || diceMap[bIndex] != b)
                return false;

            diceMap[aIndex] = b;
            diceMap[bIndex] = a;

            a.SetSlotIndex(bIndex);
            b.SetSlotIndex(aIndex);

            a.transform.SetParent(slots[bIndex].transform);
            a.transform.localPosition = Vector3.zero;
            a.transform.localScale = Vector3.one;

            b.transform.SetParent(slots[aIndex].transform);
            b.transform.localPosition = Vector3.zero;
            b.transform.localScale = Vector3.one;

            return true;
        }
    }
}
