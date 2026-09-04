using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private TMP_Text lockText;

        private Action<int> clickHandler;

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

            if (rewardText != null)
                rewardText.SetText(FormatReward(definition));

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
                rewardText.SetText("이번 판은 부르지 않아요");

            if (icon != null)
                icon.enabled = false;

            ApplyDim(unlocked);
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
            {
                Color c = icon.color;
                c.a = alpha;
                icon.color = c;
            }
        }

        private static void SetAlpha(TMP_Text text, float alpha)
        {
            if (text == null)
                return;

            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }

        private static string FormatReward(BountyDefinition definition)
        {
            string amount = ShortNumberFormat.Format(definition.rewardAmount);

            switch (definition.rewardKind)
            {
                case BountyRewardKind.SummonPoint:
                    return "SP +" + amount;
                case BountyRewardKind.EnhanceStone:
                    return "강화석 +" + amount;
                default:
                    return amount;
            }
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
