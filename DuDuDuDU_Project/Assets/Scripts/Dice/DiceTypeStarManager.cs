using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class DiceTypeStarManager : MonoBehaviour
    {
        public static DiceTypeStarManager Instance;
        public event System.Action OnDiceInventoryChanged;

        public Dictionary<DiceType, int> typeCountTotals = new Dictionary<DiceType, int>();
        private Dictionary<DiceType, int> typeStarTotals = new Dictionary<DiceType, int>();
        private Dictionary<(DiceType type, int star), int> typeStarCounts = new Dictionary<(DiceType type, int star), int>();

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
                if (type == DiceType.Max)
                    continue;
                typeCountTotals[type] = 0;
                typeStarTotals[type] = 0;

                for (int star = 1; star <= MergeSystem.MaxStar; star++)
                    typeStarCounts[(type, star)] = 0;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void OnDiceSpawn(DiceType type, int star)
        {
            AddStars(type, star);
            UIDiceBoardUI.Instance?.UpdateTypeStars();
            PlayerController.Instance?.RefreshDice();
            OnDiceInventoryChanged?.Invoke();
        }

        public void OnDiceRemove(DiceType type, int star)
        {
            RemoveStars(type, star);
            UIDiceBoardUI.Instance?.UpdateTypeStars();
            PlayerController.Instance?.RefreshDice();
            OnDiceInventoryChanged?.Invoke();
        }

        private void AddStars(DiceType type, int stars)
        {
            if (stars <= 0)
                return;

            if (typeCountTotals.ContainsKey(type))
                typeCountTotals[type] += 1;

            if (typeStarTotals.ContainsKey(type))
                typeStarTotals[type] += stars;

            var key = (type, stars);
            typeStarCounts.TryGetValue(key, out int count);
            typeStarCounts[key] = count + 1;
        }

        private void RemoveStars(DiceType type, int stars)
        {
            if (stars <= 0)
                return;

            if (typeCountTotals.ContainsKey(type))
            {
                typeCountTotals[type] -= 1;
                if (typeCountTotals[type] < 0) typeCountTotals[type] = 0;
            }

            if (!typeStarTotals.ContainsKey(type)) return;
            typeStarTotals[type] -= stars;
            if (typeStarTotals[type] < 0) typeStarTotals[type] = 0;

            var key = (type, stars);
            if (typeStarCounts.TryGetValue(key, out int count))
            {
                count--;
                if (count < 0) count = 0;
                typeStarCounts[key] = count;
            }
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

        public int GetTypeStarCount(DiceType type, int star)
        {
            if (star <= 0)
                return 0;

            typeStarCounts.TryGetValue((type, star), out int count);
            return count;
        }

        public int GetTypeBaseEquivalent(DiceType type)
        {
            int total = 0;
            for (int star = 1; star <= MergeSystem.MaxStar; star++)
            {
                int count = GetTypeStarCount(type, star);
                total += count * GetBaseUnitFromStar(star);
            }

            return total;
        }

        public bool CanCraft(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            if (recipe == null || recipe.Count == 0)
                return false;

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                if (GetTypeStarCount(req.diceType, req.star) < req.count)
                    return false;
            }

            return true;
        }

        public int GetRecipeProgressPercent(IReadOnlyList<DiceMetaDataDatabase.DiceRecipeMaterial> recipe)
        {
            if (recipe == null || recipe.Count == 0)
                return 0;

            long totalRequiredBase = 0;
            long satisfiedBase = 0;

            for (int i = 0; i < recipe.Count; i++)
            {
                DiceMetaDataDatabase.DiceRecipeMaterial req = recipe[i];
                long requiredBase = (long)req.count * GetBaseUnitFromStar(req.star);
                totalRequiredBase += requiredBase;

                int haveExact = GetTypeStarCount(req.diceType, req.star);
                int usedCount = Mathf.Min(haveExact, req.count);
                satisfiedBase += (long)usedCount * GetBaseUnitFromStar(req.star);
            }

            if (totalRequiredBase <= 0)
                return 0;

            float ratio = (float)satisfiedBase / totalRequiredBase;
            return Mathf.Clamp(Mathf.RoundToInt(ratio * 100f), 0, 100);
        }

        public void ResetAll()
        {
            foreach (var key in typeCountTotals.Keys)
                typeCountTotals[key] = 0;

            foreach (var key in typeStarTotals.Keys)
                typeStarTotals[key] = 0;

            foreach (var key in typeStarCounts.Keys)
                typeStarCounts[key] = 0;

            UIDiceBoardUI.Instance?.UpdateTypeStars();
            OnDiceInventoryChanged?.Invoke();
        }

        private static int GetBaseUnitFromStar(int star)
        {
            int s = Mathf.Max(1, star);
            return 1 << (s - 1);
        }

    }
}
