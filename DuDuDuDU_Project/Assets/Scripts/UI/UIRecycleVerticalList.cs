using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.UI
{
    /// <summary>
    /// 높이가 같은 항목을 세로로 수백 개 늘어놓는 스크롤 목록. <b>보이는 만큼만 만들고 돌려 쓴다.</b>
    ///
    /// 무한의 탑 층 목록(300층)이 첫 사용처다. 300장을 다 만들면 창을 열 때마다 멈칫하고,
    /// 카드마다 레이아웃·캔버스 재구성 비용을 낸다. 여기서는 Content 높이만 전체 개수로 잡고,
    /// 카드는 뷰포트에 들어가는 수 + 여유 2장만 만들어 스크롤 위치에 맞춰 옮기고 다시 채운다.
    ///
    /// <b>Content 에 LayoutGroup·ContentSizeFitter 를 두지 않는다.</b> 위치를 이 컴포넌트가
    /// 직접 정하므로, 레이아웃이 있으면 둘이 같은 값을 번갈아 덮는다.
    ///
    /// 항목 템플릿은 꺼 둔 채로 두고 복제해서 쓴다. 채우는 방법은 <see cref="SetCount"/> 에
    /// 넘기는 콜백이 정한다 — 이 컴포넌트는 항목이 무엇인지 모른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIRecycleVerticalList : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform itemTemplate;
        [SerializeField] private float itemHeight = 100f;
        [SerializeField] private float spacing;
        [SerializeField] private float paddingTop;
        [SerializeField] private float paddingBottom;

        private readonly List<RectTransform> pool = new List<RectTransform>();
        private readonly List<int> boundIndex = new List<int>();
        private Action<int, RectTransform> bind;
        private int count;

        private float Pitch => itemHeight + spacing;

        private void Awake()
        {
            if (itemTemplate != null)
                itemTemplate.gameObject.SetActive(false);

            if (scroll != null)
                scroll.onValueChanged.AddListener(OnScrolled);
        }

        private void OnDestroy()
        {
            if (scroll != null)
                scroll.onValueChanged.RemoveListener(OnScrolled);
        }

        /// <summary>
        /// 항목 수와 채우는 방법을 정하고 보이는 항목을 전부 다시 채운다.
        /// 같은 수로 다시 불러도 된다 — 화면을 새로 그릴 때 이것 하나면 된다.
        /// </summary>
        /// <param name="bindItem">(항목 번호, 항목) — 번호 0 이 맨 위다.</param>
        public void SetCount(int itemCount, Action<int, RectTransform> bindItem)
        {
            count = Mathf.Max(0, itemCount);
            bind = bindItem;

            if (content != null)
            {
                Vector2 size = content.sizeDelta;
                size.y = paddingTop + paddingBottom + (count > 0 ? count * Pitch - spacing : 0f);
                content.sizeDelta = size;
            }

            EnsurePool();

            for (int i = 0; i < boundIndex.Count; i++)
                boundIndex[i] = -1;

            Relayout();
        }

        /// <summary>
        /// 이 번호의 항목이 뷰포트 위에서 <paramref name="rowsFromTop"/> 칸째에 오도록 스크롤한다.
        /// 목록 끝에서는 넘치지 않게 잘린다.
        /// </summary>
        public void ScrollTo(int index, int rowsFromTop)
        {
            if (content == null || scroll == null)
                return;

            scroll.StopMovement();

            float y = paddingTop + Mathf.Max(0, index - rowsFromTop) * Pitch;
            float max = Mathf.Max(0f, content.rect.height - ViewportHeight());

            Vector2 pos = content.anchoredPosition;
            pos.y = Mathf.Clamp(y, 0f, max);
            content.anchoredPosition = pos;

            Relayout();
        }

        private void OnScrolled(Vector2 _)
        {
            Relayout();
        }

        private float ViewportHeight()
        {
            RectTransform viewport = scroll != null && scroll.viewport != null
                ? scroll.viewport
                : transform as RectTransform;
            return viewport != null ? viewport.rect.height : 0f;
        }

        /// <summary>뷰포트를 채우는 데 필요한 수 + 위아래로 한 장씩 여유.</summary>
        private void EnsurePool()
        {
            if (itemTemplate == null || content == null)
                return;

            int needed = Mathf.Min(count, Mathf.CeilToInt(ViewportHeight() / Pitch) + 2);
            while (pool.Count < needed)
            {
                RectTransform item = Instantiate(itemTemplate, content);
                item.anchorMin = new Vector2(0.5f, 1f);
                item.anchorMax = new Vector2(0.5f, 1f);
                item.pivot = new Vector2(0.5f, 1f);
                pool.Add(item);
                boundIndex.Add(-1);
            }
        }

        private void Relayout()
        {
            if (content == null || pool.Count == 0)
                return;

            int first = Mathf.Max(0, Mathf.FloorToInt((content.anchoredPosition.y - paddingTop) / Pitch));

            // 항목 번호 → 슬롯을 "번호 % 풀 크기" 로 고정한다. 한 줄 스크롤하면 맨 위 슬롯
            // 하나만 맨 아래로 옮겨 다시 채우고, 나머지는 번호가 그대로라 손대지 않는다.
            for (int k = 0; k < pool.Count; k++)
            {
                int index = first + k;
                int slot = index % pool.Count;
                RectTransform item = pool[slot];

                if (index >= count)
                {
                    item.gameObject.SetActive(false);
                    boundIndex[slot] = -1;
                    continue;
                }

                item.anchoredPosition = new Vector2(0f, -(paddingTop + index * Pitch));

                if (!item.gameObject.activeSelf)
                    item.gameObject.SetActive(true);

                if (boundIndex[slot] != index)
                {
                    boundIndex[slot] = index;
                    bind?.Invoke(index, item);
                }
            }
        }

        /// <summary>에디터 굽기 전용.</summary>
        public void BakeSetup(ScrollRect bakedScroll, RectTransform bakedContent, RectTransform template,
            float height, float gap, float padTop, float padBottom)
        {
            scroll = bakedScroll;
            content = bakedContent;
            itemTemplate = template;
            itemHeight = height;
            spacing = gap;
            paddingTop = padTop;
            paddingBottom = padBottom;
        }
    }
}
