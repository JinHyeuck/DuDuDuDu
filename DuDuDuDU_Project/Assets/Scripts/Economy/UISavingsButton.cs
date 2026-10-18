using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Battle;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.Hunting;
using OJ.UI;

namespace OJ.Economy
{
    /// <summary>
    /// 저금통 아이콘 버튼 — 현상금과 소환 버튼 사이. 누르면 <b>선택 창</b>(<see cref="UISavingsSelectDialog"/>)이 열린다.
    /// 방식이 둘이다(<see cref="SavingsMode"/>): 기본은 "지금 바로 / 45초 뒤" 선택, DEV 치트로 이전 3단 방식을 켤 수 있다.
    ///
    /// <list type="bullet">
    /// <item><b>Ready</b>: 고를 수 있다. 초록 READY 칩과 말풍선("선택!" / 3단은 "이자 받기"). 3단 방식에서 가장 싼 선택지(30 SP)도
    ///   못 내면 칩이 회색으로 필요한 금액을 보여 주고 눌러도 아무 일이 없다.</item>
    /// <item><b>Open</b>: 선택 창이 열려 있다. 노란 OPEN 칩.</item>
    /// <item><b>Saving</b>: 다음 선택(또는 지급)을 기다리는 중. 칩에 남은 시간을 mm:ss 로 센다. 받기로 한 SP 가 있으면 말풍선에 보인다.</item>
    /// <item><b>지급</b>: 코인이 아이콘에서 터져 나와 소환 버튼으로 빨려 든다(즉시 받을 때도 같다). 도착하면 소환 버튼이 한 번 튄다.</item>
    /// </list>
    ///
    /// <b>여기에 판단을 두지 않는다.</b> 고르고 지급하는 일은 <see cref="SavingsManager"/> 가 한다.
    /// 되돌리기가 <c>RunState</c> 를 직접 고쳐도 어긋나지 않도록 <b>매 프레임 읽어 그린다</b>(글자가 같으면 쓰지 않는다).
    /// </summary>
    public sealed class UISavingsButton : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button button;
        [SerializeField] private RectTransform bobTarget;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject bubble;
        [SerializeField] private TMP_Text bubbleText;
        [SerializeField] private Image chipBackground;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite savingSprite;
        [SerializeField] private Sprite blockedSprite;

        private static readonly Color DimColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private const float BobHeight = 8f;
        private const float BobSpeed = 6f;
        private const float PopTime = 0.3f;

        private const int CoinCount = 9;
        private const float BurstTime = 0.28f;
        private const float FlyTime = 0.5f;
        private const float CoinSize = 54f;

        private sealed class Coin
        {
            public RectTransform Rect;
            public Vector3 Burst;
            public float Delay;
            public float Age;
        }

        private readonly List<Coin> coins = new List<Coin>();

        private SavingsManager listening;
        private int shownKind = -1;
        private string lastChip;
        private string lastBubble;
        private string lastName;
        private float popElapsed = PopTime;
        private float time;

        protected override void OnLoad()
        {
            button.onClick.AddListener(OnClick);
        }

        protected override void OnEnter()
        {
            shownKind = -1;
            lastChip = null;
            lastBubble = null;
            lastName = null;
            popElapsed = PopTime;

            ClearCoins();
            Listen();
            Refresh();
        }

        protected override void OnExit()
        {
            Unlisten();
            ClearCoins();
        }

        protected override void OnUnload()
        {
            Unlisten();
        }

        private SavingsManager Savings => battle != null && battle.Game != null ? battle.Game.Savings : null;

        private void Listen()
        {
            Unlisten();

            listening = Savings;
            if (listening != null)
                listening.OnPaid += OnPaid;
        }

        private void Unlisten()
        {
            if (listening != null)
                listening.OnPaid -= OnPaid;

            listening = null;
        }

