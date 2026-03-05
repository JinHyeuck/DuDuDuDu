using System;
using System.Collections.Generic;
using UnityEngine;

namespace OJ
{
    public class UIEquipmentPage : IDialog
    {
        public event Action OnDataChanged;

        protected override void OnEnter()
        {
            base.OnEnter();
            Subscribe();
            NotifyChanged();
        }

        protected override void OnExit()
        {
            Unsubscribe();
            base.OnExit();
        }

        protected override void OnUnload()
        {
            Unsubscribe();
            base.OnUnload();
        }

        public int GetEquipmentLevel(EquipmentType equipmentType)
        {
            if (EquipmentManager.Instance == null)
                return 1;

            return EquipmentManager.Instance.GetLevel(equipmentType);
        }

        public int GetEquipmentAttack(EquipmentType equipmentType)
        {
            if (EquipmentManager.Instance == null)
                return 0;

            return EquipmentManager.Instance.GetEquipmentAttack(equipmentType);
        }

        public int GetTotalEquipmentAttack()
        {
            if (EquipmentManager.Instance == null)
                return 0;

            return EquipmentManager.Instance.GetTotalEquipmentAttack();
        }

        public (int goldCost, int scrollCost) GetUpgradeCost(EquipmentType equipmentType)
        {
            if (EquipmentManager.Instance == null)
                return (0, 0);

            return EquipmentManager.Instance.GetNextUpgradeCost(equipmentType);
        }

        public bool IsSlotUnlocked(EquipmentType equipmentType, int slotIndex)
        {
            if (EquipmentManager.Instance == null)
                return false;

            return EquipmentManager.Instance.IsSlotUnlocked(equipmentType, slotIndex);
        }

        public int GetSlotUnlockLevel(int slotIndex)
        {
            if (EquipmentManager.Instance == null)
                return int.MaxValue;

            return EquipmentManager.Instance.GetSlotUnlockLevel(slotIndex);
        }

        public string GetEquippedGemId(EquipmentType equipmentType, int slotIndex)
        {
            if (EquipmentManager.Instance == null)
                return string.Empty;

            return EquipmentManager.Instance.GetEquippedGemId(equipmentType, slotIndex);
        }

        public int GetGemCount(string gemId)
        {
            if (EquipmentManager.Instance == null)
                return 0;

            return EquipmentManager.Instance.GetGemCount(gemId);
        }

        public IReadOnlyList<GemDefinition> GetGemDefinitions()
        {
            if (EquipmentManager.Instance == null)
                return Array.Empty<GemDefinition>();

            return EquipmentManager.Instance.GetGemDefinitions();
        }

        public bool TryLevelUp(EquipmentType equipmentType)
        {
            if (EquipmentManager.Instance == null)
                return false;

            bool result = EquipmentManager.Instance.TryLevelUp(equipmentType);
            if (result)
                NotifyChanged();
            return result;
        }

        public int TryLevelUpAll()
        {
            if (EquipmentManager.Instance == null)
                return 0;

            int result = EquipmentManager.Instance.TryLevelUpAll();
            if (result > 0)
                NotifyChanged();
            return result;
        }

        public bool TryEquipGem(EquipmentType equipmentType, int slotIndex, string gemId)
        {
            if (EquipmentManager.Instance == null)
                return false;

            bool result = EquipmentManager.Instance.TryEquipGem(equipmentType, slotIndex, gemId);
            if (result)
                NotifyChanged();
            return result;
        }

        public bool UnequipGem(EquipmentType equipmentType, int slotIndex)
        {
            if (EquipmentManager.Instance == null)
                return false;

            bool result = EquipmentManager.Instance.UnequipGem(equipmentType, slotIndex);
            if (result)
                NotifyChanged();
            return result;
        }

        private void Subscribe()
        {
            if (EquipmentManager.Instance == null)
                return;

            EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
            EquipmentManager.Instance.OnGemChanged -= OnGemChanged;
            EquipmentManager.Instance.OnEquipmentChanged += OnEquipmentChanged;
            EquipmentManager.Instance.OnGemChanged += OnGemChanged;
        }

        private void Unsubscribe()
        {
            if (EquipmentManager.Instance == null)
                return;

            EquipmentManager.Instance.OnEquipmentChanged -= OnEquipmentChanged;
            EquipmentManager.Instance.OnGemChanged -= OnGemChanged;
        }

        private void OnEquipmentChanged(EquipmentType equipmentType)
        {
            NotifyChanged();
        }

        private void OnGemChanged()
        {
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            OnDataChanged?.Invoke();
        }
    }
}
