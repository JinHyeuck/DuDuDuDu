using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public class UIBattleDiceDetailPanel : IDialog
    {
        [Header("Header")]
        [SerializeField] private Image iconImage;
        [SerializeField] private List<Image> elementIcons;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;

        [Header("Stats")]
        [SerializeField] private TMP_Text coolTimeText;
        [SerializeField] private TMP_Text descText;

        [Header("Milestones")]
        [SerializeField] private Transform milestoneListRoot;
        [SerializeField] private UIMilestoneElement milestoneElementPrefab;

        private readonly List<UIMilestoneElement> milestoneElements = new List<UIMilestoneElement>();
        private DiceType currentDiceType = DiceType.Normal;
        private int currentDiceStar = 1;
        private UIDice currentDice;

        protected override void OnEnter()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged += OnDiceLevelChanged;
        }

        protected override void OnExit()
        {
            if (DiceLevelManager.Instance != null)
                DiceLevelManager.Instance.OnDiceLevelChanged -= OnDiceLevelChanged;
        }

        private void Update()
        {
            if (!isEnter)
                return;

            if (currentDice == null || GameManager.Instance == null || GameManager.Instance.inGameState != InGameState.Wave)
            {
                Exit();
                return;
            }
        }

        public void Open(UIDice dice)
        {
            if (dice == null)
                return;

            currentDice = dice;
            currentDiceType = dice.Type;
            currentDiceStar = Mathf.Max(1, dice.Star);

            Enter();
            Refresh();
        }

        public void Refresh()
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(currentDiceType);
            int level = DiceLevelManager.Instance != null ? DiceLevelManager.Instance.GetLevel(currentDiceType) : 1;
            float cooldown = GetBattleCooldown(currentDiceType, currentDiceStar);

            if (iconImage != null)
                iconImage.sprite = DiceMetaDataProvider.GetIcon(currentDiceType);

            if (meta != null)
            {
                for (int i = 0; i < meta.elementType.Length && i < elementIcons.Count; i++)
                {
                    if (elementIcons[i] == null)
                        continue;

                    ElementResource elementResource = StaticResource.Instance.GetElementResource(meta.elementType[i]);
                    elementIcons[i].sprite = elementResource != null ? elementResource.Icon : null;
                    elementIcons[i].color = elementResource != null ? elementResource.Color : Color.white;
                    elementIcons[i].gameObject.SetActive(true);
                }
            }

            for (int i = meta != null ? meta.elementType.Length : 0; i < elementIcons.Count; i++)
            {
                if (elementIcons[i] != null)
                    elementIcons[i].gameObject.SetActive(false);
            }

            if (nameText != null)
                nameText.SetText(meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : currentDiceType.ToString());
            if (levelText != null)
                levelText.SetText("Lv. {0}  x{1}", level, currentDiceStar);
            if (coolTimeText != null)
                coolTimeText.SetText("{0:0.0}", cooldown);
            if (descText != null)
                descText.SetText(BuildDescriptionText(meta, level, currentDiceStar));

            RefreshMilestoneRows(meta, level);
        }

        private void OnDiceLevelChanged(DiceType diceType, int level)
        {
            if (diceType == currentDiceType)
                Refresh();
        }

        private void RefreshMilestoneRows(DiceMetaDataDatabase.DiceMeta meta, int currentLevel)
        {
            int count = meta != null && meta.milestones != null ? meta.milestones.Count : 0;
            for (int i = 0; i < count; i++)
            {
                UIMilestoneElement row = GetOrCreateMilestoneElement(i);
                if (row == null)
                    continue;

                bool unlocked = currentLevel >= meta.milestones[i].level;
                row.gameObject.SetActive(true);
                row.Bind(meta.milestones[i].level, meta.milestones[i].description, unlocked);

                RectTransform rowRect = row.transform as RectTransform;
                if (rowRect != null)
                {
                    rowRect.anchorMin = new Vector2(0.06f, 0.46f);
                    rowRect.anchorMax = new Vector2(0.94f, 0.46f);
                    rowRect.pivot = new Vector2(0.5f, 1f);
                    rowRect.anchoredPosition = new Vector2(0f, -(i * 62f));
                    rowRect.sizeDelta = new Vector2(0f, 54f);
                }
            }

            for (int i = count; i < milestoneElements.Count; i++)
            {
                if (milestoneElements[i] != null)
                    milestoneElements[i].gameObject.SetActive(false);
            }
        }

        private UIMilestoneElement GetOrCreateMilestoneElement(int index)
        {
            if (index < milestoneElements.Count && milestoneElements[index] != null)
                return milestoneElements[index];

            if (milestoneElementPrefab == null || milestoneListRoot == null)
                return null;

            UIMilestoneElement created = Instantiate(milestoneElementPrefab, milestoneListRoot);
            milestoneElements.Add(created);
            return created;
        }

        private string BuildDescriptionText(DiceMetaDataDatabase.DiceMeta meta, int level, int star)
        {
            if (meta == null)
                return string.Empty;

            BattleMonsterSnapshot monster = GetBattleMonsterSnapshot();
            int rawDamage = DiceMetaDataProvider.CalculateDamage(currentDiceType, star, level);
            int directDamage = meta.baseAttack > 0 ? CalculateAppliedDamage(rawDamage, monster.Defense, 0) : 0;
            int followUpBonusPercent = GetFollowUpDamageBonusPercent(currentDiceType, level);
            int followUpDefense = GetFollowUpDefense(currentDiceType, level, monster.Defense);
            int followUpDamage = meta.baseAttack > 0
                ? CalculateAppliedDamage(rawDamage, followUpDefense, followUpBonusPercent)
                : 0;

            StringBuilder builder = new StringBuilder();

            if (meta.baseAttack > 0)
            {
                builder.AppendFormat("{0} 기준 피해: {1}", monster.Label, directDamage);
                builder.AppendFormat(" (방어력 {0})", monster.Defense);

                if (followUpDamage > directDamage)
                {
                    builder.AppendLine();
                    builder.AppendFormat("효과 적용 후 피해: {0}", followUpDamage);
                }
            }
            else
            {
                builder.Append("피해 없음");
            }

            AppendEffectDescription(builder, level, directDamage, followUpDamage, monster);
            return builder.ToString();
        }

        private void AppendEffectDescription(StringBuilder builder, int level, int directDamage, int followUpDamage, BattleMonsterSnapshot monster)
        {
            switch (currentDiceType)
            {
                case DiceType.Fire:
                    builder.AppendLine();
                    builder.AppendFormat("폭발: 주변 최대 {0}명에게 동일 피해", GetFireTargetCount(false));
                    break;
                case DiceType.Ice:
                    builder.AppendLine();
                    builder.AppendFormat("둔화: {0:0.#}초", DiceMetaDataProvider.GetSlowDuration(currentDiceType, level));
                    break;
                case DiceType.Thunder:
                    builder.AppendLine();
                    builder.AppendFormat("연쇄 타격: 추가 {0}명", GetThunderChainCount(false, level));
                    if (level >= 12)
                    {
                        builder.AppendLine();
                        builder.AppendFormat("12레벨 추가 타격: {0}", Mathf.Max(1, Mathf.RoundToInt(directDamage * 0.5f)));
                    }
                    break;
                case DiceType.Poison:
                    builder.AppendLine();
                    builder.AppendFormat(
                        "중독 첫 틱: {0} ({1:0.#}초, 0.5초 간격)",
                        CalculatePoisonTickDamage(level, monster, directDamage),
                        DiceMetaDataProvider.GetPoisonDuration(currentDiceType));
                    break;
                case DiceType.KingNormal:
                    builder.AppendLine();
                    builder.Append("추가 타격: 주변 최대 3명에게 동일 피해");
                    break;
                case DiceType.KingFire:
                    builder.AppendLine();
                    builder.AppendFormat("강화 폭발: 주변 최대 {0}명에게 동일 피해", GetFireTargetCount(true));
                    break;
                case DiceType.KingIce:
                    builder.AppendLine();
                    builder.Append("연쇄 타격: 주변 최대 3명에게 동일 피해");
                    builder.AppendLine();
                    builder.AppendFormat("둔화: {0:0.#}초", DiceMetaDataProvider.GetSlowDuration(currentDiceType, level));
                    if (level >= 9)
                    {
                        builder.AppendLine();
                        builder.Append("추가 효과: 1.0초 기절");
                    }
                    break;
                case DiceType.KingThunder:
                    builder.AppendLine();
                    builder.AppendFormat("연쇄 타격: 추가 {0}명", GetThunderChainCount(true, level));
                    if (level >= 9)
                    {
                        builder.AppendLine();
                        builder.AppendFormat("9레벨 추가 타격: {0}", Mathf.Max(1, Mathf.RoundToInt(directDamage * 0.5f)));
                    }
                    break;
                case DiceType.KingPoison:
                    builder.AppendLine();
                    builder.AppendFormat(
                        "중독 첫 틱: {0} ({1:0.#}초, 0.5초 간격)",
                        CalculatePoisonTickDamage(level, monster, directDamage),
                        DiceMetaDataProvider.GetPoisonDuration(currentDiceType));
                    builder.AppendLine();
                    builder.AppendFormat("둔화: {0:0.#}초", DiceMetaDataProvider.GetSlowDuration(currentDiceType, level));
                    break;
                case DiceType.Stun:
                    builder.AppendLine();
                    builder.AppendFormat("기절 확률: {0:0.#}%", DiceMetaDataProvider.GetStunChancePercent(level));
                    if (followUpDamage > directDamage)
                    {
                        builder.AppendLine();
                        builder.Append("기절 후 추가 피해 증가 반영");
                    }
                    break;
                case DiceType.ArmorBreak:
                    builder.AppendLine();
                    builder.AppendFormat("방어력 감소: {0}% (4.0초)", DiceMetaDataProvider.GetArmorBreakPercent(level));
                    if (followUpDamage > directDamage)
                    {
                        builder.AppendLine();
                        builder.Append("방어 감소 후 다음 타격 피해 반영");
                    }
                    break;
                case DiceType.Wind:
                    builder.AppendLine();
                    builder.AppendFormat(
                        "밀쳐내기: {0:0.#}% 확률, 최대 {1}명",
                        DiceMetaDataProvider.GetWindPushChancePercent(level),
                        DiceMetaDataProvider.GetWindTargetCount(level));
                    if (followUpDamage > 0)
                    {
                        builder.AppendLine();
                        builder.AppendFormat("효과 적용 후 다음 타격 피해: {0}", followUpDamage);
                    }
                    break;
                case DiceType.Time:
                    builder.AppendLine();
                    builder.AppendFormat(
                        "쿨타임 감소: 남은 쿨타임 {0:0.#}% 감소, 랜덤 {1}개",
                        DiceMetaDataProvider.GetTimeCooldownReducePercent(level),
                        DiceMetaDataProvider.GetTimeTargetCount(level));
                    break;
            }
        }

        private int GetFireTargetCount(bool isKing)
        {
            int targetCount = isKing ? 14 : 10;
            if (EquipmentManager.Instance != null)
            {
                targetCount += EquipmentManager.Instance.GetFireExplosionExtraTargetCount(currentDiceType);
                if (isKing)
                    targetCount += 2;
            }

            return targetCount;
        }

        private int GetThunderChainCount(bool isKing, int level)
        {
            int targetCount = DiceMetaDataProvider.GetThunderTargetCount(level);
            if (EquipmentManager.Instance != null)
                targetCount += EquipmentManager.Instance.GetThunderChainExtraCount(DiceType.Thunder);
            if (!isKing)
                return targetCount;

            targetCount += 2;
            if (level >= 3)
                targetCount += 2;
            return targetCount;
        }

        private int CalculatePoisonTickDamage(int level, BattleMonsterSnapshot monster, int directDamage)
        {
            int remainingHp = Mathf.Max(1, monster.Hp - directDamage);
            float poisonMultiplier = currentDiceType == DiceType.KingPoison
                ? 1f
                : DiceMetaDataProvider.GetPoisonDamageMultiplier(currentDiceType, level);
            int poisonRawDamage = Mathf.CeilToInt((remainingHp * 0.1f) * poisonMultiplier);
            int poisonBonusPercent = 0;

            if (currentDiceType == DiceType.Poison && level >= 12)
                poisonBonusPercent += 10;

            if (currentDiceType == DiceType.KingPoison || currentDiceType == DiceType.Poison)
            {
                if (DiceMetaDataProvider.HasKingPoisonDamageBonus())
                    poisonBonusPercent += 15;
            }

            if (currentDiceType == DiceType.KingPoison && DiceMetaDataProvider.HasKingIceDamageBonus())
                poisonBonusPercent += 15;

            return CalculateAppliedDamage(poisonRawDamage, monster.Defense, poisonBonusPercent);
        }

        private static float GetBattleCooldown(DiceType diceType, int star)
        {
            float shotDelay = PlayerController.Instance != null ? Mathf.Max(0f, PlayerController.Instance.fireRate) : 0f;
            return shotDelay + DiceMetaDataProvider.GetCooldown(diceType, star);
        }

        private static int CalculateAppliedDamage(int rawDamage, int defense, int bonusPercent)
        {
            if (rawDamage <= 0)
                return 0;

            float defenseMultiplier = defense >= 0
                ? 100f / (100f + defense)
                : 2f - (100f / (100f - defense));

            float totalMultiplier = defenseMultiplier * (1f + Mathf.Max(0, bonusPercent) * 0.01f);
            return Mathf.Max(1, Mathf.CeilToInt(rawDamage * totalMultiplier));
        }

        private static int GetFollowUpDefense(DiceType diceType, int level, int defense)
        {
            if (diceType != DiceType.ArmorBreak)
                return defense;

            int reducedAmount = Mathf.RoundToInt(defense * (DiceMetaDataProvider.GetArmorBreakPercent(level) * 0.01f));
            return defense - reducedAmount;
        }

        private static int GetFollowUpDamageBonusPercent(DiceType diceType, int level)
        {
            int bonusPercent = 0;

            if ((diceType == DiceType.Ice || diceType == DiceType.KingIce || diceType == DiceType.KingPoison)
                && DiceMetaDataProvider.HasKingIceDamageBonus())
            {
                bonusPercent += 15;
            }

            if ((diceType == DiceType.Poison || diceType == DiceType.KingPoison)
                && DiceMetaDataProvider.HasKingPoisonDamageBonus())
            {
                bonusPercent += 15;
            }

            switch (diceType)
            {
                case DiceType.Poison:
                    if (level >= 12)
                        bonusPercent += 10;
                    break;
                case DiceType.Stun:
                    if (level >= 12)
                        bonusPercent += 20;
                    break;
                case DiceType.ArmorBreak:
                    if (level >= 12)
                        bonusPercent += 10;
                    break;
                case DiceType.Wind:
                    if (level >= 6)
                        bonusPercent += 10;
                    break;
                case DiceType.KingThunder:
                    if (level >= 12)
                        bonusPercent += 15;
                    break;
            }

            return bonusPercent;
        }

        private static BattleMonsterSnapshot GetBattleMonsterSnapshot()
        {
            if (GameManager.Instance == null)
                return new BattleMonsterSnapshot("몬스터", 1, 0);

            bool isBoss = GameManager.Instance.IsBossWave();
            return isBoss
                ? new BattleMonsterSnapshot("보스", GameManager.Instance.GetCurrentWaveBossHp(), GameManager.Instance.GetCurrentWaveBossDefense())
                : new BattleMonsterSnapshot("몬스터", GameManager.Instance.GetCurrentWaveMonsterHp(), GameManager.Instance.GetCurrentWaveMonsterDefense());
        }

        private readonly struct BattleMonsterSnapshot
        {
            public BattleMonsterSnapshot(string label, int hp, int defense)
            {
                Label = label;
                Hp = Mathf.Max(1, hp);
                Defense = defense;
            }

            public string Label { get; }
            public int Hp { get; }
            public int Defense { get; }
        }
    }
}
