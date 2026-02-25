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

        private void Highlight(UIDice dice, bool on)
        {
        }

        public UIDice GetDice(int slotIndex) => diceMap[slotIndex];
    }
}
