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

        public static string GetLevelUpAllConfirmMessage()
        {
            return "장비 도면과 골드를 소모해 가능한 만큼 강화하시겠습니까?";
        }
    }
}
