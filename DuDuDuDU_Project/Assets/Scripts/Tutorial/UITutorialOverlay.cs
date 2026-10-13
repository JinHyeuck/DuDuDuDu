using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;
using OJ.UI;

namespace OJ.Tutorial
{
    /// <summary>
    /// 튜토리얼의 화면 한 장 — 딤드(구멍 뚫린 어둠), 손가락, 말풍선. (Docs/IngameTutorialSteps.md)
    ///
    /// <b>무엇을 언제 보일지는 모른다.</b> 그 판단은 <see cref="TutorialDirector"/> 가 하고,
    /// 이 창은 "이 사각형만 열어라 / 여기를 가리켜라 / 이 말을 하라" 를 받아 그리기만 한다.
    ///
    /// <b>막는 것과 보이는 것이 따로다.</b>
    /// <list type="bullet">
    /// <item><b>막는 것</b> — 구멍 둘레의 투명한 사각형 네 장. 구멍을 마스크로 뚫으면 그 자리의
    ///       클릭까지 막는다(마스크는 그리기만 바꾸고 레이캐스트는 그대로다). 둘레에 깔면 구멍 자리에는
    ///       이 창의 그래픽이 없으므로 클릭이 <b>아래 버튼에 그대로 닿는다.</b></item>
    /// <item><b>보이는 것</b> — 구멍을 둥근 모서리 + 바깥으로 짙어지는 그라데이션 테두리로 감싼
    ///       9-슬라이스 한 장(<see cref="holeFrame"/>)과 그 바깥 사각형 네 장. 테두리 그림은 런타임에
    ///       만든다(<see cref="BuildFrameSprite"/>) — 아트 에셋을 하나 더 들일 일이 아니다.</item>
    /// </list>
    ///
    /// <b>구멍은 매 프레임 다시 잰다.</b> 상세 패널은 다이스를 따라다니고, 다이스는 드래그로
    /// 칸을 옮긴다. 한 번 잰 사각형을 들고 있으면 대상이 움직인 뒤 엉뚱한 곳이 열린다.
    ///
    /// <b>항상 맨 위에 있어야 한다.</b> 같은 팝업 캔버스에 나중에 열린 창(현상금 선택·확인 창·
    /// 상세 패널)이 <c>SetAsLastSibling</c> 으로 올라오므로, 그 위로 매 프레임 다시 올라간다.
    ///
    /// <b>눌림 연출을 끈다.</b> <c>DialogBase</c> 는 창 안의 버튼마다 <see cref="UIButtonPress"/> 를
    /// 붙이는데, 탭 받이는 화면 전체 크기라 누르는 순간 줄어들어 <b>화면 가장자리의 딤드가 빈다.</b>
    /// 가리키는 버튼도 같다 — 눌려 줄어들면 매 프레임 재는 구멍이 따라 출렁인다.
    /// </summary>
    public class UITutorialOverlay : DialogBase
    {
        [SerializeField] private RectTransform view;

        [Header("막는 것(투명)")]
        [SerializeField] private Image blockTop;
        [SerializeField] private Image blockBottom;
        [SerializeField] private Image blockLeft;
        [SerializeField] private Image blockRight;

        [Header("보이는 것")]
        [SerializeField] private Image shadeTop;
        [SerializeField] private Image shadeBottom;
        [SerializeField] private Image shadeLeft;
        [SerializeField] private Image shadeRight;
        [SerializeField] private Image holeFrame;

        [Tooltip("말풍선을 탭해서 넘기는 단계에서 화면 전체를 받는다.")]
        [SerializeField] private Button tapCatcher;
        [SerializeField] private Image tapCatcherImage;

        [Header("손가락")]
        [SerializeField] private RectTransform finger;
        [SerializeField] private RectTransform fingerRing;
        [SerializeField] private RectTransform fingerArrow;

        [Header("말풍선")]
        [SerializeField] private RectTransform bubble;
        [SerializeField] private TMP_Text bubbleText;
        [SerializeField] private TMP_Text hintText;

        /// <summary>말풍선을 어디 둘지. 대부분은 <see cref="Auto"/> 로 충분하다.</summary>
        public enum TalkPlacement
        {
            /// <summary>가리키는 곳을 피해서 둔다(<see cref="PlaceBubble"/> 주석).</summary>
            Auto = 0,

            /// <summary>화면 위쪽 전장. 아래쪽 창(상세 패널)을 가리면 안 될 때.</summary>
            Top,
        }

