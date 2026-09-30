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
                icon.enabled = true;
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
                nameText.SetText("현상금 선택 X");

            if (hpText != null)
                hpText.SetText(string.Empty);

            if (rewardText != null)
            {
                RestoreRewardTextPosition();
                rewardText.SetText("이번 판은 부르지 않아요");
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
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        private static readonly Color SlotColor = new Color(0.14f, 0.17f, 0.31f, 1f);
        private static readonly Color SelectedEdgeColor = new Color(1f, 0.84f, 0.38f, 1f);
        private static readonly Color MutedTextColor = new Color(0.78f, 0.84f, 0.94f, 1f);
        private static readonly Color HpTextColor = new Color(1f, 0.86f, 0.55f, 1f);
        private static readonly Color RewardTextColor = new Color(0.72f, 0.92f, 1f, 1f);
        private static readonly Color LockTextColor = new Color(1f, 0.55f, 0.52f, 1f);

        /// <summary>에디터 굽기 전용. <see cref="UIBountySelectDialog.Create"/> 가 부른다.</summary>
        internal static UIBountySlot Create(
            Transform parent, int grade, Vector2 size, Vector2 position, TMP_FontAsset font)
        {
            // 선택 테두리를 <b>배경의 형제로, 배경보다 먼저</b> 만든다. 자식으로 넣으면
            // uGUI 가 자식을 부모 위에 그려서 테두리가 칸 내용을 통째로 덮는다.
            Image edge = UIBountyUIFactory.CreateImage("Slot" + grade + "Edge", parent, SelectedEdgeColor);
            UIBountyUIFactory.SetRect(edge.rectTransform, size + new Vector2(8f, 8f), position);
            edge.raycastTarget = false;

            Image background = UIBountyUIFactory.CreateImage("Slot" + grade, parent, SlotColor);
            UIBountyUIFactory.SetRect(background.rectTransform, size, position);

            var slot = background.gameObject.AddComponent<UIBountySlot>();
            slot.grade = grade;
            slot.background = background;
            slot.selectionEdge = edge;

            slot.button = background.gameObject.AddComponent<Button>();
            slot.button.targetGraphic = background;

            float halfHeight = size.y * 0.5f;

            slot.nameText = UIBountyUIFactory.CreateText("Name", background.transform, "이름", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(slot.nameText.rectTransform,
                new Vector2(size.x - 16f, 40f), new Vector2(0f, halfHeight - 30f));

            slot.icon = UIBountyUIFactory.CreateImage("Icon", background.transform, Color.white);
            UIBountyUIFactory.SetRect(slot.icon.rectTransform, new Vector2(120f, 120f), new Vector2(0f, 6f));
            slot.icon.preserveAspect = true;
            slot.icon.raycastTarget = false;

            slot.hpText = UIBountyUIFactory.CreateText("Hp", background.transform, "HP 0", 26f,
                TextAlignmentOptions.Center, HpTextColor, font);
            UIBountyUIFactory.SetRect(slot.hpText.rectTransform,
                new Vector2(size.x - 16f, 34f), new Vector2(0f, -halfHeight + 72f));

            slot.rewardText = UIBountyUIFactory.CreateText("Reward", background.transform, "보상", 26f,
                TextAlignmentOptions.Center, RewardTextColor, font);
            UIBountyUIFactory.SetRect(slot.rewardText.rectTransform,
                new Vector2(size.x - 16f, 34f), new Vector2(0f, -halfHeight + 36f));
            slot.rewardText.textWrappingMode = TextWrappingModes.Normal;

            slot.rewardIcon = CreateRewardIcon(background.transform, 30f);
            UIBountyUIFactory.SetRect(slot.rewardIcon.rectTransform,
                new Vector2(30f, 30f), new Vector2(0f, -halfHeight + 36f));
            slot.rewardIcon.gameObject.SetActive(false);

            slot.lockText = UIBountyUIFactory.CreateText("Lock", background.transform, "앞 등급을 먼저", 24f,
                TextAlignmentOptions.Center, LockTextColor, font);
            UIBountyUIFactory.SetRect(slot.lockText.rectTransform,
                new Vector2(size.x - 16f, 32f), new Vector2(0f, halfHeight - 64f));

            // "소환 X" 칸은 X 표시가 아이콘을 대신한다. 스크린샷의 첫 칸 그대로다.
            if (grade == 0)
            {
                TMP_Text cross = UIBountyUIFactory.CreateText("Cross", background.transform, "X", 96f,
                    TextAlignmentOptions.Center, MutedTextColor, font);
                UIBountyUIFactory.SetRect(cross.rectTransform, new Vector2(140f, 140f), new Vector2(0f, 6f));
            }

            return slot;
        }
    }
}
