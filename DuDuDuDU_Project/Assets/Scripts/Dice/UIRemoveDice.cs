using UnityEngine;

namespace OJ
{
    public class UIRemoveDice : MonoBehaviour
    {
        public void RemoveDice(UIDice uIDice)
        {
            DiceTypeStarManager.Instance.OnDiceRemove(uIDice.Type, uIDice.Star);

            // 원본 다이스 제거
            Destroy(uIDice.gameObject);

            UIDiceSummonSystem.Instance?.AddSP(5);
        }
    }
}
