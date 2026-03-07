using UnityEditor;
using UnityEngine;

namespace OJ.Editor
{
    [CustomPropertyDrawer(typeof(DiceMetaDataDatabase.DiceMeta))]
    public class DiceMetaPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty diceTypeProp = property.FindPropertyRelative("diceType");
            string diceTypeName = "Dice Meta";
            if (diceTypeProp != null && diceTypeProp.propertyType == SerializedPropertyType.Enum)
            {
                int index = diceTypeProp.enumValueIndex;
                if (index >= 0 && index < diceTypeProp.enumDisplayNames.Length)
                    diceTypeName = diceTypeProp.enumDisplayNames[index];
            }

            EditorGUI.PropertyField(position, property, new GUIContent(diceTypeName), true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true);
        }
    }
}