        private const float HolePadding = 14f;

        /// <summary>구멍 가장자리에서 바깥으로 딤드가 짙어지는 폭.</summary>
        private const float Feather = 36f;

        /// <summary>구멍 모서리의 반지름.</summary>
        private const float CornerRadius = 28f;

        private const float BubbleOffsetY = 560f;
        private const float BubbleGap = 30f;

        /// <summary>
        /// 말풍선 중심이 내려갈 수 있는 가장 낮은 자리. 이 아래는 다이스 보드다 —
        /// 손가락 단계(입력을 막지 않는다)에서 보드를 덮으면 유저가 만지려는 것을 가린다.
        /// </summary>
        private const float BubbleLowestY = -190f;
        private const float FingerBob = 22f;
        private const float ArrowRestY = 90f;

        /// <summary>드래그 손가락 한 바퀴(초). 출발에서 잠깐 머물고, 끌고, 도착에서 잠깐 머문다.</summary>
        private const float DragCycle = 1.6f;

        private readonly List<RectTransform> holeTargets = new List<RectTransform>();
        private bool dimOn;
        private RectTransform fingerTarget;
        private RectTransform dragFrom;
        private RectTransform dragTo;
        private bool tapMode;
        private bool tapped;
        private TalkPlacement placement;

        /// <summary>눌림 연출을 꺼 둔 것들. 대상이 바뀌면 되살린다.</summary>
        private readonly List<UIButtonPress> mutedPress = new List<UIButtonPress>();

        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<RectTransform> single = new List<RectTransform>(1);

        private static Sprite frameSprite;

        protected override void OnLoad()
        {
            if (tapCatcher != null)
            {
                tapCatcher.onClick.AddListener(OnTapCatcher);

                // DialogBase 가 방금 붙인 눌림 연출을 뗀다(클래스 주석 — 가장자리 딤드가 빈다).
                UIButtonPress press = tapCatcher.GetComponent<UIButtonPress>();
                if (press != null)
                    Destroy(press);
            }

            if (holeFrame != null)
            {
                holeFrame.sprite = frameSprite != null ? frameSprite : (frameSprite = BuildFrameSprite());
                holeFrame.type = Image.Type.Sliced;
                holeFrame.fillCenter = true;
            }

            ClearAll();
        }

        protected override void OnDestroy()
        {
            RestorePress();

            if (tapCatcher != null)
                tapCatcher.onClick.RemoveListener(OnTapCatcher);

            base.OnDestroy();
        }

        // ── 디렉터가 부르는 것 ────────────────────────────────────────

        /// <summary>
        /// 이 대상들만 열고 나머지를 막는다. 대상이 없으면 화면 전체를 막는다.
        /// 대상이 여럿이면 그 전부를 감싸는 사각형 하나가 구멍이다.
        /// </summary>
        public void SetDim(params RectTransform[] targets)
        {
            RestorePress();
            holeTargets.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] == null)
                        continue;

