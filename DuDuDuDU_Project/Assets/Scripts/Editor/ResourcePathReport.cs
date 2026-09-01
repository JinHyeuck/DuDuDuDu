using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Equipment;
using OJ.Point;
using OJ.Relic;
using OJ.Utils;

namespace OJ.EditorTools
{
    /// <summary>
    /// <c>Resources.Load</c> 가 실제로 무언가를 돌려주는지 확인한다. (MIGRATION_BASELINE 11.6)
    ///
    /// <b>왜 정적 검사로는 안 되나.</b> 경로가 문자열 보간으로 만들어진다 —
    /// <c>$"{ItemSlotPath}Itme_Slot_{GetRarityIndex(rarity)}"</c> 같은 식이라 코드만 읽어서는
    /// 최종 경로를 알 수 없다. 그래서 <b>실제로 불러 보는 것</b>이 유일하게 확실한 방법이다.
    ///
    /// <b>왜 필요한가.</b> 아트를 옮기거나 이름을 바꾸면 이 호출들은 <b>컴파일을 통과한 채
    /// 런타임에 null 을 돌려준다.</b> 그 null 은 대부분 <c>if (sprite != null)</c> 에 걸러져
    /// 아이콘이 안 보이는 것으로만 나타나고, 콘솔에는 아무것도 남지 않는다.
    ///
    /// 이 도구는 <b>코드가 만들 수 있는 경로를 전부 만들어 본다.</b> enum 을 돌면서
    /// 같은 규칙으로 조합하므로, 규칙이 바뀌면 여기도 같이 고쳐야 한다 — 그 중복이
    /// 이 검사의 값이다. 규칙과 검사가 따로 어긋나면 검사가 먼저 실패한다.
    /// </summary>
    public static class ResourcePathReport
    {
        private const string ItemSlot = "Art/ItemSlot/";

        [MenuItem("OJ/개발/Resources 경로 보고서")]
        private static void Run()
        {
            var sb = new StringBuilder("[리소스] Resources.Load 경로 확인").AppendLine();
            var missing = new List<string>();

            // 고정 경로
            Check<Sprite>(sb, missing, "Art/Upgrade/Icon_lock");
            Check<Sprite>(sb, missing, "Art/Main/Icon_Reward_Pig");
            Check<Sprite>(sb, missing, ItemSlot + "Icon_ItemGem_Normal");
            Check<Sprite>(sb, missing, ItemSlot + "Icon_ItemGem_Full");
            Check<GameObject>(sb, missing, "StaticResource");

            // 장비 아이콘 — UIEquipmentText 의 switch 와 같은 이름 집합이다.
            foreach (string n in new[]
                     {
                         "Item_weapon", "Item_Hat", "Item_Armor", "Item_Ring", "Item_Shose", "Item_Necklace",
                         "Icon_Weapon", "Icon_Hat", "Icon_Armor", "Icon_Ring", "Icon_Shose", "Icon_Necklace",
                     })
            {
                Check<Sprite>(sb, missing, ItemSlot + n);
            }

            // 등급별 슬롯. GetRarityIndex 가 만드는 범위를 넉넉히 훑는다.
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                int index = (int)rarity;
                Check<Sprite>(sb, missing, ItemSlot + "Itme_Slot_" + index);
                Check<Sprite>(sb, missing, ItemSlot + "Slot_Gem_" + index);
            }

            // 재화 아이콘은 Resources 경로가 아니라 PointMetadataDatabase 가 정본이다.
            //
            // 처음에는 "이름이 Scroll 로 끝나는 PointType 은 Art/Gem/{이름} 에 있다"고
            // 가정해 경로를 조합했다. <b>그 규칙은 틀렸다.</b> 원소 스크롤 5종에만
            // 성립하고, 장비 스크롤 6종과 MythicScroll 은 그 폴더에 아예 없다.
            // 그래서 도구가 7건을 "없음"으로 올렸는데 게임은 멀쩡했다 —
            // <c>UIEquipmentConfirmDialog.GetScrollCostIconSprite</c> 는 이 에셋을
            // <b>먼저</b> 보고, 거기에 아이콘이 다 들어 있기 때문이다.
            // Resources 폴백은 그 뒤에 있어서 도달해도 쓰이지 않는다.
            //
            // 교훈: 경로를 짐작해 만들지 말고 <b>정본을 검사하라.</b> 여기서 물어야 할
            // 것은 "그 경로에 파일이 있나"가 아니라 "항목마다 아이콘이 채워져 있나"다.
            var pointDb = AssetDatabase.LoadAssetAtPath<PointMetadataDatabase>(
                "Assets/ScriptableObject/PointMetadataDatabase.asset");
            if (pointDb != null)
            {
                foreach (PointType type in Enum.GetValues(typeof(PointType)))
                {
                    if (type == PointType.Max)
                        continue;

                    PointMetadataDatabase.PointMetadata meta = pointDb.Get(type);
                    if (meta == null)
                        Fail(sb, missing, "PointMetadataDatabase 에 항목이 없다: " + type);
                    else if (meta.icon == null)
                        Fail(sb, missing, "아이콘이 비었다: PointMetadataDatabase[" + type + "]");
                }
            }
            else
            {
                sb.AppendLine("  !! PointMetadataDatabase 를 못 읽어 재화 아이콘을 확인하지 못했다.");
            }

            // 유물 배경 — RelicDatabase.BuildDefaults 가 쓰는 이름 그대로.
            foreach (string n in new[] { "Passive_Normal", "Passive_Rare", "Passive_Epic", "Passive_Mystic" })
                Check<Sprite>(sb, missing, "Art/Relic/" + n);

            // 유물 아이콘은 Relic_{index} 규칙이다. 데이터베이스가 들고 있는 index 를 그대로 쓴다 —
            // 1..N 을 짐작하면 실제로 안 쓰는 번호까지 없다고 보고하게 된다.
            var relicDb = AssetDatabase.LoadAssetAtPath<RelicDatabase>("Assets/Resources/RelicDatabase.asset");
            if (relicDb != null && relicDb.relics != null)
            {
                foreach (RelicDefinition relic in relicDb.relics)
                {
                    if (relic != null)
                        Check<Sprite>(sb, missing, "Art/Relic/Relic_" + relic.index);
                }
            }
            else
            {
                sb.AppendLine("  !! RelicDatabase 를 못 읽어 유물 아이콘을 확인하지 못했다.");
            }

            sb.AppendLine();
            sb.AppendLine(missing.Count == 0
                ? "  전부 찾았다."
                : "  !! 못 찾은 것 " + missing.Count + "건 — 런타임에 조용히 null 이 된다.");

            if (missing.Count == 0)
                Debug.Log(sb.ToString());
            else
                Debug.LogError(sb.ToString());
        }

        private static void Check<T>(StringBuilder sb, List<string> missing, string path) where T : UnityEngine.Object
        {
            if (Resources.Load<T>(path) != null)
                return;

            // 있는 것은 굳이 나열하지 않는다. 30줄짜리 "OK" 목록은 읽히지 않고,
            // 그 안에 섞인 한 줄의 실패를 오히려 가린다.
            Fail(sb, missing, "없음: " + path + "   (" + typeof(T).Name + ")");
        }

        private static void Fail(StringBuilder sb, List<string> missing, string message)
        {
            missing.Add(message);
            sb.AppendLine("  !! " + message);
        }
    }
}
