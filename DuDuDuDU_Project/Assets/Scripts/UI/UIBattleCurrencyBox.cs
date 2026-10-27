using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;

namespace OJ.UI
{
    /// <summary>
    /// 전투 화면의 보유 재화 칸(SP · 마석). 통나무 울타리 아래 · HP 바 위 한 줄에 왼쪽 마석, 오른쪽 SP
    /// (시안은 왼쪽 SP · 오른쪽 마석인데 사용자 지시로 서로 바꿨다).
    /// 시안 <c>Art/Layout/Ingame_NewLayour.png</c> — Merge_Bg x2 276x86 (31,1130)~(307,1216) · (784,1130)~(1060,1216),
    /// 아이콘 + 35pt 숫자. 한때 울타리 위에 두었다가(그 줄을 도우미 줄이 쓰고 있었다) 도우미 줄을 지우고 시안 자리로 옮겼다.
    ///
    /// <b>코드로 만든다.</b> 전투 씬 YAML 을 고치지 않고 붙이기 위해서다(<c>UpgradeRedDot</c> 와 같은 방식).
    /// 재화가 날아와 닿는 자리이기도 해서 <see cref="Rect"/> 를 내놓는다.
    ///
    /// <b>숫자를 쥐고 있을 수 있다.</b> 재화는 지급되는 순간 데이터에 들어가지만, 날아오는 연출이 있는 동안
    /// 화면 숫자가 먼저 오르면 "닿으면 오른다"가 깨진다. <see cref="Hold"/> 로 그만큼 빼 보여 주고
    /// 아이콘이 닿을 때마다 <see cref="Release"/> 로 올린다. 보여 주는 값은 언제나 0 이상이다.
    /// </summary>
    public sealed class UIBattleCurrencyBox : MonoBehaviour
    {
        /// <summary>보이는 판 크기(시안). 그림 Merge_Bg 는 32 안에 7px 여백이 있어 x2 로 +28.</summary>
        private static readonly Vector2 VisibleSize = new Vector2(276f, 86f);

        /// <summary>칸 줄의 가운데 y(화면 1080x1920, 좌상단 원점). 시안 1130~1216.</summary>
        public const float RowCenterY = 1173f;

        /// <summary>칸 가운데 x. 시안 자리 31~307 · 784~1060(좌우 대칭이 아니다)에 SP · 마석을 서로 바꿔 놓는다.</summary>
        public const float SummonPointCenterX = 922f;
        public const float EnhanceStoneCenterX = 169f;

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

        /// <summary>
        /// 부모 캔버스에 칸을 만든다. 글꼴은 <paramref name="fontSource"/> 를 따라간다.
        /// <paramref name="iconScale"/> 은 아이콘 그림(64px) 배율 — 시안 실측 SP 코인 x1.4, 마석 x2.
        /// </summary>
        public static UIBattleCurrencyBox Create(RectTransform parent, string name, float centerX,
            Sprite icon, float iconScale, TMP_Text fontSource)
        {
            GameObject root = UIBountyUIFactory.CreateRect(name, parent);
            var rect = (RectTransform)root.transform;

            // 가로는 비율, 세로는 화면 아래에서의 거리로 잡는다. 전투는 아래 끝을 붙인 채 위쪽 시야를 늘리므로
            // 울타리는 세로가 긴 기기(22:9)에서도 아래에서 같은 거리에 있다 — 위쪽 비율로 잡으면 허공으로 뜬다.
            rect.anchorMin = rect.anchorMax = new Vector2(centerX / 1080f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = VisibleSize;
            rect.anchoredPosition = new Vector2(0f, 1920f - RowCenterY);

            var box = root.AddComponent<UIBattleCurrencyBox>();
            box.home = rect.anchoredPosition;

            // 판 — Merge_Bg 한 겹. 가운데 알파 62% 라 뒤가 비친다 — 반투명으로 보이게 하라는 사용자 지시다
            // (한때 두 겹 86% 로 막았었다).
            Sprite plate = Resources.Load<Sprite>("Art/Ingame/Merge_Bg");
            if (plate == null)
                Debug.LogError("[보유 재화 칸] Resources/Art/Ingame/Merge_Bg 가 없다. 판 없이 숫자만 뜬다.");
            Image back = UIBountyUIFactory.CreateImage("Back", root.transform, Color.white);
            back.sprite = plate;
            back.type = Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = 0.5f;
            UIBountyUIFactory.SetRect(back.rectTransform, VisibleSize + new Vector2(28f, 28f), Vector2.zero);
            back.raycastTarget = false;

            // 아이콘 — 판 왼쪽 끝에서 37 안쪽이 가운데(시안 SP 코인·마석 모두).
            const float iconX = -138f + 37f;
            Image image = UIBountyUIFactory.CreateImage("Icon", root.transform, Color.white);
            UIBountyUIFactory.SetRect(image.rectTransform, new Vector2(64f, 64f) * iconScale, new Vector2(iconX, 0f));
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // 숫자 — 아이콘 오른쪽부터 판 오른쪽 안쪽까지의 가운데. 1,000,000 도 줄지 않고 들어간다.
            const float textLeft = iconX + 34f;
            const float textRight = 138f - 12f;
            box.countText = UIBountyUIFactory.CreateText("Count", root.transform, "0", 35f,
                TextAlignmentOptions.Center, Normal, fontSource != null ? fontSource.font : null,
                fontSource != null ? fontSource.fontSharedMaterial : null);
            UIBountyUIFactory.SetRect(box.countText.rectTransform, new Vector2(textRight - textLeft, 50f),
                new Vector2((textLeft + textRight) * 0.5f, 0f));
            box.countText.textWrappingMode = TextWrappingModes.NoWrap;
            box.countText.enableAutoSizing = true;
            box.countText.fontSizeMin = 22f;
            box.countText.fontSizeMax = 35f;

            return box;
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

        /// <summary>모자랄 때 좌우로 흔든다.</summary>
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
            countText.SetText(value.ToString("N0", CultureInfo.InvariantCulture));
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
