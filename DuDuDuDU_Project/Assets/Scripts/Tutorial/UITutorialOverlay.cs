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
    /// <b>딤드는 사각형 네 장이다.</b> 구멍을 마스크로 뚫으면 그 자리의 클릭까지 막는다
    /// (마스크는 그리기만 바꾸고 레이캐스트는 그대로다). 구멍 둘레에 네 장을 깔면
    /// 구멍 자리에는 이 창의 그래픽이 아예 없으므로, 클릭이 <b>아래 버튼에 그대로 닿는다.</b>
    ///
    /// <b>구멍은 매 프레임 다시 잰다.</b> 상세 패널은 다이스를 따라다니고, 다이스는 드래그로
    /// 칸을 옮긴다. 한 번 잰 사각형을 들고 있으면 대상이 움직인 뒤 엉뚱한 곳이 열린다.
    ///
    /// <b>항상 맨 위에 있어야 한다.</b> 같은 팝업 캔버스에 나중에 열린 창(현상금 선택·확인 창·
    /// 상세 패널)이 <c>SetAsLastSibling</c> 으로 올라오므로, 그 위로 매 프레임 다시 올라간다.
    /// </summary>
    public class UITutorialOverlay : DialogBase
    {
        [SerializeField] private RectTransform view;

        [Header("딤드")]
        [SerializeField] private Image blockTop;
        [SerializeField] private Image blockBottom;
        [SerializeField] private Image blockLeft;
        [SerializeField] private Image blockRight;

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
        private const float BubbleOffsetY = 560f;
        private const float BubbleGap = 30f;

        /// <summary>
        /// 말풍선 중심이 내려갈 수 있는 가장 낮은 자리. 이 아래는 다이스 보드다 —
        /// 손가락 단계(입력을 막지 않는다)에서 보드를 덮으면 유저가 만지려는 것을 가린다.
        /// </summary>
        private const float BubbleLowestY = -190f;
        private const float FingerBob = 22f;

        private readonly List<RectTransform> holeTargets = new List<RectTransform>();
        private bool dimOn;
        private RectTransform fingerTarget;
        private bool tapMode;
        private bool tapped;
        private TalkPlacement placement;

        private readonly Vector3[] corners = new Vector3[4];

        protected override void OnLoad()
        {
            if (tapCatcher != null)
                tapCatcher.onClick.AddListener(OnTapCatcher);

            ClearAll();
        }

        protected override void OnDestroy()
        {
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
            holeTargets.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null)
                        holeTargets.Add(targets[i]);
                }
            }

            dimOn = true;
            RefreshBlockers();
            UpdateHole();
        }

        public void ClearDim()
        {
            dimOn = false;
            holeTargets.Clear();
            RefreshBlockers();
        }

        /// <summary>손가락을 대상 위에 띄운다. null 이면 거둔다. 입력은 막지 않는다.</summary>
        public void SetFinger(RectTransform target)
        {
            fingerTarget = target;
            if (finger != null)
                finger.gameObject.SetActive(target != null);
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

            RefreshBlockers();
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
        /// 탭 대기 중에는 딤드 네 장을 내린다. 네 장은 버튼이 아닌 그림이라 클릭을 <b>먹기만</b>
        /// 하고, 그 아래 깔린 탭 받이까지 닿지 않는다 — 그러면 말풍선이 영영 안 넘어간다.
        /// 탭 받이가 같은 색으로 화면 전체를 덮으므로 보이는 것은 같다.
        /// </summary>
        private void RefreshBlockers()
        {
            SetBlockersActive(dimOn && !tapMode);
        }

        private void SetBlockersActive(bool active)
        {
            if (blockTop != null) blockTop.gameObject.SetActive(active);
            if (blockBottom != null) blockBottom.gameObject.SetActive(active);
            if (blockLeft != null) blockLeft.gameObject.SetActive(active);
            if (blockRight != null) blockRight.gameObject.SetActive(active);
        }

        private void UpdateHole()
        {
            if (view == null)
                return;

            // 구멍이 없으면 한가운데의 넓이 0 짜리 사각형이다 — 위·아래 두 장이 화면을 다 덮는다.
            Vector2 min = new Vector2(0.5f, 0.5f);
            Vector2 max = min;

            if (TryGetUnionRect(holeTargets, out Rect local))
            {
                Rect area = view.rect;
                min = new Vector2(
                    Mathf.Clamp01((local.xMin - HolePadding - area.xMin) / area.width),
                    Mathf.Clamp01((local.yMin - HolePadding - area.yMin) / area.height));
                max = new Vector2(
                    Mathf.Clamp01((local.xMax + HolePadding - area.xMin) / area.width),
                    Mathf.Clamp01((local.yMax + HolePadding - area.yMin) / area.height));
            }

            SetAnchors(blockTop, new Vector2(0f, max.y), new Vector2(1f, 1f));
            SetAnchors(blockBottom, new Vector2(0f, 0f), new Vector2(1f, min.y));
            SetAnchors(blockLeft, new Vector2(0f, min.y), new Vector2(min.x, max.y));
            SetAnchors(blockRight, new Vector2(max.x, min.y), new Vector2(1f, max.y));
        }

        private static void SetAnchors(Image block, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (block == null)
                return;

            RectTransform rect = block.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void UpdateFinger()
        {
            if (finger == null || fingerTarget == null)
                return;

            if (!TryGetUnionRect(SingleTarget(fingerTarget), out Rect local))
            {
                finger.gameObject.SetActive(false);
                return;
            }

            finger.gameObject.SetActive(true);
            finger.anchoredPosition = local.center;

            // 배속과 일시정지를 타지 않는다. 튜토리얼은 시간을 멈춘 채 가리키는 일이 많다.
            float t = Time.unscaledTime;
            if (fingerArrow != null)
                fingerArrow.anchoredPosition = new Vector2(0f, 90f + Mathf.Abs(Mathf.Sin(t * 4f)) * FingerBob);
            if (fingerRing != null)
                fingerRing.localScale = Vector3.one * (0.85f + 0.25f * Mathf.PingPong(t * 1.6f, 1f));
        }

        private readonly List<RectTransform> single = new List<RectTransform>(1);

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
        /// <item>위쪽(현상금 띠·딜미터기)이면 그 <b>바로 아래</b>에 두되 보드까지는 내려가지 않는다.
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

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────
        //
        // 조립 헬퍼는 UIBountyUIFactory 를 쓴다(UIWaveRewindButton 주석 참조 — 네 번째 사본을 만들지 않는다).
        // 손가락 그림은 아직 없다. Unity 내장 UI 스프라이트(원·아래 화살표)로 자리를 잡아 두고,
        // 손가락 아트가 들어오면 이 두 장만 바꾸면 된다.

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.68f);
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

            overlay.blockTop = UIBountyUIFactory.CreateImage("DimTop", viewObject.transform, DimColor);
            overlay.blockBottom = UIBountyUIFactory.CreateImage("DimBottom", viewObject.transform, DimColor);
            overlay.blockLeft = UIBountyUIFactory.CreateImage("DimLeft", viewObject.transform, DimColor);
            overlay.blockRight = UIBountyUIFactory.CreateImage("DimRight", viewObject.transform, DimColor);

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
            UIBountyUIFactory.SetRect(overlay.fingerArrow, new Vector2(96f, 96f), new Vector2(0f, 90f));

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
    }
}
