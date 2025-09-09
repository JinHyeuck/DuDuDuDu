using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIBoard : MonoBehaviour
{
    public static UIBoard Instance;

    [Header("Board Settings")]
    public GridLayoutGroup grid;      // BoardPanel�� ���� GridLayoutGroup
    public GameObject slotPrefab;     // Slot (��ĭ UI)
    public UIDice dicePrefab;         // UIDice ������
    public int rows = 6;
    public int cols = 4;

    private UIDice selectedDice;
    private List<GameObject> slots = new();
    private UIDice[] diceMap; // ���� �ε����� ���̽� ����

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

            // ���� �õ�
            bool merged = MergeSystem.Instance.TryMerge(selectedDice, dice);
            Highlight(selectedDice, false);
            selectedDice = null;
        }
    }

    private void Highlight(UIDice dice, bool on)
    {
        // ���� �� �׵θ� ���� �ٲٱ� ���� ȿ�� �ֱ�
    }

    public UIDice GetDice(int slotIndex) => diceMap[slotIndex];
}
