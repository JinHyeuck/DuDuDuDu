using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.EditorTools
{
    /// <summary>
    /// 시안(PSD) 좌표를 uGUI 로 옮기는 굽기 도구 모음. 가방·옆 메뉴·로비 개편이 함께 쓴다.
    ///
    /// 좌표는 전부 <b>시안 좌표</b>(1080x1920, 좌상단 원점, 아래가 +)로 적고 <see cref="Pos"/> 로 화면 중앙
    /// 기준 anchoredPosition 으로 옮긴다. 만든 오브젝트는 앵커·피벗이 모두 중앙이라 크기를 바꿔도 중심이 안 움직인다.
    ///
    /// 킷 그림은 투명 여백이 있어 시안에서 잰 <b>보이는 크기</b>로 Image 크기를 거꾸로 구해야 한다.
    /// 그 계산이 <see cref="Art"/> 와 <see cref="PlaceVisible"/> 이다 (Tools/ui/PORTING.md).
    /// </summary>
    internal static class UiBakeKit
    {
        public const int UILayer = 5;
        public const string ArtRoot = "Assets/Resources/Art/";
        public const string FontPath = "Assets/BMHANNAProOTF/BMHANNAProOTF SDF.asset";

        /// <summary>시안의 킷 9슬라이스 판은 전부 x4 로 그려져 있다.</summary>
        public const float PixelScale = 4f;

        public static readonly Color Backdrop = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color InnerBoxColor = Hex(0x636481);

        // ──────────────────────────────────────────────── 좌표·색

        public static Vector2 Pos(float designX, float designY)
        {
            return new Vector2(designX - 540f, 960f - designY);
        }

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>
        /// Ui_Popup_Bg(여백 3px, x4) 의 보이는 사각형 → Image 크기·위치.
        /// 사방 여백이 같아 중심은 그대로고 크기만 24 커진다.
        /// </summary>
        public static void PopupBgRect(float x, float y, float w, float h, out Vector2 size, out Vector2 position)
        {
            size = new Vector2(w + 24f, h + 24f);
            position = Pos(x + w * 0.5f, y + h * 0.5f);
        }

        /// <summary>
        /// Ui_Popup_SmallBox(여백 L6 R7 T6 B7, x4) 의 보이는 사각형 → Image 크기·위치.
        /// 여백이 오른쪽·아래가 1px 더 커서 중심이 시안 쪽으로 (2, 2) 밀린다.
        /// </summary>
        public static void SmallBoxRect(float x, float y, float w, float h, out Vector2 size, out Vector2 position)
        {
            size = new Vector2(w + 52f, h + 52f);
            position = Pos(x + w * 0.5f + 2f, y + h * 0.5f + 2f);
        }

        // ──────────────────────────────────────────────── 스프라이트

        /// <summary>스프라이트 하나와 그 <b>보이는 사각형</b>(알파 8 초과). y 는 위가 0 이다.</summary>
        public sealed class Art
        {
            public Sprite sprite;
            public int width, height;
            public int x0, x1, yTop0, yTop1;

            public float VisW => x1 - x0;
            public float VisH => yTop1 - yTop0;
            public float VisCx => (x0 + x1) * 0.5f;
            public float VisCy => (yTop0 + yTop1) * 0.5f;
        }

        private static readonly Dictionary<string, Art> ArtCache = new Dictionary<string, Art>();

        public static void ClearCache() => ArtCache.Clear();

        /// <summary><b>없으면 멈춘다.</b> null 로 구우면 흰 사각형만 가득한 프리팹이 조용히 저장된다.</summary>
        public static Sprite LoadSprite(string pathUnderArt)
        {
            string path = ArtRoot + pathUnderArt + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new System.InvalidOperationException(
                    "[굽기] 스프라이트가 없다(파일이 없거나 Sprite 로 임포트되지 않았다): " + path);
            }

            return sprite;
        }

        public static Art LoadArt(string pathUnderArt)
        {
            if (ArtCache.TryGetValue(pathUnderArt, out Art cached))
                return cached;

            Art art = Measure(LoadSprite(pathUnderArt));
            ArtCache[pathUnderArt] = art;
            return art;
        }

        public static Art Measure(Sprite sprite)
        {
            string assetPath = AssetDatabase.GetAssetPath(sprite);
            string path = System.IO.Path.GetFullPath(assetPath);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(path));
                int w = texture.width;
                int h = texture.height;
                if ((int)sprite.rect.width != w || (int)sprite.rect.height != h)
                {
                    throw new System.InvalidOperationException(
                        "[굽기] 스프라이트 크기가 파일과 다르다(임포트 최대 크기에 눌렸는지 확인): " + assetPath);
                }

                Color32[] pixels = texture.GetPixels32();
                int minX = w, maxX = -1, minRow = h, maxRow = -1;
                for (int row = 0; row < h; row++)
                {
                    for (int col = 0; col < w; col++)
                    {
                        if (pixels[row * w + col].a <= 8)
                            continue;

                        if (col < minX) minX = col;
                        if (col > maxX) maxX = col;
                        if (row < minRow) minRow = row;
                        if (row > maxRow) maxRow = row;
                    }
                }

                if (maxX < 0)
                    throw new System.InvalidOperationException("[굽기] 보이는 픽셀이 없다: " + assetPath);

                return new Art
                {
                    sprite = sprite, width = w, height = h,
                    x0 = minX, x1 = maxX + 1,
                    yTop0 = h - 1 - maxRow, yTop1 = h - minRow,
                };
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public static TMP_FontAsset LoadFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogError("[굽기] BM HANNA 폰트를 못 찾았다: " + FontPath + " — 굽지 않는다.");

            return font;
        }

        // ──────────────────────────────────────────────── 조립

        public static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UILayer };
            if (parent != null)
                go.transform.SetParent(parent, false);

            return go;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            Image image = NewRect(name, parent).AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>글자. 한 줄이고 넘쳐도 줄바꿈하지 않는다. 폰트 재질이 시안의 검은 외곽선을 이미 갖고 있다.</summary>
        public static TMP_Text NewText(
            string name, Transform parent, string text, float size, Color color, TMP_FontAsset font,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var label = NewRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.font = font;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        public static void AutoSize(TMP_Text label, float min, float max)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = min;
            label.fontSizeMax = max;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>앵커·피벗 중앙, 크기와 중심 위치(부모 중심 기준)를 준다.</summary>
        public static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>
        /// 그림의 보이는 사각형이 부모 중심 기준 (cx, cyUp) 에 visW x visH 로 보이게 놓는다.
        /// 좌표는 화면 px, 위가 + 다. 가로세로 배율이 달라도 맞춘다.
        /// </summary>
        public static Image PlaceVisible(
            Transform parent, string name, Art art, float cx, float cyUp, float visW, float visH)
        {
            Image image = NewImage(name, parent, Color.white);
            image.sprite = art.sprite;
            image.raycastTarget = false;

            float sx = visW / art.VisW;
            float sy = visH / art.VisH;
            float w = art.width * sx;
            float h = art.height * sy;
            SetRect(image.rectTransform, new Vector2(w, h),
                    new Vector2(cx + (w * 0.5f - art.VisCx * sx), cyUp - (h * 0.5f - art.VisCy * sy)));
            return image;
        }

        /// <summary>시안 좌표의 보이는 사각형 (x, y, w, h) 에 그림을 놓는다. 부모는 화면 중앙에 앉은 것으로 본다.</summary>
        public static Image PlaceDesign(
            Transform parent, string name, Art art, float x, float y, float w, float h, Vector2 parentOffset)
        {
            Vector2 center = Pos(x + w * 0.5f, y + h * 0.5f) - parentOffset;
            return PlaceVisible(parent, name, art, center.x, center.y, w, h);
        }

        /// <summary>9슬라이스 판. 테두리는 스프라이트 .meta 의 것을 쓰고 여기서는 배율만 준다.</summary>
        public static Image Sliced(
            Transform parent, string name, string pathUnderArt, Vector2 size, Vector2 position, Color color,
            float pixelScale = PixelScale)
        {
            Image image = NewImage(name, parent, color);
            image.sprite = LoadSprite(pathUnderArt);
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f / pixelScale;
            image.raycastTarget = false;
            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>
        /// 시안 좌표의 <b>보이는 사각형</b> (x, y, w, h) 에 9슬라이스 그림을 놓는다.
        /// 투명 여백도 테두리의 일부라 x4 로 늘어난다 — 그만큼 Image 가 커지고 중심이 한쪽으로 밀린다.
        /// <paramref name="parentOffset"/> 은 부모 중심이 화면 중앙에서 벗어난 만큼(디자인 좌표계, 위가 +)이다.
        /// </summary>
        public static Image SlicedVisible(
            Transform parent, string name, string pathUnderArt,
            float x, float y, float w, float h, Color color, Vector2 parentOffset, float pixelScale = PixelScale)
        {
            Art art = LoadArt(pathUnderArt);
            float left = art.x0, right = art.width - art.x1;
            float top = art.yTop0, bottom = art.height - art.yTop1;

            Vector2 size = new Vector2(w + (left + right) * pixelScale, h + (top + bottom) * pixelScale);
            Vector2 center = Pos(x + w * 0.5f + (right - left) * pixelScale * 0.5f,
                                 y + h * 0.5f + (bottom - top) * pixelScale * 0.5f) - parentOffset;
            return Sliced(parent, name, pathUnderArt, size, center, color, pixelScale);
        }

        /// <summary>
        /// 스프라이트의 9슬라이스 테두리를 맞춘다(x=왼 y=아래 z=오른 w=위, 투명 여백 포함).
        /// 새로 들여온 그림은 테두리가 0 이라 늘리면 통째로 퍼진다. 같으면 건드리지 않는다.
        /// </summary>
        public static void EnsureSliceBorder(string pathUnderArt, Vector4 border)
        {
            string path = ArtRoot + pathUnderArt + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new System.InvalidOperationException("[굽기] 임포터를 못 찾았다: " + path);

            if (importer.spriteBorder == border)
                return;

            importer.spriteBorder = border;
            importer.SaveAndReimport();
            ArtCache.Remove(pathUnderArt);
        }

        /// <summary>그림 버튼. 꺼졌을 때 어둡게 덮지 않는다 — 글자까지 같이 어두워져 안 읽힌다.</summary>
        public static Button SpriteButton(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            return button;
        }

        /// <summary>시안 좌표 (x, y, w, h) 를 칠한 사각형. 부모는 화면 중앙에 앉은 것으로 본다.</summary>
        public static Image FlatRect(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            Image image = NewImage(name, parent, color);
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(w, h), Pos(x + w * 0.5f, y + h * 0.5f));
            return image;
        }

        /// <summary>바깥을 눌러도 닫히는 딤. 닫기 버튼 하나뿐이면 누를 곳을 못 찾는 사람이 생긴다.</summary>
        public static Image AddBackdrop(OJ.UI.DialogBase dialog, Transform parent, Color color)
        {
            Image backdrop = NewImage("Backdrop", parent, color);
            Stretch(backdrop.rectTransform);

            var button = backdrop.gameObject.AddComponent<Button>();
            button.targetGraphic = backdrop;
            button.transition = Selectable.Transition.None;
            dialog.AddExitButton(button);
            return backdrop;
        }

        // ──────────────────────────────────────────────── 직렬화·저장

        public static void SetRef(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                // 필드 이름이 바뀌면 배선이 조용히 빠진다. 그 사고를 지금 말한다.
                Debug.LogError("[굽기] 배선할 필드를 못 찾았다: " + so.targetObject.GetType().Name +
                               "." + field + " — 이름이 바뀌었는지 확인할 것.");
                return;
            }

            property.objectReferenceValue = value;
        }

        /// <summary>
        /// 루트를 프리팹으로 저장하고 지운다. 같은 경로에 저장하면 GUID 가 유지되어
        /// 다이얼로그 카탈로그를 다시 훑지 않아도 된다.
        /// </summary>
        public static bool SavePrefab(GameObject root, string path)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok)
                    Debug.LogError("[굽기] 프리팹 저장에 실패했다: " + path);

                return ok;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
