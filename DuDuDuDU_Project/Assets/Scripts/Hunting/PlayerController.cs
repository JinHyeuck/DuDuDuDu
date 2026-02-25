using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance;

        public CharacterAnimation characterAnimation;
        public CharacterAnimation bowAnimation;
        public Transform bowTransform;


        public Transform firePoint;
        public float fireRate = 0.5f;
        private float timer = 0f;
        private int shotindex = 0;

        private readonly Dictionary<UIDice, float> diceNextReadyTime = new Dictionary<UIDice, float>();
        private readonly List<UIDice> removeCooldownBuffer = new List<UIDice>();
        private bool wasWaveState = false;

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

        private void Start()
        {
            characterAnimation.PlayAnimation(CharacterState.Idle);
            bowAnimation.PlayAnimation(CharacterState.Idle);
        }

        void Update()
        {
            bool isWaveState = GameManager.Instance.inGameState == InGameState.Wave;
            if (!isWaveState)
            {
                if (wasWaveState)
                    ResetAllDiceCooldowns();
                return;
            }
            wasWaveState = true;

            if (UIBoard.Instance == null
                || UIBoard.Instance.diceMap == null
                || UIBoard.Instance.diceMap.Length <= 0)
                return;


            timer += Time.deltaTime;
            if (timer < fireRate)
                return;

            CleanupCooldownMap();

            if (!TryGetReadyDice(out UIDice selectedDice))
                return;

            if (!ShootAtClosest(selectedDice.Type, selectedDice.Star))
                return;

            shotindex = selectedDice.SlotIndex;
            SetDiceNextCooldown(selectedDice);
            selectedDice.PlayLevelUpEffect();
            timer = 0f;
        }

        public void RefreshDice()
        {
        }

        private bool TryGetReadyDice(out UIDice selectedDice)
        {
            selectedDice = null;

            UIDice[] map = UIBoard.Instance.diceMap;
            int total = map.Length;
            if (total <= 0)
                return false;

            float now = Time.time;
            float bestReadyTime = float.MaxValue;
            int start = (shotindex + 1 + total) % total;

            for (int offset = 0; offset < total; offset++)
            {
                int idx = (start + offset) % total;
                UIDice dice = map[idx];
                if (dice == null)
                    continue;

                float readyTime = GetNextReadyTime(dice);
                if (readyTime > now)
                    continue;

                if (selectedDice == null || readyTime < bestReadyTime - 0.0001f)
                {
                    selectedDice = dice;
                    bestReadyTime = readyTime;
                }
            }

            return selectedDice != null;
        }

        private void SetDiceNextCooldown(UIDice dice)
        {
            if (dice == null)
                return;

            float cooldown = DiceMetaDataProvider.GetCooldown(dice.Type, dice.Star);
            float effectDuration = Mathf.Max(0f, fireRate);
            diceNextReadyTime[dice] = Time.time + effectDuration + cooldown;
        }

        private float GetNextReadyTime(UIDice dice)
        {
            if (dice == null)
                return 0f;

            return diceNextReadyTime.TryGetValue(dice, out float readyTime) ? readyTime : 0f;
        }

        private void CleanupCooldownMap()
        {
            removeCooldownBuffer.Clear();

            foreach (var pair in diceNextReadyTime)
            {
                if (pair.Key == null)
                    removeCooldownBuffer.Add(pair.Key);
            }

            for (int i = 0; i < removeCooldownBuffer.Count; i++)
            {
                diceNextReadyTime.Remove(removeCooldownBuffer[i]);
            }
        }

        public float GetDiceCooldownFill(UIDice dice)
        {
            if (dice == null)
                return 0f;

            float cooldown = DiceMetaDataProvider.GetCooldown(dice.Type, dice.Star);
            if (cooldown <= 0f)
                return 0f;

            float endTime = GetNextReadyTime(dice);
            float startTime = endTime - cooldown;
            if (Time.time < startTime)
                return 0f;

            float remain = Mathf.Max(0f, endTime - Time.time);
            return Mathf.Clamp01(remain / cooldown);
        }

        public float GetDiceCooldownRemaining(UIDice dice)
        {
            if (dice == null)
                return 0f;

            float cooldown = DiceMetaDataProvider.GetCooldown(dice.Type, dice.Star);
            if (cooldown <= 0f)
                return 0f;

            float endTime = GetNextReadyTime(dice);
            float startTime = endTime - cooldown;
            if (Time.time < startTime)
                return 0f;

            return Mathf.Max(0f, endTime - Time.time);
        }

        private void ResetAllDiceCooldowns()
        {
            timer = 0f;
            diceNextReadyTime.Clear();
            wasWaveState = false;
        }

        bool ShootAtClosest(DiceType diceType, int diceStar)
        {
            Monster target = MonsterManager.Instance.GetClosestMonster(firePoint.position);
            if (target == null) return false;

            Vector2 dir = (target.transform.position - firePoint.position).normalized;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            bowTransform.rotation = Quaternion.Euler(0, 0, angle);

            Bullet bulletObj = BulletPool.Instance.GetBullet();
            bulletObj.transform.position = firePoint.position;
            bulletObj.transform.rotation = Quaternion.identity;
            bulletObj.SetBulletStat(diceType, diceStar);
            bulletObj.Shoot(dir);
            characterAnimation.PlayAnimation(CharacterState.Attack, fireRate);
            bowAnimation.PlayAnimation(CharacterState.Attack, fireRate);
            return true;
        }
    }

}
