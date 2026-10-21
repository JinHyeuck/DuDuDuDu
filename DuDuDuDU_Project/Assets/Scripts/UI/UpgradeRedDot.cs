using UnityEngine;
using UnityEngine.UI;

namespace OJ.UI
{
    /// <summary>
    /// "강화할 수 있다" 레드닷. <b>코드로 만든다</b> — 프리팹·씬 YAML 을 고치지 않고 칸·탭 어디에나 붙이기 위해서다
    /// (<c>LobbyBottomBtn</c> 의 자물쇠와 같은 방식). 빨간 원 + 흰 느낌표, 숨 쉬듯 커졌다 작아져서 눈에 띈다.
    /// 그림은 런타임에 한 번 만든다 — 스프라이트 에셋을 새로 들이지 않는다.
    /// </summary>
    public static class UpgradeRedDot
    {
        private static Sprite circle;

        /// <summary>
        /// 부모의 오른쪽 위 모서리에 레드닷을 만든다. <paramref name="size"/> 는 지름(px, 캔버스 기준).
        /// 레이캐스트를 받지 않으므로 아래 버튼 클릭을 막지 않는다.
        /// </summary>
        public static GameObject Create(Transform parent, float size = 46f, Vector2? offset = null)
        {
            var go = new GameObject("UpgradeRedDot", typeof(RectTransform), typeof(Image), typeof(UpgradeRedDotPulse));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = offset ?? new Vector2(-size * 0.35f, -size * 0.35f);

            var image = go.GetComponent<Image>();
            image.sprite = GetCircle();
            image.color = new Color(0.95f, 0.12f, 0.12f, 1f);
            image.raycastTarget = false;

            // 흰 느낌표: 가는 막대 + 점. 글꼴에 기대지 않는다(폰트 에셋이 없는 화면에서도 보이게).
            CreateBar(rect, new Vector2(0f, size * 0.08f), new Vector2(size * 0.14f, size * 0.38f));
            CreateBar(rect, new Vector2(0f, -size * 0.25f), new Vector2(size * 0.16f, size * 0.16f));

            go.SetActive(false);
            return go;
        }

        /// <summary>필드에 없으면 만들고, 켜짐 상태를 맞춘다.</summary>
        public static void Set(ref GameObject dot, Transform parent, bool on, float size = 46f, Vector2? offset = null)
        {
            if (dot == null)
            {
                if (!on || parent == null)
                    return;

                dot = Create(parent, size, offset);
            }

            if (dot.activeSelf != on)
                dot.SetActive(on);
        }

        private static void CreateBar(RectTransform parent, Vector2 position, Vector2 size)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)bar.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = bar.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static Sprite GetCircle()
        {
            if (circle != null)
                return circle;

            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - n * 0.5f + 0.5f) * (x - n * 0.5f + 0.5f) + (y - n * 0.5f + 0.5f) * (y - n * 0.5f + 0.5f));
                    float a = Mathf.Clamp01(r - d + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }
    }

    /// <summary>레드닷이 숨 쉬듯 커졌다 작아진다. 배속·일시정지와 무관하게 실제 시간으로 돈다.</summary>
    public sealed class UpgradeRedDotPulse : MonoBehaviour
    {
        private const float Period = 0.9f;
        private const float Amplitude = 0.14f;

        private void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / Period)) * Amplitude;
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
