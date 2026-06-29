using TMPro;
using UnityEngine;

namespace OJ
{
    public class UIEquipmentEffectRowItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text gemNameText;
        [SerializeField] private TMP_Text effectText;

        public void Refresh(string gemName, string effect)
        {
            if (gemNameText != null)
                gemNameText.SetText(gemName ?? string.Empty);
            if (effectText != null)
                effectText.SetText(effect ?? string.Empty);
        }
    }
}
