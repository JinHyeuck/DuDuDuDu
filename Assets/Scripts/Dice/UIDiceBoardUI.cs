using System.Collections.Generic;
using UnityEngine;

public class UIDiceBoardUI : MonoBehaviour
{
    public static UIDiceBoardUI Instance;

    public Transform TypeUIParent;
    public GameObject TypeUIPrefab;

    private Dictionary<DiceType, TypeUIComponent> typeUIDict = new Dictionary<DiceType, TypeUIComponent>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
        {
            GameObject go = Instantiate(TypeUIPrefab, TypeUIParent);
            TypeUIComponent comp = go.GetComponent<TypeUIComponent>();
            comp.Init(type);
            comp.UpdateVisual();
            typeUIDict[type] = comp;
        }
    }

    public void UpdateTypeStars()
    {
        foreach (var kvp in typeUIDict)
            kvp.Value.UpdateVisual();
    }
}
