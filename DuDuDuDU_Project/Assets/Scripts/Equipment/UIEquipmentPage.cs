using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;
using OJ.UI;

namespace OJ.Equipment
{
    /// <summary>
    /// 로비의 장비 탭. 위는 캐릭터와 장비 6칸·일괄 레벨업, 아래는 장착 효과 목록.
    ///
    /// 예전 아래쪽은 "장착 효과" / "보석" 두 탭이었다. 보석 목록과 합성은 장비 칸을 누르면 뜨는
    /// 강화 팝업(<see cref="UIEquipmentConfirmDialog"/>)으로 옮겨 갔고(2026-10-22 시안),
    /// 그래서 아래쪽은 <b>장착 효과만 고정으로 보여 준다.</b>
    /// </summary>
    public class UIEquipmentPage : DialogBase
    {
        private class EquippedEffectGroup
        {
            public EquipmentType equipmentType;
            public readonly List<string> gemNames = new List<string>();
            public readonly List<string> effectTexts = new List<string>();
        }

        [Header("Summary")]
        [SerializeField] private TMP_Text totalAttackText;

        [Header("Equipped Effects")]
        [SerializeField] private TMP_Text emptyEquippedEffectText;
        [SerializeField] private Transform equippedEffectRoot;
        [SerializeField] private UIEquipmentEffectGroupItem equippedEffectGroupPrefab;

        [Header("Equipment List")]
        [SerializeField] private List<UIEquipmentItem> manualEquipmentItems = new List<UIEquipmentItem>();
        [SerializeField] private Transform equipmentListRoot;
        [SerializeField] private UIEquipmentItem equipmentItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button levelUpAllButton;

        [Header("Dialogs")]
        [SerializeField] private UIEquipmentConfirmDialog confirmDialog;

        public event Action OnDataChanged;

        private readonly List<UIEquipmentItem> equipmentItems = new List<UIEquipmentItem>();
        private readonly List<UIEquipmentEffectGroupItem> equippedEffectGroups = new List<UIEquipmentEffectGroupItem>();
        private readonly List<EquipmentType> equipmentTypes = new List<EquipmentType>();

        private EquipmentType selectedEquipmentType = EquipmentType.Weapon;
        private bool buttonsBound;
        private bool missingReferenceWarned;

        // 일괄 레벨업 동안은 단계마다 오는 변경 이벤트로 다시 그리지 않는다 — 한 단계에 이벤트가
        // 넷(장비·보석·재화 둘)이라 50단계면 200번 다시 그린다. 끝나고 한 번만 그린다.
        private bool suppressRefresh;

        protected override void OnLoad()
        {
            TryBindButtons();
        }

        protected override void OnUnload()
        {
            if (buttonsBound && levelUpAllButton != null)
                levelUpAllButton.onClick.RemoveListener(OnClickLevelUpAll);
        }

        protected override void OnEnter()
        {
            ValidateSceneReferences();
            Subscribe();
            BuildIfNeeded();
            RefreshAll();

            // RefreshAll 이 목록을 다 채운 뒤여야 한다.
            UIAppear.PlayChildren(equipmentListRoot);
            UIAppear.PlayChildren(equippedEffectRoot);
        }

        protected override void OnExit()
        {
            Unsubscribe();

            confirmDialog?.Exit();
        }

        public void RefreshAll()
        {
            RefreshSummary();
            RefreshEquipmentList();
            RefreshEquippedEffects();
            RefreshLevelUpAllButton();
            NotifyChanged();
        }

        private void ValidateSceneReferences()
        {
            if (totalAttackText != null &&
                (manualEquipmentItems.Count > 0 || equipmentListRoot != null) &&
                equippedEffectRoot != null &&
                equippedEffectGroupPrefab != null &&
                levelUpAllButton != null &&
                confirmDialog != null)
            {
                return;
            }

            if (missingReferenceWarned)
                return;

            missingReferenceWarned = true;
            Debug.LogWarning("UIEquipmentPage: Scene UI references are missing. Assign the equipment UI fields in the Inspector.");
        }

