using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Bounty
{
    /// <summary>
    /// 코드로 UI 를 굽는 세 클래스(<see cref="UIBountySelectDialog"/>·<see cref="UIBountySlot"/>·
    /// <see cref="UIBountyBanner"/>)가 함께 쓰는 조립 도구.
    ///
    /// <b>왜 따로 뺐나.</b> 기존 <c>UIBattleDiceDetailPanel</c> 은 같은 헬퍼 다섯 개를
    /// 자기 파일 안에 private static 으로 들고 있는데, 현상금은 파일이 셋이라 그대로 하면
    /// <b>같은 함수가 세 벌</b>이 된다. 셋이 조금씩 어긋나는 순간 칸마다 여백이 다른
    /// 화면이 나오고, 그 원인은 눈으로 못 찾는다.
    ///
    /// <c>UIBattleDiceDetailPanel</c> 쪽을 여기로 끌어오지는 않았다 — 그 파일은 이번
    /// 작업의 대상이 아니고, 건드리면 이미 구운 프리팹과 대조할 것이 늘어난다.
    ///
    /// <b>런타임에 쓰지 않는다.</b> 전부 에디터 굽기 경로에서만 불린다.
    /// </summary>
    internal static class UIBountyUIFactory
    {
        internal const int UILayer = 5;

        internal static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UILayer;
            go.transform.SetParent(parent, false);
            return go;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static TMP_Text CreateText(
            string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.font = font;

            // 글자는 클릭을 먹지 않는다. 켜 두면 칸 안의 이름·체력 글자가 그 칸의
            // 버튼보다 위에 있어서 <b>글자 위를 누르면 선택이 안 되는</b> 자리가 생긴다.
            label.raycastTarget = false;
            return label;
        }

        internal static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
