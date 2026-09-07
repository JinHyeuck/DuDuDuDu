using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ.Tower
{
    /// <summary>
    /// 층 선택 목록의 카드 한 장. 기획서 5.2 의 (3) 이다.
    ///
    /// <b>세 가지 모습을 한 프리팹이 다 한다</b> — 미해금(? ? ?) · 도전 가능 · 클리어(재도전).
    /// 모습마다 프리팹을 나누면 목록을 만들 때 어느 것을 찍을지 고르는 분기가 생기고,
    /// 그 분기가 <c>UITowerFloorSelectDialog</c> 와 여기 두 곳에 걸친다.
    ///
    /// <b>DialogBase 가 아니다.</b> 스스로 열리고 닫히는 것이 아니라 목록 안의 부품이라
    /// 카탈로그에 등재되면 안 된다(<c>DialogCatalogBuilder</c> 가 루트에 <c>DialogBase</c> 가
    /// 붙은 프리팹만 줍는 것이 그 경계다).
    /// </summary>
    public class UITowerFloorCard : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private TMP_Text conceptText;
        [SerializeField] private TMP_Text recordText;
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Image highlightEdge;

        private int floor;
        private Action<int> clickHandler;

        public int Floor => floor;

        /// <summary>
        /// 카드를 채운다.
        /// </summary>
        /// <param name="plan">층 계획. 미해금 층이면 null 을 넘겨 정보를 감춘다.</param>
        /// <param name="revealed">정보를 보여 줄 수 있는가. false 면 <c>? ? ?</c> 로 가린다.</param>
        /// <param name="challengeable">
        /// 지금 들어갈 수 있는가. <b>목록에서 딱 한 장만 참이다</b> —
        /// 클리어한 층은 다시 못 들어간다(반복 없음).
        /// </param>
        public void Bind(
            int floor,
            TowerFloorPlan plan,
            bool revealed,
            bool challengeable,
            bool cleared,
            int bestClearMilliseconds,
            int bestRemainingPercent,
            Action<int> onClick)
        {
            this.floor = floor;
            clickHandler = onClick;

            if (floorText != null)
                floorText.SetText("{0}", floor);

            if (highlightEdge != null)
                highlightEdge.enabled = challengeable;

            if (background != null)
                background.color = revealed ? UITowerUIFactory.CardColor : UITowerUIFactory.CardDimColor;

            // 버튼은 <b>도전 가능한 한 장에만</b> 붙는다. 클리어한 층에 재도전 버튼을
            // 남기면 눌러도 아무 일이 없거나, 있어서는 안 될 경로가 열린다.
            // 치우는 편이 규칙을 말없이 알려 준다 — 이 목록은 "올라온 길" 이고
            // 되돌아갈 수 있는 곳이 아니다.
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnClick);
                actionButton.gameObject.SetActive(challengeable);
                actionButton.image.color = UITowerUIFactory.Accent;
            }

            if (actionLabel != null)
                actionLabel.SetText("도전");

            if (!revealed || plan == null)
            {
                // 미해금 층은 <b>정보를 감춘다.</b> 기획서 5.2 "미해금 층은 정보를 숨겨
                // 다음 목표의 궁금증을 유지". 감추는 것은 여기뿐이고, 이미 열린 층은
                // 위로도 아래로도 전부 보여 준다.
                if (conceptText != null)
                    conceptText.SetText("? ? ?");

                if (recordText != null)
                    recordText.SetText("이전 층 클리어 필요");

                return;
            }

            if (conceptText != null)
                conceptText.SetText(plan.DisplayName);

            if (recordText != null)
                recordText.SetText(BuildRecordText(cleared, bestClearMilliseconds, bestRemainingPercent));
        }

        /// <summary>
        /// 카드 아래 줄. 클리어한 층은 <b>그때의 기록</b>을, 지금 도전하는 층은
        /// 지난 실패가 어디까지 갔는지를 적는다 — 기획서 5.6 이 말하는
        /// "실패도 진척으로 읽히게" 를 목록에서도 지킨다.
        ///
        /// 클리어한 층의 기록은 <b>더 이상 갱신되지 않는다</b>(반복 없음).
        /// 그래서 "최고 기록" 이 아니라 "클리어" 라고 적는다 — 경신할 수 없는 값을
        /// 최고 기록이라 부르면 다시 도전할 수 있다는 뜻으로 읽힌다.
        /// </summary>
        private static string BuildRecordText(bool cleared, int bestMs, int bestRemainingPercent)
        {
            if (cleared && bestMs > 0)
                return UITowerUIFactory.FormatSeconds(bestMs) + " · 클리어";

            if (cleared)
                return "클리어";

            if (bestRemainingPercent < 100)
                return "지난 도전 · 적 " + bestRemainingPercent + "% 남김";

            return "미도전";
        }

        private void OnClick()
        {
            clickHandler?.Invoke(floor);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        internal const float CardWidth = 900f;
        internal const float CardHeight = 128f;

        /// <summary>에디터 굽기 전용. <see cref="UITowerFloorSelectDialog.Create"/> 가 부른다.</summary>
        internal static UITowerFloorCard Create(Transform parent, TMP_FontAsset font)
        {
            // 강조 테두리를 <b>배경의 형제로, 배경보다 먼저</b> 만든다. 자식으로 넣으면
            // uGUI 가 자식을 부모 위에 그려서 테두리가 카드 내용을 통째로 덮는다.
            // (현상금 칸에서 같은 실수를 한 번 잡았다.)
            GameObject root = UITowerUIFactory.CreateRect("TowerFloorCard", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(CardWidth, CardHeight);

            var element = root.AddComponent<LayoutElement>();
            element.preferredHeight = CardHeight;
            element.minHeight = CardHeight;

            var card = root.AddComponent<UITowerFloorCard>();

            Image edge = UITowerUIFactory.CreateImage("Edge", root.transform, UITowerUIFactory.GoldText);
            UITowerUIFactory.Stretch(edge.rectTransform);
            edge.raycastTarget = false;
            card.highlightEdge = edge;

            Image background = UITowerUIFactory.CreateImage("Background", root.transform, UITowerUIFactory.CardColor);
            RectTransform backgroundRect = background.rectTransform;
            UITowerUIFactory.Stretch(backgroundRect);
            backgroundRect.offsetMin = new Vector2(4f, 4f);
            backgroundRect.offsetMax = new Vector2(-4f, -4f);
            card.background = background;

            card.floorText = UITowerUIFactory.CreateText("Floor", background.transform, "26", 46f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(card.floorText.rectTransform,
                new Vector2(140f, 60f), new Vector2(-CardWidth * 0.5f + 90f, 0f));

            card.conceptText = UITowerUIFactory.CreateText("Concept", background.transform, "고방어 부대", 32f,
                TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(card.conceptText.rectTransform,
                new Vector2(500f, 40f), new Vector2(-70f, 20f));

            card.recordText = UITowerUIFactory.CreateText("Record", background.transform, "미도전", 26f,
                TextAlignmentOptions.Left, UITowerUIFactory.MutedText, font);
            UITowerUIFactory.SetRect(card.recordText.rectTransform,
                new Vector2(500f, 34f), new Vector2(-70f, -22f));

            card.actionButton = UITowerUIFactory.CreateButton(
                "Action", background.transform, "도전",
                new Vector2(150f, 72f), new Vector2(CardWidth * 0.5f - 100f, 0f),
                UITowerUIFactory.Accent, Color.white, 30f, font);
            card.actionLabel = card.actionButton.GetComponentInChildren<TMP_Text>();

            return card;
        }
    }
}
