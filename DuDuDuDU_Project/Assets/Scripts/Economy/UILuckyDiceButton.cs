using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Battle;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Economy
{
    /// <summary>
    /// 행운 주사위 버튼 — 소환 버튼과 확률업 사이. 100 SP 를 내고 굴리면 마석 1~10개가 나온다.
    /// 옆의 "?" 를 누르면 확률표(<see cref="UILuckyDiceOddsTooltip"/>)가 열린다.
    ///
    /// 굴림은 눌린 순간 이미 끝나 있다(SP 차감·마석 지급). 숫자가 빠르게 도는 것은 <b>연출</b>이고,
    /// 도는 동안 다시 눌러도 막지 않는다 — 막으면 "눌렀는데 안 된다" 로 보인다.
    /// 되돌리기 등으로 SP 가 바뀌어도 어긋나지 않도록 상태는 매 프레임 읽는다.
    /// </summary>
    public sealed class UILuckyDiceButton : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button button;
        [SerializeField] private Button helpButton;
        [SerializeField] private RectTransform bobTarget;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject bubble;
        [SerializeField] private TMP_Text bubbleText;
        [SerializeField] private Image bubbleStone;
        [SerializeField] private Image chipBackground;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite blockedSprite;

        private static readonly Color DimColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color ResultColor = new Color(1f, 0.92f, 0.4f, 1f);

        private const float SpinTime = 0.7f;
        private const float SpinStep = 0.05f;
        private const float ResultHold = 1.3f;
        private const float PopTime = 0.3f;

        private int result;
        private float rollAge = -1f;
        private float nextSpin;
        private float popElapsed = PopTime;
        private bool canRoll;
        private int shownKind = -1;

        private LuckyDiceManager listening;

        protected override void OnLoad()
        {
            button.onClick.AddListener(OnClick);
            helpButton.onClick.AddListener(OnHelp);
        }

        protected override void OnEnter()
        {
            shownKind = -1;
            rollAge = -1f;
            popElapsed = PopTime;

            if (chipText != null)
                chipText.SetText(LuckyDiceRules.CostSp + " SP");

            if (bubbleStone != null && bubbleStone.sprite == null)
            {
                bubbleStone.sprite = BattlePointUtility.GetIcon(BattlePointType.EnhanceStone);
                bubbleStone.enabled = bubbleStone.sprite != null;
            }

            Unlisten();
            listening = battle != null && battle.Game != null ? battle.Game.LuckyDice : null;
            if (listening != null)
                listening.OnRolled += OnRolled;

            bubble.SetActive(false);
            Refresh();
        }

        protected override void OnExit()
        {
            Unlisten();
            GameContainer.UI?.Hide<UILuckyDiceOddsTooltip>();
        }

        protected override void OnUnload()
        {
            Unlisten();
        }

        private void Unlisten()
        {
            if (listening != null)
                listening.OnRolled -= OnRolled;

            listening = null;
        }

        private void Update()
        {
            if (!_isEnter)
                return;

            Refresh();
            AnimateRoll();
            AnimateBob();
        }

        private void Refresh()
        {
            LuckyDiceManager dice = battle != null && battle.Game != null ? battle.Game.LuckyDice : null;
            canRoll = dice != null && dice.CanRoll;

            int kind = canRoll ? 0 : 1;
            if (kind == shownKind)
                return;

            shownKind = kind;
            icon.color = canRoll ? Color.white : DimColor;
            chipBackground.sprite = canRoll ? readySprite : blockedSprite;
            button.interactable = canRoll;
        }

        private void OnClick()
        {
            if (listening == null)
                return;

            listening.TryRoll();
        }

        private void OnHelp()
        {
            GameContainer.UI?.Show<UILuckyDiceOddsTooltip>();
        }

        private void OnRolled(int stones)
        {
            result = stones;
            rollAge = 0f;
            nextSpin = 0f;
            popElapsed = 0f;
            bubble.SetActive(true);
            bubbleText.color = Color.white;
        }

        private void AnimateRoll()
        {
            if (rollAge < 0f)
                return;

            rollAge += Time.unscaledDeltaTime;

            if (rollAge < SpinTime)
            {
                if (rollAge >= nextSpin)
                {
                    nextSpin = rollAge + SpinStep;
                    bubbleText.SetText("+" + Random.Range(LuckyDiceRules.MinStones, LuckyDiceRules.MaxStones + 1));
                }

                return;
            }

            if (rollAge < SpinTime + ResultHold)
            {
                bubbleText.SetText("+" + result);
                bubbleText.color = ResultColor;
                return;
            }

            rollAge = -1f;
            bubble.SetActive(false);
        }

        private void AnimateBob()
        {
            if (bobTarget == null)
                return;

            float y = 0f;
            float scale = 1f;
            float angle = 0f;

            // 굴리는 동안은 데굴데굴 흔들고, 결과가 나온 순간 한 번 부푼다.
            if (rollAge >= 0f && rollAge < SpinTime)
                angle = Mathf.Sin(rollAge * 40f) * 14f;

            if (popElapsed < PopTime)
            {
                popElapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(popElapsed / PopTime);
                scale = 1f + 0.2f * Mathf.Sin(k * Mathf.PI);
            }

            bobTarget.anchoredPosition = new Vector2(0f, y);
            bobTarget.localScale = Vector3.one * scale;
            bobTarget.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        private static readonly Vector2 Size = new Vector2(125f, 190f);
        private static readonly Vector2 Position = new Vector2(757.5f, 100f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UILuckyDiceButton Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UILuckyDiceButton", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 0f);
            rootRect.anchorMax = new Vector2(0f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Size;
            rootRect.anchoredPosition = Position;

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UILuckyDiceButton>();
            dialog.dialogView = view;
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            Image hit = UIBountyUIFactory.CreateImage("Hit", view.transform, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.Stretch(hit.rectTransform);
            dialog.button = hit.gameObject.AddComponent<Button>();
            dialog.button.targetGraphic = hit;
            dialog.button.transition = Selectable.Transition.None;

            GameObject bob = UIBountyUIFactory.CreateRect("Bob", view.transform);
            UIBountyUIFactory.SetRect(bob.GetComponent<RectTransform>(), new Vector2(110f, 110f), new Vector2(0f, 38f));
            dialog.bobTarget = bob.GetComponent<RectTransform>();

            dialog.icon = CreateDiceFace(bob.transform);

            // 결과 말풍선 — 굴릴 때만 뜬다. 마석 아이콘 + "+N".
            GameObject bubble = UIBountyUIFactory.CreateRect("Bubble", view.transform);
            UIBountyUIFactory.SetRect(bubble.GetComponent<RectTransform>(), new Vector2(112f, 40f), new Vector2(0f, 112f));
            dialog.bubble = bubble;

            Image bubbleBg = UIBountyUIFactory.CreateSprite("BubbleBg", bubble.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f, new Vector2(112f + 26f, 40f + 26f), Vector2.zero, true);
            bubbleBg.color = new Color(0.25f, 0.18f, 0.12f, 0.95f);
            bubbleBg.raycastTarget = false;

            dialog.bubbleStone = UIBountyUIFactory.CreateImage("Stone", bubble.transform, Color.white);
            UIBountyUIFactory.SetRect(dialog.bubbleStone.rectTransform, new Vector2(32f, 32f), new Vector2(-34f, 0f));
            dialog.bubbleStone.preserveAspect = true;
            dialog.bubbleStone.raycastTarget = false;

            dialog.bubbleText = UIBountyUIFactory.CreateText("BubbleText", bubble.transform, "+7", 30f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.bubbleText.rectTransform, new Vector2(70f, 40f), new Vector2(18f, 0f));
            bubble.SetActive(false);

            dialog.nameText = UIBountyUIFactory.CreateText("Name", view.transform, "행운 주사위", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.nameText.rectTransform, new Vector2(Size.x + 20f, 34f), new Vector2(0f, -30f));

            dialog.readySprite = UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Green");
            dialog.blockedSprite = UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray");

            dialog.chipBackground = UIBountyUIFactory.CreateSprite("Chip", view.transform,
                dialog.readySprite, 3f, new Vector2(106f + 24f, 36f + 24f), new Vector2(0f, -66f), true);
            dialog.chipBackground.raycastTarget = false;

            dialog.chipText = UIBountyUIFactory.CreateText("ChipText", dialog.chipBackground.transform,
                LuckyDiceRules.CostSp + " SP", 26f, TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.chipText.rectTransform, new Vector2(106f, 36f), new Vector2(0f, 2f));

            // "?" — 확률표. 아이콘 오른쪽 위에 겹쳐 둔다(다이스 위에서 눈에 띄도록 노랑 칩).
            Image help = UIBountyUIFactory.CreateSprite("Help", view.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Yellow"), 3f, new Vector2(84f, 84f), new Vector2(40f, 70f), true);
            help.raycastTarget = true;
            dialog.helpButton = help.gameObject.AddComponent<Button>();
            dialog.helpButton.targetGraphic = help;

            TMP_Text mark = UIBountyUIFactory.CreateText("Mark", help.transform, "?", 40f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(mark.rectTransform, new Vector2(60f, 60f), new Vector2(0f, 2f));

            return dialog;
        }

        /// <summary>
        /// 주사위 눈금. 킷에 주사위 그림이 없어 크림색 상자 위에 다섯 점을 찍는다 —
        /// 둥근 점 스프라이트도 없으니 픽셀 아트 결에 맞게 네모 점이다.
        /// </summary>
        private static Image CreateDiceFace(Transform parent)
        {
            Image face = UIBountyUIFactory.CreateSprite("Icon", parent,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f, new Vector2(84f, 84f), Vector2.zero, true);
            face.color = new Color(1f, 0.97f, 0.85f, 1f);

            Color pip = new Color(0.45f, 0.28f, 0.15f, 1f);
            Vector2[] spots =
            {
                new Vector2(-17f, 17f), new Vector2(17f, 17f), new Vector2(0f, 0f),
                new Vector2(-17f, -17f), new Vector2(17f, -17f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                Image dot = UIBountyUIFactory.CreateImage("Pip" + i, face.transform, pip);
                dot.raycastTarget = false;
                UIBountyUIFactory.SetRect(dot.rectTransform, new Vector2(13f, 13f), spots[i]);
            }

            return face;
        }
    }
}
