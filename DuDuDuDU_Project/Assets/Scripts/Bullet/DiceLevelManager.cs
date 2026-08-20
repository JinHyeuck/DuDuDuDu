using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class DiceLevelManager : MonoSingleton<DiceLevelManager>
    {
        private const string SaveKeyPrefix = "OJ.Bullet.Level.";
        private readonly Dictionary<DiceType, int> levels = new Dictionary<DiceType, int>();

        public event Action<DiceType, int> OnDiceLevelChanged;

        protected override void Init()
        {
            LoadAll();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                SaveAll();
        }

        private void OnApplicationQuit()
        {
            SaveAll();
        }

        public int GetLevel(DiceType diceType)
        {
            if (levels.TryGetValue(diceType, out int level))
                return level;
            return 1;
        }

        public void SetLevel(DiceType diceType, int level, bool saveNow = true)
        {
            if (diceType == DiceType.Max)
                return;

            int clamped = Mathf.Max(1, level);
            levels[diceType] = clamped;
            OnDiceLevelChanged?.Invoke(diceType, clamped);

            if (saveNow)
            {
                Save(diceType);
                PlayerPrefs.Save();
            }
        }

        public bool TryLevelUp(DiceType diceType)
        {
            int currentLevel = GetLevel(diceType);
            var cost = DiceMetaDataProvider.GetUpgradeCost(diceType, currentLevel);

            var costs = new Dictionary<PointType, int>
            {
                { PointType.Gold, cost.goldCost },
                { PointManager.ToScrollType(diceType), cost.scrollCost }
            };

            if (PointManager.Instance == null || !PointManager.Instance.TrySpend(costs))
                return false;

            SetLevel(diceType, currentLevel + 1);
            return true;
        }

        public (int goldCost, int scrollCost) GetNextUpgradeCost(DiceType diceType)
        {
            return DiceMetaDataProvider.GetUpgradeCost(diceType, GetLevel(diceType));
        }

        public void SaveAll()
        {
            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;
                Save(diceType);
            }

            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            levels.Clear();

            foreach (DiceType diceType in Enum.GetValues(typeof(DiceType)))
            {
                if (diceType == DiceType.Max)
                    continue;

                string key = SaveKeyPrefix + diceType;
                int level = Mathf.Max(1, PlayerPrefs.GetInt(key, 1));
                levels[diceType] = level;
            }
        }

        private void Save(DiceType diceType)
        {
            if (diceType == DiceType.Max)
                return;
            PlayerPrefs.SetInt(SaveKeyPrefix + diceType, GetLevel(diceType));
        }
    }
}
