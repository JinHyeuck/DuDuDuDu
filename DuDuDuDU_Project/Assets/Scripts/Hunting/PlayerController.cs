using System.Collections.Generic;
using UnityEngine;
using VContainer;
using OJ.Dice;
using OJ.DI;
using OJ.Utils;

namespace OJ.Hunting
{
    public class PlayerController : MonoBehaviour
    {
        public CharacterAnimation characterAnimation;
        public CharacterAnimation bowAnimation;
        public Transform bowTransform;

        public DiceType CheatDiceType = DiceType.Max;

        public Transform firePoint;
        public float fireRate = 0.5f;
        private float timer = 0f;
        private int shotindex = -1;

        private readonly Dictionary<UIDice, float> diceNextReadyTime = new Dictionary<UIDice, float>();
        private readonly List<UIDice> removeCooldownBuffer = new List<UIDice>();
        private readonly List<UIDice> cooldownAdjustBuffer = new List<UIDice>();
        private bool wasWaveState = false;

        // 8.3b: 배틀 스코프가 채운다. BattleScene 안에서는 null 이 아니다.
        // 스코프 빌드는 씬의 모든 Awake 뒤·모든 Start 앞이므로 Awake 에서는 아직 쓸 수 없다.
        [Inject] private IBattleRefs battle;

        private void Start()
        {
            characterAnimation.PlayAnimation(CharacterState.Idle);
            bowAnimation.PlayAnimation(CharacterState.Idle);
        }

        void Update()
        {
            bool isWaveState = battle.Game.inGameState == InGameState.Wave;
            if (!isWaveState)
            {
                if (wasWaveState)
                    ResetAllDiceCooldowns();
                return;
            }
            wasWaveState = true;

            // UIBoard 자체는 씬에 상주하므로 null 검사를 지웠다. diceMap 은 UIBoard 가
            // 보드를 만들기 전까지 비어 있을 수 있는 '데이터'라 검사를 남긴다.
            if (battle.Board.diceMap == null
                || battle.Board.diceMap.Length <= 0)
                return;


            timer += Time.deltaTime;
            if (timer < fireRate)
                return;

            CleanupCooldownMap();

            if (!TryShootReadyDice())
                return;
        }

        public void RefreshDice()
        {
        }

        public float GetFireCycleProgress01()
        {
            // 이 메서드를 부르는 PlayerFireRateUI 도 BattleScene 에 산다. 창구가 채워진 뒤에만
            // 도는 자리라 GameManager null 검사를 지웠다.
            if (battle.Game.inGameState != InGameState.Wave)
                return 0f;

            if (fireRate <= 0f)
                return 1f;

            return Mathf.Clamp01(timer / fireRate);
        }

        private bool TryGetReadyDice(out UIDice selectedDice)
        {
            selectedDice = null;

            UIDice[] map = battle.Board.diceMap;
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

        private bool TryShootReadyDice()
        {
            UIDice[] map = battle.Board.diceMap;
            int total = map.Length;
            if (total <= 0)
                return false;

            float now = Time.time;
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

                if (!ShootAtClosest(dice))
                    continue;

                shotindex = dice.SlotIndex;
                SetDiceNextCooldown(dice);
                dice.PlayLevelUpEffect();
                timer = 0f;
                return true;
            }

            return false;
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
            shotindex = -1;
            diceNextReadyTime.Clear();
            wasWaveState = false;
        }

        public void ReduceRemainingCooldownPercentForOtherDice(float percent, int targetCount, UIDice sourceDice = null)
        {
            if (percent <= 0f || targetCount <= 0)
                return;

            cooldownAdjustBuffer.Clear();
            foreach (var pair in diceNextReadyTime)
            {
                if (pair.Key == null)
                    continue;

                if (sourceDice != null && pair.Key == sourceDice)
                    continue;

                float remaining = GetDiceCooldownRemaining(pair.Key);
                if (remaining <= 0.0001f)
                    continue;

                cooldownAdjustBuffer.Add(pair.Key);
            }

            for (int i = cooldownAdjustBuffer.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cooldownAdjustBuffer[i], cooldownAdjustBuffer[j]) = (cooldownAdjustBuffer[j], cooldownAdjustBuffer[i]);
            }

