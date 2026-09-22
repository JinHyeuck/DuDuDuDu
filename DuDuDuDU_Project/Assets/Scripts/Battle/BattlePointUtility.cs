using UnityEngine;
using OJ.Utils;

namespace OJ.Battle
{
    /// <summary>전투 재화 한 덩이. <c>OJ.Point.PointRewardEntry</c> 의 전투판이다.</summary>
    public struct BattleRewardEntry
    {
        public BattlePointType BattlePointType;
        public int Amount;

        public BattleRewardEntry(BattlePointType battlePointType, int amount)
        {
            BattlePointType = battlePointType;
            Amount = amount;
        }
    }

    /// <summary>
    /// 전투 재화의 이름·아이콘 조회. <c>OJ.Point.PointRewardUtility</c> 의 전투판이다.
    ///
    /// <b>지급 메서드가 없는 것이 의도다.</b> 영구 재화 쪽 <c>PointRewardUtility.GrantRewards</c>
    /// 는 싱글톤을 잡아 쓸 수 있지만 전투 재화는 판마다 다른 <c>RunState</c> 에 얹혀 있어서,
    /// 전역에서 지급하면 <b>어느 판에 넣는지가 불분명해진다.</b> 지급은 판을 들고 있는
    /// <c>BattlePointManager</c> 로만 한다(<c>IBattleRefs.BattlePoints</c>).
    /// </summary>
    public static class BattlePointUtility
    {
        public static Sprite GetIcon(BattlePointType battlePointType)
        {
            if (!StaticResource.isAlive || StaticResource.Instance == null ||
                StaticResource.Instance.BattlePointMetadataDatabase == null)
                return null;

            BattlePointMetadataDatabase.BattlePointMetadata metadata =
                StaticResource.Instance.BattlePointMetadataDatabase.Get(battlePointType);

            return metadata != null ? metadata.icon : null;
        }

        /// <summary>
        /// 화면에 적을 재화 이름. <b>에셋이 정본이고 코드 표는 폴백이다</b> —
        /// 규칙은 <c>PointRewardUtility.GetPointName</c> 과 같은 것을 쓴다.
        /// </summary>
        public static string GetName(BattlePointType battlePointType)
        {
            string registered = null;

            if (StaticResource.isAlive && StaticResource.Instance != null &&
                StaticResource.Instance.BattlePointMetadataDatabase != null)
            {
                BattlePointMetadataDatabase.BattlePointMetadata metadata =
                    StaticResource.Instance.BattlePointMetadataDatabase.Get(battlePointType);

                if (metadata != null)
                    registered = metadata.displayName;
            }

            return ResolveName(registered, battlePointType);
        }

        /// <summary>
        /// 등록된 이름과 폴백 중 무엇을 쓸지 고른다. <b>순수 함수라 따로 빼 뒀다</b> —
        /// <see cref="GetName"/> 은 <c>StaticResource</c> 싱글톤을 잡으므로 에디터 밖
        /// 테스트 러너에서 돌지 않는데, 판단 자체는 거기서도 검증돼야 한다.
        ///
        /// <b>enum 명과 같은 <paramref name="registeredName"/> 은 "등록 안 됨" 으로 본다.</b>
        /// 그러지 않으면 등록되지 않은 재화가 화면에 <c>EnhanceStone</c> 으로 뜨는데,
        /// 그건 값이 있는 것처럼 보여서 빠뜨렸다는 사실이 드러나지 않는다.
        /// </summary>
        public static string ResolveName(string registeredName, BattlePointType battlePointType)
        {
            if (!string.IsNullOrWhiteSpace(registeredName) &&
                registeredName != battlePointType.ToString())
            {
                return registeredName;
            }

            return FallbackName(battlePointType);
        }

        /// <summary>
        /// 에셋에 아무것도 등록되지 않았을 때 화면을 한글로 잡아 주는 표.
        /// 등록이 끝나면 에셋 값이 이기고 이 표는 저절로 안 쓰인다.
        /// </summary>
        public static string FallbackName(BattlePointType battlePointType)
        {
            switch (battlePointType)
            {
                case BattlePointType.SummonPoint: return "SP";
                case BattlePointType.EnhanceStone: return "강화석";
                default: return battlePointType.ToString();
            }
        }
    }
}
