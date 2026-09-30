using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.Dice;
using OJ.Point;

namespace OJ.Tower
{
    /// <summary>
    /// 층 선택 목록의 카드 한 장. 기획서 5.2 의 (3), 아트 시안 Infinity_Layout.
    ///
    /// <b>네 가지 모습을 한 프리팹이 다 한다</b> — 잠김(철문) · 도전 가능(주황, 노란 테두리) ·
    /// 완료(회색) · 구간 보상 층(빨강). 모습마다 프리팹을 나누면 목록을 만들 때 어느 것을
    /// 찍을지 고르는 분기가 생기고, 그 분기가 <c>UITowerFloorSelectDialog</c> 와 여기 두 곳에 걸친다.
    ///
    /// <b>DialogBase 가 아니다.</b> 스스로 열리고 닫히는 것이 아니라 목록 안의 부품이라
    /// 카탈로그에 등재되면 안 된다(<c>DialogCatalogBuilder</c> 가 루트에 <c>DialogBase</c> 가
    /// 붙은 프리팹만 줍는 것이 그 경계다).
    /// </summary>
    public class UITowerFloorCard : MonoBehaviour
    {
        [SerializeField] private Button cardButton;
        [SerializeField] private Image background;
        [SerializeField] private Image numberBlock;
        [SerializeField] private Image lockedGate;
        [SerializeField] private Image highlightEdge;
        [SerializeField] private TMP_Text floorText;
        [SerializeField] private TMP_Text conceptText;
        [SerializeField] private TMP_Text recordText;
        [SerializeField] private TMP_Text clearedText;

        [Header("보상 칸")]
        [SerializeField] private GameObject rewardBox;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardAmountText;

        [Header("모습별 스프라이트 (굽기에서 채움)")]
        [SerializeField] private Sprite panelNormal;
        [SerializeField] private Sprite panelMilestone;
        [SerializeField] private Sprite numberNormal;
        [SerializeField] private Sprite numberCurrent;
        [SerializeField] private Sprite numberMilestone;

        private int floor;
        private Action<int> clickHandler;

        public int Floor => floor;

        private void Awake()
        {
            if (cardButton != null)
                cardButton.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (cardButton != null)
                cardButton.onClick.RemoveListener(OnClick);
        }

        /// <summary>
        /// 카드를 채운다.
        /// </summary>
        /// <param name="plan">층 계획. 미해금 층이면 null 을 넘겨 정보를 감춘다.</param>
        /// <param name="revealed">정보를 보여 줄 수 있는가. false 면 <c>? ? ?</c> 로 가린다.</param>
        /// <param name="challengeable">
        /// 지금 들어갈 수 있는가. <b>목록에서 딱 한 장만 참이다</b> —
        /// 클리어한 층은 다시 못 들어간다(반복 없음).
        /// </param>
        /// <param name="bestClearMilliseconds">
        /// 시안의 카드에는 기록 칸이 없어 지금은 쓰지 않는다. 결과 화면이 같은 값을 보여 준다.
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

            bool locked = !challengeable && !cleared;
            bool milestone = TowerFormula.IsBandLastFloor(floor);

            // 카드를 누르면 편성으로 간다. <b>도전 가능한 한 장만</b> 눌린다 — 클리어한 층을
            // 누를 수 있게 두면 눌러도 아무 일이 없거나, 있어서는 안 될 경로가 열린다.
            // 이 목록은 "올라온 길" 이고 되돌아갈 수 있는 곳이 아니다.
            if (cardButton != null)
                cardButton.interactable = challengeable;

            if (background != null)
                background.sprite = milestone ? panelMilestone : panelNormal;

            if (highlightEdge != null)
                highlightEdge.enabled = challengeable;

            // 번호 블록: 잠긴 층은 블록 대신 철문 그림이 번호를 받친다.
            if (numberBlock != null)
            {
                numberBlock.enabled = !locked;
                numberBlock.sprite = challengeable ? numberCurrent : milestone ? numberMilestone : numberNormal;
            }

            if (lockedGate != null)
                lockedGate.enabled = locked;

            if (floorText != null)
            {
                floorText.SetText("{0}", floor);
                floorText.color = challengeable ? UITowerUIFactory.TextGold : Color.white;
            }

            if (conceptText != null)
            {
                // 미해금 층은 <b>정보를 감춘다.</b> 기획서 5.2 "미해금 층은 정보를 숨겨
                // 다음 목표의 궁금증을 유지". 감추는 것은 여기뿐이고, 이미 열린 층은
                // 위로도 아래로도 전부 보여 준다.
                conceptText.SetText(revealed && plan != null ? plan.DisplayName : "? ? ?");
            }