            int applyCount = Mathf.Min(targetCount, cooldownAdjustBuffer.Count);
            float ratio = Mathf.Clamp01(percent * 0.01f);
            for (int i = 0; i < applyCount; i++)
            {
                UIDice dice = cooldownAdjustBuffer[i];
                if (!diceNextReadyTime.TryGetValue(dice, out float endTime))
                    continue;

                float remaining = Mathf.Max(0f, endTime - Time.time);
                float reduceAmount = remaining * ratio;
                diceNextReadyTime[dice] = Mathf.Max(Time.time, endTime - reduceAmount);
            }
        }

        bool ShootAtClosest(UIDice sourceDice)
        {
            if (sourceDice == null)
                return false;

            DiceType diceType = sourceDice.Type;
            if (System.Enum.IsDefined(typeof(DiceType), CheatDiceType) && CheatDiceType != DiceType.Max)
            {
                diceType = CheatDiceType;
            }
            int diceStar = sourceDice.Star;

            // 타임 다이스는 <b>쿨감을 먼저 하고 총알 경로로 흘러간다.</b> (진화 개편)
            //
            // 예전에는 여기서 쿨감만 하고 return true 로 끝냈다 — 피해가 0 인 순수 유틸이라
            // 쏠 것이 없었기 때문이다. 지금은 4성 썬더가 진화해 도달하는 상위 단계라
            // 피해가 있어야 한다(바람 다이스와 같은 사정이다. WindDiceEffect 주석 참조).
            //
            // <b>쿨감을 사거리 안에 적이 있을 때만 하는 이유.</b> 아래 총알 경로는 대상이
            // 없으면 <c>return false</c> 로 빠지고, 그러면 이 다이스는 쿨타임을 소모하지
            // 않아 <b>다음 프레임에 다시 들어온다.</b> 쿨감을 먼저 해 두면 그 재시도마다
            // 쿨감이 또 걸려서, 적이 없는 동안 보드 전체 쿨타임이 프레임마다 깎인다.
            // 그래서 대상 유무를 먼저 확인하고, 없으면 예전처럼 쿨감만 하고 끝낸다.
            if (diceType == DiceType.Time)
            {
                int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(DiceType.Time) : 1;
                float reducePercent = DiceMetaDataProvider.GetTimeCooldownReducePercent(DiceType.Time, level);
                int targetCount = DiceMetaDataProvider.GetTimeTargetCount(level);
                ReduceRemainingCooldownPercentForOtherDice(reducePercent, targetCount, sourceDice);

                if (battle.Monsters.GetClosestMonster(firePoint.position) == null)
                {
                    characterAnimation.PlayAnimation(CharacterState.Attack, fireRate);
                    bowAnimation.PlayAnimation(CharacterState.Attack, fireRate);
                    return true;
                }

                // 대상이 있으면 아래 일반 경로로 내려간다. TimeDiceEffect.ApplyOnHit 는
                // 쿨감을 하지 않는다 — 여기서 이미 했고, 거기서는 sourceDice 를 몰라
                // 자기 자신의 쿨타임까지 깎아 버린다.
            }

            if (diceType == DiceType.Wind)
            {
                // AttackContent 는 씬 상주라 null 검사를 지웠다. 캐스트 실패(false)만 남긴다 —
                // 그쪽은 자원 부족 같은 정상 실패라 의미가 다르다.
                bool casted = battle.Attack.TryCastNoTarget(diceType, diceStar);
                if (!casted)
                    return false;

                characterAnimation.PlayAnimation(CharacterState.Attack, fireRate);
                bowAnimation.PlayAnimation(CharacterState.Attack, fireRate);
                return true;
            }

            Monster target = battle.Monsters.GetClosestMonster(firePoint.position);
            if (target == null)
                return false;

            Vector2 dir = (target.transform.position - firePoint.position).normalized;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            bowTransform.rotation = Quaternion.Euler(0, 0, angle);

            Bullet bulletObj = battle.Bullets.GetBullet();
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