        private void Update()
        {
            if (!_isEnter)
                return;

            time += Time.unscaledDeltaTime;
            Refresh();
            Animate();
            UpdateCoins(Time.unscaledDeltaTime);
        }

        // 0 = Ready(누를 수 있음) · 1 = Ready(SP 부족) · 2 = 맡기는 중 · 3 = 선택 창 열림
        private void Refresh()
        {
            SavingsManager savings = Savings;
            if (savings == null)
                return;

            int kind;
            if (savings.State == SavingsState.Saving)
                kind = 2;
            else if (UISavingsSelectDialog.IsOpen)
                kind = 3;
            else
                kind = savings.CanStart ? 0 : 1;

            if (kind != shownKind)
            {
                if (kind == 0 && shownKind == 2)
                    popElapsed = 0f;

                shownKind = kind;
                ApplyKind(kind);
            }

            if (kind == 2)
            {
                SetChip(BountyFormula.FormatCooldown(savings.TimeRemaining));

                // 받기로 한 SP 가 있을 때만 말풍선을 띄운다(선택 방식에서 "지금 바로" 를 골랐다면 받을 것이 없다).
                bool hasPending = savings.PendingPayout > 0;
                if (bubble.activeSelf != hasPending)
                    bubble.SetActive(hasPending);
                if (hasPending)
                    SetBubble(savings.PendingPayout + " SP");
            }
            else if (kind == 3)
            {
                SetChip("OPEN");
            }
            else if (kind == 1)
            {
                SetChip(SavingsRules.CheapestDeposit + " SP");
            }
            else
            {
                SetChip("READY");
                SetBubble(savings.Mode == SavingsMode.Tiered ? "이자 받기" : "선택!");
            }

            SetName("저금통");
        }

        private void ApplyKind(int kind)
        {
            icon.color = kind == 1 ? DimColor : Color.white;
            button.interactable = kind == 0;
            bubble.SetActive(kind == 0 || kind == 2);

            switch (kind)
            {
                case 0:
                    chipBackground.sprite = readySprite;
                    break;
                case 2:
                case 3:
                    chipBackground.sprite = savingSprite;
                    break;
                default:
                    chipBackground.sprite = blockedSprite;
                    break;
            }
        }

        private void SetChip(string text)
        {
            if (text == lastChip)
                return;

            chipText.SetText(text);
            lastChip = text;
        }

        private void SetBubble(string text)
        {
            if (text == lastBubble)
                return;

            bubbleText.SetText(text);
            lastBubble = text;
        }

        private void SetName(string text)
        {
            if (text == lastName)
                return;

            nameText.SetText(text);
            lastName = text;
        }

        private void Animate()
        {
            if (bobTarget == null)
                return;

            float y = shownKind == 0 ? Mathf.Sin(time * BobSpeed) * BobHeight : 0f;
            bobTarget.anchoredPosition = new Vector2(0f, y);

            float scale = 1f;
            if (popElapsed < PopTime)
            {
                popElapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(popElapsed / PopTime);
                scale = 1f + 0.25f * Mathf.Sin(k * Mathf.PI);
            }

            bobTarget.localScale = Vector3.one * scale;
        }

        private void OnClick()
        {
            if (Savings != null && Savings.CanStart)
                GameContainer.UI?.Show<UISavingsSelectDialog>();
        }

        // ──────────────────────────────────────────────────────────────
        // 지급 연출: 코인이 터졌다가 소환 버튼으로 빨려 든다.
        // ──────────────────────────────────────────────────────────────

