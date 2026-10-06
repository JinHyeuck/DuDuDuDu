#if UNITY_EDITOR || DEV_DEFINE
using TMPro;
using UnityEngine;

namespace OJ.Dev
{
    /// <summary>
    /// 치트 창의 고르는 칸. <b>펼친 목록을 치트 캔버스보다 위에 그린다.</b>
    ///
    /// TMP_Dropdown 은 펼친 목록의 정렬을 <b>30000 으로 박아 둔다</b>(<c>SetupTemplate</c>).
    /// 그런데 치트 캔버스는 페이드(short.MaxValue - 100)보다도 위에 있어야 해서 그보다 높다 —
    /// 그래서 목록이 열려도(<c>IsExpanded</c> = true) <b>창 뒤에 그려져</b> 아무것도 안 보였다.
    /// 목록은 정렬 최댓값으로, 목록 뒤의 클릭 막이(blocker)는 TMP 가 그 한 칸 아래로 둔다.
    /// </summary>
    internal sealed class DevCheatDropdown : TMP_Dropdown
    {
        /// <summary>펼친 목록의 정렬. 캔버스 정렬은 short 범위로 잘리므로 이것이 최댓값이다.</summary>
        internal const int ListSortingOrder = short.MaxValue;

        protected override GameObject CreateDropdownList(GameObject template)
        {
            GameObject list = base.CreateDropdownList(template);

            Canvas canvas = list.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = ListSortingOrder;
            }

            return list;
        }
    }
}
#endif
