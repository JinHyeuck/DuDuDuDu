using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Point;

namespace OJ.EditorTools
{
    /// <summary>
    /// 재화 아이콘·이름·설명을 <c>PointMetadataDatabase.asset</c> 에 굽는다.
    ///
    /// <b>무엇을 덮고 무엇을 두나.</b> 상자 열쇠 두 종(107·108)은 이 도구가 정본이라 매번 맞춘다 —
    /// 예전 값이 "롱기권"(오타)에 레어석·신화석 그림을 빌려 쓰고 있었다.
    /// 나머지는 <b>비어 있거나 enum 이름 그대로인 칸만</b> 채운다. 사람이 적은 이름과 설명을 지우지 않는다.
    ///
    /// 재실행해도 결과가 같다(멱등). 메뉴로만 돈다.
    /// </summary>
    public static class CurrencyIconApplier
    {
        private const string DatabasePath = "Assets/ScriptableObject/PointMetadataDatabase.asset";
        private const string GemArtFolder = "Assets/Resources/Art/Gem/";

        private const string KeySilverName = "일반 보석 상자 열쇠";
        private const string KeyGoldName = "최고급 보석 상자 열쇠";

        private const string CopyDescription = "다이스를 성장시킬 때 쓰는 카피예요.";

        /// <summary>이 도구가 정본인 항목. 아이콘·이름·설명을 항상 맞춘다.</summary>
        private static readonly (PointType type, string name, string description, string icon)[] Owned =
        {
            (PointType.NormalGemTicket, KeySilverName, "일반 보석 상자를 1회 열 수 있어요.", "GemKey_Silver"),
            (PointType.PremiumGemTicket, KeyGoldName, "최고급 보석 상자를 1회 열 수 있어요.", "GemKey_Gold"),
        };

        /// <summary>설명이 비어 있을 때만 채우는 문구. 가방의 재화 상세에 한 줄로 뜬다.</summary>
        private static readonly Dictionary<PointType, string> Descriptions = new Dictionary<PointType, string>
        {
            { PointType.Gold, "가장 기본이 되는 재화예요." },
            { PointType.FreeGem, "게임을 플레이하며 모으는 젬이에요." },
            { PointType.PaidGem, "결제로 얻는 젬이에요." },
            { PointType.Stamina, "스테이지에 입장할 때 쓰는 고기예요." },
            { PointType.RelicTicket, "유물을 뽑을 때 쓰는 권이에요." },
            { PointType.NormalScroll, CopyDescription },
            { PointType.FireScroll, CopyDescription },
            { PointType.IceScroll, CopyDescription },
            { PointType.PoisonScroll, CopyDescription },
            { PointType.ThunderScroll, CopyDescription },
            { PointType.TornadoCopy, CopyDescription },
            { PointType.StunCopy, CopyDescription },
            { PointType.ArmorBreakCopy, CopyDescription },
            { PointType.WindCopy, CopyDescription },
            { PointType.TimeCopy, CopyDescription },
            { PointType.KingNormalCopy, CopyDescription },
            { PointType.KingFireCopy, CopyDescription },
            { PointType.KingIceCopy, CopyDescription },
            { PointType.KingPoisonCopy, CopyDescription },
            { PointType.KingThunderCopy, CopyDescription },
            { PointType.SelectTicket, "레어·킹 다이스 중 하나를 골라 카피 1장으로 바꿔요." },
            { PointType.EventCurrency1, "이벤트에서 쓰는 재화예요." },
            { PointType.EventCurrency2, "이벤트에서 쓰는 재화예요." },
            { PointType.EventCurrency3, "이벤트에서 쓰는 재화예요." },
            { PointType.WeaponScroll, "무기를 강화할 때 쓰는 강화권이에요." },
            { PointType.HelmetScroll, "투구를 강화할 때 쓰는 강화권이에요." },
            { PointType.ArmorScroll, "갑옷을 강화할 때 쓰는 강화권이에요." },
            { PointType.RingScroll, "반지를 강화할 때 쓰는 강화권이에요." },
            { PointType.ShoesScroll, "신발을 강화할 때 쓰는 강화권이에요." },
            { PointType.NecklaceScroll, "목걸이를 강화할 때 쓰는 강화권이에요." },
        };

        [MenuItem("OJ/개발/재화/아이콘 적용")]
        private static void Apply()
        {
            var database = AssetDatabase.LoadAssetAtPath<PointMetadataDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError("[재화] PointMetadataDatabase 를 못 찾았다: " + DatabasePath);
                return;
            }

            var so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("metadataList");
            if (list == null || !list.isArray)
            {
                Debug.LogError("[재화] metadataList 를 못 찾았다. 필드 이름이 바뀌었는지 확인할 것.");
                return;
            }

            var log = new StringBuilder("[재화] 아이콘 적용").AppendLine();
            int changed = 0;

            foreach (var owned in Owned)
            {
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(GemArtFolder + owned.icon + ".png");
                if (icon == null)
                {
                    Debug.LogError("[재화] 그림이 없다: " + GemArtFolder + owned.icon + ".png");
                    return;
                }

                SerializedProperty entry = FindOrAdd(list, owned.type);
                changed += SetString(entry.FindPropertyRelative("displayName"), owned.name) ? 1 : 0;
                changed += SetString(entry.FindPropertyRelative("description"), owned.description) ? 1 : 0;

                SerializedProperty iconProp = entry.FindPropertyRelative("icon");
                if (iconProp.objectReferenceValue != icon)
                {
                    iconProp.objectReferenceValue = icon;
                    changed++;
                }

                log.AppendLine("  " + owned.type + " → " + owned.icon + " / " + owned.name);
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                var type = (PointType)entry.FindPropertyRelative("pointType").intValue;
                if (!Descriptions.TryGetValue(type, out string description))
                    continue;

                SerializedProperty descriptionProp = entry.FindPropertyRelative("description");
                if (!IsPlaceholder(descriptionProp.stringValue, type))
                    continue;

                descriptionProp.stringValue = description;
                changed++;
                log.AppendLine("  설명 채움 " + type);
            }

            if (changed == 0)
            {
                Debug.Log("[재화] 이미 맞다. 바꿀 것이 없다.");
                return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString(), database);
        }

        /// <summary>비었거나 enum 이름을 소문자로 옮긴 임시값이면 true — 사람이 쓴 설명은 건드리지 않는다.</summary>
        private static bool IsPlaceholder(string value, PointType type)
        {
            return string.IsNullOrWhiteSpace(value) ||
                   string.Equals(value.Trim(), type.ToString(), System.StringComparison.OrdinalIgnoreCase);
        }

        private static SerializedProperty FindOrAdd(SerializedProperty list, PointType type)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("pointType").intValue == (int)type)
                    return entry;
            }

            list.arraySize++;
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("pointType").intValue = (int)type;
            return added;
        }

        private static bool SetString(SerializedProperty property, string value)
        {
            if (property.stringValue == value)
                return false;

            property.stringValue = value;
            return true;
        }
    }
}
