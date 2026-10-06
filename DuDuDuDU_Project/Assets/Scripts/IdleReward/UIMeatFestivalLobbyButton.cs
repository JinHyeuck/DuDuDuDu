using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Point;
using OJ.Hunting;
using OJ.DI;

namespace OJ.IdleReward
{
    /// <summary>
    /// 로비의 고기 축제 위젯. <b>팝업을 열지 않고 여기서 바로 받는다.</b>
    ///
    /// 예전에는 방치보상 팝업의 두 번째 탭이었다. 거기서 뺀 이유는 둘이다.
    ///
    /// <b>1. 받는 것 말고 할 일이 없다.</b> 고기 축제는 선택지가 없어서 팝업을 한 단계 끼우면
    /// 그 탭은 순수한 군더더기가 된다. 로비에서 한 번 누르면 끝나는 것이 맞다.
    ///
    /// <b>2. 세트 그리드가 감당이 안 된다.</b> 옛 탭은 세트마다 칸을 하나씩 그렸는데
    /// (6열 x 5행 = 30칸), 저장 상한이 60세트가 되면서 6열 x 10행이 되어 팝업이 못 담는다.
    /// 애초에 유저가 알고 싶은 것은 "지금 누르면 몇 개 들어오나" 하나이고, 그건 숫자 하나로 충분하다.
    ///
    /// <b>가득 참을 반드시 알린다.</b> 2시간에 한 세트라 60세트는 5일이다 — 주말을 한 번
    /// 건너뛰면 닿는 거리이고, 그 뒤로 구워지는 고기는 조용히 버려진다.
    /// </summary>
    public class UIMeatFestivalLobbyButton : MonoBehaviour
    {
        private static readonly Color NormalCountColor = Color.white;
        private static readonly Color FullCountColor = new Color(1f, 0.42f, 0.30f, 1f);
        private static readonly Color TimerColor = new Color(0.78f, 0.85f, 0.95f, 1f);

        [SerializeField] private Button button;

        [Tooltip("받으면 들어올 고기 수. 한 세트가 30개이므로 한 세트일 때 \"x30\" 이 된다.")]
        [SerializeField] private TMP_Text countText;

        [Tooltip("다음 세트까지 남은 시간. 가득 차면 \"가득\" 이 된다.")]
        [SerializeField] private TMP_Text timerText;

        [Tooltip("받을 것이 있을 때만 켜진다. 비워도 동작한다.")]
        [SerializeField] private GameObject redDot;

        [Header("월드 기준점")]
        [Tooltip("이 오브젝트의 월드 위치를 따라간다. 비우면 씬에 놓인 자리 그대로 있는다.")]
        [SerializeField] private Transform worldAnchor;

        [Tooltip("기준점에서 픽셀 단위로 밀어낼 양. 아이콘이 기준점을 가릴 때 쓴다.")]
        [SerializeField] private Vector2 screenOffset;

        [Tooltip("월드 좌표를 화면으로 옮길 카메라. 비우면 Camera.main 을 쓴다.")]
        [SerializeField] private Camera worldCamera;

        /// <summary>
        /// 이 위젯이 붙은 캔버스. <c>GetComponentInParent</c> 를 매 프레임 부르지 않으려고 잡아 둔다.
        /// </summary>
        private Canvas cachedCanvas;

        /// <summary>1초마다 갱신한다. 타이머가 초 단위라 그보다 자주 돌 이유가 없다.</summary>
        private float nextRefreshTime;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
                button.onClick.AddListener(OnClickClaim);
        }

