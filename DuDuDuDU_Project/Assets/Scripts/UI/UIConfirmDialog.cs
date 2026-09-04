using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.UI
{
    /// <summary>
    /// 예/아니오를 묻는 범용 확인 창.
    ///
    /// <b>왜 범용인가.</b> 전투 이탈 말고도 던전형 콘텐츠가 늘어나면 같은 모양의 질문이
    /// 계속 생긴다("포기할까요", "재도전할까요"). 그때마다 창을 새로 만들면 문구만 다른
    /// 프리팹이 쌓이고, 그중 하나에만 백키 처리가 빠지는 식으로 갈라진다.
    /// 그래서 <b>문구와 버튼 이름을 전부 인자로 받는다.</b>
    ///
    /// <b>이 창은 아무것도 결정하지 않는다.</b> "확인을 눌렀다"는 사실만 콜백으로 알린다.
    /// 무엇을 할지는 부르는 쪽이 안다 — 그것이 이 창이 어느 콘텐츠에나 붙을 수 있는 이유다.
    ///
    /// <b>재사용된다는 것을 잊지 말 것.</b> <see cref="UIService"/> 는 같은 타입의 인스턴스를
    /// 하나만 만들어 돌려쓴다. 그래서 닫을 때 콜백을 <b>반드시</b> 비운다 — 안 그러면
    /// 다음에 다른 곳에서 열었을 때 <b>지난번 확인 버튼이 같이 눌린다.</b>
    /// </summary>
    public sealed class UIConfirmDialog : DialogBase
    {
        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private TMP_Text cancelLabel;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action onConfirm;
        private Action onCancel;

        /// <summary>
        /// 문구를 넣고 연다.
        ///
        /// <paramref name="onCancel"/> 은 생략할 수 있다 — 취소는 "아무 일도 없음"이
        /// 대부분이라 매번 빈 람다를 쓰게 하는 것이 낭비다.
        /// </summary>
        public void Open(
            string title,
            string message,
            string confirmText,
            string cancelText,
            Action onConfirm,
            Action onCancel = null)
        {
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            SetText(titleText, title);
            SetText(messageText, message);
            SetText(confirmLabel, confirmText);
            SetText(cancelLabel, cancelText);

            Enter();
        }

        protected override void OnLoad()
        {
            // 델리게이트는 프리팹에 직렬화되지 않는다. 굽는 시점에 붙여 봐야 저장되지
            // 않으므로 최초 1회 여기서 붙인다. (10.5 에서 같은 이유로 겪은 자리다.)
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnClickConfirm);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnClickCancel);
        }

        /// <summary>
        /// 백키·바깥 클릭은 <b>취소</b>다. 그냥 닫기가 아니다 —
        /// 확인 창에서 "닫기"와 "취소"가 다른 뜻이면 그 차이를 아무도 설명할 수 없다.
        /// </summary>
        public override void BackKeyCall()
        {
            OnClickCancel();
        }

        private void OnClickConfirm()
        {
            // 콜백을 먼저 떼어 낸 뒤에 부른다. 콜백 안에서 이 창을 다시 열거나
            // 씬을 바꾸는 일이 흔한데, 그때 아래 Exit 가 <b>새로 설정된 콜백</b>을
            // 지워 버리면 다음 확인이 조용히 죽는다.
            Action callback = onConfirm;
            Clear();
            Exit();
            callback?.Invoke();
        }

        private void OnClickCancel()
        {
            Action callback = onCancel;
            Clear();
            Exit();
            callback?.Invoke();
        }

        private void Clear()
        {
            onConfirm = null;
            onCancel = null;
        }

        protected override void OnExit()
        {
            // 확인/취소를 거치지 않고 닫히는 경로(씬 전환 등)에서도 콜백이 남지 않게 한다.
            Clear();
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target == null)
                return;

            // 빈 문자열을 넣으면 그 줄이 자리만 차지한다. 제목 없는 확인 창을 위해
            // 아예 끈다 — 레이아웃 그룹이 알아서 접힌다.
            bool has = !string.IsNullOrEmpty(value);
            target.gameObject.SetActive(has);
            if (has)
                target.text = value;
        }

        // ──────────────────────────────────────────────────────────────
        // 아래는 <b>에디터 굽기 전용</b>이다. 런타임에 부르지 않는다.
        //
        // 손으로 프리팹을 짜지 않는 이유는 10.5 와 같다 — 위치·색·크기를 옮겨 적으면
        // 오차가 생기고, 값을 아는 것은 코드다. 한 번 돌려 프리팹으로 저장한다.
        // ──────────────────────────────────────────────────────────────

        private static readonly Color OverlayColor = new Color(0.015f, 0.025f, 0.08f, 0.86f);
        private static readonly Color PanelColor = new Color(0.075f, 0.10f, 0.22f, 1f);
        private static readonly Color ConfirmColor = new Color(0.83f, 0.32f, 0.28f, 1f);
        private static readonly Color CancelColor = new Color(0.16f, 0.22f, 0.38f, 1f);
        private static readonly Color MutedTextColor = new Color(0.78f, 0.84f, 0.94f, 1f);

        /// <summary>에디터 굽기 전용. <c>ConfirmDialogPrefabBaker</c> 만 부른다.</summary>
        public static UIConfirmDialog Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = CreateRect("UIConfirmDialog", parent);
            Stretch(root.GetComponent<RectTransform>());

            GameObject view = CreateRect("DialogView", root.transform);
            Stretch(view.GetComponent<RectTransform>());
            Image overlay = view.AddComponent<Image>();
            overlay.color = OverlayColor;
            overlay.raycastTarget = true;

            UIConfirmDialog dialog = root.AddComponent<UIConfirmDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            Image panel = CreateImage("Panel", view.transform, PanelColor);
            SetRect(panel.rectTransform, new Vector2(860f, 520f), Vector2.zero);

            dialog.titleText = CreateText("Title", panel.transform, "제목", 46f,
                TextAlignmentOptions.Center, Color.white, font);
            SetRect(dialog.titleText.rectTransform, new Vector2(760f, 70f), new Vector2(0f, 178f));

            dialog.messageText = CreateText("Message", panel.transform, "본문", 34f,
                TextAlignmentOptions.Center, MutedTextColor, font);
            SetRect(dialog.messageText.rectTransform, new Vector2(760f, 230f), new Vector2(0f, 20f));
            dialog.messageText.enableWordWrapping = true;

            dialog.cancelButton = CreateButton("CancelButton", panel.transform, "취소",
                CancelColor, font, out dialog.cancelLabel);
            SetRect(dialog.cancelButton.GetComponent<RectTransform>(),
                new Vector2(360f, 108f), new Vector2(-196f, -170f));

            dialog.confirmButton = CreateButton("ConfirmButton", panel.transform, "확인",
                ConfirmColor, font, out dialog.confirmLabel);
            SetRect(dialog.confirmButton.GetComponent<RectTransform>(),
                new Vector2(360f, 108f), new Vector2(196f, -170f));

            return dialog;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            Image image = CreateRect(name, parent).AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(
            string name, Transform parent, string text, float size,
            TextAlignmentOptions align, Color color, TMP_FontAsset font)
        {
            TextMeshProUGUI label = CreateRect(name, parent).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.font = font;
            label.raycastTarget = false;
            return label;
        }

        private static Button CreateButton(
            string name, Transform parent, string text, Color color,
            TMP_FontAsset font, out TMP_Text label)
        {
            Image background = CreateImage(name, parent, color);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            label = CreateText("Label", background.transform, text, 36f,
                TextAlignmentOptions.Center, Color.white, font);
            Stretch(label.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }
    }
}
