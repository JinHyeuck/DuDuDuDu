using System.Collections.Generic;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OJ.Synergy
{
    /// <summary>
    /// 전투 화면 오른쪽의 시너지 슬롯 줄. 사용자 시안(2026-10-28): 원 안에 <c>n/N</c>, 버프가 걸리면 원 아래에
    /// 합산 수치, 단일 타격은 대상까지 직선, 통합계가 다른 계열을 움직이면 곡선.
    ///
    /// <b>코드로 만든다.</b> 전투 씬 YAML 을 고치지 않고 붙이기 위해서다(<c>UIBattleCurrencyBox</c> 와 같은 방식).
    /// 전투 HUD 캔버스의 뿌리에 붙어 그 캔버스의 배율을 그대로 따른다. 연출은 코루틴 없이 Update 로 돈다.
    /// 안 열린 계열은 아예 만들지 않는다(사용자 결정).
    /// </summary>
    public sealed class UISynergyHud : MonoBehaviour
    {
        // 시안 실측(1080x1920, 위에서의 거리). 원 가운데 x 973, 첫 원 y 492, 간격 140, 지름 약 105.
        private const float SlotCenterFromRight = 107f;
        private const float FirstSlotTop = 492f;
        private const float SlotDiameter = 104f;
        private const float SlotGap = 36f;
        private const float BuffLineHeight = 36f;
        private const float BuffLineWidth = 132f;

        private const float FlashSeconds = 0.3f;
        private const float LineSeconds = 0.35f;
        private const float DetailSeconds = 4f;

        private static readonly Color PlateColor = new Color(0.08f, 0.07f, 0.12f, 0.82f);
        private static readonly Color BuffBackColor = new Color(0f, 0f, 0f, 0.6f);

        private sealed class SlotView
        {
            public SynergyCategory Category;
            public RectTransform Root;
            public Image Ring;
            public TMP_Text Count;
            public readonly List<Image> BuffBacks = new List<Image>();
            public readonly List<TMP_Text> BuffTexts = new List<TMP_Text>();
            public float FlashLeft;
        }

        private sealed class LineFx
        {
            public readonly List<Image> Segments = new List<Image>();
            public float Left;
        }

        private SynergyRunManager run;
        private RectTransform root;

        /// <summary>HUD 캔버스의 카메라. 전투 HUD 는 ScreenSpaceCamera 라 화면 좌표를 바꿀 때 이것을 넘겨야 한다(Overlay 면 null).</summary>
        private Camera canvasCamera;
        private readonly SlotView[] views = new SlotView[SynergyRules.CategoryCount];
        private readonly List<LineFx> lines = new List<LineFx>();

        private RectTransform detail;
        private TMP_Text detailText;
        private SynergyCategory detailCategory;
        private float detailLeft;

        /// <summary>전투 HUD 캔버스에 슬롯 줄을 만든다. 캔버스를 못 찾으면 만들지 않는다(전투는 그대로 돈다).</summary>
        public static UISynergyHud Create(SynergyRunManager run)
        {
            IBattleRefs battle = GameContainer.Battle;
            // 일시정지 버튼은 전투 HUD 캔버스(UI/Canvas) 안에 있다. 소환 시스템 컴포넌트 자체는 캔버스 밖에 붙어 있어 못 쓴다.
            Canvas canvas = battle != null && battle.Game != null && battle.Game.Pause != null
                ? battle.Game.Pause.GetComponentInParent<Canvas>(true)
                : null;
            if (canvas == null)
            {
                Debug.LogError("[시너지] 전투 HUD 캔버스를 못 찾아 슬롯 줄을 만들지 못했다. 시너지 효과는 그대로 돈다.");
                return null;
            }

            var go = new GameObject("UISynergyHud", typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.rootCanvas.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var hud = go.AddComponent<UISynergyHud>();
            Canvas rootCanvas = canvas.rootCanvas;
            hud.canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            hud.Build(run, rect);
            return hud;
        }

        private void Build(SynergyRunManager owner, RectTransform rootRect)
        {
            run = owner;
            root = rootRect;

            float top = FirstSlotTop - SlotDiameter * 0.5f;
            for (int c = 0; c < SynergyRules.CategoryCount; c++)
            {
                SynergyRunManager.Slot slot = run.GetSlot((SynergyCategory)c);
                if (slot == null)
                    continue;

                int buffLines = CountBuffLines(slot);
                views[c] = CreateSlot(slot, top);
                top += SlotDiameter + buffLines * (BuffLineHeight + 4f) + SlotGap;
            }

            BuildDetail();

            run.OnStateChanged += Refresh;
            run.OnFired += OnFired;
            run.OnStrike += OnStrike;
            run.OnUnifiedLink += OnUnifiedLink;
            Refresh();
        }

        private void OnDestroy()
        {
            if (run == null)
                return;

            run.OnStateChanged -= Refresh;
            run.OnFired -= OnFired;
            run.OnStrike -= OnStrike;
            run.OnUnifiedLink -= OnUnifiedLink;
        }

        // ── 만들기 ─────────────────────────────────────────────

        /// <summary>그 슬롯이 버프 줄을 몇 줄 쓰는가. 켜졌는지와 무관하게 <b>장착한 버프 종류 수</b>로 자리를 잡는다 — 켜질 때마다 아래가 밀리면 읽을 수 없다.</summary>
        private static int CountBuffLines(SynergyRunManager.Slot slot)
        {
            var kinds = new HashSet<string>();
            foreach (SynergyRunManager.Buff buff in slot.Buffs)
            {
                if (buff != null)
                    kinds.Add(BuffKindKey(buff.Effect));
            }

            return kinds.Count;
        }

        private SlotView CreateSlot(SynergyRunManager.Slot slot, float top)
        {
            var view = new SlotView { Category = slot.Category };

            GameObject go = NewRect("Slot_" + slot.Category, root);
            view.Root = (RectTransform)go.transform;
            view.Root.anchorMin = view.Root.anchorMax = new Vector2(1f, 1f);
            view.Root.pivot = new Vector2(0.5f, 1f);
            view.Root.sizeDelta = new Vector2(SlotDiameter, SlotDiameter);
            view.Root.anchoredPosition = new Vector2(-SlotCenterFromRight, -top);

            Image plate = NewImage("Plate", view.Root, Circle, PlateColor, new Vector2(SlotDiameter, SlotDiameter), Vector2.zero);
            plate.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.None;
            SynergyCategory category = slot.Category;
            button.onClick.AddListener(() => ToggleDetail(category));

            Image track = NewImage("Track", view.Root, Ring, new Color(1f, 1f, 1f, 0.15f), new Vector2(SlotDiameter, SlotDiameter), Vector2.zero);
            track.raycastTarget = false;

            view.Ring = NewImage("Ring", view.Root, Ring, CategoryColor(slot.Category), new Vector2(SlotDiameter, SlotDiameter), Vector2.zero);
            view.Ring.type = Image.Type.Filled;
            view.Ring.fillMethod = Image.FillMethod.Radial360;
            view.Ring.fillOrigin = (int)Image.Origin360.Top;
            view.Ring.fillClockwise = true;
            view.Ring.raycastTarget = false;

            TMP_Text label = NewText("Category", view.Root, SynergyText.CategoryName(slot.Category), 19f, CategoryColor(slot.Category));
            SetRect(label.rectTransform, new Vector2(SlotDiameter, 24f), new Vector2(0f, 20f));

            view.Count = NewText("Count", view.Root, "0/0", 28f, Color.white);
            SetRect(view.Count.rectTransform, new Vector2(SlotDiameter - 16f, 36f), new Vector2(0f, -12f));
            view.Count.enableAutoSizing = true;
            view.Count.fontSizeMin = 18f;
            view.Count.fontSizeMax = 28f;

            int buffLines = CountBuffLines(slot);
            for (int i = 0; i < buffLines; i++)
            {
                float y = -SlotDiameter * 0.5f - 6f - BuffLineHeight * 0.5f - i * (BuffLineHeight + 4f);
                Image back = NewImage("BuffBack" + i, view.Root, null, BuffBackColor, new Vector2(BuffLineWidth, BuffLineHeight), new Vector2(0f, y));
                back.raycastTarget = false;
                TMP_Text text = NewText("Buff" + i, back.rectTransform, string.Empty, 24f, new Color(1f, 0.92f, 0.45f, 1f));
                SetRect(text.rectTransform, new Vector2(BuffLineWidth - 8f, BuffLineHeight), Vector2.zero);
                text.enableAutoSizing = true;
                text.fontSizeMin = 14f;
                text.fontSizeMax = 24f;
                view.BuffBacks.Add(back);
                view.BuffTexts.Add(text);
            }

            return view;
        }

        private void BuildDetail()
        {
            GameObject go = NewRect("Detail", root);
            detail = (RectTransform)go.transform;
            detail.anchorMin = detail.anchorMax = new Vector2(1f, 1f);
            detail.pivot = new Vector2(1f, 1f);
            detail.sizeDelta = new Vector2(640f, 300f);

            Image back = NewImage("Back", detail, null, new Color(0.05f, 0.04f, 0.09f, 0.92f), Vector2.zero, Vector2.zero);
            Stretch(back.rectTransform);
            back.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = back;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(HideDetail);

            detailText = NewText("Text", detail, string.Empty, 24f, Color.white);
            detailText.alignment = TextAlignmentOptions.TopLeft;
            detailText.rectTransform.anchorMin = Vector2.zero;
            detailText.rectTransform.anchorMax = Vector2.one;
            detailText.rectTransform.offsetMin = new Vector2(20f, 16f);
            detailText.rectTransform.offsetMax = new Vector2(-20f, -16f);
            detailText.textWrappingMode = TextWrappingModes.Normal;

            go.SetActive(false);
        }

        // ── 갱신 ───────────────────────────────────────────────

        private void Refresh()
        {
            foreach (SlotView view in views)
            {
                if (view == null)
                    continue;

                SynergyRunManager.Slot slot = run.GetSlot(view.Category);
                if (slot == null)
                    continue;

                view.Count.text = slot.Counter.Progress + "/" + slot.Counter.Threshold;
                view.Ring.fillAmount = Mathf.Clamp01((float)slot.Counter.Progress / slot.Counter.Threshold);
                RefreshBuffs(view, slot);
            }

            if (detail.gameObject.activeSelf)
                detailText.text = BuildDetailText(detailCategory);
        }

        /// <summary>버프 종류별 합산값을 한 줄씩. 꺼진 버프는 글자와 배경을 함께 끈다(사용자 결정).</summary>
        private static void RefreshBuffs(SlotView view, SynergyRunManager.Slot slot)
        {
            var totals = new List<KeyValuePair<string, int>>();
            var labels = new List<string>();
            foreach (SynergyRunManager.Buff buff in slot.Buffs)
            {
                if (buff == null)
                    continue;

                string key = BuffKindKey(buff.Effect);
                int index = labels.IndexOf(key);
                if (index < 0)
                {
                    labels.Add(key);
                    totals.Add(new KeyValuePair<string, int>(key, 0));
                    index = labels.Count - 1;
                }

                totals[index] = new KeyValuePair<string, int>(key, totals[index].Value + buff.Total);
            }

            for (int i = 0; i < view.BuffTexts.Count; i++)
            {
                bool on = i < totals.Count && totals[i].Value > 0;
                view.BuffBacks[i].gameObject.SetActive(on);
                if (on)
                    view.BuffTexts[i].text = BuffLabel(totals[i].Key, totals[i].Value);
            }
        }

        private static string BuffKindKey(SynergyEffectId effect)
        {
            switch (effect)
            {
                case SynergyEffectId.AttackBuff:
                case SynergyEffectId.AttackBuffStack:
                    return "공격";
                case SynergyEffectId.ElementBuff:
                case SynergyEffectId.ElementBuffStack:
                    return "속성";
                case SynergyEffectId.DiceBuff:
                case SynergyEffectId.DiceBuffStack:
                case SynergyEffectId.RandomDiceBuff:
                    return "다이스";
                case SynergyEffectId.BossDamageBuff:
                    return "보스";
                case SynergyEffectId.WallGuard:
                    return "벽";
                default:
                    return effect.ToString();
            }
        }

        private static string BuffLabel(string kind, int value)
        {
            return kind == "벽" ? "벽 -" + value + "%" : kind + " +" + value + "%";
        }

        // ── 연출 ───────────────────────────────────────────────

        private void OnFired(SynergyCategory category, bool fromUnified)
        {
            SlotView view = views[(int)category];
            if (view != null)
                view.FlashLeft = FlashSeconds;
        }

        private void OnStrike(SynergyCategory category, Monster target)
        {
            SlotView view = views[(int)category];
            Camera cam = Camera.main;
            if (view == null || target == null || cam == null)
                return;

            Vector2 from = CenterOf(view);
            Vector2 screen = cam.WorldToScreenPoint(target.transform.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, canvasCamera, out Vector2 to))
                return;

            AddLine(new[] { from, to }, CategoryColor(category), 8f);
        }

        /// <summary>통합계 → 다른 계열. 슬롯이 세로로 서 있어 곧은 선은 원끼리 겹친다 — 왼쪽으로 휘는 곡선으로 잇는다.</summary>
        private void OnUnifiedLink(SynergyCategory category)
        {
            SlotView from = views[(int)SynergyCategory.Unified];
            SlotView to = views[(int)category];
            if (from == null || to == null)
                return;

            Vector2 a = CenterOf(from);
            Vector2 b = CenterOf(to);
            Vector2 control = (a + b) * 0.5f + new Vector2(-Mathf.Max(90f, Mathf.Abs(a.y - b.y) * 0.45f), 0f);

            const int segments = 14;
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points[i] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b;
            }

            AddLine(points, CategoryColor(SynergyCategory.Unified), 7f);
            to.FlashLeft = FlashSeconds;
        }

        private void AddLine(Vector2[] points, Color color, float width)
        {
            var fx = new LineFx { Left = LineSeconds };
            for (int i = 0; i < points.Length - 1; i++)
            {
                Image segment = NewImage("Line", root, null, color, Vector2.zero, Vector2.zero);
                segment.raycastTarget = false;
                PlaceSegment(segment.rectTransform, points[i], points[i + 1], width);

                // 슬롯 뒤에 깐다. 위에 그리면 선이 원 안의 숫자를 가린다.
                segment.rectTransform.SetAsFirstSibling();
                fx.Segments.Add(segment);
            }

            lines.Add(fx);
        }

        /// <summary>두 점을 잇는 막대(<c>DiceFx.PlaceSegment</c> 와 같은 방식). 길이에 폭을 더해 꺾이는 자리의 틈을 막는다.</summary>
        private static void PlaceSegment(RectTransform segment, Vector2 a, Vector2 b, float width)
        {
            Vector2 delta = b - a;
            segment.anchorMin = segment.anchorMax = new Vector2(0.5f, 0.5f);
            segment.pivot = new Vector2(0.5f, 0.5f);
            segment.anchoredPosition = (a + b) * 0.5f;
            segment.sizeDelta = new Vector2(width, delta.magnitude + width);
            segment.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, delta));
        }

        private Vector2 CenterOf(SlotView view)
        {
            Vector3 world = view.Root.TransformPoint(new Vector3(0f, -SlotDiameter * 0.5f, 0f));
            return root.InverseTransformPoint(world);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            foreach (SlotView view in views)
            {
                if (view == null)
                    continue;

                float scale = 1f;
                if (view.FlashLeft > 0f)
                {
                    view.FlashLeft -= dt;
                    float t = 1f - Mathf.Clamp01(view.FlashLeft / FlashSeconds);
                    scale = 1f + 0.22f * Mathf.Sin(t * Mathf.PI);
                }

                view.Root.localScale = new Vector3(scale, scale, 1f);
            }

            for (int i = lines.Count - 1; i >= 0; i--)
            {
                LineFx fx = lines[i];
                fx.Left -= dt;
                float alpha = Mathf.Clamp01(fx.Left / LineSeconds);
                foreach (Image segment in fx.Segments)
                {
                    if (segment == null)
                        continue;

                    Color c = segment.color;
                    c.a = alpha;
                    segment.color = c;
                }

                if (fx.Left > 0f)
                    continue;

                foreach (Image segment in fx.Segments)
                {
                    if (segment != null)
                        Destroy(segment.gameObject);
                }

                lines.RemoveAt(i);
            }

            if (detail.gameObject.activeSelf)
            {
                detailLeft -= dt;
                if (detailLeft <= 0f)
                    HideDetail();
                else
                    detailText.text = BuildDetailText(detailCategory);
            }
        }

        // ── 상세 ───────────────────────────────────────────────

        private void ToggleDetail(SynergyCategory category)
        {
            if (detail.gameObject.activeSelf && detailCategory == category)
            {
                HideDetail();
                return;
            }

            detailCategory = category;
            detailLeft = DetailSeconds;
            detailText.text = BuildDetailText(category);

            SlotView view = views[(int)category];
            Vector2 center = CenterOf(view);
            detail.gameObject.SetActive(true);
            detail.SetAsLastSibling();

            // 슬롯 왼쪽에 붙인다. 위쪽 기준(오른쪽 위 앵커) 좌표로 바꾼다.
            float topFromRootTop = -(center.y - root.rect.height * 0.5f) - SlotDiameter * 0.5f;
            detail.anchoredPosition = new Vector2(-(SlotCenterFromRight + SlotDiameter * 0.5f + 12f), -Mathf.Max(0f, topFromRootTop));
            detail.sizeDelta = new Vector2(640f, Mathf.Max(160f, detailText.GetPreferredValues(detailText.text, 600f, 0f).y + 36f));
        }

        private void HideDetail()
        {
            detail.gameObject.SetActive(false);
        }

        private string BuildDetailText(SynergyCategory category)
        {
            SynergyRunManager.Slot slot = run.GetSlot(category);
            if (slot == null)
                return string.Empty;

            var sb = new System.Text.StringBuilder();
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(CategoryColor(category))).Append('>')
                .Append(SynergyText.CategoryName(category)).Append(" 시너지</color>  <size=20>이번 판 ")
                .Append(slot.Fires).AppendLine("회 발동</size>");
            sb.Append("조건  ").Append(SynergyText.Describe(slot.Condition, true))
                .Append("  (").Append(slot.Counter.Progress).Append('/').Append(slot.Counter.Threshold).AppendLine(")");

            for (int i = 0; i < slot.Effects.Count; i++)
            {
                sb.Append("효과").Append(i + 1).Append("  ").Append(SynergyText.Describe(slot.Effects[i], false));
                SynergyRunManager.Buff buff = slot.Buffs[i];
                if (buff != null && buff.IsOn)
                {
                    sb.Append("  <color=#FFE873>");
                    if (buff.MaxStacks > 1)
                        sb.Append(buff.Stacks).Append('/').Append(buff.MaxStacks).Append("중첩 ");
                    sb.Append(buff.Remaining.ToString("0.0")).Append("초</color>");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // ── 모양 ───────────────────────────────────────────────

        public static Color CategoryColor(SynergyCategory category)
        {
            switch (category)
            {
                case SynergyCategory.Dice: return new Color(1f, 0.36f, 0.33f, 1f);
                case SynergyCategory.Combat: return new Color(1f, 0.66f, 0.2f, 1f);
                case SynergyCategory.Action: return new Color(0.35f, 0.8f, 1f, 1f);
                case SynergyCategory.Unified: return new Color(0.78f, 0.5f, 1f, 1f);
                default: return Color.white;
            }
        }

        private static Sprite circle;
        private static Sprite ring;

        /// <summary>속이 찬 원. 스프라이트 에셋 없이 한 번 굽는다(가장자리 1px 안티앨리어스).</summary>
        internal static Sprite Circle => circle != null ? circle : (circle = BakeCircle(0f));

        /// <summary>두께 12% 의 고리. 진행 링에 쓴다.</summary>
        internal static Sprite Ring => ring != null ? ring : (ring = BakeCircle(0.82f));

        private static Sprite BakeCircle(float innerRatio)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * 0.5f;
            float inner = r * innerRatio;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float outer = Mathf.Clamp01(r - d);
                    float hole = innerRatio > 0f ? Mathf.Clamp01(d - inner) : 1f;
                    byte a = (byte)(255 * Mathf.Min(outer, hole));
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 position)
        {
            Image image = NewRect(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>글꼴은 TMP 기본값 — 이 프로젝트에서는 BM HANNA 다(<c>DevCheatUI</c> 주석).</summary>
        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
        {
            TextMeshProUGUI label = NewRect(name, parent).AddComponent<TextMeshProUGUI>();

            // 글꼴을 직접 넣는다. 꺼진 dialogView 아래에서 만들면 TMP 가 OnEnable 전이라 글꼴이 비어 있고,
            // 그 상태로 GetPreferredValues 를 부르면 NRE 가 난다.
            label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