        private void OnEnable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged -= Refresh;
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClickClaim);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + 1f;
            Refresh();
        }

        /// <summary>
        /// 기준점을 따라간다. <b>LateUpdate 여야 한다</b> — 카메라가 이번 프레임에 움직였다면
        /// Update 에서 잡은 화면 좌표는 이미 낡은 값이고, 그러면 위젯이 한 프레임씩 끌려다닌다.
        /// </summary>
        private void LateUpdate()
        {
            if (worldAnchor == null)
                return;

            FollowWorldAnchor();
        }

        /// <summary>
        /// 월드 위치를 부모 RectTransform 의 좌표로 옮긴다.
        ///
        /// <b>Overlay 캔버스는 uiCamera 가 null 이어야 한다.</b> 로비 캔버스가 Overlay 인데
        /// 여기에 카메라를 넘기면 좌표가 화면 밖으로 튄다 — 위젯이 안 보이는 것으로만 드러나서
        /// 원인을 찾기 어렵다. 그래서 캔버스 모드를 보고 정한다.
        /// </summary>
        private void FollowWorldAnchor()
        {
            Camera camera = worldCamera != null ? worldCamera : Camera.main;
            if (camera == null)
                return;

            var rect = transform as RectTransform;
            var parent = rect != null ? rect.parent as RectTransform : null;
            if (parent == null)
                return;

            if (cachedCanvas == null)
                cachedCanvas = rect.GetComponentInParent<Canvas>();

            Camera uiCamera = cachedCanvas != null && cachedCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? cachedCanvas.worldCamera
                : null;

            Vector3 screenPoint = camera.WorldToScreenPoint(worldAnchor.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPoint, uiCamera, out Vector2 localPoint))
            {
                rect.anchoredPosition = localPoint + screenOffset;
            }
        }

        private void OnClickClaim()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[고기축제] IdleRewardManager 가 없다.");
                return;
            }

            // TryClaimMeat 이 지급과 타이머 되감기를 같이 한다. 여기서 세트 수를 다시
            // 세어 판단하지 않는다 — 그 사이에 한 세트가 더 구워지면 값이 갈린다.
            if (!manager.TryClaimMeat(out int meatAmount, out int setCount))
            {
                Refresh();
                return;
            }

            int sweeps = MeatFestivalRules.SweepCount(meatAmount, OJ.Stage.SweepRules.StaminaCostPerSweep);
            Debug.Log($"[고기축제] {setCount}세트 수령 | 고기 +{meatAmount} (소탕 {sweeps}회분)");

            Refresh();
            ShowRewardPopup(meatAmount);
        }

        /// <summary>
        /// 획득 팝업. 다른 보상과 같은 <c>UIRewardResultDialog</c> 를 쓴다 — 위젯에서 바로 받는 구조라
        /// 팝업이 없으면 숫자가 0 으로 돌아가는 것 말고는 받았다는 표시가 없었다.
        /// 지급은 이미 끝났으므로 못 열어도 로그만 남긴다.
        /// </summary>
        private static void ShowRewardPopup(int meatAmount)
        {
            if (meatAmount <= 0)
                return;

            var rewards = new[] { new PointRewardEntry(PointType.Stamina, meatAmount) };

            UIRewardResultDialog dialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (dialog == null)
            {
                Debug.LogError("[고기축제] UIRewardResultDialog 를 못 열었다. 지급은 이미 끝났다 — 고기 +" + meatAmount);
                return;
            }

            dialog.Open(rewards, "고기 축제에서 고기를 받았습니다.");
        }

        private void Refresh()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            int setCount = manager != null ? manager.GetStoredMeatSetCount() : 0;
            bool canClaim = MeatFestivalRules.CanClaim(setCount);
            bool full = MeatFestivalRules.IsFull(setCount, IdleRewardManager.MaxMeatSetCount);

            if (button != null)
                button.interactable = canClaim;

            if (redDot != null)
                redDot.SetActive(canClaim);

            if (countText != null)
            {
                // <b>세트가 아니라 고기 개수를 찍는다.</b> 세트는 내부 단위일 뿐이라
                // "x3" 을 본 유저가 고기 90개를 떠올릴 방법이 없다 — 소탕 한 번이 5개인데
                // 화면의 3 과는 아무 관계가 없어서, 받기 전에 몇 번 돌 수 있는지 셀 수 없다.
                countText.SetText("x{0}", MeatFestivalRules.TotalMeat(setCount, IdleRewardManager.MeatPerSet));
                countText.color = full ? FullCountColor : NormalCountColor;
            }

            if (timerText == null)
                return;

            if (full)
            {
                // 가득 차면 타이머가 의미가 없다 — 더 구워도 버려지므로 그 사실을 적는다.
                timerText.SetText("가득");
                timerText.color = FullCountColor;
                return;
            }

            timerText.color = TimerColor;
            timerText.SetText(FormatTime(manager != null
                ? manager.GetTimeUntilNextMeatSet()
                : System.TimeSpan.Zero));
        }

        private static string FormatTime(System.TimeSpan time)
        {
            if (time.TotalSeconds <= 0d)
                return "00:00";

            int totalMinutes = Mathf.FloorToInt((float)time.TotalMinutes);
            return string.Format("{0:00}:{1:00}", totalMinutes, time.Seconds);
        }

        // ── 에디터 도구용 ────────────────────────────────────────────

        /// <summary>
        /// 기준점을 물리고 그 자리로 즉시 옮긴다. <c>MeatFestivalLobbyInstaller</c> 가 부른다.
        ///
        /// <b>에디터에서는 LateUpdate 가 안 돈다.</b> 그래서 기준점만 물려 두면 플레이를
        /// 눌러야 위젯이 움직이고, 그 사이에는 자리가 틀린 것처럼 보인다. 설치 직후 한 번
        /// 맞춰 두면 씬 화면에서 바로 확인할 수 있다.
        /// </summary>
        public void BindWorldAnchor(Transform anchor)
        {
            worldAnchor = anchor;
            cachedCanvas = null;

            if (worldAnchor != null)
                FollowWorldAnchor();
        }

        /// <summary>물려 둔 기준점. 도구가 이미 물렸는지 볼 때 쓴다.</summary>
        public Transform WorldAnchor => worldAnchor;

        // ── 조립 (에디터 도구가 부른다) ──────────────────────────────

        /// <summary>
        /// 위젯을 코드로 짓는다. <c>UITowerEntryButton.Create</c> 와 같은 방식이다 —
        /// 손으로 만들면 위치·색·크기를 옮겨 적어야 하는데, 정확한 값을 아는 것은 코드다.
        ///
        /// <b>델리게이트는 여기서 붙이지 않는다.</b> <see cref="Awake"/> 가 붙인다.
        /// </summary>
        public static UIMeatFestivalLobbyButton Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = NewRect("UIMeatFestivalLobbyButton", parent);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(192f, 192f);

            var background = root.AddComponent<Image>();
            background.color = new Color(0.10f, 0.15f, 0.29f, 0.85f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = background;

            var widget = root.AddComponent<UIMeatFestivalLobbyButton>();
            widget.button = button;

            Sprite meatIcon = PointRewardUtility.GetPointIcon(PointType.Stamina);
            if (meatIcon != null)
            {
                GameObject iconObject = NewRect("Icon", root.transform);
                var icon = iconObject.AddComponent<Image>();
                icon.sprite = meatIcon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SetAnchored(iconObject.GetComponent<RectTransform>(), new Vector2(0f, 24f), new Vector2(96f, 96f));
            }

            widget.countText = NewLabel("Count", root.transform, "x0", 40f, font, NormalCountColor);
            SetAnchored(widget.countText.rectTransform, new Vector2(0f, -40f), new Vector2(170f, 50f));

            widget.timerText = NewLabel("Timer", root.transform, "00:00", 26f, font, TimerColor);
            SetAnchored(widget.timerText.rectTransform, new Vector2(0f, -78f), new Vector2(170f, 36f));

            GameObject dot = NewRect("RedDot", root.transform);
            var dotImage = dot.AddComponent<Image>();
            dotImage.color = new Color(0.92f, 0.22f, 0.22f, 1f);
            dotImage.raycastTarget = false;
            SetAnchored(dot.GetComponent<RectTransform>(), new Vector2(76f, 76f), new Vector2(28f, 28f));
            widget.redDot = dot;

            return widget;
        }

        private static GameObject NewRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static TMP_Text NewLabel(
            string name, Transform parent, string value, float size, TMP_FontAsset font, Color color)
        {
            GameObject gameObject = NewRect(name, parent);
            var text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
            return text;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
