using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Bounty;
using OJ.DI;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 신화 소환 연출. 화면 전체에 신화 아이콘을 크게 띄우고 아래에 "{이름} 소환" 을 적은 뒤,
    /// 아이콘이 작아지며 <b>그 다이스의 자리로 날아 들어가고</b> 도착하는 순간 기존 신화가 나올 때의
    /// 움직임·파티클(<see cref="DiceFx"/>)이 터진다.
    ///
    /// <b>시간은 실제 시간이다.</b> 배속·일시정지를 타지 않는다(<c>DialogBase</c> 연출과 같은 규약) —
    /// 3배속으로 놀던 사람에게도 같은 길이로 보여야 한다.
    ///
    /// <b>화면 전체가 입력을 막는다.</b> 연출 동안 보드가 바뀌어 도착 자리가 틀어지는 일을 막는다.
    /// 소환된 다이스는 <c>TryCraftMythic</c> 이 이미 신화로 바꾸고 숨겨 둔 상태이고, 이 창이 도착할 때
    /// 드러낸다. 도중에 창이 사라져도 다이스가 숨은 채로 남지 않게 <see cref="OnExit"/> 가 드러낸다.
    /// </summary>
    public sealed class UIMythicSummonCinematic : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Image dim;
        [SerializeField] private Image bigIcon;
        [SerializeField] private TMP_Text caption;

        private const float IntroTime = 0.35f;
        private const float HoldTime = 0.6f;
        private const float FlyTime = 0.45f;
        private const float BigSize = 440f;

        private UIDice pending;
        private int sequence;

        /// <summary>연출을 시작한다. <paramref name="target"/> 은 숨겨진 채 소환된 신화 다이스다.</summary>
        public void Play(DiceType king, UIDice target)
        {
            pending = target;
            Enter();
            RunAsync(king, target, ++sequence).Forget();
        }

        protected override void OnExit()
        {
            sequence++;
            RevealQuietly();
        }

        private void RevealQuietly()
        {
            if (pending == null)
                return;

            CanvasGroup group = pending.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = 1f;

            pending = null;
        }

        private async UniTaskVoid RunAsync(DiceType king, UIDice target, int mySequence)
        {
            bigIcon.sprite = DiceMetaDataProvider.GetIcon(king, 1);
            bigIcon.enabled = bigIcon.sprite != null;
            caption.text = UIShopCardSlot.NameOf(king) + " 소환";

            var iconRect = bigIcon.rectTransform;
            iconRect.sizeDelta = new Vector2(BigSize, BigSize);
            iconRect.anchoredPosition = new Vector2(0f, 90f);

            SetAlpha(dim, 0f);
            SetAlpha(bigIcon, 0f);
            caption.alpha = 0f;
            iconRect.localScale = Vector3.one * 0.3f;

            // 1. 나타난다.
            for (float t = 0f; t < IntroTime;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / IntroTime);

                SetAlpha(dim, 0.8f * UIEase.OutQuad(k));
                SetAlpha(bigIcon, UIEase.OutQuad(k));
                caption.alpha = UIEase.OutQuad(k);
                iconRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.3f, 1f, UIEase.OutBack(k));
            }

            // 2. 잠깐 보여 준다.
            for (float t = 0f; t < HoldTime;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
            }

            // 3. 작아지며 자리로 날아간다.
            Vector2 from = iconRect.anchoredPosition;
            Vector2 to = TargetLocalPosition(target, from);
            float targetScale = TargetScale(target);

            for (float t = 0f; t < FlyTime;)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (Dead(mySequence)) return;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / FlyTime);
                float ease = k * k;

                iconRect.anchoredPosition = Vector2.LerpUnclamped(from, to, ease);
                iconRect.localScale = Vector3.one * Mathf.Lerp(1f, targetScale, ease);
                SetAlpha(dim, 0.8f * (1f - k));
                caption.alpha = 1f - Mathf.Clamp01(k * 2.5f);
            }

            // 4. 도착 — 다이스를 드러내고 기존 신화 연출을 터뜨린다.
            UIDice arrived = pending;
            pending = null;
            if (arrived != null && battle != null && battle.Merge != null)
                battle.Merge.RevealMythic(arrived);
            else if (arrived != null)
            {
                CanvasGroup group = arrived.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = 1f;
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

        /// <summary>대상 다이스의 화면 위치를 이 창 안의 좌표로 바꾼다. 못 구하면 <paramref name="fallback"/>.</summary>
        private Vector2 TargetLocalPosition(UIDice target, Vector2 fallback)
        {
            if (target == null)
                return fallback;

            Canvas diceCanvas = target.GetComponentInParent<Canvas>();
            Camera diceCamera = diceCanvas != null && diceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? (diceCanvas.worldCamera != null ? diceCanvas.worldCamera : Camera.main)
                : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(diceCamera, target.transform.position);

            var space = bigIcon.rectTransform.parent as RectTransform;
            Canvas myCanvas = space != null ? space.GetComponentInParent<Canvas>() : null;
            Camera myCamera = myCanvas != null && myCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? myCanvas.worldCamera
                : null;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, myCamera, out Vector2 local)
                ? local
                : fallback;
        }

        private static float TargetScale(UIDice target)
        {
            var rect = target != null ? target.transform as RectTransform : null;
            float size = rect != null ? Mathf.Max(rect.rect.width, 60f) : 110f;
            return Mathf.Clamp(size / BigSize, 0.1f, 0.5f);
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        /// <summary>에디터 굽기 전용.</summary>
        public static UIMythicSummonCinematic Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIMythicSummonCinematic", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIMythicSummonCinematic>();
            dialog.dialogView = view;

            // 백키로 닫지 않는다 — 짧은 연출이고, 도중에 닫히면 다이스가 숨은 채로 남는 경로가 생긴다.
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);

            // 화면 전체를 덮어 입력을 막는다.
            dialog.dim = UIBountyUIFactory.CreateImage("Dim", view.transform, new Color(0f, 0f, 0f, 0.8f));
            UIBountyUIFactory.Stretch(dialog.dim.rectTransform);
            dialog.dim.raycastTarget = true;

            dialog.bigIcon = UIBountyUIFactory.CreateImage("BigIcon", view.transform, Color.white);
            UIBountyUIFactory.SetRect(dialog.bigIcon.rectTransform, new Vector2(BigSize, BigSize), new Vector2(0f, 90f));
            dialog.bigIcon.preserveAspect = true;
            dialog.bigIcon.raycastTarget = false;

            dialog.caption = UIBountyUIFactory.CreateText("Caption", view.transform, "신화 다이스 소환", 72f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(dialog.caption.rectTransform, new Vector2(960f, 110f), new Vector2(0f, -250f));

            return dialog;
        }
    }
}
