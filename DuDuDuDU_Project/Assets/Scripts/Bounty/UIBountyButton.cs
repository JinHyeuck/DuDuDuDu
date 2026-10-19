using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 아이콘 버튼 — 하단 왼쪽(시작 버튼이 있던 자리). 세 가지 상태를 그린다.
    ///
    /// <list type="bullet">
    /// <item><b>Ready</b>: 쿨타임이 끝났다. "READY!" 말풍선이 뜨고 아이콘이 살짝 흔들리며 초록 READY 칩이 붙는다.
    ///   눌러서 선택 창을 연다 — <b>유저에게 다시 고르라고 알리는 인터랙션</b>이다.</item>
    /// <item><b>Open</b>: 선택 창이 열려 있다. 노란 OPEN 칩.</item>
    /// <item><b>Already</b>: 쿨타임 중이다. 아이콘이 어두워지고 남은 시간 "mm:ss" 를 보여 준다. 눌러도 아무 일이 없다.</item>
    /// </list>
    ///
    /// <b>여기에 판단을 두지 않는다.</b> 소환·쿨타임은 <see cref="BountyManager"/> 가 한다. 이 창은 그 상태를 읽어
    /// 그리고 눌림을 선택 창으로 넘길 뿐이다. 되돌리기가 쿨타임을 직접 고쳐도 어긋나지 않도록 <b>매 프레임
    /// 읽는다</b>(글자가 같으면 쓰지 않는다).
    ///
    /// <b>왜 다이얼로그인가.</b> 상시 HUD 라면 씬에 놓는 것이 자연스럽지만 <c>BattleScene.unity</c> 를 편집해야 해서
    /// (절대 규칙 3) <c>UIWaveRewindButton</c> 과 같은 방식으로 프리팹을 구워 카탈로그에 올린다.
    /// 루트는 아이콘 크기만 차지한다 — 화면 전체로 늘리면 보드 클릭을 먹는다.
    /// </summary>
    public class UIBountyButton : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button button;
        [SerializeField] private RectTransform bobTarget;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject bubble;
        [SerializeField] private Image chipBackground;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite openSprite;
        [SerializeField] private Sprite alreadySprite;

        private static readonly Color DimColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        private const float BobHeight = 8f;
        private const float BobSpeed = 6f;
        private const float PopTime = 0.3f;

        private BountyButtonState shown = (BountyButtonState)(-1);
        private string lastChipText;
        private float popElapsed = PopTime;
        private float time;

        /// <summary>지금 그려야 할 상태. 선택 창이 열려 있으면 Open, 아니면 쿨타임으로 가른다.</summary>
        public BountyButtonState CurrentState
        {
            get
            {
                if (UIBountySelectDialog.IsOpen)
                    return BountyButtonState.Open;

                return battle != null && battle.Bounty != null && battle.Bounty.IsReady
                    ? BountyButtonState.Ready
                    : BountyButtonState.Already;
            }
        }

        protected override void OnLoad()
        {
            button.onClick.AddListener(OnClick);
        }

        protected override void OnEnter()
        {
            shown = (BountyButtonState)(-1);
            lastChipText = null;
            popElapsed = PopTime;
            Refresh();
        }

        private void Update()
        {
            if (!_isEnter)
                return;

            time += Time.unscaledDeltaTime;
            Refresh();
            Animate();
        }

        private void Refresh()
        {
            if (battle == null || battle.Bounty == null)
                return;

            BountyButtonState state = CurrentState;

            if (state != shown)
            {
                // 쿨타임이 끝나 Ready 가 되는 순간을 짚는다 — 한 번 튀어 오른다.
                if (state == BountyButtonState.Ready && shown == BountyButtonState.Already)
                    popElapsed = 0f;

                shown = state;
                ApplyState(state);
            }

            if (state == BountyButtonState.Already)
                SetChipText(BountyFormula.FormatCooldown(battle.Bounty.CooldownRemaining));
        }

        private void ApplyState(BountyButtonState state)
        {
            bool ready = state == BountyButtonState.Ready;

            bubble.SetActive(ready);
            icon.color = state == BountyButtonState.Already ? DimColor : Color.white;
            button.interactable = ready;

            switch (state)
            {
                case BountyButtonState.Ready:
                    chipBackground.sprite = readySprite;
                    SetChipText("READY");
                    break;
                case BountyButtonState.Open:
                    chipBackground.sprite = openSprite;
                    SetChipText("OPEN");
                    break;
                default:
                    chipBackground.sprite = alreadySprite;
                    break;
            }
        }

        private void SetChipText(string text)
        {
            if (text == lastChipText)
                return;

            chipText.SetText(text);
            lastChipText = text;
        }

        /// <summary>Ready 일 때 아이콘이 위아래로 흔들리고, 막 Ready 가 된 순간에는 한 번 부푼다.</summary>
        private void Animate()
        {
            if (bobTarget == null)
                return;

            float y = shown == BountyButtonState.Ready ? Mathf.Sin(time * BobSpeed) * BobHeight : 0f;
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
            if (CurrentState != BountyButtonState.Ready)
                return;

            GameContainer.UI?.Show<UIBountySelectDialog>();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        // 시작 버튼이 있던 하단 왼쪽. 보드 아래 여백(약 205px) 안에 들어가는 크기다.
        private static readonly Vector2 Size = new Vector2(170f, 190f);
        private static readonly Vector2 Position = new Vector2(105f, 100f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBountyButton Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBountyButton", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Size;
            rootRect.anchoredPosition = Position;

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIBountyButton>();
            dialog.dialogView = view;
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            // 클릭 면적은 아이콘 전체. 투명 판이 받는다.
            Image hit = UIBountyUIFactory.CreateImage("Hit", view.transform, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.Stretch(hit.rectTransform);
            dialog.button = hit.gameObject.AddComponent<Button>();
            dialog.button.targetGraphic = hit;
            dialog.button.transition = Selectable.Transition.None;

            // 흔들리는 덩어리 — 아이콘과 말풍선.
            GameObject bob = UIBountyUIFactory.CreateRect("Bob", view.transform);
            UIBountyUIFactory.SetRect(bob.GetComponent<RectTransform>(), new Vector2(160f, 130f), new Vector2(0f, 34f));
            dialog.bobTarget = bob.GetComponent<RectTransform>();

            dialog.icon = UIBountyUIFactory.CreateImage("Icon", bob.transform, Color.white);
            UIBountyUIFactory.SetRect(dialog.icon.rectTransform, new Vector2(112f, 112f), new Vector2(0f, -8f));
            dialog.icon.sprite = UIBountyUIFactory.LoadSprite("Main/Icon_Beg");
            dialog.icon.preserveAspect = true;
            dialog.icon.raycastTarget = false;

            // "READY!" 말풍선 — 종이 카드와 같은 갈색 글자.
            GameObject bubble = UIBountyUIFactory.CreateRect("Bubble", bob.transform);
            UIBountyUIFactory.SetRect(bubble.GetComponent<RectTransform>(), new Vector2(124f, 40f), new Vector2(26f, 56f));
            dialog.bubble = bubble;

            Image bubbleBg = UIBountyUIFactory.CreateSprite("BubbleBg", bubble.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f, new Vector2(124f + 26f, 40f + 26f), Vector2.zero, true);
            bubbleBg.color = new Color(1f, 0.97f, 0.85f, 1f);
            bubbleBg.raycastTarget = false;

            TMP_Text bubbleText = UIBountyUIFactory.CreateText("BubbleText", bubble.transform, "READY!", 26f,
                TextAlignmentOptions.Center, new Color(0x86 / 255f, 0x57 / 255f, 0x31 / 255f, 1f), font, plainMaterial);
            UIBountyUIFactory.Stretch(bubbleText.rectTransform);

            dialog.nameText = UIBountyUIFactory.CreateText("Name", view.transform, "현상금", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.nameText.rectTransform, new Vector2(Size.x, 38f), new Vector2(0f, -30f));

            // 상태 칩 — Ready 초록 / Open 노랑 / Already 회색(남은 시간).
            dialog.readySprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            dialog.openSprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Yellow");
            dialog.alreadySprite = UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray");

            dialog.chipBackground = UIBountyUIFactory.CreateSprite("Chip", view.transform,
                dialog.readySprite, 3f, new Vector2(124f + 24f, 36f + 24f), new Vector2(0f, -66f), true);
            dialog.chipBackground.raycastTarget = false;

            dialog.chipText = UIBountyUIFactory.CreateText("ChipText", dialog.chipBackground.transform, "READY", 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.chipText.rectTransform, new Vector2(124f, 36f), new Vector2(0f, 2f));

            return dialog;
        }
    }
}