        private void BuildIfNeeded()
        {
            if (equipmentTypes.Count <= 0)
            {
                foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
                    equipmentTypes.Add(equipmentType);
            }

            if (manualEquipmentItems != null && manualEquipmentItems.Count > 0)
            {
                if (equipmentItems.Count == 0)
                    equipmentItems.AddRange(manualEquipmentItems);

                for (int i = 0; i < equipmentItems.Count; i++)
                {
                    if (i < equipmentTypes.Count)
                    {
                        equipmentItems[i].gameObject.SetActive(true);
                        equipmentItems[i].Bind(equipmentTypes[i], OnClickEquipmentItem);
                    }
                    else
                    {
                        equipmentItems[i].gameObject.SetActive(false);
                    }
                }
            }
            else if (equipmentItemPrefab != null && equipmentListRoot != null)
            {
                while (equipmentItems.Count < equipmentTypes.Count)
                {
                    UIEquipmentItem item = Instantiate(equipmentItemPrefab, equipmentListRoot);
                    item.gameObject.SetActive(true);
                    equipmentItems.Add(item);
                }

                for (int i = 0; i < equipmentItems.Count; i++)
                {
                    if (i >= equipmentTypes.Count)
                    {
                        equipmentItems[i].gameObject.SetActive(false);
                        continue;
                    }

                    equipmentItems[i].gameObject.SetActive(true);
                    equipmentItems[i].Bind(equipmentTypes[i], OnClickEquipmentItem);
                }
            }
        }

        private void RefreshSummary()
        {
            if (totalAttackText == null || EquipmentManager.Instance == null)
                return;

            totalAttackText.SetText("{0}", EquipmentManager.Instance.GetTotalEquipmentAttack());
        }

        private void RefreshEquipmentList()
        {
            if (EquipmentManager.Instance == null)
                return;

            for (int i = 0; i < equipmentItems.Count; i++)
            {
                if (i >= equipmentTypes.Count)
                    continue;

                EquipmentType equipmentType = equipmentTypes[i];
                int level = EquipmentManager.Instance.GetLevel(equipmentType);
                int attack = EquipmentManager.Instance.GetEquipmentAttack(equipmentType);
                bool selected = selectedEquipmentType == equipmentType;
                List<EquipmentSlotVisualState> slotStates = BuildSlotStates(equipmentType);
                equipmentItems[i].Refresh(level, attack, selected, slotStates);
            }
        }

        private List<EquipmentSlotVisualState> BuildSlotStates(EquipmentType equipmentType)
        {
            List<EquipmentSlotVisualState> states = new List<EquipmentSlotVisualState>(Define.MaxEquipmentSlot);
            if (EquipmentManager.Instance == null)
                return states;

            for (int slotIndex = 0; slotIndex < Define.MaxEquipmentSlot; slotIndex++)
            {
                if (!EquipmentManager.Instance.IsSlotUnlocked(equipmentType, slotIndex))
                {
                    states.Add(EquipmentSlotVisualState.Locked);
                    continue;
                }

                string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
                states.Add(string.IsNullOrEmpty(gemId) ? EquipmentSlotVisualState.Empty : EquipmentSlotVisualState.Equipped);
            }

            return states;
        }

        private void RefreshEquippedEffects()
        {
            List<EquippedEffectGroup> effectGroups = BuildEquippedEffectGroups();
            int effectCount = CountEquippedEffectRows(effectGroups);

            RefreshEquippedEffectGroups(effectGroups);

            if (emptyEquippedEffectText != null)
            {
                emptyEquippedEffectText.gameObject.SetActive(effectCount <= 0);
                if (effectCount <= 0)
                    emptyEquippedEffectText.SetText("장착한 보석 효과가 없습니다.");
            }
        }

        private List<EquippedEffectGroup> BuildEquippedEffectGroups()
        {
            List<EquippedEffectGroup> groups = new List<EquippedEffectGroup>();
            if (EquipmentManager.Instance == null)
                return groups;

            foreach (EquipmentType equipmentType in Enum.GetValues(typeof(EquipmentType)))
            {
                EquippedEffectGroup group = null;

                for (int slotIndex = 0; slotIndex < Define.MaxEquipmentSlot; slotIndex++)
                {
                    string gemId = EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
                    if (string.IsNullOrEmpty(gemId))
                        continue;

                    if (!EquipmentManager.Instance.TryGetGemDefinition(gemId, out GemDefinition definition) || definition == null)
                        continue;

                    if (group == null)
                    {
                        group = new EquippedEffectGroup { equipmentType = equipmentType };
                        groups.Add(group);
                    }

                    if (definition.effects == null || definition.effects.Count <= 0)
                    {
                        group.gemNames.Add(definition.displayName);
                        group.effectTexts.Add("효과 없음");
                        continue;
                    }

                    for (int effectIndex = 0; effectIndex < definition.effects.Count; effectIndex++)
                    {
                        group.gemNames.Add(definition.displayName);
                        group.effectTexts.Add(UIEquipmentEffectTextFormatter.BuildEffectText(definition.effects[effectIndex]));
                    }
                }
            }

            return groups;
        }

