using UnityEngine;

namespace OJ
{
    public class StaticResource : MonoBehaviour
    {
        public static StaticResource Instance;

        public DiceTypeResourceManager DiceTypeResourceManager;

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
    }

}
