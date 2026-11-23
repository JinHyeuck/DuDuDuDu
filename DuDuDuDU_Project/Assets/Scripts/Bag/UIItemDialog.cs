using System.Collections.Generic;
using UnityEngine;
using OJ.DI;
using OJ.Point;
using OJ.UI;

namespace OJ.Bag
{
    /// <summary>
    /// 가방. 가진 재화를 4열 격자로 보여 주고, 한 칸을 누르면 <see cref="UIItemDetailPopup"/> 이 뜬다.
    ///
    /// 칸은 템플릿 하나를 복제해 쓴다. 한 번 만든 칸은 닫아도 지우지 않고 다시 쓴다 —
    /// 재화가 바뀔 때마다 새로 만들면 스크롤 위치와 할당이 같이 흔들린다.
    /// </summary>
    public sealed class UIItemDialog : DialogBase
    {
        [SerializeField] private RectTransform gridContent;
        [SerializeField] private UIItemSlot slotTemplate;

        private readonly List<UIItemSlot> slots = new List<UIItemSlot>();

        protected override void OnLoad()
        {
            UseBackBtn = true;

            if (slotTemplate != null)
                slotTemplate.gameObject.SetActive(false);
        }

        protected override void OnEnter()
        {
            PointManager points = PointManager.Instance;
            if (points != null)
                points.OnPointChanged += OnPointChanged;

            if (gridContent != null)
                gridContent.anchoredPosition = Vector2.zero;

            Refresh();
        }

        protected override void OnExit()
        {
            PointManager points = PointManager.Instance;
            if (points != null)
                points.OnPointChanged -= OnPointChanged;
        }

        private void OnPointChanged(PointType changed, int value)
        {
            // 목록 자체(어떤 재화가 보이나)가 바뀔 수 있으니 값만 고치지 않고 다시 그린다.
            Refresh();
        }

        private void Refresh()
        {
            if (slotTemplate == null || gridContent == null)
                return;

            PointManager points = PointManager.Instance;
            List<PointType> types = BagItemRules.Collect(t => points != null ? points.Get(t) : 0);

            while (slots.Count < types.Count)
            {
                UIItemSlot slot = Instantiate(slotTemplate, gridContent);
                slot.gameObject.SetActive(true);
                slots.Add(slot);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                bool used = i < types.Count;
                slots[i].gameObject.SetActive(used);
                if (used)
                    slots[i].Bind(types[i], points != null ? points.Get(types[i]) : 0, OpenDetail);
            }
        }

        private void OpenDetail(PointType type)
        {
            UIItemDetailPopup popup = GameContainer.UI?.Get<UIItemDetailPopup>();
            if (popup == null)
            {
                Debug.LogError("[가방] 재화 설명 창을 열지 못했다. DialogCatalog 에 등재됐는지 확인할 것.");
                return;
            }

            popup.Open(type);
        }
    }
}
