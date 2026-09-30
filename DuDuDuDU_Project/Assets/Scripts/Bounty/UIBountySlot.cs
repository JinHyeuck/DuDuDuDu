using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Battle;
using OJ.Core;

namespace OJ.Bounty
{
    /// <summary>
    /// 선택 창의 한 칸. <see cref="Grade"/> 가 0 이면 "소환 X" 칸이다.
    ///
    /// <b>DialogBase 가 아니다.</b> 스스로 열리고 닫히는 것이 아니라 창 안의 부품이라,
    /// 카탈로그에 등재되면 안 된다(<c>DialogCatalogBuilder</c> 가 루트에 <c>DialogBase</c> 가
    /// 붙은 프리팹만 줍는 것이 그 경계다).
    /// </summary>
    public class UIBountySlot : MonoBehaviour
    {
        [SerializeField] private int grade;

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image selectionEdge;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text rewardText;
        [Tooltip("보상 재화 아이콘. 비워 두면 rewardText 옆에 런타임으로 만든다.")]
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text lockText;

        private const float RewardIconGap = 6f;

        private Action<int> clickHandler;
        private Vector2 rewardTextBasePosition;
        private bool rewardTextBaseCaptured;

        public int Grade => grade;

        /// <summary>
        /// 칸을 채운다. <paramref name="definition"/> 이 null 이면 "소환 X" 칸이다 —
        /// 등급 0 에는 정의가 없는 것이 정상이며 사고가 아니다.
        /// </summary>
        public void Bind(
            BountyDefinition definition,
            int hp,
            bool selected,
            bool unlocked,
            Action<int> onClick)
        {
            clickHandler = onClick;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);

                // 잠긴 칸은 <b>보이되 눌리지 않는다.</b> 내용을 지우면 다음에 무엇을
                // 노릴지 계획할 수 없어서 순서대로 잡는 것 말고 할 일이 없어진다.
                button.interactable = unlocked;
            }

            if (selectionEdge != null)
                selectionEdge.enabled = selected;

            if (lockText != null)
                lockText.gameObject.SetActive(!unlocked);

            if (definition == null)
            {
                BindNoneSlot(unlocked);
                return;
            }

            if (nameText != null)
                nameText.SetText(definition.displayName);

            if (hpText != null)
                hpText.SetText("HP " + ShortNumberFormat.Format(hp));

            BindReward(definition);

            if (icon != null)
            {
                icon.enabled = definition.icon != null;
                icon.sprite = definition.icon;

                // 아이콘 에셋이 아직 없으면 스프라이트 없는 Image 가 흰 사각형으로 그려진다.
                // 등급 색을 먹여 두면 그 사각형이 <b>등급을 구분하는 표식</b>으로 쓰인다 —
                // 전용 아트가 붙으면 sprite 가 채워지면서 색은 틴트로 남는다.
                icon.color = definition.tint;
            }

