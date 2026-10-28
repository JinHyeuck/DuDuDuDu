using OJ.Core;
using OJ.DI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Synergy
{
    /// <summary>
    /// 로비의 시너지 입구. 확정 콘텐츠가 아니라서 탭이 아니라 무한의 탑 입구 같은 버튼 하나로 둔다(사용자 결정 2026-10-28).
    /// 구조는 <c>UITowerEntryButton</c> 과 같다 — 씬에 넣는 일은 에디터 도구(<c>SynergyInstaller</c>)가 하고,
    /// 이 파일은 그 오브젝트가 하는 일만 갖는다.
    /// </summary>
    public class UISynergyEntryButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(OnClick);
        }

        private void OnEnable()
        {
            if (SynergyManager.Instance != null)
                SynergyManager.Instance.OnChanged += Refresh;

            Refresh();
        }

        private void OnDisable()
        {
            if (SynergyManager.Instance != null)
                SynergyManager.Instance.OnChanged -= Refresh;
        }

        private void Refresh()
        {
            if (titleText != null)
                titleText.SetText("시너지");

            SynergyManager manager = SynergyManager.Instance;
            if (progressText == null || manager == null)
                return;

            int open = 0;
            for (int c = 0; c < SynergyRules.CategoryCount; c++)
            {
                if (manager.IsOpen((SynergyCategory)c))
                    open++;
            }

            progressText.SetText(open + "/" + SynergyRules.CategoryCount + " 계열");
        }

        private void OnClick()
        {
            UISynergyDialog dialog = GameContainer.UI?.Get<UISynergyDialog>();
            if (dialog == null)
            {
                Debug.LogError("[시너지] 시너지 창을 열지 못했다. OJ/개발/시너지/설치 를 돌렸는지 볼 것.");
                return;
            }

            dialog.Enter();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 설치 전용.
        // ──────────────────────────────────────────────────────────────

        // 왼쪽 아이콘 줄(멤버십·특별한 7일)과 같은 칸 크기.
        public const float Width = 160f;
        public const float Height = 160f;

        /// <summary>에디터 설치 전용. <c>SynergyInstaller</c> 가 부른다.</summary>
        public static UISynergyEntryButton Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("UISynergyEntryButton", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            Image background = go.AddComponent<Image>();
            background.color = new Color(0.5f, 0.3f, 0.75f, 0.95f);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(Width, Height);

            var entry = go.AddComponent<UISynergyEntryButton>();
            entry.button = go.AddComponent<Button>();
            entry.button.targetGraphic = background;

            entry.titleText = CreateText("Title", go.transform, "시너지", 34f, Color.white, font, new Vector2(0f, 22f), new Vector2(Width - 12f, 48f));
            entry.progressText = CreateText("Progress", go.transform, "1/4 계열", 24f, new Color(1f, 0.85f, 0.4f, 1f), font,
                new Vector2(0f, -30f), new Vector2(Width - 12f, 34f));
            return entry;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color,
            TMP_FontAsset font, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            if (font != null)
                label.font = font;
            label.raycastTarget = false;

            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = box;
            rect.anchoredPosition = position;
            return label;
        }
    }
}
