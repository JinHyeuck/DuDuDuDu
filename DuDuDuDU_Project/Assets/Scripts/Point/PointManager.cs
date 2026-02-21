using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class PointManager : MonoBehaviour
    {
        public static PointManager Instance { get; private set; }

        private const string SaveKeyPrefix = "OJ.Point.";

        public event Action<PointType, int> OnPointChanged;

        private readonly Dictionary<PointType, int> points = new Dictionary<PointType, int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var go = new GameObject(nameof(PointManager));
            go.AddComponent<PointManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
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

        public int Get(PointType pointType)
        {
            if (points.TryGetValue(pointType, out int value))
                return value;

            return 0;
        }

        public void Set(PointType pointType, int value, bool saveNow = true)
        {
            if (pointType == PointType.Max)
                return;

            int clamped = Mathf.Max(0, value);
            points[pointType] = clamped;
            OnPointChanged?.Invoke(pointType, clamped);

            if (saveNow)
                Save(pointType);
        }

        public void Add(PointType pointType, int amount, bool saveNow = true)
        {
            if (amount <= 0)
                return;

            Set(pointType, Get(pointType) + amount, saveNow);
        }

        public bool TrySpend(PointType pointType, int amount, bool saveNow = true)
        {
            if (amount < 0)
                return false;

            if (Get(pointType) < amount)
                return false;

            Set(pointType, Get(pointType) - amount, saveNow);
            return true;
        }

        public bool CanAfford(IReadOnlyDictionary<PointType, int> costs)
        {
            foreach (var pair in costs)
            {
                if (Get(pair.Key) < pair.Value)
                    return false;
            }

            return true;
        }

        public bool TrySpend(IReadOnlyDictionary<PointType, int> costs)
        {
            if (!CanAfford(costs))
                return false;

            foreach (var pair in costs)
            {
                Set(pair.Key, Get(pair.Key) - pair.Value, false);
            }

            SaveAll();
            return true;
        }

        public bool TrySpendUpgrade(DiceType diceType, int goldCost, int scrollCost)
        {
            var costs = new Dictionary<PointType, int>
            {
                { PointType.Gold, goldCost },
                { ToScrollType(diceType), scrollCost }
            };

            return TrySpend(costs);
        }

        public static PointType ToScrollType(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.Normal:
                    return PointType.NormalScroll;
                case DiceType.Fire:
                    return PointType.FireScroll;
                case DiceType.Ice:
                    return PointType.IceScroll;
                case DiceType.Poison:
                    return PointType.PoisonScroll;
                case DiceType.Thunder:
                    return PointType.ThunderScroll;
                default:
                    throw new ArgumentOutOfRangeException(nameof(diceType), diceType, "Unsupported dice type.");
            }
        }

        public void SaveAll()
        {
            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                Save(pointType);
            }

            PlayerPrefs.Save();
        }

        public void LoadAll()
        {
            points.Clear();

            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                string key = BuildKey(pointType);
                int value = PlayerPrefs.GetInt(key, 0);
                points[pointType] = Mathf.Max(0, value);
            }
        }

        public void ResetAllForDebug()
        {
            foreach (PointType pointType in Enum.GetValues(typeof(PointType)))
            {
                if (pointType == PointType.Max)
                    continue;

                Set(pointType, 0, false);
                PlayerPrefs.DeleteKey(BuildKey(pointType));
            }

            PlayerPrefs.Save();
        }

        private void Save(PointType pointType)
        {
            if (pointType == PointType.Max)
                return;

            PlayerPrefs.SetInt(BuildKey(pointType), Get(pointType));
        }

        private static string BuildKey(PointType pointType)
        {
            return SaveKeyPrefix + pointType;
        }
    }
}
