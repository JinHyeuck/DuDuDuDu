using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIBoard : MonoBehaviour
{
    public static UIBoard Instance;

    [Header("Board Settings")]
    public GridLayoutGroup grid;      // BoardPanel에 붙인 GridLayoutGroup
    public GameObject slotPrefab;     // Slot (빈칸 UI)
    public UIDice dicePrefab;         // UIDice 프리팹
    public int rows = 6;
    public int cols = 4;

    private UIDice selectedDice;
    private List<GameObject> slots = new();
    private UIDice[] diceMap; // 슬롯 인덱스별 다이스 참조

    private void Awake()
    {
        Instance = this;
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

            // 병합 시도
            bool merged = MergeSystem.Instance.TryMerge(selectedDice, dice);
            Highlight(selectedDice, false);
            selectedDice = null;
        }
    }

    private void Highlight(UIDice dice, bool on)
    {
        // 선택 시 테두리 색상 바꾸기 같은 효과 넣기
    }

    public UIDice GetDice(int slotIndex) => diceMap[slotIndex];
}