            ApplyDim(unlocked);
        }

        private void BindNoneSlot(bool unlocked)
        {
            if (nameText != null)
                nameText.SetText("사냥 안하기");

            if (hpText != null)
                hpText.SetText(string.Empty);

            if (rewardText != null)
            {
                RestoreRewardTextPosition();
                rewardText.SetText(string.Empty);
            }

            if (rewardIcon != null)
                rewardIcon.gameObject.SetActive(false);

            if (icon != null)
                icon.enabled = false;

            ApplyDim(unlocked);
        }

        /// <summary>
        /// 보상을 <b>[재화 아이콘] +수량</b> 으로 적는다. 아이콘을 못 구하면(메타데이터 미등록)
        /// 예전처럼 재화 이름을 글자로 적는다 — 수량만 덩그러니 남으면 무엇을 주는지 모른다.
        ///
        /// 글자가 가운데 정렬이라 아이콘과 글자를 <b>한 덩어리로 묶어 가운데</b>에 놓는다.
        /// 수량 자릿수에 따라 폭이 바뀌므로 매번 preferredWidth 로 다시 잰다.
        /// </summary>
        private void BindReward(BountyDefinition definition)
        {
            if (rewardText == null)
                return;

            RestoreRewardTextPosition();

            Sprite sprite = BattlePointUtility.GetIcon(definition.rewardKind);
            if (sprite == null)
            {
                if (rewardIcon != null)
                    rewardIcon.gameObject.SetActive(false);

                rewardText.SetText(definition.FormatReward());
                return;
            }

            rewardText.SetText("+" + ShortNumberFormat.Format(definition.rewardAmount));

            Image iconImage = EnsureRewardIcon();
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = sprite;

            float iconSize = iconImage.rectTransform.sizeDelta.x;
            float textWidth = rewardText.GetPreferredValues(rewardText.text).x;
            float total = iconSize + RewardIconGap + textWidth;

            // 덩어리의 왼쪽 끝이 -total/2. 아이콘은 그 자리에서, 글자는 아이콘 뒤에서 시작한다.
            // 글자는 가운데 정렬이므로 글자 중심 = 왼쪽 끝 + 아이콘 + 간격 + 글자폭/2.
            float textCenter = -total * 0.5f + iconSize + RewardIconGap + textWidth * 0.5f;
            rewardText.rectTransform.anchoredPosition = rewardTextBasePosition + new Vector2(textCenter, 0f);

            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchoredPosition = rewardTextBasePosition +
                                        new Vector2(-total * 0.5f + iconSize * 0.5f, 0f);
        }

        private void RestoreRewardTextPosition()
        {
            if (!rewardTextBaseCaptured)
            {
                rewardTextBasePosition = rewardText.rectTransform.anchoredPosition;
                rewardTextBaseCaptured = true;
            }

            rewardText.rectTransform.anchoredPosition = rewardTextBasePosition;
        }

        /// <summary>
        /// 이미 구워진 프리팹에는 아이콘 칸이 없다. 다시 구우면 손으로 고친 폰트·버튼이
        /// 날아가므로, 비어 있으면 rewardText 의 형제로 그 자리에서 만든다.
        /// </summary>
        private Image EnsureRewardIcon()
        {
            if (rewardIcon != null)
                return rewardIcon;

            RectTransform textRect = rewardText.rectTransform;
            rewardIcon = CreateRewardIcon(textRect.parent, rewardText.fontSize * 1.2f);

            // 글자 칸의 기준점과 같은 점에 앵커를 둬야 anchoredPosition 을 그대로 나눠 쓸 수 있다.
            RectTransform iconRect = rewardIcon.rectTransform;
            Vector2 anchor = (textRect.anchorMin + textRect.anchorMax) * 0.5f;
            iconRect.anchorMin = anchor;
            iconRect.anchorMax = anchor;
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            // 글자 바로 뒤 형제로 두어 그리기 순서를 글자와 맞춘다.
            rewardIcon.transform.SetSiblingIndex(rewardText.transform.GetSiblingIndex() + 1);
            return rewardIcon;
        }

        private static Image CreateRewardIcon(Transform parent, float size)
        {
            var go = new GameObject("RewardIcon", typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            return image;
        }

        /// <summary>
        /// 잠긴 칸을 어둡게 한다. <c>Button.interactable</c> 만으로는 uGUI 기본 전환이
        /// <c>targetGraphic</c> 하나만 흐리게 만들어서, 글자는 멀쩡하고 배경만 흐려진
        /// 어중간한 모습이 된다.
        /// </summary>
        private void ApplyDim(bool unlocked)
        {
            float alpha = unlocked ? 1f : 0.45f;

            SetAlpha(nameText, alpha);
            SetAlpha(hpText, alpha);
            SetAlpha(rewardText, alpha);

            if (icon != null && icon.enabled)
                SetAlpha(icon, alpha);

            if (rewardIcon != null)
                SetAlpha(rewardIcon, alpha);
        }

        private static void SetAlpha(Image image, float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
                return;

            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }


        private void OnClick()
        {
            clickHandler?.Invoke(grade);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 시안: Art/Layout/Wanted_Layout(2).png — 수치는 Docs/WantedUIArtPort.md
        // ──────────────────────────────────────────────────────────────

        /// <summary>카드(Wanted_Bg x2)의 보이는 크기. 시안 x65~216 · y1421~1678.</summary>
        internal static readonly Vector2 VisibleSize = new Vector2(152f, 258f);

        private static readonly Color CardTextColor = new Color(0x86 / 255f, 0x57 / 255f, 0x31 / 255f, 1f);
        private static readonly Color SelectedEdgeColor = new Color(1f, 0.87f, 0f, 1f);
        private static readonly Color LockTextColor = new Color(0.83f, 0f, 0.15f, 1f);

        /// <summary>
        /// 에디터 굽기 전용. <see cref="UIBountySelectDialog.Create"/> 가 부른다.
        /// <paramref name="designCenter"/> 는 카드의 보이는 중심(시안 좌표).
        /// </summary>
        internal static UIBountySlot Create(
            Transform parent, int grade, Vector2 designCenter, Vector2 parentDesignCenter,
            TMP_FontAsset font, Material plainMaterial)
        {
            Vector2 position = UIBountyUIFactory.Local(designCenter.x, designCenter.y, parentDesignCenter);

            // 선택 테두리를 <b>카드의 형제로, 카드보다 먼저</b> 만든다. 자식으로 넣으면
            // uGUI 가 자식을 부모 위에 그려서 테두리가 카드 내용을 통째로 덮는다.
            Image edge = UIBountyUIFactory.CreateSprite("Slot" + grade + "Edge", parent,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 2f,
                VisibleSize + new Vector2(12f + 26f, 12f + 26f), position, true);
            edge.color = SelectedEdgeColor;
            edge.enabled = false;

            // 카드 루트 = 보이는 크기의 투명 판. <b>클릭 면적을 보이는 종이에 맞추려는 것이다</b> —
            // 종이 그림은 투명 여백 때문에 보이는 것보다 사방 48~68px 크고, 그것을 클릭 면적으로
            // 쓰면 이웃 카드(틈 8px)를 누른 탭을 이 카드가 가로챈다.
            Image hit = UIBountyUIFactory.CreateImage("Slot" + grade, parent, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.SetRect(hit.rectTransform, VisibleSize, position);

            var slot = hit.gameObject.AddComponent<UIBountySlot>();
            slot.grade = grade;
            slot.selectionEdge = edge;

            slot.background = UIBountyUIFactory.CreateSprite("Background", hit.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Wanted_Bg"), 2f,
                UIBountyUIFactory.WantedBgRect(VisibleSize.x, VisibleSize.y, 2f), Vector2.zero, true);

            slot.button = hit.gameObject.AddComponent<Button>();

            // 누름·잠김 색은 종이에 먹인다. 투명 판에 먹이면 아무 변화도 안 보인다.
            slot.button.targetGraphic = slot.background;

            Transform card = hit.transform;

            if (grade == 0)
            {
                // "사냥 안하기" 칸 — 초상 대신 빨간 X, 이름은 X 아래.
                UIBountyUIFactory.CreateCross(card,
                    UIBountyUIFactory.Local(designCenter.x, 1531f, designCenter), 84f, 20f);

                slot.nameText = UIBountyUIFactory.CreateText("Name", card, "사냥 안하기", 18f,
                    TextAlignmentOptions.Center, CardTextColor, font, plainMaterial);
                UIBountyUIFactory.SetRect(slot.nameText.rectTransform, new Vector2(VisibleSize.x - 8f, 28f),
                    UIBountyUIFactory.Local(designCenter.x, 1595f, designCenter));
            }
            else
            {
                slot.nameText = UIBountyUIFactory.CreateText("Name", card, "이름", 18f,
                    TextAlignmentOptions.Center, CardTextColor, font, plainMaterial);
                UIBountyUIFactory.SetRect(slot.nameText.rectTransform, new Vector2(VisibleSize.x - 8f, 28f),
                    UIBountyUIFactory.Local(designCenter.x, 1463f, designCenter));

                // 초상 칸(113², cda280). 몬스터를 1.4배로 넣고 칸이 자른다(시안 "클리핑마스크").
                Image portrait = UIBountyUIFactory.CreateImage("Portrait", card, UIBountyUIFactory.Hex(0xCDA280));
                UIBountyUIFactory.SetRect(portrait.rectTransform, new Vector2(113f, 113f),
                    UIBountyUIFactory.Local(designCenter.x, 1542f, designCenter));
                portrait.raycastTarget = false;
                portrait.gameObject.AddComponent<RectMask2D>();

                slot.icon = UIBountyUIFactory.CreateImage("Icon", portrait.transform, Color.white);
                float iconSize = 128f * UIBountyBanner.MonsterScale;
                UIBountyUIFactory.SetRect(slot.icon.rectTransform, new Vector2(iconSize, iconSize), Vector2.zero);
                slot.icon.preserveAspect = true;
                slot.icon.raycastTarget = false;
                slot.icon.enabled = false;

                // 보상 — [재화 아이콘] 수량. 시안은 금화 아이콘이지만 실제로 주는 재화의 아이콘을 쓴다.
                Vector2 rewardPos = UIBountyUIFactory.Local(designCenter.x, 1637f, designCenter);

                slot.rewardText = UIBountyUIFactory.CreateText("Reward", card, "0", 25f,
                    TextAlignmentOptions.Center, CardTextColor, font, plainMaterial);
                UIBountyUIFactory.SetRect(slot.rewardText.rectTransform, new Vector2(VisibleSize.x - 8f, 34f), rewardPos);

                slot.rewardIcon = CreateRewardIcon(card, 30f);
                UIBountyUIFactory.SetRect(slot.rewardIcon.rectTransform, new Vector2(30f, 30f), rewardPos);
                slot.rewardIcon.gameObject.SetActive(false);

                // 잠김 — 초상 칸 아래쪽에 겹친다. 칸 안에 빈자리가 없어 보상 줄을 가리지 않는 곳을 골랐다.
                slot.lockText = UIBountyUIFactory.CreateText("Lock", card, "앞 등급을 먼저", 18f,
                    TextAlignmentOptions.Center, LockTextColor, font, plainMaterial);
                UIBountyUIFactory.SetRect(slot.lockText.rectTransform, new Vector2(VisibleSize.x - 8f, 28f),
                    UIBountyUIFactory.Local(designCenter.x, 1585f, designCenter));
            }

            return slot;
        }
    }
}
