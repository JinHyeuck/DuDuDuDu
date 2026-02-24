using UnityEngine;

namespace OJ
{
    public class MergeSystem : MonoBehaviour
    {
        public static MergeSystem Instance;

        public const int MaxStar = 5;

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

        public bool TryMerge(UIDice from, UIDice to)
        {
            if (GameManager.Instance.inGameState == InGameState.Wave)
                return false;

            if (from == null || to == null)
                return false;

            // No merge beyond 5-star. Composite/multi-attribute dice is removed.
            if (to.Star >= MaxStar || from.Star >= MaxStar)
                return false;

            if (from.Type != to.Type || from.Star != to.Star)
                return false;

            DiceType mergedType = from.Type;
            DiceTypeStarManager.Instance.OnDiceRemove(from.Type, from.Star);
            DiceTypeStarManager.Instance.OnDiceRemove(to.Type, to.Star);

            int newStar = Mathf.Min(MaxStar, to.Star + 1);

            to.Init(mergedType, newStar, to.SlotIndex);

            DiceTypeStarManager.Instance.OnDiceSpawn(mergedType, newStar);

            Destroy(from.gameObject);

            return true;
        }
    }
}
