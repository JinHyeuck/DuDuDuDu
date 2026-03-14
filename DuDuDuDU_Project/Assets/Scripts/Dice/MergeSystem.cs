using UnityEngine;
using System.Collections.Generic;

namespace OJ
{
    public class MergeSystem : MonoBehaviour
    {
        public static MergeSystem Instance;

        public const int MaxStar = 4;

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

            if (!DiceMetaDataProvider.CanMerge(from.Type) || !DiceMetaDataProvider.CanMerge(to.Type))
                return false;

            // No merge beyond max star. Composite/multi-attribute dice is removed.
            if (to.Star >= MaxStar || from.Star >= MaxStar)
                return false;

            if (from.Type != to.Type || from.Star != to.Star)
                return false;

            DiceType fromType = from.Type;
            int fromStar = from.Star;
            DiceType toType = to.Type;
            int toStar = to.Star;
            DiceType mergedType = GetRandomMergedType(fromType);
            DiceTypeStarManager.Instance.OnDiceRemove(fromType, fromStar);
            DiceTypeStarManager.Instance.OnDiceRemove(toType, toStar);

            int newStar = Mathf.Min(MaxStar, to.Star + 1);

            to.Init(mergedType, newStar, to.SlotIndex);

            DiceTypeStarManager.Instance.OnDiceSpawn(mergedType, newStar);
            RunHistoryManager.Instance?.RecordMerge(
                fromType,
                fromStar,
                toType,
                toStar,
                mergedType,
                newStar,
                GameManager.Instance != null ? GameManager.Instance.CurrentWaveIndex : 0);

            Destroy(from.gameObject);

            return true;
        }

        private DiceType GetRandomMergedType(DiceType fallbackType)
        {
            UIDiceSummonSystem summonSystem = UIDiceSummonSystem.Instance;
            if (summonSystem == null || summonSystem.deckTypes == null || summonSystem.deckTypes.Count == 0)
                return fallbackType;

            List<DiceType> candidates = new List<DiceType>(summonSystem.deckTypes.Count);
            for (int i = 0; i < summonSystem.deckTypes.Count; i++)
            {
                DiceType type = summonSystem.deckTypes[i];
                if (!DiceMetaDataProvider.IsSummonable(type))
                    continue;

                candidates.Add(type);
            }

            if (candidates.Count == 0)
                return fallbackType;

            return candidates[Random.Range(0, candidates.Count)];
        }
    }
}
