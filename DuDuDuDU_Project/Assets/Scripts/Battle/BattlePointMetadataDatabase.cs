using System.Collections.Generic;
using UnityEngine;

namespace OJ.Battle
{
    /// <summary>
    /// 전투 재화의 아이콘·표시명 정본. <c>OJ.Point.PointMetadataDatabase</c> 의 전투판이다.
    ///
    /// <b>왜 따로 있는가.</b> 전투 재화에는 이런 표가 아예 없었고, 그래서 현상금 UI 세 곳이
    /// <c>"SP +"</c> · <c>"강화석 +"</c> 를 각자 하드코딩하고 아이콘은 통째로 포기하고 있었다.
    /// 표를 한 곳에 두면 그 셋이 한 줄로 줄고, 앞으로 만들 전투 컨텐츠는 아무것도 더
    /// 만들지 않아도 이름과 아이콘을 얻는다.
    ///
    /// <b>영구 재화 표와 합치지 않은 이유</b>는 <see cref="BattlePointType"/> 주석에 있다 —
    /// 두 재화의 수명이 달라서, 합치면 그 차이가 타입에서 사라진다.
    /// </summary>
    [CreateAssetMenu(fileName = "BattlePointMetadataDatabase", menuName = "OJ/Battle Point Metadata Database")]
    public sealed class BattlePointMetadataDatabase : ScriptableObject
    {
        [System.Serializable]
        public class BattlePointMetadata
        {
            public BattlePointType battlePointType;
            public string displayName;
            [TextArea(2, 4)] public string description;
            public Sprite icon;
        }

        [SerializeField] private List<BattlePointMetadata> metadataList = new List<BattlePointMetadata>();

        private readonly Dictionary<BattlePointType, BattlePointMetadata> metadataMap =
            new Dictionary<BattlePointType, BattlePointMetadata>();

        private void OnEnable()
        {
            RebuildMap();
        }

        public BattlePointMetadata Get(BattlePointType battlePointType)
        {
            if (metadataMap.Count != metadataList.Count)
                RebuildMap();

            metadataMap.TryGetValue(battlePointType, out BattlePointMetadata metadata);
            return metadata;
        }

        public bool TryGet(BattlePointType battlePointType, out BattlePointMetadata metadata)
        {
            if (metadataMap.Count != metadataList.Count)
                RebuildMap();

            return metadataMap.TryGetValue(battlePointType, out metadata);
        }

        private void RebuildMap()
        {
            metadataMap.Clear();

            for (int i = 0; i < metadataList.Count; i++)
            {
                BattlePointMetadata metadata = metadataList[i];
                if (metadata == null)
                    continue;

                metadataMap[metadata.battlePointType] = metadata;
            }
        }
    }
}
