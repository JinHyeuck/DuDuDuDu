using System.Collections.Generic;
using OJ.Core;
using UnityEngine;
using OJ.Element;
using OJ.Equipment;
using OJ.Hunting;
using OJ.Relic;
using OJ.Utils;

using OJ.DI;
namespace OJ.Dice
{
    public static class DiceMetaDataProvider
    {
        // GlobalDamageBalanceMultiplier / KingDiceDamageMultiplier 는 DamageFormula 로 옮겼다.
        // 여기 복사본을 남겨 두면 둘이 조용히 갈라져 데미지가 어긋나므로 일부러 지웠다.
        // 값이 필요하면 DamageFormula.GlobalDamageBalanceMultiplier 를 참조할 것.
        private const float CooldownBalanceMultiplier = 2f;
        private static DiceMetaDataDatabase database;
        private static bool missingDatabaseLogged;
        private static Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta> defaults;

        public static DiceMetaDataDatabase Database
        {
            get
            {
                if (database != null)
                    return database;

                StaticResource resource = StaticResource.Instance;
                if (resource != null && resource.DiceMetaDataDatabase != null)
                {
                    database = resource.DiceMetaDataDatabase;
                    return database;
                }

                // 예전에는 Resources.Load("DiceMetaDataDatabase") 로 물러섰다. 그런데 그 에셋은
                // Assets/ScriptableObject/ 에 있어 Resources 규약 밖이다 — 이 폴백은 한 번도
                // 성공한 적이 없고, 실패를 조용히 코드 기본값으로 덮기만 했다. (2.2)
                LogMissingDatabaseOnce();
                return null;
            }
        }

        private static void LogMissingDatabaseOnce()
        {
            if (missingDatabaseLogged)
                return;

            missingDatabaseLogged = true;
            Debug.LogError(
                "DiceMetaDataDatabase 를 찾지 못했다. StaticResource 프리팹의 슬롯이 비었거나 " +
                "StaticResource 자체가 만들어지지 않았다. 이 상태에서도 게임은 굴러가지만 " +
                "다이스 수치가 전부 코드 기본값이라 에셋을 고쳐도 반영되지 않는다.");
        }

        /// <summary>
        /// 에셋이 정본이다. (4.3)
        ///
        /// 예전에는 에셋 meta 를 찾은 뒤 <c>MergeMeta</c> 로 코드 기본값과 합쳤다. 그 표는 필드마다
        /// 어느 쪽이 이길지 못박아 뒀는데, 수치(baseAttack / baseCooldown / 강화 비용 4종) · 표시
        /// 문구(displayName / description / milestones) · 등급 플래그
        /// (isMythic / summonable / canMerge / showStarUI)가 <b>전부 코드 쪽</b>이었다.
        /// 에셋에서 이기던 것은 시각 참조(icon / color / projectileSprite / primaryEffect /
        /// effectPrefabs)와 elementType 뿐이다. 즉 에셋의 수치를 고쳐도 게임에는 반영되지 않았고,
        /// 그 사실이 조용했다.
        ///
        /// 이제 에셋에 그 DiceType 이 있으면 <b>에셋 meta 를 그대로</b> 돌려준다. 반환값은 에셋의
        /// 실제 인스턴스다 — 복사본이 아니므로 <b>호출자가 고치면 에셋이 더러워진다.</b> 현재
        /// 호출자는 전부 읽기만 한다(UI 표시 · ElementUpgradeManager 의 원소 합산). 읽기 전용을
        /// 깨는 호출자를 새로 만들지 말 것.
        ///
        /// 코드 기본값은 <b>에셋이 통째로 없거나 그 DiceType 이 등재되지 않은 경우에만</b> 쓴다.
        /// 그때는 위 <see cref="Database"/> 게터가 LogError 로 한 번 크게 운다. (2.2)
        /// </summary>
        public static DiceMetaDataDatabase.DiceMeta GetMeta(DiceType diceType)
        {
            if (Database != null && Database.TryGet(diceType, out var meta))
                return meta;

            EnsureDefaults();
            defaults.TryGetValue(diceType, out var fallback);
            return fallback;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 코드 기본값 표. <b>런타임은 이걸 거의 쓰지 않는다</b> — 에셋이 정본이고, 여기는
        /// 에셋이 없을 때의 방어선일 뿐이다.
        ///
        /// 노출하는 이유는 하나다. 4단계에서 MergeMeta 를 걷어내자 그동안 코드가 덮어쓰고 있던
        /// 표시 문구(description/milestones)가 에셋의 낡은 판으로 드러났다. 그 문구를 에셋으로
        /// 옮기는 에디터 도구가 원본을 읽어야 해서 열어 둔다. UNITY_EDITOR 밖에서는 존재하지 않는다.
        /// </summary>
        public static IReadOnlyDictionary<DiceType, DiceMetaDataDatabase.DiceMeta> EditorOnlyCodeDefaults
        {
            get
            {
                EnsureDefaults();
                return defaults;
            }
        }
#endif

        public static (int goldCost, int scrollCost) GetUpgradeCost(DiceType diceType, int currentLevel)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return (0, 0);

            // 4.3 이전에는 이 우회가 킹 다이스 5종에서 <b>항상</b> 탔다. MergeMeta 가 강화 비용을
            // 코드 기본값에서 가져왔는데 CreateMythicDefault 는 네 값을 0 으로 두기 때문이다.
            // 이제 에셋이 있으면 그 값(260/270/255/280/250 …)이 그대로 와서 이 분기가 거짓이 된다.
            // 산 것을 죽인 게 아니라 같은 표가 두 군데 있었을 뿐이다 — 값은 한 자리도 안 바뀐다.
            //
            // <b>지우지 말 것.</b> "이제 안 탄다"는 에셋이 있을 때 이야기다. 이 분기는 두 경우에
            // 여전히 산다:
            //   1) 에셋이 통째로 없거나 그 DiceType 이 등재되지 않아 GetMeta 가 CreateMythicDefault
            //      를 돌려줄 때. 그때 킹 5종의 비용 네 개는 전부 0 이고, 이 표만이 0원 강화를 막는다.
            //   2) 에셋에서 사람이 비용 네 개를 모두 0 으로 만들었을 때. 조용히 코드 표로 되돌아간다.
            // 즉 지우는 순간 1) 에서 킹 강화가 공짜가 된다. (헤드리스 차분 대조로 실측 확인)
            if (meta.baseGoldCost <= 0 && meta.goldCostPerLevel <= 0 && meta.baseScrollCost <= 0 && meta.scrollCostPerLevel <= 0)
            {
                if (TryGetFallbackUpgradeCost(diceType, currentLevel, out var fallbackCost))
                    return fallbackCost;
            }

            int level = Mathf.Max(1, currentLevel);
            int gold = Mathf.Max(0, meta.baseGoldCost + (level - 1) * meta.goldCostPerLevel);
            int scroll = Mathf.Max(0, meta.baseScrollCost + (level - 1) * meta.scrollCostPerLevel);
            return (gold, scroll);
        }

