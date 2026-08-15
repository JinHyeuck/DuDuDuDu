using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace OJ
{
    public class IdleRewardManager : MonoBehaviour
    {
        public const double AutoBattleMaxSeconds = 8d * 60d * 60d;
        public const double SecondsPerAutoBattleClear = 20d * 60d;
        public const double MeatSetIntervalSeconds = 6d * 60d * 60d;
        public const int MeatPerSet = 30;
        public const int MaxMeatSetCount = 30;

        private const string AutoBattleStartKey = "OJ.IdleReward.AutoBattleStartUtcTicks";
        private const string MeatFestivalStartKey = "OJ.IdleReward.MeatFestivalStartUtcTicks";

        public static IdleRewardManager Instance { get; private set; }

        public event Action OnChanged;

        private long autoBattleStartUtcTicks;
        private long meatFestivalStartUtcTicks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
                return;

            var gameObject = new GameObject(nameof(IdleRewardManager));
            gameObject.AddComponent<IdleRewardManager>();
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
            LoadOrInitialize(DateTime.UtcNow);
        }

        public TimeSpan GetAutoBattleElapsed()
        {
            return GetAutoBattleElapsed(DateTime.UtcNow);
        }

        public TimeSpan GetAutoBattleElapsed(DateTime utcNow)
        {
            double seconds = GetElapsedSeconds(autoBattleStartUtcTicks, utcNow);
            return TimeSpan.FromSeconds(Math.Min(AutoBattleMaxSeconds, seconds));
        }

        public float GetAutoBattleProgress01()
        {
            return Mathf.Clamp01((float)(GetAutoBattleElapsed().TotalSeconds / AutoBattleMaxSeconds));
        }

        public int GetAutoBattleStageIndex()
        {
            return StageProgressManager.Instance != null
                ? StageProgressManager.Instance.GetLastClearedStageIndex()
                : 0;
        }

        public List<PointRewardEntry> GetAutoBattleRewards()
        {
            return GetAutoBattleRewards(DateTime.UtcNow);
        }

        public List<PointRewardEntry> GetAutoBattleRewards(DateTime utcNow)
        {
            int stageIndex = GetAutoBattleStageIndex();
            if (stageIndex < 1)
                return new List<PointRewardEntry>();

            double clearCount = GetAutoBattleElapsed(utcNow).TotalSeconds / SecondsPerAutoBattleClear;
            return StageRewardCalculator.BuildAutoBattleRewards(stageIndex, clearCount, BuildRewardSeed(stageIndex));
        }

        public bool CanClaimAutoBattle()
        {
            return GetAutoBattleRewards().Count > 0;
        }

        public bool TryClaimAutoBattle(out List<PointRewardEntry> rewards, out int stageIndex)
        {
            DateTime utcNow = DateTime.UtcNow;
            stageIndex = GetAutoBattleStageIndex();
            rewards = GetAutoBattleRewards(utcNow);
            if (stageIndex < 1 || rewards.Count == 0)
                return false;

            PointRewardUtility.GrantRewards(rewards);
            autoBattleStartUtcTicks = utcNow.Ticks;
            Save();
            OnChanged?.Invoke();
            return true;
        }

        public int GetStoredMeatSetCount()
        {
            return GetStoredMeatSetCount(DateTime.UtcNow);
        }

        public int GetStoredMeatSetCount(DateTime utcNow)
        {
            double elapsedSeconds = GetElapsedSeconds(meatFestivalStartUtcTicks, utcNow);
            int setCount = (int)Math.Floor(elapsedSeconds / MeatSetIntervalSeconds);
            return Mathf.Clamp(setCount, 0, MaxMeatSetCount);
        }

        public TimeSpan GetTimeUntilNextMeatSet()
        {
            DateTime utcNow = DateTime.UtcNow;
            int storedSetCount = GetStoredMeatSetCount(utcNow);
            if (storedSetCount >= MaxMeatSetCount)
                return TimeSpan.Zero;

            double elapsedSeconds = GetElapsedSeconds(meatFestivalStartUtcTicks, utcNow);
            double secondsIntoInterval = elapsedSeconds % MeatSetIntervalSeconds;
            double remainingSeconds = MeatSetIntervalSeconds - secondsIntoInterval;
            return TimeSpan.FromSeconds(Math.Max(0d, remainingSeconds));
        }

        public bool TryClaimMeat(out int meatAmount, out int setCount)
        {
            DateTime utcNow = DateTime.UtcNow;
            setCount = GetStoredMeatSetCount(utcNow);
            meatAmount = setCount * MeatPerSet;
            if (setCount <= 0 || PointManager.Instance == null)
                return false;

            PointManager.Instance.Add(PointType.Stamina, meatAmount);

            if (setCount >= MaxMeatSetCount)
            {
                meatFestivalStartUtcTicks = utcNow.Ticks;
            }
            else
            {
                long consumedTicks = TimeSpan.FromSeconds(MeatSetIntervalSeconds * setCount).Ticks;
                meatFestivalStartUtcTicks = Math.Min(utcNow.Ticks, meatFestivalStartUtcTicks + consumedTicks);
            }

            Save();
            OnChanged?.Invoke();
            return true;
        }

        public void ResetTimersForDebug()
        {
            DateTime utcNow = DateTime.UtcNow;
            autoBattleStartUtcTicks = utcNow.Ticks;
            meatFestivalStartUtcTicks = utcNow.Ticks;
            Save();
            OnChanged?.Invoke();
        }

        private void LoadOrInitialize(DateTime utcNow)
        {
            autoBattleStartUtcTicks = LoadTicks(AutoBattleStartKey, utcNow.Ticks);
            meatFestivalStartUtcTicks = LoadTicks(MeatFestivalStartKey, utcNow.Ticks);

            if (autoBattleStartUtcTicks > utcNow.Ticks)
                autoBattleStartUtcTicks = utcNow.Ticks;
            if (meatFestivalStartUtcTicks > utcNow.Ticks)
                meatFestivalStartUtcTicks = utcNow.Ticks;

            Save();
        }

        private void Save()
        {
            PlayerPrefs.SetString(AutoBattleStartKey, autoBattleStartUtcTicks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(MeatFestivalStartKey, meatFestivalStartUtcTicks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }

        private static long LoadTicks(string key, long fallback)
        {
            string value = PlayerPrefs.GetString(key, string.Empty);
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) && ticks > 0
                ? ticks
                : fallback;
        }

        private static double GetElapsedSeconds(long startUtcTicks, DateTime utcNow)
        {
            if (startUtcTicks <= 0 || utcNow.Ticks <= startUtcTicks)
                return 0d;

            return TimeSpan.FromTicks(utcNow.Ticks - startUtcTicks).TotalSeconds;
        }

        private int BuildRewardSeed(int stageIndex)
        {
            unchecked
            {
                return ((int)autoBattleStartUtcTicks * 397) ^ (int)(autoBattleStartUtcTicks >> 32) ^ stageIndex;
            }
        }
    }
}
