using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "DiceTypeResources", menuName = "Dice/TypeResourceManager")]
public class DiceTypeResourceManager : ScriptableObject
{
    [System.Serializable]
    public class TypeVisual
    {
        public DiceType Type;
        public Color Color;
        public Sprite Icon;
    }

    public List<TypeVisual> TypeVisuals = new List<TypeVisual>();
    private Dictionary<DiceType, TypeVisual> visualDict = new Dictionary<DiceType, TypeVisual>();

    private void OnEnable()
    {
        visualDict.Clear();
        foreach (var t in TypeVisuals)
            visualDict[t.Type] = t;
    }

    public Color GetColor(DiceType type)
    {
        if (visualDict.TryGetValue(type, out var visual))
            return visual.Color;
        return Color.white;
    }

    public Sprite GetIcon(DiceType type)
    {
        if (visualDict.TryGetValue(type, out var visual))
            return visual.Icon;
        return null;
    }
}
