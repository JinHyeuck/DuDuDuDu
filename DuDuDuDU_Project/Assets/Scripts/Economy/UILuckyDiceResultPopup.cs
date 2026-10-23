using Cysharp.Threading.Tasks;
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
    /// 행운 주사위 결과 연출. <b>신화 다이스가 나올 때처럼 화면 전체</b>를 어둡게 덮고 마석 아이콘을 크게 띄운다.
    /// 숫자가 빠르게 돌다가 결과에서 멈추고(높은 눈은 "대박!"), 잠깐 보여 준 뒤 <b>보유 마석 칸으로 날아 들어간다</b>.
    ///
    /// <b>굴림은 이 창이 뜨기 전에 이미 끝나 있다</b>(SP 차감·마석 지급). 이 창은 결과를 보여 주기만 하므로
    /// 도중에 닫혀도 잃는 것이 없다. 화면을 한 번 누르면 숫자 돌리기·멈춤을 건너뛰고 곧바로 날아간다.
    ///
    /// <b>시간은 실제 시간이다.</b> 배속·일시정지를 타지 않는다(<see cref="UIMythicSummonCinematic"/> 과 같은 규약).
    /// 화면 전체가 입력을 막는다 — 연출 동안 연달아 굴려 창이 겹치는 일을 막는다.
    /// </summary>
    public sealed class UILuckyDiceResultPopup : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Image dim;
        [SerializeField] private Button skipButton;
        [SerializeField] private Image bigIcon;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text caption;

        private const float IntroTime = 0.3f;
        private const float SpinTime = 0.85f;
        private const float SpinStep = 0.05f;
        private const float HoldTime = 0.9f;
        private const float FlyTime = 0.45f;
        private const float BigSize = 380f;

        private static readonly Color NormalColor = Color.white;
        private static readonly Color GoodColor = new Color(1f, 0.92f, 0.4f, 1f);
        private static readonly Color GreatColor = new Color(1f, 0.6f, 0.2f, 1f);

        /// <summary>연출이 돌고 있는가. 튜토리얼이 연출이 끝나기를 기다릴 때 읽는다.</summary>
        public static bool IsPlaying { get; private set; }

        private int sequence;
        private bool skip;

        // 이미 지급된 마석을 이 창이 보여 주는 동안 보유 마석 칸의 숫자에서 쥐어 둔 몫. 닿는 순간 푼다.
        private int heldStones;

        protected override void OnLoad()
        {
            skipButton.onClick.AddListener(() => skip = true);
        }

        /// <summary>연출을 시작한다. <paramref name="stones"/> 는 이미 지급된 마석 개수.</summary>
        public void Play(int stones)
        {
            ReleaseHeldStones();

            // 데이터는 이미 올랐다. 화면 숫자만 마석이 칸에 닿을 때까지 기다리게 한다.
            if (stones > 0 && battle != null && battle.ElementUpgrade != null)
            {
                heldStones = stones;
                battle.ElementUpgrade.HoldStone(stones);
            }

            Enter();
            RunAsync(stones, ++sequence).Forget();
        }

        protected override void OnEnter()
        {
            IsPlaying = true;
            skip = false;
            transform.SetAsLastSibling();
        }

        protected override void OnExit()
        {
            sequence++;
            IsPlaying = false;
            ReleaseHeldStones();
        }

        protected override void OnUnload()
        {
            IsPlaying = false;
            ReleaseHeldStones();
        }

        // 몇 번 불려도 한 번만 푼다. 도중에 닫혀도(판 종료·되돌리기) 숫자가 영영 모자란 채 남지 않게 한다.
        private void ReleaseHeldStones()
        {
            if (heldStones <= 0)
                return;

            int amount = heldStones;
            heldStones = 0;
            battle?.ElementUpgrade?.ReleaseStone(amount);
        }

        private async UniTaskVoid RunAsync(int stones, int mySequence)
        {
            bigIcon.sprite = BattlePointUtility.GetIcon(BattlePointType.EnhanceStone);
            bigIcon.enabled = bigIcon.sprite != null;
            caption.text = "행운 주사위";
            numberText.text = "?";
            numberText.color = NormalColor;

            var iconRect = bigIcon.rectTransform;
            var numberRect = numberText.rectTransform;
            iconRect.sizeDelta = new Vector2(BigSize, BigSize);
            iconRect.anchoredPosition = new Vector2(0f, 250f);
            iconRect.localRotation = Quaternion.identity;
            numberRect.anchoredPosition = new Vector2(0f, -70f);

            SetAlpha(dim, 0f);
            SetAlpha(bigIcon, 0f);
            numberText.alpha = 0f;
            caption.alpha = 0f;
            iconRect.localScale = Vector3.one * 0.3f;
            numberRect.localScale = Vector3.one;

            // 1. 나타난다.
            for (float t = 0f; t < IntroTime;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / IntroTime);

                SetAlpha(dim, 0.8f * UIEase.OutQuad(k));
                SetAlpha(bigIcon, UIEase.OutQuad(k));
                numberText.alpha = UIEase.OutQuad(k);
                caption.alpha = UIEase.OutQuad(k);
                iconRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, UIEase.OutBack(k));
            }

            // 2. 숫자가 돈다. 아이콘은 데굴데굴 흔들린다.
            float nextSwap = 0f;
            for (float t = 0f; t < SpinTime && !skip;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;

                if (t >= nextSwap)
                {
                    nextSwap = t + SpinStep;
                    numberText.text = Random.Range(LuckyDiceRules.MinStones, LuckyDiceRules.MaxStones + 1).ToString();
                }

                iconRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 38f) * 14f);
            }

            // 3. 멈춘다. 결과를 크게 터뜨린다.
            iconRect.localRotation = Quaternion.identity;
            bool great = stones >= 8;
            bool good = stones >= 5;
            numberText.text = "+" + stones;
            numberText.color = great ? GreatColor : (good ? GoodColor : NormalColor);
            caption.text = great ? "대박!" : "마석 획득!";

            float landTime = 0.3f;
            for (float t = 0f; t < landTime;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / landTime);
                numberRect.localScale = Vector3.one * Mathf.LerpUnclamped(1.8f, 1f, UIEase.OutBack(k));
                iconRect.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            }

            numberRect.localScale = Vector3.one;
            iconRect.localScale = Vector3.one;

            // 4. 잠깐 보여 준다. 누르면 넘어간다.
            skip = false;
            for (float t = 0f; t < HoldTime && !skip;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
            }

            // 5. 작아지며 마석 수가 있는 칸으로 날아간다.
            Vector2 from = iconRect.anchoredPosition;
            Vector2 to = TargetLocalPosition(from);
            Vector2 numberFrom = numberRect.anchoredPosition;

            for (float t = 0f; t < FlyTime;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / FlyTime);
                float ease = k * k;

                iconRect.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
                iconRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, ease);
                numberRect.anchoredPosition = Vector2.LerpUnclamped(numberFrom, to, ease);
                numberRect.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, ease);
                SetAlpha(dim, 0.8f * (1f - k));
                caption.alpha = 1f - Mathf.Clamp01(k * 2.5f);
                numberText.alpha = 1f - Mathf.Clamp01((k - 0.6f) * 2.5f);
            }

            // 닿았다 — 칸이 튕기고 "+N" 이 뜨며 숫자가 오른다.
            RectTransform stoneRect = battle != null && battle.ElementUpgrade != null ? battle.ElementUpgrade.StoneRect : null;
            if (stoneRect != null && stones > 0)
            {
                UIRewardFly.Punch(stoneRect);
                UIRewardFly.FloatText(stoneRect, "+" + stones, numberText, GoodColor);
            }

            Exit();
        }

        private bool Dead(int mySequence)
        {
            return this == null || mySequence != sequence;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        /// <summary>
        /// 보유 마석 칸(없으면 강화 칸)의 화면 위치를 이 창 안의 좌표로 바꾼다. 못 구하면 <paramref name="fallback"/>.
        /// 대상이 다른 캔버스(카메라 캔버스)에 있어 화면 좌표를 거쳐 건넌다.
        /// </summary>
        private Vector2 TargetLocalPosition(Vector2 fallback)
        {
            RectTransform target = battle != null && battle.ElementUpgrade != null ? battle.ElementUpgrade.StoneRect : null;
            if (target == null)
                target = battle != null && battle.ElementUpgrade != null ? battle.ElementUpgrade.UpgradeButtonRect : null;
            if (target == null)
                return fallback;

            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera targetCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main)
                : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, target.position);

            var space = bigIcon.rectTransform.parent as RectTransform;
            Canvas myCanvas = space != null ? space.GetComponentInParent<Canvas>() : null;
            Camera myCamera = myCanvas != null && myCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? myCanvas.worldCamera
                : null;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, myCamera, out Vector2 local)
                ? local
                : fallback;
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        /// <summary>에디터 굽기 전용.</summary>
        public static UILuckyDiceResultPopup Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UILuckyDiceResultPopup", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UILuckyDiceResultPopup>();
            dialog.dialogView = view;

            // 백키로 닫지 않는다 — 짧은 연출이고 결과는 이미 지급돼 있다.
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            // 화면 전체를 덮어 입력을 막는다. 누르면 연출을 건너뛴다.
            dialog.dim = UIBountyUIFactory.CreateImage("Dim", view.transform, new Color(0f, 0f, 0f, 0.8f));
            UIBountyUIFactory.Stretch(dialog.dim.rectTransform);
            dialog.dim.raycastTarget = true;
            dialog.skipButton = dialog.dim.gameObject.AddComponent<Button>();
            dialog.skipButton.targetGraphic = dialog.dim;
            dialog.skipButton.transition = Selectable.Transition.None;

            dialog.bigIcon = UIBountyUIFactory.CreateImage("BigIcon", view.transform, Color.white);
            UIBountyUIFactory.SetRect(dialog.bigIcon.rectTransform, new Vector2(BigSize, BigSize), new Vector2(0f, 250f));
            dialog.bigIcon.preserveAspect = true;
            dialog.bigIcon.raycastTarget = false;

            dialog.numberText = UIBountyUIFactory.CreateText("Number", view.transform, "+7", 180f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.numberText.rectTransform, new Vector2(900f, 230f), new Vector2(0f, -70f));

            dialog.caption = UIBountyUIFactory.CreateText("Caption", view.transform, "마석 획득!", 72f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.caption.rectTransform, new Vector2(960f, 110f), new Vector2(0f, -270f));

            return dialog;
        }
    }
}
