using System.Text;
using UnityEditor;
using UnityEngine;
using OJ.Pinball;
using OJ.Point;

namespace OJ.EditorTools
{
    /// <summary>
    /// <see cref="PinballRewardDatabase"/> 에셋을 만들고 검사한다.
    /// <c>BountyDatabaseAssetBuilder</c> 와 같은 자리의 도구다.
    ///
    /// <b>덮어쓰지 않는다.</b> 이미 있으면 검사만 하고 끝낸다 — 손으로 조정한 경품표를
    /// 도구가 조용히 되돌리면, 값이 왜 바뀌었는지 아무도 못 찾는다.
    /// 기본값으로 되돌리려면 에셋을 지우고 다시 돌릴 것.
    ///
    /// 티켓 재화의 메타데이터(<see cref="PointType.PinballTicket"/>)를
    /// <c>PointMetadataDatabase.asset</c> 에 등록하는 일도 여기서 한다 — 둘 다 "핀볼을
    /// 붙일 때 한 번 하는 일"이라 메뉴가 둘이면 하나를 빠뜨린다.
    /// </summary>
    public static class PinballDatabaseAssetBuilder
    {
        private const string AssetPath = "Assets/ScriptableObject/PinballRewardDatabase.asset";
        private const string MetadataPath = "Assets/ScriptableObject/PointMetadataDatabase.asset";

        /// <summary>현재 판(<c>PinballBoard.asset</c>)의 칸 수. 검사에만 쓴다.</summary>
        private const int ExpectedSlotCount = 5;

        [MenuItem("OJ/개발/핀볼/경품표 에셋 만들기")]
        private static void CreateOrValidate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PinballRewardDatabase>(AssetPath);
            if (existing != null)
            {
                UpgradeIfNeeded(existing);
                Report("[핀볼] 이미 있다: " + AssetPath, existing);
            }
            else
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(AssetPath));

                var database = ScriptableObject.CreateInstance<PinballRewardDatabase>();
                database.PopulateDefaults();

                AssetDatabase.CreateAsset(database, AssetPath);
                AssetDatabase.SaveAssets();

                Report("[핀볼] 새로 만들었다: " + AssetPath + System.Environment.NewLine +
                       "  StaticResource 빈 슬롯 채우기가 다음 컴파일에 자동으로 잇는다.", database);
            }

            RegisterTicketMetadata();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("OJ/개발/핀볼/경품표 검사")]
        private static void ValidateOnly()
        {
            var database = AssetDatabase.LoadAssetAtPath<PinballRewardDatabase>(AssetPath);
            if (database == null)
            {
                Debug.LogError("[핀볼] 경품표 에셋이 없다: " + AssetPath +
                               " — 지금은 코드 기본값(골드만 주는 5칸)으로 돌아가고 있다.");
                return;
            }

            Report("[핀볼] 검사: " + AssetPath, database);
        }

        /// <summary>
        /// 나중에 생긴 필드를 보충한다. <b>덮어쓰지 않는다</b> — 비어 있을 때만 채운다.
        ///
        /// 배율 표가 비면 x1 조차 못 골라 발사가 통째로 막히는데, 그건 이 필드가 생기기 전에
        /// 만든 에셋의 기본 상태다. 손으로 조정한 경품 밸런스는 건드리지 않는다.
        /// </summary>
        private static void UpgradeIfNeeded(PinballRewardDatabase database)
        {
            if (database.multiplierTiers != null && database.multiplierTiers.Count > 0)
                return;

            database.PopulateMultiplierDefaults();
            EditorUtility.SetDirty(database);

            Debug.Log("[핀볼] 배율 표가 비어 있어 기본값 8단계를 채웠다. 경품은 그대로 두었다.", database);
        }

        private static void Report(string header, PinballRewardDatabase database)
        {
            var sb = new StringBuilder(header).AppendLine();
            sb.Append("  티켓 ").Append(database.TicketCost).AppendLine("장 / 공 1개(x1)");
            sb.Append("  세션당 최대 ").Append(database.MaxShotsPerSession).AppendLine("발");
            sb.Append("  칸 ").Append(database.slotRewards.Count).AppendLine("개");
            sb.Append("  특수 핀 ").Append(database.specialRewards.Count).AppendLine("종");
            sb.Append("  배율 ").Append(database.multiplierTiers.Count).AppendLine("단계");

            if (database.Validate(ExpectedSlotCount, out string error))
            {
                sb.Append("  검사 통과");
                Debug.Log(sb.ToString(), database);
            }
            else
            {
                sb.Append("  문제: ").Append(error);
                Debug.LogError(sb.ToString(), database);
            }
        }

        /// <summary>
        /// 티켓을 <c>PointMetadataDatabase</c> 에 등록한다.
        ///
        /// <b>아이콘은 비워 둔다.</b> 어떤 스프라이트를 쓸지는 사람이 정할 일이고,
        /// 아무 그림이나 넣으면 "등록됐다"고 보여서 빠졌다는 사실이 가려진다.
        /// 비어 있으면 <c>OJ/개발/Resources 경로 보고서</c> 가 매번 짚어 준다.
        /// </summary>
        private static void RegisterTicketMetadata()
        {
            var metadata = AssetDatabase.LoadAssetAtPath<PointMetadataDatabase>(MetadataPath);
            if (metadata == null)
            {
                Debug.LogError("[핀볼] PointMetadataDatabase 를 못 찾아 티켓을 등록하지 못했다: " + MetadataPath);
                return;
            }

            SerializedObject so = new SerializedObject(metadata);
            SerializedProperty list = so.FindProperty("metadataList");
            if (list == null || !list.isArray)
            {
                Debug.LogError("[핀볼] PointMetadataDatabase 의 metadataList 를 못 찾았다. " +
                               "필드 이름이 바뀌었는지 확인할 것.");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("pointType").intValue == (int)PointType.PinballTicket)
                {
                    Debug.Log("[핀볼] 티켓 메타데이터가 이미 등록돼 있다. 그대로 둔다.");
                    return;
                }
            }

            list.arraySize++;
            SerializedProperty added = list.GetArrayElementAtIndex(list.arraySize - 1);
            added.FindPropertyRelative("pointType").intValue = (int)PointType.PinballTicket;
            added.FindPropertyRelative("displayName").stringValue = "핀볼 티켓";
            added.FindPropertyRelative("description").stringValue = "핀볼을 1회 즐길 수 있는 티켓입니다.";
            added.FindPropertyRelative("icon").objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(metadata);

            Debug.Log("[핀볼] 티켓 메타데이터를 등록했다. " +
                      "<b>아이콘은 비어 있다</b> — PointMetadataDatabase.asset 에서 스프라이트를 지정할 것.", metadata);
        }
    }
}
