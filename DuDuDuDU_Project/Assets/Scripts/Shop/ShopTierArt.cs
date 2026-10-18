using System.Collections.Generic;
using UnityEngine;

namespace OJ.Shop
{
    /// <summary>
    /// 재화 상점 카드의 <b>수량 티어 그림</b>을 고른다. (시안 <c>Art/Layout/Shop_*.png</c>)
    ///
    /// <b>왜 수량마다 그림이 다른가.</b> 시안은 티어를 낱개 → 봉지 → 상자로 그린다.
    /// 재화 아이콘 하나를 세 번 그리면 5 와 150 이 같은 그림이 되어, 숫자를 읽기 전에는
    /// 무엇이 더 큰 묶음인지 알 수 없다. 그림이 티어를 먼저 말한다.
    ///
    /// <b>왜 SO 가 아니라 규약인가.</b> 파일 이름이 이미 <c>Shop_Dia_1·2·3</c> 처럼
    /// 티어 번호로 끝나 있고, 이것은 밸런스 수치가 아니라 그림이다. SO 필드로 올리면
    /// 에셋 스물네 칸을 손으로 채워야 하고, 한 칸만 비어도 그 카드만 조용히
    /// <b>재화 아이콘으로 떨어진다</b> — 눈으로 찾기 어려운 어긋남이다.
    /// 아트가 섹션별로 달라지는 날 SO 로 올리면 된다.
    ///
    /// 티어 번호는 <b>1부터</b>다. 그림이 없는 티어는 <c>null</c> 이고,
    /// 받는 쪽(<see cref="UIShopGridCard.Bind"/>)이 재화 아이콘으로 떨어진다.
    /// </summary>
    internal static class ShopTierArt
    {
        private const string Root = "Art/Shop/";

        /// <summary>깃털(유료젬) 상점. 시안이 두 줄 여섯 칸이라 그림도 여섯이다.</summary>
        internal static Sprite Feather(int tier) => Load("Shop_Feather_", tier);

        /// <summary>다이아(무료젬) 상점.</summary>
        internal static Sprite Diamond(int tier) => Load("Shop_Dia_", tier);

        /// <summary>골드 상점.</summary>
        internal static Sprite Gold(int tier) => Load("Shop_Money_", tier);

        /// <summary>
        /// 강화석 상점. 신화석은 빨강(<c>MergeLevelUp_2</c>), 그 밖은 파랑(<c>MergeLevelUp_1</c>) —
        /// <c>PointMetadataDatabase</c> 의 재화 아이콘과 같은 짝이라 색이 어긋나지 않는다.
        /// </summary>
        internal static Sprite Stone(PointType stoneType, int tier)
        {
            return Load(stoneType == PointType.MythicStone
                ? "Shop_MergeLevelUp_2_"
                : "Shop_MergeLevelUp_1_", tier);
        }

        /// <summary>보석 보물상자의 상자 그림. 시안이 일반=은상자, 영웅=금상자다.</summary>
        internal static Sprite GemBoxArt(int index) => Load("DiaBox_", index + 1);

        /// <summary>보석 보물상자의 카드 바탕. 시안이 일반=청록, 영웅=보라다.</summary>
        internal static Sprite GemBoxCard(int index)
        {
            return index <= 0
                ? Load("Shop_DiaBox_Btn", 0, bare: true)
                : Load("Shop_DiaBox_Btn_", 2);
        }

        /// <summary>
        /// <see cref="Resources.Load"/> 는 없을 때마다 경로를 다시 뒤진다. 카드가 새로 그려질
        /// 때마다(재화가 바뀔 때마다) 불리므로 결과를 없는 것까지 기억한다.
        /// </summary>
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Sprite Load(string prefix, int tier, bool bare = false)
        {
            if (!bare && tier < 1)
                return null;

            string key = bare ? prefix : prefix + tier;
            if (Cache.TryGetValue(key, out Sprite cached))
                return cached;

            Sprite sprite = Resources.Load<Sprite>(Root + key);
            Cache[key] = sprite;
            return sprite;
        }
    }
}
