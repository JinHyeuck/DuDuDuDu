using System;
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
            TextAlignmentOptions align, Color color, TMP_FontAsset font, Material material = null)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.font = font;

            // 기본 머티리얼은 검은 외곽선이 있다. 시안에서 색 글자(갈색·회갈색)는 외곽선이
            // 없으므로 그때만 외곽선 없는 머티리얼로 바꾼다.
            if (material != null)
                label.fontSharedMaterial = material;

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

        // ── 아트 시안 (Art/Layout/Wanted_Layout*.png · Warning_Layout*.png) ──────
        //
        // 좌표는 <b>시안 픽셀(좌상단 원점, 1080x1920) 그대로</b> 적고 <see cref="Pos"/> 가
        // 중앙 기준으로 바꾼다. 옮겨 적을 때 뺄셈을 손으로 하면 그 자리에서 오차가 난다.
        // 탑(<c>UITowerUIFactory</c>)과 같은 방식이지만 그 파일을 끌어다 쓰지 않는다 —
        // 현상금이 탑에 의존할 이유가 없다.

        internal static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>시안 좌표 → 화면 중앙 기준 anchoredPosition.</summary>
        internal static Vector2 Pos(float designX, float designY)
        {
            return new Vector2(designX - 540f, 960f - designY);
        }

        /// <summary>
        /// 시안 좌표 → <paramref name="parentDesignCenter"/> 에 놓인 부모 안의 상대 좌표.
        /// 카드 안의 글자처럼 부모가 움직이는 것들에 쓴다.
        /// </summary>
        internal static Vector2 Local(float designX, float designY, Vector2 parentDesignCenter)
        {
            return new Vector2(designX - parentDesignCenter.x, parentDesignCenter.y - designY);
        }

        /// <summary>
        /// <c>Resources/Art/</c> 아래 스프라이트. <b>없으면 멈춘다</b> — null 스프라이트로 구우면
        /// 흰 사각형만 가득한 프리팹이 조용히 저장된다.
        /// </summary>
        internal static Sprite LoadSprite(string pathUnderArt)
        {
            Sprite sprite = Resources.Load<Sprite>("Art/" + pathUnderArt);
            if (sprite == null)
                throw new InvalidOperationException("[현상금 굽기] 스프라이트가 없다: Resources/Art/" + pathUnderArt);
            return sprite;
        }

        /// <summary>
        /// 스프라이트 이미지. <paramref name="pixelScale"/> 은 시안의 "x4배" 다.
        /// 9슬라이스면 테두리가 그 배율로 그려진다(<c>pixelsPerUnitMultiplier = 1/배율</c>).
        /// </summary>
        internal static Image CreateSprite(
            string name, Transform parent, Sprite sprite, float pixelScale,
            Vector2 size, Vector2 position, bool sliced)
        {
            Image image = CreateImage(name, parent, Color.white);
            image.sprite = sprite;
            image.raycastTarget = false;

            if (sliced)
            {
                image.type = Image.Type.Sliced;
                image.fillCenter = true;
                image.pixelsPerUnitMultiplier = 1f / pixelScale;
            }

            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>
        /// 가로만 부모 전체로 늘린다. 경고 띠처럼 화면 끝까지 닿아야 하는 것에 쓴다 —
        /// 1080 으로 고정하면 폭이 넓은 기기에서 양 끝이 빈다.
        /// </summary>
        internal static void StretchHorizontal(RectTransform rect, float height, float y)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        // ── 킷 스프라이트 투명 여백 ─────────────────────────────────────────
        //
        // 시안에서 잰 것은 <b>보이는 외곽선</b>이다. Image 크기 = 보이는 크기 + 여백 x 배율.

        /// <summary>
        /// Wanted_Bg(128²) 의 보이는 사각형 → Image 크기. 여백 좌우 24 · 위아래 34 px 이다.
        /// </summary>
        internal static Vector2 WantedBgRect(float visibleWidth, float visibleHeight, float pixelScale)
        {
            return new Vector2(visibleWidth + 48f * pixelScale, visibleHeight + 68f * pixelScale);
        }

        /// <summary>
        /// Big_Btn_Yellow 같은 킷 버튼(32²)의 보이는 사각형 → Image 크기·중심.
        /// 여백이 좌우 4 · 위 6 · 아래 2 px 이라 중심이 (6-2)/2 x 배율 만큼 위로 간다.
        /// </summary>
        internal static void ButtonRect(
            float visibleCenterX, float visibleCenterY, float visibleWidth, float visibleHeight, float pixelScale,
            out Vector2 size, out Vector2 position)
        {
            size = new Vector2(visibleWidth + 8f * pixelScale, visibleHeight + 8f * pixelScale);
            position = Pos(visibleCenterX, visibleCenterY - 2f * pixelScale);
        }

        /// <summary>
        /// 빨간 X 표시("사냥 안 하기"). 킷에 X 아이콘이 없어서 막대 둘을 엇갈려 그린다.
        /// 글자 "X" 로 쓰지 않는 이유 — 픽셀 그림 옆에서 글꼴 X 는 혼자 매끈해 튄다.
        /// </summary>
        internal static void CreateCross(Transform parent, Vector2 position, float length, float thickness)
        {
            GameObject root = CreateRect("Cross", parent);
            SetRect(root.GetComponent<RectTransform>(), new Vector2(length, length), position);

            // 테두리 두 개를 먼저, 빨강 두 개를 나중에 — 안 그러면 두 번째 막대의 검은
            // 테두리가 첫 번째 빨강 위를 가로지른다.
            for (int pass = 0; pass < 2; pass++)
            {
                bool outline = pass == 0;
                for (int i = 0; i < 2; i++)
                {
                    Image bar = CreateImage(outline ? "Outline" + i : "Bar" + i, root.transform,
                        outline ? Color.black : Hex(0xD30027));
                    bar.raycastTarget = false;
                    float pad = outline ? 8f : 0f;
                    SetRect(bar.rectTransform, new Vector2(length + pad, thickness + pad), Vector2.zero);
                    bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                }
            }
        }
    }
}