            if (recordText != null)
            {
                recordText.gameObject.SetActive(!cleared);

                if (challengeable)
                {
                    recordText.SetText("도전 가능");
                    recordText.fontSize = 40f;
                    recordText.color = UITowerUIFactory.TextGold;
                }
                else
                {
                    recordText.SetText("이전 층 클리어 필요");
                    recordText.fontSize = 30f;
                    recordText.color = UITowerUIFactory.TextMuted;
                }
            }

            if (clearedText != null)
                clearedText.gameObject.SetActive(cleared);

            BindReward(floor, cleared);
        }

        /// <summary>
        /// 오른쪽 보상 칸. 이미 깬 층은 "완료" 가 그 자리를 차지한다.
        ///
        /// <b>무엇을 보여 줄지의 순서</b>: 이 층에서 열리는 다이스 → 구간 보상 다이아 →
        /// 최초 클리어 골드. 한 층의 보상이 여럿이어도 칸은 하나라서, 가장 드문 것을 앞에 둔다.
        /// </summary>
        private void BindReward(int floor, bool cleared)
        {
            if (rewardBox == null)
                return;

            rewardBox.SetActive(!cleared);
            if (cleared)
                return;

            Sprite icon;
            string amount;

            DiceType unlockDice = FindDiceUnlockedAt(floor);
            if (unlockDice != DiceType.Max)
            {
                icon = DiceMetaDataProvider.GetIcon(unlockDice);
                amount = string.Empty;
            }
            else if (TowerFormula.IsBandLastFloor(floor))
            {
                icon = PointRewardUtility.GetPointIcon(PointType.FreeGem);
                amount = TowerFormula.BandRewardDia(floor).ToString();
            }
            else
            {
                icon = PointRewardUtility.GetPointIcon(PointType.Gold);
                amount = ShortNumberFormat.Format(TowerFormula.FirstClearGold(floor));
            }

            if (rewardIcon != null)
            {
                rewardIcon.enabled = icon != null;
                rewardIcon.sprite = icon;
            }

            if (rewardAmountText != null)
            {
                rewardAmountText.gameObject.SetActive(!string.IsNullOrEmpty(amount));
                rewardAmountText.SetText(amount);
            }
        }

        private static DiceType FindDiceUnlockedAt(int floor)
        {
            IReadOnlyList<DiceUnlockDefinition> unlocks = DiceUnlockDatabaseProvider.Database.Definitions;
            for (int i = 0; i < unlocks.Count; i++)
            {
                DiceUnlockDefinition unlock = unlocks[i];
                if (unlock != null && unlock.towerFloor == floor)
                    return unlock.diceType;
            }

            return DiceType.Max;
        }

