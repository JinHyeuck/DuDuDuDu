using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;

namespace OJ.Element
{
    /// <summary>
    /// 보유 마석 수를 따로 보여 주는 작은 칸. 보드 오른쪽 아래 빈 자리(확률UP 버튼 위)에 뜬다.
    ///
    /// <b>코드로 만든다.</b> 전투 씬 YAML 을 고치지 않고 붙이기 위해서다(<c>UpgradeRedDot</c> 와 같은 방식).
    /// 마석이 날아와 닿는 자리이기도 해서 <see cref="Rect"/> 를 내놓는다.
    ///
    /// <b>숫자를 쥐고 있을 수 있다.</b> 마석은 지급되는 순간 데이터에 들어가지만, 날아오는 연출이 있는 동안
    /// 화면 숫자가 먼저 오르면 "닿으면 오른다"가 깨진다. <see cref="Hold"/> 로 그만큼 빼 보여 주고
    /// 아이콘이 닿을 때마다 <see cref="Release"/> 로 올린다. 보여 주는 값은 언제나 0 이상이다.
    /// </summary>
    public sealed class UIEnhanceStoneChip : MonoBehaviour
    {
        private static readonly Vector2 Size = new Vector2(176f, 36f);

        // 화면(1080x1920, 좌상단 원점) 기준 가운데 점. 보드 아래 테두리와 확률UP 버튼 사이, 오른쪽 끝의 빈 자리다.
        private static readonly Vector2 CenterOnScreen = new Vector2(975f, 1756f);

        private const float ShakeTime = 0.3f;
        private const float ShakeAmplitude = 9f;

        private static readonly Color Normal = Color.white;
        private static readonly Color Short = new Color(1f, 0.35f, 0.3f, 1f);

        private TMP_Text countText;
        private int owned;
        private int held;
        private int shown = -1;
        private float shakeElapsed = ShakeTime;
        private Vector2 home;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>부모 캔버스에 칸을 만든다. 글꼴은 <paramref name="fontSource"/> 를 따라간다.</summary>
        public static UIEnhanceStoneChip Create(RectTransform parent, TMP_Text fontSource, Sprite icon)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIEnhanceStoneChip", parent);
            var rect = (RectTransform)root.transform;

            // 앵커를 비율로 잡는다. 캔버스 단위가 화면 픽셀과 달라도 같은 자리에 선다.
            Vector2 anchor = new Vector2(CenterOnScreen.x / 1080f, 1f - CenterOnScreen.y / 1920f);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Size;
            rect.anchoredPosition = Vector2.zero;

            var chip = root.AddComponent<UIEnhanceStoneChip>();
            chip.home = rect.anchoredPosition;

            Image back = UIBountyUIFactory.CreateImage("Back", root.transform, new Color(0.17f, 0.14f, 0.32f, 0.96f));
            UIBountyUIFactory.SetRect(back.rectTransform, Size, Vector2.zero);
            back.raycastTarget = false;
            var edge = back.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0.62f, 0.55f, 0.95f, 1f);
            edge.effectDistance = new Vector2(2f, -2f);

            Image stone = UIBountyUIFactory.CreateImage("Icon", root.transform, Color.white);
            UIBountyUIFactory.SetRect(stone.rectTransform, new Vector2(52f, 52f), new Vector2(-Size.x * 0.5f + 24f, 0f));
            stone.sprite = icon;
            stone.preserveAspect = true;
            stone.raycastTarget = false;

            TMP_Text name = UIBountyUIFactory.CreateText("Name", root.transform, "마석", 22f,
                TextAlignmentOptions.Left, new Color(0.8f, 0.85f, 1f, 1f), fontSource.font, fontSource.fontSharedMaterial);
            UIBountyUIFactory.SetRect(name.rectTransform, new Vector2(60f, 30f), new Vector2(-Size.x * 0.5f + 70f, 0f));
            name.raycastTarget = false;

            chip.countText = UIBountyUIFactory.CreateText("Count", root.transform, "0", 28f,
                TextAlignmentOptions.Right, Normal, fontSource.font, fontSource.fontSharedMaterial);
            UIBountyUIFactory.SetRect(chip.countText.rectTransform, new Vector2(84f, 32f), new Vector2(Size.x * 0.5f - 52f, 0f));
            chip.countText.raycastTarget = false;
            chip.countText.enableAutoSizing = true;
            chip.countText.fontSizeMin = 18f;
            chip.countText.fontSizeMax = 28f;

            return chip;
        }

        /// <summary>실제 보유 수를 알린다. 쥐고 있는 만큼은 빼서 보여 준다.</summary>
        public void SetOwned(int value)
        {
            owned = Mathf.Max(0, value);
            Redraw();
        }

        /// <summary>이만큼은 아직 화면 숫자에 넣지 않는다(날아오는 중).</summary>
        public void Hold(int amount)
        {
            if (amount <= 0)
                return;

            held += amount;
            Redraw();
        }

        /// <summary>쥐고 있던 것 중 이만큼이 닿았다.</summary>
        public void Release(int amount)
        {
            held = Mathf.Max(0, held - Mathf.Max(0, amount));
            Redraw();
        }

        /// <summary>쥐고 있던 것을 전부 푼다. 판이 새로 시작될 때 부른다.</summary>
        public void ReleaseAll()
        {
            held = 0;
            Redraw();
        }

        /// <summary>마석이 모자랄 때 좌우로 흔든다.</summary>
        public void Shake()
        {
            shakeElapsed = 0f;
        }

        private void Redraw()
        {
            int value = Mathf.Max(0, owned - held);
            if (value == shown)
                return;

            shown = value;
            countText.SetText("{0}", value);
        }

        private void Update()
        {
            if (shakeElapsed >= ShakeTime)
                return;

            shakeElapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(shakeElapsed / ShakeTime);
            float offset = k >= 1f ? 0f : Mathf.Sin(k * Mathf.PI * 6f) * (1f - k) * ShakeAmplitude;
            ((RectTransform)transform).anchoredPosition = home + new Vector2(offset, 0f);

            // 흔들리는 동안 숫자가 붉다.
            countText.color = k >= 1f ? Normal : Short;
        }
    }
}
