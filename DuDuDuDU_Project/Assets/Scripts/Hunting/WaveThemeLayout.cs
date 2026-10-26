using UnityEngine;

namespace OJ.Hunting
{
    /// <summary>
    /// 웨이브 테마의 하단 UI 자리 잡기. 씬·프리팹을 고치지 않고 런타임에 옮긴다(AGENTS 절대 규칙 3).
    /// </summary>
    public static class WaveThemeLayout
    {
        /// <summary>
        /// 강화 버튼: 왼쪽 세로 막대(보드 옆) → 왼쪽 아래 가로 버튼. 현상금·영혼 보너스 버튼이 있던 자리다.
        /// 그 자리가 비면서 보드가 한 줄 8칸을 다 쓴다.
        /// </summary>
        public static void MoveUpgradeButtonToBottomLeft(RectTransform upgrade)
        {
            if (upgrade == null)
                return;

            upgrade.anchorMin = upgrade.anchorMax = Vector2.zero;
            upgrade.pivot = Vector2.zero;
            upgrade.anchoredPosition = new Vector2(20f, 24f);
            upgrade.sizeDelta = new Vector2(340f, 150f);

            // 세로 막대에 맞춰 놓였던 안쪽 요소들을 가로 버튼에 맞게 다시 놓는다: 위에 "강화", 아래 줄에 마석 가격과 효과.
            int imageIndex = 0;
            for (int i = 0; i < upgrade.childCount; i++)
            {
                var child = upgrade.GetChild(i) as RectTransform;
                if (child == null)
                    continue;

                switch (child.name)
                {
                    case "SummoText":
                        Place(child, new Vector2(0f, 28f), new Vector2(320f, 80f));
                        break;
                    case "Text (TMP)":
                        Place(child, new Vector2(-6f, -36f), new Vector2(110f, 56f));
                        break;
                    case "Bonus":
                        Place(child, new Vector2(100f, -36f), new Vector2(150f, 56f));
                        break;
                    case "Image":
                        // 첫 Image 는 배경(늘어나 있다), 둘째가 마석 아이콘이다.
                        if (imageIndex++ > 0)
                            Place(child, new Vector2(-82f, -36f), new Vector2(56f, 56f));
                        break;
                }
            }
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>확률업 버튼을 키운다(작아서 잘 안 보인다는 평). 자리는 오른쪽 아래 그대로이고 오른쪽 아래 모서리를 기준으로 커진다.</summary>
        public static void EnlargeProbabilityButton(RectTransform button)
        {
            if (button == null)
                return;

            // 오른쪽 아래 모서리가 화면 밖으로 나가지 않게 키운 만큼 안쪽으로 당긴다(크기 230 → 약 300).
            const float scale = 1.3f;
            button.localScale = Vector3.one * scale;
            button.anchoredPosition = new Vector2(-(20f + button.sizeDelta.x * scale * 0.5f), button.anchoredPosition.y + 10f);
        }
    }
}