        private void OnPaid(int amount)
        {
            if (!_isEnter)
                return;

            Sprite sprite = BattlePointUtility.GetIcon(BattlePointType.SummonPoint);
            Vector3 origin = icon.transform.position;

            for (int i = 0; i < CoinCount; i++)
            {
                GameObject go = new GameObject("Coin", typeof(RectTransform));
                go.layer = icon.gameObject.layer;
                go.transform.SetParent(dialogView.transform, false);

                var image = go.AddComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                image.preserveAspect = true;

                var rect = go.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(CoinSize, CoinSize);
                rect.position = origin;

                float angle = (i / (float)CoinCount) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float radius = Random.Range(60f, 130f);
                float canvasScale = transform.lossyScale.x;

                coins.Add(new Coin
                {
                    Rect = rect,
                    Burst = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f, 0f) * radius * canvasScale,
                    Delay = i * 0.03f,
                });
            }
        }

        private void UpdateCoins(float dt)
        {
            if (coins.Count == 0)
                return;

            Button summon = battle != null && battle.Summon != null ? battle.Summon.summonButton : null;
            Vector3 target = summon != null ? ToLocalWorld(summon.transform) : icon.transform.position;
            Vector3 origin = icon.transform.position;

            bool arrived = false;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                Coin coin = coins[i];
                coin.Age += dt;

                float t = coin.Age - coin.Delay;
                if (t < 0f)
                    continue;

                if (t < BurstTime)
                {
                    float k = 1f - Mathf.Pow(1f - t / BurstTime, 3f);
                    coin.Rect.position = Vector3.Lerp(origin, coin.Burst, k);
                    continue;
                }

                float f = (t - BurstTime) / FlyTime;
                if (f >= 1f)
                {
                    Destroy(coin.Rect.gameObject);
                    coins.RemoveAt(i);
                    arrived = true;
                    continue;
                }

                float e = f * f;
                coin.Rect.position = Vector3.Lerp(coin.Burst, target, e);
                coin.Rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, e);
            }

            if (arrived && summon != null)
                SummonPop.Play(summon.transform);
        }

        /// <summary>
        /// 다른 캔버스에 있는 대상의 위치를 이 창 캔버스의 월드 좌표로 바꾼다.
        ///
        /// <b>월드 좌표를 그대로 쓰면 안 된다.</b> 이 창은 오버레이 캔버스(좌표가 곧 화면 픽셀)이고 소환 버튼은
        /// 카메라 캔버스(보드와 같은 월드)라 같은 숫자가 전혀 다른 자리를 가리킨다 — 코인이 엉뚱한 곳으로 날아갔다.
        /// 화면 좌표를 거쳐 건너간다(<c>UIMythicSummonCinematic</c> 이 도착 자리를 구하는 것과 같은 방식).
        /// </summary>
        private Vector3 ToLocalWorld(Transform target)
        {
            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main)
                : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, target.position);

            var space = dialogView.transform as RectTransform;
            Canvas myCanvas = space != null ? space.GetComponentInParent<Canvas>() : null;
            Camera myCamera = myCanvas != null && myCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? myCanvas.worldCamera
                : null;

            return space != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(space, screen, myCamera, out Vector3 world)
                ? world
                : target.position;
        }

        private void ClearCoins()
        {
            foreach (Coin coin in coins)
            {
                if (coin.Rect != null)
                    Destroy(coin.Rect.gameObject);
            }

            coins.Clear();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 Size = new Vector2(130f, 190f);
        private static readonly Vector2 Position = new Vector2(285f, 100f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UISavingsButton Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UISavingsButton", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Size;
            rootRect.anchoredPosition = Position;

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISavingsButton>();
            dialog.dialogView = view;
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            Image hit = UIBountyUIFactory.CreateImage("Hit", view.transform, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.Stretch(hit.rectTransform);
            dialog.button = hit.gameObject.AddComponent<Button>();
            dialog.button.targetGraphic = hit;
            dialog.button.transition = Selectable.Transition.None;

            GameObject bob = UIBountyUIFactory.CreateRect("Bob", view.transform);
            UIBountyUIFactory.SetRect(bob.GetComponent<RectTransform>(), new Vector2(130f, 130f), new Vector2(0f, 40f));
            dialog.bobTarget = bob.GetComponent<RectTransform>();

            dialog.icon = UIBountyUIFactory.CreateImage("Icon", bob.transform, Color.white);
            UIBountyUIFactory.SetRect(dialog.icon.rectTransform, new Vector2(88f, 88f), new Vector2(0f, -8f));
            dialog.icon.sprite = UIBountyUIFactory.LoadSprite("Main/Icon_Reward_Pig");
            dialog.icon.preserveAspect = true;
            dialog.icon.raycastTarget = false;

            // 말풍선 — 받을 금액. 갈색 글자(폰트에 화살표 글리프가 없어 "30→33" 대신 받을 금액만 적는다).
            GameObject bubble = UIBountyUIFactory.CreateRect("Bubble", bob.transform);
            UIBountyUIFactory.SetRect(bubble.GetComponent<RectTransform>(), new Vector2(112f, 38f), new Vector2(0f, 56f));
            dialog.bubble = bubble;

            Image bubbleBg = UIBountyUIFactory.CreateSprite("BubbleBg", bubble.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f, new Vector2(112f + 26f, 38f + 26f), Vector2.zero, true);
            bubbleBg.color = new Color(1f, 0.97f, 0.85f, 1f);
            bubbleBg.raycastTarget = false;

            dialog.bubbleText = UIBountyUIFactory.CreateText("BubbleText", bubble.transform, "이자 받기", 24f,
                TextAlignmentOptions.Center, new Color(0x86 / 255f, 0x57 / 255f, 0x31 / 255f, 1f), font, plainMaterial);
            UIBountyUIFactory.Stretch(dialog.bubbleText.rectTransform);

            dialog.nameText = UIBountyUIFactory.CreateText("Name", view.transform, "저금통", 28f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.nameText.rectTransform, new Vector2(Size.x + 20f, 34f), new Vector2(0f, -30f));

            dialog.readySprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            dialog.savingSprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Yellow");
            dialog.blockedSprite = UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray");

            dialog.chipBackground = UIBountyUIFactory.CreateSprite("Chip", view.transform,
                dialog.readySprite, 3f, new Vector2(106f + 24f, 36f + 24f), new Vector2(0f, -66f), true);
            dialog.chipBackground.raycastTarget = false;

            dialog.chipText = UIBountyUIFactory.CreateText("ChipText", dialog.chipBackground.transform, "READY", 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.chipText.rectTransform, new Vector2(106f, 36f), new Vector2(0f, 2f));

            return dialog;
        }
    }

    /// <summary>
    /// 소환 버튼이 한 번 튀는 연출. 코인이 도착할 때마다 부르면 겹쳐서 계속 튄다.
    /// 소환 버튼의 크기를 직접 고치므로 <b>끝나면 반드시 원래 크기로 돌려놓는다</b>.
    /// </summary>
    internal static class SummonPop
    {
        public static void Play(Transform target)
        {
            if (target == null)
                return;

            var pop = target.GetComponent<SummonPopBehaviour>();
            if (pop == null)
                pop = target.gameObject.AddComponent<SummonPopBehaviour>();

            pop.Restart();
        }
    }

    internal sealed class SummonPopBehaviour : MonoBehaviour
    {
        private const float Duration = 0.18f;
        private const float Amount = 0.12f;

        private Vector3 baseScale;
        private float elapsed = Duration;
        private bool hasBase;

        public void Restart()
        {
            if (!hasBase)
            {
                baseScale = transform.localScale;
                hasBase = true;
            }

            elapsed = 0f;
        }

        private void Update()
        {
            if (!hasBase || elapsed >= Duration)
                return;

            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / Duration);
            transform.localScale = baseScale * (1f + Amount * Mathf.Sin(k * Mathf.PI));

            if (elapsed >= Duration)
                transform.localScale = baseScale;
        }

        private void OnDisable()
        {
            if (hasBase)
                transform.localScale = baseScale;
        }
    }
}
