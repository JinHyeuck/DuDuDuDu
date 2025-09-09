using System.Collections.Generic;
using UnityEngine;

public class DiceTypeStarManager : MonoBehaviour
{
    public static DiceTypeStarManager Instance;

    public Dictionary<DiceType, int> typeCountTotals = new Dictionary<DiceType, int>();
    private Dictionary<DiceType, int> typeStarTotals = new Dictionary<DiceType, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
        {
            typeCountTotals[type] = 0;
            typeStarTotals[type] = 0;
        }
    }

    public void OnDiceSpawn(DiceType type, int star)
    {
        AddStars(type, star);
        UIDiceBoardUI.Instance?.UpdateTypeStars();
        PlayerController.Instance?.RefreshDice();
    }

    public void OnDiceRemove(DiceType type, int star)
    {
        RemoveStars(type, star);
        UIDiceBoardUI.Instance?.UpdateTypeStars();
        PlayerController.Instance?.RefreshDice();
    }

    public void OnDiceMerge(DiceType typeFrom, int starFrom, DiceType typeTo)
    {
        RemoveStars(typeFrom, starFrom);
        AddStars(typeTo, 1);
        UIDiceBoardUI.Instance?.UpdateTypeStars();
        PlayerController.Instance?.RefreshDice();
    }

    private void AddStars(DiceType type, int stars)
    {
        if (typeCountTotals.ContainsKey(type))
            typeCountTotals[type] += 1;

        if (typeStarTotals.ContainsKey(type))
            typeStarTotals[type] += stars;
    }

    private void RemoveStars(DiceType type, int stars)
    {
        if (typeCountTotals.ContainsKey(type))
        { 
            typeCountTotals[type] -= 1;
            if (typeCountTotals[type] < 0) typeCountTotals[type] = 0;
        }


        if (!typeStarTotals.ContainsKey(type)) return;
        typeStarTotals[type] -= stars;
        if (typeStarTotals[type] < 0) typeStarTotals[type] = 0;
    }

    public int GetTypeCount(DiceType type)
    {
        if (!typeCountTotals.ContainsKey(type)) return 0;
        return typeCountTotals[type];
    }

    public int GetTypeStars(DiceType type)
    {
        if (!typeStarTotals.ContainsKey(type)) return 0;
        return typeStarTotals[type];
    }

    public void ResetAll()
    {
        foreach (var key in typeStarTotals.Keys)
            typeStarTotals[key] = 0;

        UIDiceBoardUI.Instance?.UpdateTypeStars();
    }
}