        private void OnClick()
        {
            clickHandler?.Invoke(floor);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 좌표는 시안 Infinity_Layout 의 102층 카드(중심 550.5, 740.5) 기준
        // 상대값이다.
        // ──────────────────────────────────────────────────────────────

        internal const float CardWidth = UITowerUIFactory.PanelWidth;
        internal const float CardHeight = 172f;

        /// <summary>에디터 굽기 전용. <see cref="UITowerFloorSelectDialog.Create"/> 가 부른다.</summary>
        internal static UITowerFloorCard Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UITowerUIFactory.CreateRect("TowerFloorCard", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(CardWidth, CardHeight);

            var element = root.AddComponent<LayoutElement>();
            element.preferredWidth = CardWidth;
            element.preferredHeight = CardHeight;
            element.minHeight = CardHeight;

            var card = root.AddComponent<UITowerFloorCard>();

            card.panelNormal = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Bg_Light");
            card.panelMilestone = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Bg_Red");
            card.numberNormal = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Number_Normal");
            card.numberCurrent = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Number_Mission");
            card.numberMilestone = UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Number_Special");

            // 패널 — x4, 764x172
            // 그림은 칸보다 사방 12px 크다(투명 여백 3px x4). 칸(레이아웃 단위)은 보이는 크기 그대로다.
            card.background = UITowerUIFactory.CreateSprite("Background", root.transform, card.panelNormal, 4f,
                UITowerUIFactory.PanelRect(CardWidth, CardHeight), Vector2.zero, true);
            card.background.raycastTarget = true;

            card.cardButton = root.AddComponent<Button>();
            card.cardButton.targetGraphic = card.background;
            card.cardButton.transition = Selectable.Transition.None;

            // 번호 블록 — 카드 왼쪽 x169~344 (176 폭)
            var blockPos = new Vector2(-294f, 0f);
            card.numberBlock = UITowerUIFactory.CreateSprite("NumberBlock", root.transform, card.numberCurrent, 4f,
                UITowerUIFactory.PanelRect(176f, CardHeight), blockPos, true);

            // 잠긴 층의 철문 — x2 (128 → 256). 보이는 영역이 그림 한가운데라 블록 중심에 그대로 둔다.
            card.lockedGate = UITowerUIFactory.CreateSprite("LockedGate", root.transform,
                UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Unlock"), 2f,
                new Vector2(256f, 256f), blockPos, false);
            card.lockedGate.enabled = false;

            card.floorText = UITowerUIFactory.CreateText("Floor", root.transform, "102", 60f,
                TextAlignmentOptions.Center, UITowerUIFactory.TextGold, font);
            UITowerUIFactory.SetRect(card.floorText.rectTransform, new Vector2(176f, 80f), blockPos);

            // 이름과 상태 — 왼쪽 x360 에서 시작
            card.conceptText = UITowerUIFactory.CreateText("Concept", root.transform, "혼합 부대 + 보호막 적", 30f,
                TextAlignmentOptions.Left, Color.white, font);
            UITowerUIFactory.SetRect(card.conceptText.rectTransform, new Vector2(400f, 44f), new Vector2(9.5f, 20f));

            card.recordText = UITowerUIFactory.CreateText("Status", root.transform, "도전 가능", 40f,
                TextAlignmentOptions.Left, UITowerUIFactory.TextGold, font);
            UITowerUIFactory.SetRect(card.recordText.rectTransform, new Vector2(400f, 52f), new Vector2(9.5f, -28f));

            card.clearedText = UITowerUIFactory.CreateText("Cleared", root.transform, "완료", 40f,
                TextAlignmentOptions.Center, UITowerUIFactory.TextGold, font);
            UITowerUIFactory.SetRect(card.clearedText.rectTransform, new Vector2(200f, 60f), new Vector2(247.5f, 0f));
            card.clearedText.gameObject.SetActive(false);

            // 보상 칸 — x775~902, y668~802
            Image box = UITowerUIFactory.CreateImage("RewardBox", root.transform, UITowerUIFactory.RewardBoxColor);
            UITowerUIFactory.SetRect(box.rectTransform, new Vector2(128f, 134f), new Vector2(288f, 5.5f));
            box.raycastTarget = false;
            card.rewardBox = box.gameObject;

            TMP_Text rewardCaption = UITowerUIFactory.CreateText("Caption", box.transform, "보상", 30f,
                TextAlignmentOptions.Center, UITowerUIFactory.TextCyan, font);
            UITowerUIFactory.SetRect(rewardCaption.rectTransform, new Vector2(128f, 40f), new Vector2(0f, 38f));

            card.rewardIcon = UITowerUIFactory.CreateImage("Icon", box.transform, Color.white);
            UITowerUIFactory.SetRect(card.rewardIcon.rectTransform, new Vector2(64f, 64f), new Vector2(0f, -8f));
            card.rewardIcon.preserveAspect = true;
            card.rewardIcon.raycastTarget = false;
            card.rewardIcon.enabled = false;

            card.rewardAmountText = UITowerUIFactory.CreateText("Amount", box.transform, "500", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UITowerUIFactory.SetRect(card.rewardAmountText.rectTransform, new Vector2(128f, 40f), new Vector2(0f, -48f));

            // 현재 층 테두리 — 보이는 선이 카드보다 사방 4px 크고(시안 772x179), 그림 여백이 2px x4.
            // 맨 마지막에 만들어
            // 카드 모서리 위에 얹는다. 속이 비어 있는 그림이라 내용을 가리지 않는다.
            card.highlightEdge = UITowerUIFactory.CreateSprite("HighlightEdge", root.transform,
                UITowerUIFactory.LoadSprite("InfinityMode/Infinity_Popup_Bg_Selcet"), 4f,
                new Vector2(CardWidth + 8f + 16f, CardHeight + 7f + 16f), Vector2.zero, true);

            // 테두리 그림이 흰색으로 바뀌었다(4297f64). 층 카드는 예전 그림 색(#FFDE00 노랑)을 그대로 입힌다 —
            // 초록(#00FF24)은 편성 화면의 다이스 선택 테두리 색이다(UITowerDiceCell, 사용자 지시 2026-10-01).
            card.highlightEdge.color = UITowerUIFactory.Hex(0xFFDE00);

            return card;
        }
    }
}
