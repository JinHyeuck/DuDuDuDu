#if UNITY_EDITOR || DEV_DEFINE
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OJ.Dev
{
    /// <summary>
    /// 치트 창을 여는 작은 "DEV" 버튼. <b>끌어서 아무 데나 옮길 수 있다.</b>
    ///
    /// <b>왜 옮길 수 있어야 하나.</b> 이 버튼은 앱 내내 맨 위에 떠 있어서 어느 화면에서든
    /// 무언가를 가린다. 가려진 것을 보려고 치트를 껐다 켜는 것은 말이 안 되고, 화면마다
    /// 안전한 자리를 미리 정해 둘 수도 없다 — 그래서 손으로 치우게 한다.
    ///
    /// <b>옮긴 자리는 기억한다.</b> 매번 제자리로 돌아오면 옮길 수 있다는 것이 쓸모가 없다.
    /// (<c>SaveResetCheat.WipeAll</c> 이 PlayerPrefs 를 통째로 지우면 같이 날아가는데,
    /// 그때는 기본 자리로 돌아올 뿐이라 사고가 아니다.)
    ///
    /// <b>탭과 드래그를 가른다.</b> 손가락은 누르는 동안 몇 픽셀씩 움직이므로, 움직인
    /// 거리가 <see cref="DragThreshold"/> 를 넘었을 때만 이동으로 보고 창을 열지 않는다.
    /// 안 가르면 옮기려 할 때마다 창이 열린다.
    /// </summary>
    internal sealed class DevCheatButton : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const string PrefsKeyX = "OJ.Dev.CheatButton.X";
        private const string PrefsKeyY = "OJ.Dev.CheatButton.Y";

        /// <summary>이 거리(화면 px)를 넘겨 움직이면 탭이 아니라 이동이다.</summary>
        private const float DragThreshold = 12f;

        internal static readonly Vector2 ButtonSize = new Vector2(128f, 96f);

        /// <summary>기본 자리. 우하단에서 살짝 띄운 곳 — 로비 하단 탭과 겹치지 않는다.</summary>
        private static readonly Vector2 DefaultPosition = new Vector2(-96f, 420f);

        private RectTransform rect;
        private Canvas canvas;
        private Vector2 pressScreenPosition;
        private Vector2 pressAnchoredPosition;
        private bool dragged;

        internal static DevCheatButton Create(Transform parent)
        {
            Image root = DevCheatUI.CreateImage("DevCheatButton", parent, DevCheatUI.AccentColor);

            // 우하단 기준으로 앉힌다. 해상도가 바뀌어도 모서리와의 거리가 유지된다.
            RectTransform rt = root.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = ButtonSize;

            TMP_Text label = DevCheatUI.CreateLabel(
                "Label", root.transform, "DEV", 38f, DevCheatUI.TextColor,
                TextAlignmentOptions.Center);
            DevCheatUI.Stretch(label.rectTransform);

            var button = root.gameObject.AddComponent<DevCheatButton>();
            button.rect = rt;
            button.canvas = root.GetComponentInParent<Canvas>();
            button.LoadPosition();
            return button;
        }

        // ── 끌기 ───────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            dragged = false;
            pressScreenPosition = eventData.position;
            pressAnchoredPosition = rect.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if ((eventData.position - pressScreenPosition).sqrMagnitude > DragThreshold * DragThreshold)
                dragged = true;

            if (!dragged)
                return;

            // 화면 px 를 캔버스 좌표로 바꿔 더한다. scaleFactor 를 안 나누면
            // 고해상도 기기에서 손가락보다 버튼이 몇 배 빨리 움직인다.
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 delta = (eventData.position - pressScreenPosition) / scale;

            rect.anchoredPosition = Clamp(pressAnchoredPosition + delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (dragged)
            {
                SavePosition();
                return;
            }

            DevCheatPanel.Toggle();
        }

        /// <summary>
        /// 화면 밖으로 못 나가게 막는다. 한 번 밖으로 나가면 <b>다시 끌어올 수가 없어서</b>
        /// 치트 입구가 통째로 사라진다 — 그때 복구하려면 PlayerPrefs 를 지워야 한다.
        /// </summary>
        private Vector2 Clamp(Vector2 anchored)
        {
            Vector2 half = ButtonSize * 0.5f;
            Vector2 screen = DevCheatUI.ReferenceResolution;

            // 앵커가 우하단이라 x 는 음수 구간, y 는 양수 구간이다.
            anchored.x = Mathf.Clamp(anchored.x, -screen.x + half.x, -half.x);
            anchored.y = Mathf.Clamp(anchored.y, half.y, screen.y - half.y);
            return anchored;
        }

        private void LoadPosition()
        {
            Vector2 position = new Vector2(
                PlayerPrefs.GetFloat(PrefsKeyX, DefaultPosition.x),
                PlayerPrefs.GetFloat(PrefsKeyY, DefaultPosition.y));

            rect.anchoredPosition = Clamp(position);
        }

        private void SavePosition()
        {
            PlayerPrefs.SetFloat(PrefsKeyX, rect.anchoredPosition.x);
            PlayerPrefs.SetFloat(PrefsKeyY, rect.anchoredPosition.y);
            PlayerPrefs.Save();
        }
    }
}
#endif
