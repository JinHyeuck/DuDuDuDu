using UnityEngine;

namespace OJ
{
    public static class UIEquipmentText
    {
        public static string GetEquipmentName(EquipmentType type)
        {
            switch (type)
            {
                case EquipmentType.Weapon: return "무기";
                case EquipmentType.Helmet: return "모자";
                case EquipmentType.Armor: return "갑옷";
                case EquipmentType.Ring: return "반지";
                case EquipmentType.Shoes: return "신발";
                case EquipmentType.Necklace: return "목걸이";
                default: return type.ToString();
            }
        }

        public static string GetRarityName(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return "언커먼";
                case Rarity.Common: return "커먼";
                case Rarity.Normal: return "노멀";
                case Rarity.Rare: return "레어";
                case Rarity.Epic: return "에픽";
                case Rarity.Mythic: return "신화";
                default: return rarity.ToString();
            }
        }
    }

    public static class UIEquipmentSpriteResolver
    {
        private const string ItemSlotPath = "Art/ItemSlot/";

        public static Sprite GetGemFrameSprite(Rarity rarity)
        {
            return Resources.Load<Sprite>($"{ItemSlotPath}Itme_Slot_{GetRarityIndex(rarity)}");
        }

        public static Sprite GetGemIconSprite(Rarity rarity)
        {
            return Resources.Load<Sprite>($"{ItemSlotPath}Slot_Gem_{GetRarityIndex(rarity)}");
        }

        public static Sprite GetEquipTypeIconSprite(EquipmentType equipmentType)
        {
            return GetEquipmentSmallIconSprite(equipmentType);
        }

        public static Sprite GetEquipmentLargeIconSprite(EquipmentType equipmentType)
        {
            Sprite staticIcon = null;
            if (StaticResource.isAlive && StaticResource.Instance != null)
                staticIcon = StaticResource.Instance.GetEquipmentLargeIcon(equipmentType);

            if (staticIcon != null)
                return staticIcon;

            switch (equipmentType)
            {
                case EquipmentType.Weapon:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Item_weapon");
                case EquipmentType.Helmet:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Item_Hat");
                case EquipmentType.Armor:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Item_Armor");
                case EquipmentType.Ring:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Item_Ring");
                case EquipmentType.Shoes:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Item_Shose");
                case EquipmentType.Necklace:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Item_Necklace");
                default:
                    return null;
            }
        }

        public static Sprite GetEquipmentSmallIconSprite(EquipmentType equipmentType)
        {
            Sprite staticIcon = null;
            if (StaticResource.isAlive && StaticResource.Instance != null)
                staticIcon = StaticResource.Instance.GetEquipmentSmallIcon(equipmentType);

            if (staticIcon != null)
                return staticIcon;

            switch (equipmentType)
            {
                case EquipmentType.Weapon:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Icon_Weapon");
                case EquipmentType.Helmet:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Icon_Hat");
                case EquipmentType.Armor:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Icon_Armor");
                case EquipmentType.Ring:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Icon_Ring");
                case EquipmentType.Shoes:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Icon_Shose");
                case EquipmentType.Necklace:
                    return Resources.Load<Sprite>($"{ItemSlotPath}Icon_Necklace");
                default:
                    return null;
            }
        }

        public static Sprite GetEmptySlotSprite()
        {
            return Resources.Load<Sprite>($"{ItemSlotPath}Icon_ItemGem_Normal");
        }

        public static Sprite GetEquippedSlotSprite()
        {
            return Resources.Load<Sprite>($"{ItemSlotPath}Icon_ItemGem_Full");
        }

        public static Sprite GetLockedSlotSprite()
        {
            return Resources.Load<Sprite>("Art/Upgrade/Icon_lock");
        }

        private static int GetRarityIndex(Rarity rarity)
        {
            return Mathf.Clamp((int)rarity, 0, 5);
        }
    }
}