        private int CountEquippedEffectRows(IReadOnlyList<EquippedEffectGroup> groups)
        {
            int count = 0;
            if (groups == null)
                return count;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null && groups[i].gemNames != null)
                    count += groups[i].gemNames.Count;
            }

            return count;
        }

        private void RefreshEquippedEffectGroups(IReadOnlyList<EquippedEffectGroup> effectGroups)
        {
            if (equippedEffectRoot == null || equippedEffectGroupPrefab == null || effectGroups == null)
                return;

            while (equippedEffectGroups.Count < effectGroups.Count)
            {
                UIEquipmentEffectGroupItem groupItem = Instantiate(equippedEffectGroupPrefab, equippedEffectRoot);
                groupItem.gameObject.SetActive(true);
                equippedEffectGroups.Add(groupItem);
            }

            for (int i = 0; i < equippedEffectGroups.Count; i++)
            {
                UIEquipmentEffectGroupItem groupItem = equippedEffectGroups[i];
                if (groupItem == null)
                    continue;

                bool active = i < effectGroups.Count;
                groupItem.gameObject.SetActive(active);
                if (!active)
                    continue;

                EquippedEffectGroup group = effectGroups[i];
                groupItem.Refresh(group.equipmentType, group.gemNames, group.effectTexts);
                groupItem.RebuildLayout();
            }

            Canvas.ForceUpdateCanvases();
            if (equippedEffectRoot is RectTransform rectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }

        private void OnClickEquipmentItem(EquipmentType equipmentType)
        {
            selectedEquipmentType = equipmentType;
            RefreshEquipmentList();

            if (confirmDialog == null)
            {
                Debug.LogWarning("UIEquipmentPage: UIEquipmentConfirmDialog is missing.");
                return;
            }

            confirmDialog.Open(selectedEquipmentType, RefreshAll);
        }

        /// <summary>
        /// 일괄 레벨업. 규칙은 <see cref="EquipmentManager.LevelUpAll"/> 에 있다 —
        /// 화면은 누르고 다시 그리기만 한다.
        /// </summary>
        private void OnClickLevelUpAll()
        {
            if (EquipmentManager.Instance == null)
                return;

            int count;
            suppressRefresh = true;
            try
            {
                count = EquipmentManager.Instance.LevelUpAll();
            }
            finally
            {
                suppressRefresh = false;
            }

            if (count > 0)
                RefreshAll();
        }

        private void RefreshLevelUpAllButton()
        {
            if (levelUpAllButton == null)
                return;

            levelUpAllButton.interactable = EquipmentManager.Instance != null &&
                                            EquipmentManager.Instance.CanLevelUpAny();
        }

        private void Subscribe()
        {
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged -= OnGemChanged;
                EquipmentManager.Instance.OnEquipmentChanged += OnEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged += OnGemChanged;
            }

            if (PointManager.Instance != null)
            {
                PointManager.Instance.OnPointChanged -= OnPointChanged;
                PointManager.Instance.OnPointChanged += OnPointChanged;
            }
        }

        private void Unsubscribe()
        {
            if (EquipmentManager.Instance != null)
            {
                EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
                EquipmentManager.Instance.OnGemChanged -= OnGemChanged;
            }

            if (PointManager.Instance != null)
                PointManager.Instance.OnPointChanged -= OnPointChanged;
        }

        private void OnEquipmentChanged(EquipmentType equipmentType)
        {
            if (suppressRefresh)
                return;

            RefreshAll();
        }

        private void OnGemChanged()
        {
            if (suppressRefresh)
                return;

            RefreshAll();
        }

        private void OnPointChanged(PointType pointType, int value)
        {
            if (suppressRefresh)
                return;

            RefreshAll();
        }

        private void NotifyChanged()
        {
            OnDataChanged?.Invoke();
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (levelUpAllButton != null)
                levelUpAllButton.onClick.AddListener(OnClickLevelUpAll);

            buttonsBound = levelUpAllButton != null;
        }
    }
}