                    holeTargets.Add(targets[i]);
                    MutePress(targets[i]);
                }
            }

            dimOn = true;
            RefreshDimActive();
            UpdateHole();
        }

        public void ClearDim()
        {
            RestorePress();
            dimOn = false;
            holeTargets.Clear();
            RefreshDimActive();
        }

        /// <summary>손가락을 대상 위에 띄운다. null 이면 거둔다. 입력은 막지 않는다.</summary>
        public void SetFinger(RectTransform target)
        {
            fingerTarget = target;
            dragFrom = null;
            dragTo = null;
            if (finger != null)
                finger.gameObject.SetActive(target != null);
            UpdateFinger();
        }

        /// <summary>
        /// 손가락이 <paramref name="from"/> 에서 <paramref name="to"/> 로 <b>끌고 가는</b> 모양을 되풀이한다.
        /// 머지처럼 "누르기" 가 아니라 "끌기" 를 가르칠 때 쓴다.
        /// </summary>
        public void SetDragFinger(RectTransform from, RectTransform to)
        {
            fingerTarget = from;
            dragFrom = from;
            dragTo = to;
            if (finger != null)
                finger.gameObject.SetActive(from != null && to != null);
            UpdateFinger();
        }

        /// <summary>
        /// 말풍선을 띄운다. <paramref name="tapToContinue"/> 면 화면 전체가 탭을 받고,
        /// <see cref="ConsumeTap"/> 이 그 탭을 한 번 돌려준다. null 이면 거둔다.
        /// </summary>
        public void SetTalk(string text, bool tapToContinue = false, TalkPlacement where = TalkPlacement.Auto)
        {
            placement = where;

            bool show = !string.IsNullOrEmpty(text);
            if (bubble != null)
                bubble.gameObject.SetActive(show);
            if (bubbleText != null)
                bubbleText.SetText(show ? text : string.Empty);

            tapMode = show && tapToContinue;
            tapped = false;

            if (hintText != null)
                hintText.gameObject.SetActive(tapMode);
            if (tapCatcher != null)
                tapCatcher.gameObject.SetActive(tapMode);

            RefreshDimActive();
            PlaceBubble();
        }

        /// <summary>탭 대기 중 화면을 눌렀으면 한 번 참이다.</summary>
        public bool ConsumeTap()
        {
            if (!tapped)
                return false;

            tapped = false;
            return true;
        }

        public void ClearAll()
        {
            ClearDim();
            SetFinger(null);
            SetTalk(null);
        }

        // ── 매 프레임 ─────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!isEnter)
                return;

            Transform parent = transform.parent;
            if (parent != null && transform.GetSiblingIndex() != parent.childCount - 1)
                transform.SetAsLastSibling();

            if (dimOn)
                UpdateHole();

            UpdateFinger();
            PlaceBubble();
        }

        private void OnTapCatcher()
        {
            if (tapMode)
                tapped = true;
        }

        /// <summary>
        /// 탭 대기 중에는 딤드를 통째로 내린다. 막는 네 장은 버튼이 아닌 그림이라 클릭을 <b>먹기만</b>
        /// 하고 그 아래 탭 받이까지 닿지 않는다 — 그러면 말풍선이 영영 안 넘어간다.
        /// 탭 받이가 같은 색으로 화면 전체를 덮으므로 보이는 것은 같다.
        /// </summary>
        private void RefreshDimActive()
        {
            bool on = dimOn && !tapMode;
            SetActive(blockTop, on);
            SetActive(blockBottom, on);
            SetActive(blockLeft, on);
            SetActive(blockRight, on);
            SetActive(shadeTop, on);
            SetActive(shadeBottom, on);
            SetActive(shadeLeft, on);
            SetActive(shadeRight, on);
            SetActive(holeFrame, on && holeTargets.Count > 0);
        }

        private static void SetActive(Graphic graphic, bool active)
        {
            if (graphic != null && graphic.gameObject.activeSelf != active)
                graphic.gameObject.SetActive(active);
        }

        private void UpdateHole()
        {
            if (view == null)
                return;

            // 구멍이 없으면 한가운데의 넓이 0 짜리 사각형이다 — 위·아래 두 장이 화면을 다 덮는다.
            Rect area = view.rect;
            Rect hole = new Rect(area.center, Vector2.zero);
            bool hasHole = TryGetUnionRect(holeTargets, out Rect local);
            if (hasHole)
                hole = Rect.MinMaxRect(
                    local.xMin - HolePadding, local.yMin - HolePadding,
                    local.xMax + HolePadding, local.yMax + HolePadding);

            // 막는 것은 구멍 그대로. 보이는 것은 그라데이션 폭만큼 바깥까지 테두리가 맡는다.
            PlaceAround(blockTop, blockBottom, blockLeft, blockRight, hole, area);

            Rect outer = hasHole
                ? Rect.MinMaxRect(hole.xMin - Feather, hole.yMin - Feather, hole.xMax + Feather, hole.yMax + Feather)
                : hole;
            PlaceAround(shadeTop, shadeBottom, shadeLeft, shadeRight, outer, area);

            if (holeFrame != null)
            {
                SetActive(holeFrame, hasHole && dimOn && !tapMode);
                if (hasHole)
                    SetAnchors(holeFrame, Normalize(outer.min, area), Normalize(outer.max, area));
            }
        }

        private static void PlaceAround(Image top, Image bottom, Image left, Image right, Rect hole, Rect area)
        {
            Vector2 min = Normalize(hole.min, area);
            Vector2 max = Normalize(hole.max, area);

            SetAnchors(top, new Vector2(0f, max.y), new Vector2(1f, 1f));
            SetAnchors(bottom, new Vector2(0f, 0f), new Vector2(1f, min.y));
            SetAnchors(left, new Vector2(0f, min.y), new Vector2(min.x, max.y));
            SetAnchors(right, new Vector2(max.x, min.y), new Vector2(1f, max.y));
        }

        private static Vector2 Normalize(Vector2 point, Rect area)
        {
            return new Vector2(
                Mathf.Clamp01((point.x - area.xMin) / area.width),
                Mathf.Clamp01((point.y - area.yMin) / area.height));
        }

        private static void SetAnchors(Graphic graphic, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (graphic == null)
                return;

            RectTransform rect = graphic.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void UpdateFinger()
        {
            if (finger == null || fingerTarget == null)
                return;

            // 배속과 일시정지를 타지 않는다. 튜토리얼은 시간을 멈춘 채 가리키는 일이 많다.
            float t = Time.unscaledTime;

            if (dragFrom != null && dragTo != null)
            {
                if (!TryGetUnionRect(SingleTarget(dragFrom), out Rect from)
                    || !TryGetUnionRect(SingleTarget(dragTo), out Rect to))
                {
                    finger.gameObject.SetActive(false);
                    return;
                }

                finger.gameObject.SetActive(true);

                // 0~0.2 출발에서 머묾 · 0.2~0.75 끌기 · 0.75~1 도착에서 머묾.
                float phase = Mathf.Repeat(t, DragCycle) / DragCycle;
                float move = Mathf.Clamp01((phase - 0.2f) / 0.55f);
                move = move * move * (3f - 2f * move);
                finger.anchoredPosition = Vector2.Lerp(from.center, to.center, move);

                if (fingerArrow != null)
                    fingerArrow.anchoredPosition = new Vector2(0f, ArrowRestY);

                // 누르고 있는 동안(끄는 동안)은 원이 작게 조여 있다가 놓으면 퍼진다.
                if (fingerRing != null)
                    fingerRing.localScale = Vector3.one * (phase < 0.75f ? 0.8f : 0.8f + (phase - 0.75f) * 1.6f);
                return;
            }

            if (!TryGetUnionRect(SingleTarget(fingerTarget), out Rect local))
            {
                finger.gameObject.SetActive(false);
                return;
            }

            finger.gameObject.SetActive(true);
            finger.anchoredPosition = local.center;

            if (fingerArrow != null)
                fingerArrow.anchoredPosition = new Vector2(0f, ArrowRestY + Mathf.Abs(Mathf.Sin(t * 4f)) * FingerBob);
            if (fingerRing != null)
                fingerRing.localScale = Vector3.one * (0.85f + 0.25f * Mathf.PingPong(t * 1.6f, 1f));
        }

        private List<RectTransform> SingleTarget(RectTransform target)
        {
            single.Clear();
            single.Add(target);
            return single;
        }

        /// <summary>
        /// 말풍선은 가리키는 곳을 가리지 않게 둔다.
        /// <list type="bullet">
        /// <item>가리키는 곳이 아래쪽(보드·버튼)이면 위쪽 전장에 둔다.</item>
        /// <item>위쪽(현상금 띠)이면 그 <b>바로 아래</b>에 두되 보드까지는 내려가지 않는다.
        ///       반대편(아래쪽)에 두면 보드를 덮어, 손가락 단계에서 유저가 만지려는 것을 가린다.</item>
        /// <item>가리키는 곳이 없으면 가운데다.</item>
        /// </list>
        /// </summary>
        private void PlaceBubble()
        {
            if (bubble == null || !bubble.gameObject.activeSelf)
                return;

            float y = 0f;
            RectTransform focus = fingerTarget != null ? fingerTarget
                : holeTargets.Count > 0 ? holeTargets[0] : null;

            if (placement == TalkPlacement.Top)
            {
                y = BubbleOffsetY;
            }
            else if (focus != null && TryGetUnionRect(SingleTarget(focus), out Rect local))
            {
                y = local.center.y < 0f
                    ? BubbleOffsetY
                    : Mathf.Max(local.yMin - BubbleGap - bubble.rect.height * 0.5f, BubbleLowestY);
            }

            bubble.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>
        /// 대상들을 이 창의 좌표(가운데 원점)로 옮긴 합집합. 대상은 다른 캔버스에 있을 수 있어
        /// (보드는 카메라 캔버스, 팝업은 오버레이) 화면 좌표를 한 번 거친다.
        /// 보이는 대상이 하나도 없으면 거짓이다.
        /// </summary>
        private bool TryGetUnionRect(List<RectTransform> targets, out Rect result)
        {
            result = default;
            if (view == null)
                return false;

            bool any = false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < targets.Count; i++)
            {
                RectTransform target = targets[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;

                Canvas canvas = target.GetComponentInParent<Canvas>();
                Camera cam = null;
                if (canvas != null)
                {
                    Canvas rootCanvas = canvas.rootCanvas;
                    if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        cam = rootCanvas.worldCamera;
                }

                target.GetWorldCorners(corners);
                for (int c = 0; c < 4; c++)
                {
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corners[c]);
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, screen, null, out Vector2 local))
                        continue;

                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                    any = true;
                }
            }

            if (!any)
                return false;

            result = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        // ── 눌림 연출 ─────────────────────────────────────────────────

        private void MutePress(RectTransform target)
        {
            UIButtonPress own = target.GetComponentInParent<UIButtonPress>();
            if (own != null && own.enabled)
            {
                own.enabled = false;
                mutedPress.Add(own);
            }

            UIButtonPress[] children = target.GetComponentsInChildren<UIButtonPress>();
            for (int i = 0; i < children.Length; i++)
            {
                if (!children[i].enabled)
                    continue;

                children[i].enabled = false;
                mutedPress.Add(children[i]);
            }
        }

        private void RestorePress()
        {
            for (int i = 0; i < mutedPress.Count; i++)
            {
                if (mutedPress[i] != null)
                    mutedPress[i].enabled = true;
            }

            mutedPress.Clear();
        }

        // ── 테두리 그림 ───────────────────────────────────────────────

        /// <summary>
        /// 가운데가 뚫린 둥근 사각형 테두리를 만든다. 9-슬라이스로 늘려 쓰므로 한 장이면 된다.
        ///
        /// 안쪽 둥근 사각형(반지름 <see cref="CornerRadius"/>) 안은 투명하고, 그 바깥으로
        /// <see cref="Feather"/> 만큼 가면서 불투명해진다. 그림 가장자리에서 완전히 불투명해져야
        /// 바깥 사각형 네 장과 이음매 없이 붙는다. 색은 검정이고 진하기는 Image 색의 알파가 정한다.
        /// </summary>
        private static Sprite BuildFrameSprite()
        {
            int border = Mathf.CeilToInt(Feather + CornerRadius);
            int size = border * 2 + 2;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TutorialHoleFrame",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = size * 0.5f;
            float half = center - Feather;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 둥근 사각형까지의 거리(바깥이 양수).
                    float qx = Mathf.Max(Mathf.Abs(x + 0.5f - center) - (half - CornerRadius), 0f);
                    float qy = Mathf.Max(Mathf.Abs(y + 0.5f - center) - (half - CornerRadius), 0f);
                    float distance = Mathf.Sqrt(qx * qx + qy * qy) - CornerRadius;

                    float a = Mathf.Clamp01(distance / Feather);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────
        //
        // 조립 헬퍼는 UIBountyUIFactory 를 쓴다(UIWaveRewindButton 주석 참조 — 네 번째 사본을 만들지 않는다).
        // 손가락 그림은 아직 없다. Unity 내장 UI 스프라이트(원·아래 화살표)로 자리를 잡아 두고,
        // 손가락 아트가 들어오면 이 두 장만 바꾸면 된다.

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.68f);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color BubbleColor = new Color(0.10f, 0.12f, 0.20f, 0.94f);
        private static readonly Color ArrowColor = new Color(1f, 0.84f, 0.25f, 0.95f);

        /// <summary>
        /// 원은 <b>옅게</b> 깐다. 내장 원 스프라이트는 속이 찬 원이라 진하게 칠하면 가리키는 버튼의
        /// 글자와 그림을 덮는다 — 플레이로 확인한 사고다.
        /// </summary>
        private static readonly Color RingColor = new Color(1f, 0.84f, 0.25f, 0.28f);
        private static readonly Color HintColor = new Color(1f, 1f, 1f, 0.6f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UITutorialOverlay Create(Transform parent, TMP_FontAsset font, Sprite ringSprite, Sprite arrowSprite)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UITutorialOverlay", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject viewObject = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            RectTransform viewRect = viewObject.GetComponent<RectTransform>();
            UIBountyUIFactory.Stretch(viewRect);

            var overlay = root.AddComponent<UITutorialOverlay>();
            overlay.dialogView = viewObject;
            overlay.view = viewRect;

            // 백키로 닫히면 튜토리얼이 구멍 없는 채로 멈춘다. 이 창은 디렉터만 닫는다.
            overlay.UseBackBtn = false;

            // 열림 연출을 쓰지 않는다. 딤드가 0.13초 동안 흐릿하게 들어오면 그 사이에
            // 구멍 밖이 눌린다.
            overlay.BakeOpenStyle(DialogOpenStyle.None);

            overlay.tapCatcherImage = UIBountyUIFactory.CreateImage("TapCatcher", viewObject.transform, DimColor);
            UIBountyUIFactory.Stretch(overlay.tapCatcherImage.rectTransform);
            overlay.tapCatcher = overlay.tapCatcherImage.gameObject.AddComponent<Button>();
            overlay.tapCatcher.targetGraphic = overlay.tapCatcherImage;
            overlay.tapCatcher.transition = Selectable.Transition.None;

            // 보이는 딤드. 클릭은 받지 않는다 — 막는 것은 아래 투명한 네 장이 한다.
            overlay.shadeTop = CreateShade("ShadeTop", viewObject.transform);
            overlay.shadeBottom = CreateShade("ShadeBottom", viewObject.transform);
            overlay.shadeLeft = CreateShade("ShadeLeft", viewObject.transform);
            overlay.shadeRight = CreateShade("ShadeRight", viewObject.transform);
            overlay.holeFrame = CreateShade("HoleFrame", viewObject.transform);

            overlay.blockTop = UIBountyUIFactory.CreateImage("BlockTop", viewObject.transform, Clear);
            overlay.blockBottom = UIBountyUIFactory.CreateImage("BlockBottom", viewObject.transform, Clear);
            overlay.blockLeft = UIBountyUIFactory.CreateImage("BlockLeft", viewObject.transform, Clear);
            overlay.blockRight = UIBountyUIFactory.CreateImage("BlockRight", viewObject.transform, Clear);

            GameObject fingerObject = UIBountyUIFactory.CreateRect("Finger", viewObject.transform);
            overlay.finger = fingerObject.GetComponent<RectTransform>();
            UIBountyUIFactory.SetRect(overlay.finger, new Vector2(10f, 10f), Vector2.zero);

            Image ring = UIBountyUIFactory.CreateImage("Ring", fingerObject.transform, RingColor);
            ring.sprite = ringSprite;
            ring.raycastTarget = false;
            overlay.fingerRing = ring.rectTransform;
            UIBountyUIFactory.SetRect(overlay.fingerRing, new Vector2(150f, 150f), Vector2.zero);

            Image arrow = UIBountyUIFactory.CreateImage("Arrow", fingerObject.transform, ArrowColor);
            arrow.sprite = arrowSprite;
            arrow.preserveAspect = true;
            arrow.raycastTarget = false;
            overlay.fingerArrow = arrow.rectTransform;
            UIBountyUIFactory.SetRect(overlay.fingerArrow, new Vector2(96f, 96f), new Vector2(0f, ArrowRestY));

            Image bubbleImage = UIBountyUIFactory.CreateImage("Bubble", viewObject.transform, BubbleColor);
            bubbleImage.raycastTarget = false;
            overlay.bubble = bubbleImage.rectTransform;
            UIBountyUIFactory.SetRect(overlay.bubble, new Vector2(960f, 300f), Vector2.zero);

            overlay.bubbleText = UIBountyUIFactory.CreateText(
                "Text", bubbleImage.transform, "", 46f, TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.Stretch(overlay.bubbleText.rectTransform);
            overlay.bubbleText.rectTransform.offsetMin = new Vector2(40f, 50f);
            overlay.bubbleText.rectTransform.offsetMax = new Vector2(-40f, -30f);
            overlay.bubbleText.textWrappingMode = TextWrappingModes.Normal;

            overlay.hintText = UIBountyUIFactory.CreateText(
                "Hint", bubbleImage.transform, "화면을 눌러 계속", 30f, TextAlignmentOptions.Bottom, HintColor, font);
            UIBountyUIFactory.Stretch(overlay.hintText.rectTransform);
            overlay.hintText.rectTransform.offsetMin = new Vector2(0f, 16f);

            return overlay;
        }

        private static Image CreateShade(string name, Transform parent)
        {
            Image image = UIBountyUIFactory.CreateImage(name, parent, DimColor);
            image.raycastTarget = false;
            return image;
        }
    }
}