        public static Color GetColor(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.color.a > 0f)
                return meta.color;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.color;
            }

            return Color.white;
        }

        public static Sprite GetIcon(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.icon != null)
                return meta.icon;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.icon;
            }

            return null;
        }

        public static Sprite GetProjectileSprite(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.projectileSprite != null)
                return meta.projectileSprite;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.projectileSprite;
            }

            return null;
        }

        public static BulletEffect GetPrimaryEffect(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.primaryEffect != null)
                return meta.primaryEffect;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.primaryEffect;
            }

            return null;
        }

        public static List<BulletEffect> GetEffectPrefabs(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.effectPrefabs != null && meta.effectPrefabs.Count > 0)
                return meta.effectPrefabs;

            DiceType baseType = GetBaseElementType(diceType);
            if (baseType != diceType)
            {
                var baseMeta = GetMeta(baseType);
                if (baseMeta != null)
                    return baseMeta.effectPrefabs;
            }

            return null;
        }

        public static bool IsMythic(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            return meta != null && meta.isMythic;
        }

        public static bool IsSummonable(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return true;
            return meta.summonable && !meta.isMythic;
        }

        public static bool CanMerge(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return true;
            return meta.canMerge;
        }

        public static bool ShowStarUI(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta == null)
                return true;
            return meta.showStarUI;
        }

        /// <summary>
        /// 이 다이스가 속한 계통의 기본 다이스. <b>진화 배선의 역방향이다.</b>
        ///
        /// 쓰이는 곳이 둘이다. 하나는 아이콘·색·투사체·이펙트의 폴백(위 Get* 들) —
        /// 상위 다이스가 자기 리소스를 안 갖고 있으면 계통의 기본 것을 빌린다.
        /// 다른 하나가 더 중요하다: <c>AttackContent.GetDiceEffect</c> 가 이 함수로
        /// <b>실제 전투 효과를 고른다.</b> 여기가 어긋나면 화면과 데미지가 갈린다.
        ///
        /// <b>Stun 과 Time 을 고쳤다.</b> 예전에는 <c>Stun → Thunder</c>,
        /// <c>Time → Normal</c> 이었는데, 같은 파일의 킹 조합식은
        /// <c>KingPoison ← Stun</c>·<c>KingThunder ← Time</c> 이라고 적고 있었다.
        /// 두 표가 서로 다른 계통을 가리켰고, 특수 다이스가 원소를 두 개씩 들고 있어
        /// (Stun = Light+Dark) 어느 쪽도 틀렸다고 말하기 어려웠던 것이 원인이다.
        /// 이제 원소가 계통마다 하나이므로 표도 하나다 —
        /// <see cref="DiceEvolution"/> 의 진화 배선과 정확히 짝이 맞는다.
        /// </summary>
        public static DiceType GetBaseElementType(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.KingNormal:
                case DiceType.Tornado:
                    return DiceType.Normal;
                case DiceType.KingFire:
                case DiceType.ArmorBreak:
                    return DiceType.Fire;
                case DiceType.KingIce:
                case DiceType.Wind:
                    return DiceType.Ice;
                case DiceType.KingThunder:
                case DiceType.Time:
                    return DiceType.Thunder;
                case DiceType.KingPoison:
                case DiceType.Stun:
                    return DiceType.Poison;
                default:
                    return diceType;
            }
        }

        /// <summary>
        /// 산술은 전부 OJ.Core.DamageFormula 로 옮겼다. 여기 남은 일은 "싱글톤에서 값을 긁어모아
        /// 스냅샷을 채우는 것"뿐이다.
        ///
        /// 왜 이렇게 나눴는가: 원본은 계산 도중에 싱글톤 4종을 조회해서, 에디터 밖에서는 물론
        /// 테스트에서도 식만 따로 검증할 수 없었다. 값 수집(여기)과 식(DamageFormula)을 갈라
        /// 놓으면 식은 입력만 주면 재현된다.
        ///
        /// 반드시 지킬 것 — 싱글톤이 null 일 때 중립값이 들어가야 한다. 원본은 null 이면 블록을
        /// 통째로 건너뛰었고, 아래 지역 변수의 초기값(0 / 0f / 1f)이 그 "건너뛰기"와 산술적으로
        /// 같은 역할을 한다. 초기값을 바꾸면 매니저가 없는 상황에서 데미지가 달라진다.
        ///
        /// GetLevelDamageMultiplier / GetKingSynergyDamageMultiplier / IsKingDice 는 DiceType 을
        /// 받으므로 OJ.Core 로 내려보낼 수 없다. 여기서 미리 계산해 결과(배수·플래그)만 넘긴다.
        /// </summary>
        /// <summary>
        /// 싱글톤에서 값을 모아 스냅샷 하나로 만든다. (MIGRATION_BASELINE 5.1)
        ///
        /// <b>수집과 계산을 가르는 경계다.</b> 계산은 <see cref="DamageFormula.Calculate"/> 가
        /// 하고 거기에는 싱글톤이 하나도 없다. 여기만 씬 상태를 읽는다.
        ///
        /// <b>스냅샷은 명중 시점에 뜬다.</b> 발사 시점이 아니다 — 지금 호출부가
        /// <c>AttackContent.PlayHit</c> 이고, 총알이 날아가는 동안 장비를 바꾸거나 웨이브가
        /// 넘어가면 값이 달라진다. 어느 쪽이 옳은지가 아니라 <b>현행 동작이 명중 시점</b>이라는
        /// 것이 중요하다. 옮기면 밸런스가 조용히 바뀐다.
        ///
        /// 조회 순서도 원본 그대로다. 지금은 전부 부작용 없는 게터지만, 순서를 흩뜨려 두면
        /// 나중에 누가 상태를 건드리는 게터를 끼워 넣었을 때 원인을 못 찾는다.
        ///
        /// 매니저가 없으면 <b>산술적으로 중립인 값</b>을 넣는다(0 또는 1f). 블록을 건너뛰는 것과
        /// 중립값을 통과시키는 것이 같음은 <see cref="DamageFormula"/> 주석에 근거가 있다.
        /// </summary>
        public static DamageInputs CaptureDamageInputs(DiceType diceType, int dicePip, int bulletLevel)
        {
            var meta = GetMeta(diceType);

            // meta 가 없으면 CalculateDamage 는 0 을 돌려준다. 그 사실은 스냅샷으로 옮겨
            // 담을 수 없다 — BaseAttack 0 으로 넣어도 하한 1 때문에 1 이 나온다.
            // 그래서 "meta 없음"은 호출부가 먼저 걸러야 하고, 여기서는 default 를 준다.
            return meta != null
                ? CaptureDamageInputs(meta, diceType, dicePip, bulletLevel)
                : default;
        }

        private static DamageInputs CaptureDamageInputs(
            DiceMetaDataDatabase.DiceMeta meta, DiceType diceType, int dicePip, int bulletLevel)
        {
            // 원본과 같은 클램프. GetLevelDamageMultiplier 는 클램프된 level 로 조회해야 한다.
            int level = Mathf.Max(1, bulletLevel);

            // 조회 순서도 원본 그대로 둔다. 지금은 전부 부작용 없는 게터지만, 순서를 흩뜨려 두면
            // 나중에 누가 상태를 건드리는 게터를 끼워 넣었을 때 원인을 못 찾는다.
            int equipmentAttackTotal = 0;
            if (EquipmentManager.Instance != null)
                equipmentAttackTotal = EquipmentManager.Instance.GetTotalEquipmentAttack();

            float levelDamageMultiplier = GetLevelDamageMultiplier(diceType, level);
            float kingSynergyMultiplier = GetKingSynergyDamageMultiplier(diceType);
            bool isKingDice = IsKingDice(diceType);

            float attackPercent = 0f;
            int attackFlat = 0;
            int earlyWaveFlat = 0;
            float finalDamagePercent = 0f;

            // 8.3b: static 클래스라 주입받을 자리가 없다. 정적 접근자를 남기는 예외 둘 중
            // 하나가 여기다(다른 하나는 개발 도구). 인스턴스 서비스로 바꾸는 것은 사용처가
            // 148곳/37파일이고 골든으로 잠긴 데미지 경로를 지나므로 8.3b 의 범위가 아니다.
            IBattleRefs battle = GameContainer.Battle;

            if (EquipmentManager.Instance != null)
            {
                attackPercent = EquipmentManager.Instance.GetAttackPercentBonus(diceType);
                attackFlat = EquipmentManager.Instance.GetAttackFlatBonus(diceType);

                // GameManager 가 없으면 0 웨이브로 조회한다. 이 조회 자체를 건너뛰는 게 아니라
                // "웨이브 0"으로 묻는 것이 원본 동작이다 — 초반 웨이브 보너스가 붙을 수 있다.
                int currentWave = 0;
                if (battle.IsActive)
                    currentWave = battle.Game.CurrentWaveIndex;
                earlyWaveFlat = EquipmentManager.Instance.GetFirstNWavesDamageFlatBonus(diceType, currentWave);
                finalDamagePercent = EquipmentManager.Instance.GetFinalDamagePercentBonus(diceType);
            }

            float elementUpgradeMultiplier = 1f;
            if (battle.IsActive)
                elementUpgradeMultiplier = battle.ElementUpgrade.GetTotalBonusMultiplier(diceType);

            float relicDamageMultiplier = 1f;
            if (RelicManager.Instance != null)
                relicDamageMultiplier = RelicManager.Instance.GetDamageMultiplier(diceType);

            return new DamageInputs
            {
                BaseAttack = meta.baseAttack,
                LevelUpAttackIncrease = meta.levelUpAttackIncrease,
                DicePip = dicePip,
                BulletLevel = bulletLevel,
                EquipmentAttackTotal = equipmentAttackTotal,
                LevelDamageMultiplier = levelDamageMultiplier,
                KingSynergyMultiplier = kingSynergyMultiplier,
                IsKingDice = isKingDice,
                AttackPercentBonus = attackPercent,
                AttackFlatBonus = attackFlat,
                EarlyWaveFlatBonus = earlyWaveFlat,
                FinalDamagePercentBonus = finalDamagePercent,
                ElementUpgradeMultiplier = elementUpgradeMultiplier,
                RelicDamageMultiplier = relicDamageMultiplier
            };
        }

        public static int CalculateDamage(DiceType diceType, int dicePip, int bulletLevel)
        {
            // meta 를 여기서 한 번만 읽어 넘긴다. 공개 오버로드를 부르면 GetMeta 가 두 번
            // 불리는데, 원본은 한 번이었다. 지금은 부작용 없는 조회라 값이 같지만 호출
            // 횟수를 바꾸지 않는 편이 맞다 — 나중에 누가 GetMeta 에 부작용을 넣으면 두 배가 된다.
            var meta = GetMeta(diceType);
            if (meta == null)
                return 0;

            return DamageFormula.Calculate(CaptureDamageInputs(meta, diceType, dicePip, bulletLevel));
        }

        private static bool IsKingDice(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.KingNormal:
                case DiceType.KingFire:
                case DiceType.KingIce:
                case DiceType.KingThunder:
                case DiceType.KingPoison:
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetFallbackUpgradeCost(DiceType diceType, int currentLevel, out (int goldCost, int scrollCost) cost)
        {
            switch (diceType)
            {
                case DiceType.KingNormal:
                    cost = BuildUpgradeCost(currentLevel, 260, 90, 15, 3);
                    return true;
                case DiceType.KingFire:
                    cost = BuildUpgradeCost(currentLevel, 270, 92, 16, 3);
                    return true;
                case DiceType.KingIce:
                    cost = BuildUpgradeCost(currentLevel, 255, 88, 15, 3);
                    return true;
                case DiceType.KingThunder:
                    cost = BuildUpgradeCost(currentLevel, 280, 96, 16, 3);
                    return true;
                case DiceType.KingPoison:
                    cost = BuildUpgradeCost(currentLevel, 250, 86, 15, 3);
                    return true;
                default:
                    cost = (0, 0);
                    return false;
            }
        }

        private static (int goldCost, int scrollCost) BuildUpgradeCost(int currentLevel, int baseGold, int goldPerLevel, int baseScroll, int scrollPerLevel)
        {
            int level = Mathf.Max(1, currentLevel);
            int gold = Mathf.Max(0, baseGold + (level - 1) * goldPerLevel);
            int scroll = Mathf.Max(0, baseScroll + (level - 1) * scrollPerLevel);
            return (gold, scroll);
        }

        public static float GetBaseCooldown(DiceType diceType)
        {
            var meta = GetMeta(diceType);
            if (meta != null && meta.baseCooldown > 0f)
                return meta.baseCooldown;

            return 3f;
        }

        public static float GetCooldown(DiceType diceType, int diceStar)
        {
            float baseCooldown = Mathf.Clamp(GetBaseCooldown(diceType), 0.1f, 10f);
            int star = Mathf.Max(1, diceStar);
            float cooldown = baseCooldown * Mathf.Pow(1.2f, star - 1) * CooldownBalanceMultiplier;
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;
            cooldown *= GetLevelCooldownMultiplier(diceType, level);

            if (EquipmentManager.Instance != null)
            {
                float reducePercent = EquipmentManager.Instance.GetCooldownReductionPercent(diceType);
                cooldown *= Mathf.Max(0.05f, 1f - reducePercent);
            }

            if (RelicManager.Instance != null)
            {
                float relicReducePercent = RelicManager.Instance.GetCooldownReductionPercent() * 0.01f;
                cooldown *= Mathf.Max(0.05f, 1f - relicReducePercent);
            }

            return cooldown;
        }

        public static float GetLevelDamageMultiplier(DiceType diceType, int level)
        {
            float multiplier = 1f;
            if (level >= 3)
            {
                switch (diceType)
                {
                    case DiceType.Normal:
                    case DiceType.Thunder:
                    case DiceType.Fire:
                    case DiceType.Ice:
                    case DiceType.Poison:
                    case DiceType.Stun:
                    case DiceType.ArmorBreak:
                        multiplier *= 1.1f;
                        break;
                    case DiceType.Tornado:
                        break;
                    case DiceType.KingNormal:
                        multiplier *= 1.3f;
                        break;
                    case DiceType.KingFire:
                    case DiceType.KingIce:
                    case DiceType.KingPoison:
                        multiplier *= 1.2f;
                        break;
                    case DiceType.KingThunder:
                        break;
                }
            }

            if (diceType == DiceType.Tornado && level >= 12)
                multiplier *= 1.3f;
            if (diceType == DiceType.KingFire && level >= 12)
                multiplier *= 1.3f;

            return multiplier;
        }

        public static float GetLevelCooldownMultiplier(DiceType diceType, int level)
        {
            float multiplier = 1f;
            switch (diceType)
            {
                case DiceType.Normal:
                    if (level >= 6) multiplier *= 0.9f;
                    break;
                case DiceType.Thunder:
                    if (level >= 9) multiplier *= 0.9f;
                    break;
                case DiceType.Fire:
                    if (level >= 12) multiplier *= 0.8f;
                    break;
                case DiceType.Ice:
                    if (level >= 12) multiplier *= 0.8f;
                    break;
                case DiceType.Tornado:
                    if (level >= 9) multiplier *= 0.8f;
                    break;
                case DiceType.Stun:
                    if (level >= 9) multiplier *= 0.9f;
                    break;
                case DiceType.ArmorBreak:
                    if (level >= 9) multiplier *= 0.8f;
                    break;
                case DiceType.Time:
                    if (level >= 9) multiplier *= 0.9f;
                    break;
            }

            return multiplier;
        }

        public static int GetThunderTargetCount(int level)
        {
            int count = 2;
            if (level >= 6)
                count += 1;
            return count;
        }

        public static float GetFireExplosionRangeMultiplier(int level)
        {
            float multiplier = level >= 9 ? 1.1f : 1f;
            int kingFireLevel = GetKingLevel(DiceType.KingFire);
            if (IsKingSummoned(DiceType.KingFire) && kingFireLevel >= 6)
                multiplier *= 1.2f;
            return multiplier;
        }

        public static float GetWindPushChancePercent(int level)
        {
            float chance = 40f + Mathf.Max(1, level) * 1f;
            if (level >= 9)
                chance += 10f;
            return chance;
        }

        public static float GetWindPushChancePercent(DiceType diceType, int level)
        {
            float chance = GetWindPushChancePercent(level);
            IBattleRefs battle = GameContainer.Battle;
            if (battle.IsActive)
                chance *= battle.ElementUpgrade.GetTotalBonusMultiplier(diceType);

            return chance;
        }

        public static int GetWindTargetCount(int level)
        {
            return level >= 12 ? 3 : 2;
        }

        public static float GetWindDistanceMultiplier(int level)
        {
            return level >= 3 ? 1.1f : 1f;
        }

        public static float GetTimeCooldownReducePercent(int level)
        {
            float percent = 10f + Mathf.Max(1, level) * 1f;
            if (level >= 3)
                percent += 5f;
            if (level >= 12)
                percent += 10f;
            return percent;
        }

        public static float GetTimeCooldownReducePercent(DiceType diceType, int level)
        {
            float percent = GetTimeCooldownReducePercent(level);
            IBattleRefs battle = GameContainer.Battle;
            if (battle.IsActive)
                percent *= battle.ElementUpgrade.GetTotalBonusMultiplier(diceType);

            return percent;
        }

        public static int GetTimeTargetCount(int level)
        {
            return level >= 6 ? 3 : 2;
        }

        public static float GetStunChancePercent(int level)
        {
            return level >= 6 ? 50f : 40f;
        }

        public static int GetArmorBreakPercent(int level)
        {
            return level >= 6 ? 40 : 30;
        }

        public static float GetGlobalCriticalChancePercent()
        {
            if (!IsKingSummoned(DiceType.KingNormal))
                return 0f;

            int kingNormalLevel = GetKingLevel(DiceType.KingNormal);
            return kingNormalLevel >= 9 ? 10f : 0f;
        }

        public static float GetGlobalCriticalDamageMultiplier()
        {
            if (!IsKingSummoned(DiceType.KingNormal))
                return 2f;

            int kingNormalLevel = GetKingLevel(DiceType.KingNormal);
            return kingNormalLevel >= 12 ? 2.2f : 2f;
        }

        public static float GetKingSynergyDamageMultiplier(DiceType diceType)
        {
            switch (diceType)
            {
                case DiceType.Normal:
                    return IsKingSummoned(DiceType.KingNormal) && GetKingLevel(DiceType.KingNormal) >= 6 ? 1.2f : 1f;
                case DiceType.Thunder:
                    return IsKingSummoned(DiceType.KingThunder) && GetKingLevel(DiceType.KingThunder) >= 6 ? 1.2f : 1f;
                case DiceType.Fire:
                    return 1f;
                case DiceType.Ice:
                    return 1f;
                case DiceType.Poison:
                    return 1f;
                default:
                    return 1f;
            }
        }

        public static float GetPoisonDamageMultiplier(DiceType diceType, int level)
        {
            float multiplier = level >= 6 ? 1.5f : 1f;
            if (diceType == DiceType.Poison && IsKingSummoned(DiceType.KingPoison) && GetKingLevel(DiceType.KingPoison) >= 6)
                multiplier *= 1.5f;
            return multiplier;
        }

        public static float GetSlowDuration(DiceType diceType, int level)
        {
            float duration = 2f;
            if (diceType == DiceType.Ice && level >= 9)
                duration *= 1.5f;
            if (diceType == DiceType.KingIce)
                duration *= IsKingSummoned(DiceType.KingIce) && GetKingLevel(DiceType.KingIce) >= 6 ? 1.5f : 1f;
            return duration;
        }

        public static float GetPoisonDuration(DiceType diceType)
        {
            return 4f;
        }

        public static bool HasKingIceDamageBonus()
        {
            return IsKingSummoned(DiceType.KingIce) && GetKingLevel(DiceType.KingIce) >= 12;
        }

        public static bool HasKingPoisonDamageBonus()
        {
            return IsKingSummoned(DiceType.KingPoison) && GetKingLevel(DiceType.KingPoison) >= 12;
        }

        private static int GetKingLevel(DiceType diceType)
        {
            return DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(diceType) : 1;
        }

        private static bool IsKingSummoned(DiceType diceType)
        {
            IBattleRefs battle = GameContainer.Battle;
            return battle.IsActive && battle.DiceStars.GetTypeCount(diceType) > 0;
        }

        private static void EnsureDefaults()
        {
            if (defaults != null)
                return;

            defaults = new Dictionary<DiceType, DiceMetaDataDatabase.DiceMeta>
            {
                { DiceType.Normal, CreateDefault(DiceType.Normal, "Normal Dice", "가장 가까운 적 하나를 공격합니다. 부가 효과는 없지만 쿨타임이 짧아 꾸준히 피해를 쌓습니다.", 12, 3, 120, 50, 8, 2, 2.4f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "쿨타임 10% 감소"),
                    (9, "공격 시 20% 확률로 SP +5"),
                    (12, "공격 시 20% 확률로 대미지 2배")
                }) },
                { DiceType.Fire, CreateDefault(DiceType.Fire, "Fire Dice", "적 하나를 공격하고 명중 지점 주변으로 폭발을 일으킵니다. 적이 몰려 있을수록 강해집니다.", 10, 4, 140, 60, 10, 2, 3.1f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "공격 시 20% 확률로 한 번 더 폭발"),
                    (9, "폭발 범위 10% 증가"),
                    (12, "쿨타임 20% 감소")
                }) },
                { DiceType.Ice, CreateDefault(DiceType.Ice, "Ice Dice", "적 하나를 공격하고 둔화를 겁니다. 벽까지 오는 시간을 벌어 주는 방어형 다이스입니다.", 9, 3, 130, 55, 9, 2, 3.8f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "공격 시 30% 확률로 범위 피해"),
                    (9, "둔화 지속시간 50% 증가"),
                    (12, "쿨타임 20% 감소")
                }) },
                { DiceType.Poison, CreateDefault(DiceType.Poison, "Poison Dice", "적 하나를 공격하고 중독을 겁니다. 중독은 남은 체력에 비례해 피해를 주므로 체력이 두꺼운 적일수록 아픕니다.", 8, 2, 125, 50, 9, 2, 3.4f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "중독 피해량 50% 증가"),
                    (9, "공격 시 40% 확률로 범위 피해"),
                    (12, "중독된 적이 받는 피해 10% 증가")
                }) },
                { DiceType.Thunder, CreateDefault(DiceType.Thunder, "Thunder Dice", "적 하나를 공격하면 번개가 주변으로 전이됩니다. 넓게 퍼진 적을 훑는 데 좋습니다.", 11, 3, 150, 65, 11, 2, 2.7f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "전이 대상 +1"),
                    (9, "쿨타임 10% 감소"),
                    (12, "공격한 적 주변 1명에게 50% 추가 번개 피해")
                }) },
                { DiceType.Tornado, CreateDefault(DiceType.Tornado, "Tornado Dice", "노말 다이스가 진화한 모습입니다. 적을 공격하면서 주변의 적들을 명중 지점으로 끌어당겨, 뒤이어 오는 광역 공격이 한 번에 쓸어 담게 만듭니다.", 80, 10, 145, 62, 10, 2, 3.0f, new []{
                    (3, "범위 10% 증가"),
                    (6, "적을 2초 동안 흡입"),
                    (9, "쿨타임 20% 감소"),
                    (12, "최종 대미지 30% 증가")
                }, new [] { ElementType.Normal },
                    false, false, false) },
                { DiceType.Stun, CreateDefault(DiceType.Stun, "Stun Dice", "포이즌 다이스가 진화한 모습입니다. 적 하나를 강하게 때리고 확률로 기절시켜 발을 묶습니다. 벽 앞까지 밀려온 적을 끊어 낼 때 믿을 수 있습니다.", 87, 11, 140, 58, 10, 2, 3.5f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "스턴 확률 10% 증가"),
                    (9, "쿨타임 10% 감소"),
                    (12, "스턴된 적이 받는 피해 20% 증가")
                }, new [] { ElementType.Dark },
                    false, false, false) },
                { DiceType.ArmorBreak, CreateDefault(DiceType.ArmorBreak, "Armor Break Dice", "파이어 다이스가 진화한 모습입니다. 적의 방어력을 깎아 그 뒤로 들어가는 모든 공격을 더 아프게 만듭니다. 단단한 적과 보스에게 특히 강합니다.", 78, 10, 150, 64, 11, 2, 3.2f, new []{
                    (3, "최종 대미지 10% 증가"),
                    (6, "방어력 감소 10% 증가"),
                    (9, "쿨타임 20% 감소"),
                    (12, "방깎 상태 적이 받는 피해 10% 증가")
                }, new [] { ElementType.Fire },
                    false, false, false) },
                { DiceType.Wind, CreateDefault(DiceType.Wind, "Wind Dice", "아이스 다이스가 진화한 모습입니다. 벽 앞에 몰린 적들을 한꺼번에 때리고 확률로 뒤쪽까지 밀어냅니다. 총알을 쏘지 않고 벽 앞 전체를 직접 칩니다.", 40, 5, 135, 56, 9, 2, 2.9f, new []{
                    (3, "밀어내는 거리 10% 증가"),
                    (6, "밀리는 적이 받는 피해 10% 증가"),
                    (9, "밀어내기 확률 10% 추가 증가"),
                    (12, "밀어내는 대상 +1")
                }, new [] { ElementType.Water },
                    false, false, false) },
                { DiceType.Time, CreateDefault(DiceType.Time, "Time Dice", "썬더 다이스가 진화한 모습입니다. 적을 공격하는 동시에 다른 다이스의 남은 쿨타임을 당겨, 보드 전체의 화력을 끌어올립니다.", 85, 11, 170, 70, 12, 3, 4.0f, new []{
                    (3, "쿨타임 감소량 5% 추가 증가"),
                    (6, "대상 +1"),
                    (9, "자신의 쿨타임 10% 감소"),
                    (12, "쿨타임 감소량 10% 추가 증가")
                }, new [] { ElementType.Light },
                    false, false, false) },
                { DiceType.KingNormal, CreateMythicDefault(DiceType.KingNormal, "King Normal", "토네이도 다이스가 도달하는 최종 형태입니다. 적과 그 주변을 첫 타 70%, 이어서 0.2초 간격으로 10%씩 세 번 더 두드립니다.", 140, 27, 3.0f, new []{
                    (3, "최종 대미지 30% 증가"),
                    (6, "소환 중인 동안 NormalDice 최종 대미지 20% 증가"),
                    (9, "소환 중인 동안 모든 다이스 크리티컬 확률 10% 증가"),
                    (12, "소환 중인 동안 모든 다이스 크리티컬 대미지 20% 증가")
                }) },
                { DiceType.KingFire, CreateMythicDefault(DiceType.KingFire, "King Fire", "아머브레이크 다이스가 도달하는 최종 형태입니다. 일반 폭발보다 훨씬 넓은 강화 폭발로 무리를 통째로 태웁니다.", 148, 30, 3.3f, new []{
                    (3, "최종 대미지 20% 증가"),
                    (6, "소환 중인 동안 FireDice 폭발 범위 20% 증가"),
                    (9, "폭발이 30% 확률로 한 번 더 발생"),
                    (12, "폭발 최종 피해 30% 증가")
                }) },
                { DiceType.KingIce, CreateMythicDefault(DiceType.KingIce, "King Ice", "윈드 다이스가 도달하는 최종 형태입니다. 적과 그 주변을 함께 때리고 강한 둔화를 겁니다.", 135, 27, 3.5f, new []{
                    (3, "최종 대미지 20% 증가"),
                    (6, "IceDice 둔화 지속시간 50% 증가"),
                    (9, "공격 시 빙결 부여"),
                    (12, "둔화된 적이 받는 피해 15% 증가")
                }) },
                { DiceType.KingPoison, CreateMythicDefault(DiceType.KingPoison, "King Poison", "스턴 다이스가 도달하는 최종 형태입니다. 적 하나를 때리고 중독과 둔화를 함께 걸어 오래 붙잡아 둡니다.", 132, 27, 3.3f, new []{
                    (3, "최종 대미지 20% 증가"),
                    (6, "소환 중인 동안 PoisonDice 중독 피해량 50% 증가"),
                    (9, "중독 적용 시 30% 확률로 주변 적 1명에게 전이"),
                    (12, "중독된 적이 받는 피해 15% 증가")
                }) },
                { DiceType.KingThunder, CreateMythicDefault(DiceType.KingThunder, "King Thunder", "타임 다이스가 도달하는 최종 형태입니다. 번개가 더 많은 적에게 전이되어 넓게 퍼진 무리를 한 번에 훑습니다.", 156, 32, 3.0f, new []{
                    (3, "전이 대상 +2"),
                    (6, "소환 중인 동안 ThunderDice 최종 대미지 20% 증가"),
                    (9, "30% 확률로 추가 1명에게 50% 피해"),
                    (12, "맞은 적이 받는 피해 15% 증가")
                }) }
            };
        }

        private static DiceMetaDataDatabase.DiceMeta CreateDefault(
            DiceType diceType,
            string displayName,
            string description,
            int baseAttack,
            int levelUpAttackIncrease,
            int baseGoldCost,
            int goldCostPerLevel,
            int baseScrollCost,
            int scrollCostPerLevel,
            float baseCooldown,
            (int level, string desc)[] milestones,
            ElementType[] elementTypes = null,
            bool summonable = true,
            bool canMerge = true,
            bool showStarUI = true)
        {
            var meta = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = diceType,
                elementType = elementTypes ?? new ElementType[0],
                displayName = displayName,
                description = description,
                summonable = summonable,
                canMerge = canMerge,
                showStarUI = showStarUI,
                baseAttack = baseAttack,
                levelUpAttackIncrease = levelUpAttackIncrease,
                baseGoldCost = baseGoldCost,
                goldCostPerLevel = goldCostPerLevel,
                baseScrollCost = baseScrollCost,
                scrollCostPerLevel = scrollCostPerLevel,
                baseCooldown = baseCooldown
            };

            for (int i = 0; i < milestones.Length; i++)
            {
                meta.milestones.Add(new DiceMetaDataDatabase.DiceLevelMilestone
                {
                    level = milestones[i].level,
                    description = milestones[i].desc
                });
            }

            return meta;
        }

        private static DiceMetaDataDatabase.DiceMeta CreateMythicDefault(
            DiceType diceType,
            string displayName,
            string description,
            int baseAttack,
            int levelUpAttackIncrease,
            float baseCooldown,
            (int level, string desc)[] milestones)
        {
            var meta = new DiceMetaDataDatabase.DiceMeta
            {
                diceType = diceType,
                displayName = displayName,
                description = description,
                baseAttack = baseAttack,
                levelUpAttackIncrease = levelUpAttackIncrease,
                baseGoldCost = 0,
                goldCostPerLevel = 0,
                baseScrollCost = 0,
                scrollCostPerLevel = 0,
                baseCooldown = baseCooldown,
                isMythic = true,
                summonable = false,
                canMerge = false,
                showStarUI = false
            };

            for (int i = 0; i < milestones.Length; i++)
            {
                meta.milestones.Add(new DiceMetaDataDatabase.DiceLevelMilestone
                {
                    level = milestones[i].level,
                    description = milestones[i].desc
                });
            }

            return meta;
        }
    }
}
