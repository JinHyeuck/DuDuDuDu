using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Battle;
using OJ.Core;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금 선택 창의 카드 한 장 — 등급 별 · 몬스터 초상 · HP · 보상(아이콘 + 수량).
    ///
    /// <b>잠긴 카드는 보이되 눌리지 않는다.</b> 내용을 지우면 다음에 무엇을 노릴지 계획할 수 없어서
    /// 순서대로 잡는 것 말고 할 일이 없어진다. 눌린 등급은 <see cref="UIBountySelectDialog"/> 가 곧바로
    /// 소환한다(고르면 바로 나온다 — 별도의 "선택됨" 표시가 필요 없다).
    ///
    /// 모양은 예전 선택 창의 종이 카드(<c>Wanted_Bg</c> + 갈색 글자)를 그대로 쓰되, 한 장의 크기를 키워
    /// HP·보상·아이콘이 한눈에 읽히게 했다(스크린샷의 3x2 큰 카드).
    /// </summary>
    public class UIBountySlot : MonoBehaviour
    {
        [SerializeField] private int grade;

        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image[] starImages;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text lockText;

        private static readonly Color DimColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        private Action<int> clickHandler;

        public int Grade => grade;

        public void Bind(BountyDefinition definition, int hp, bool unlocked, Action<int> onClick)
        {
            clickHandler = onClick;

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
                button.interactable = unlocked;
            }

            if (lockText != null)
                lockText.gameObject.SetActive(!unlocked);

            if (starImages != null)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    if (starImages[i] != null)
                        starImages[i].gameObject.SetActive(i < grade);
                }
            }

            if (definition == null)
            {
                // 데이터 사고다. 빈 카드로 두고 눌리지 않게 한다.
                if (nameText != null) nameText.SetText(string.Empty);
                if (hpText != null) hpText.SetText(string.Empty);
                if (rewardText != null) rewardText.SetText(string.Empty);
                if (rewardIcon != null) rewardIcon.gameObject.SetActive(false);
                if (icon != null) icon.enabled = false;
                if (button != null) button.interactable = false;
                ApplyDim(false);
                return;
            }

            if (nameText != null)
                nameText.SetText(definition.displayName);

            if (hpText != null)
                hpText.SetText("HP " + ShortNumberFormat.Format(hp));

            Sprite rewardSprite = BattlePointUtility.GetIcon(definition.rewardKind);
            if (rewardIcon != null)
            {
                rewardIcon.gameObject.SetActive(rewardSprite != null);
                rewardIcon.sprite = rewardSprite;
            }

            if (rewardText != null)
            {
                // 아이콘이 없으면 재화 이름을 글자로 적는다(보상이 무엇인지 모른 채 숫자만 남으면 안 된다).
                rewardText.SetText(rewardSprite != null
                    ? "+" + ShortNumberFormat.Format(definition.rewardAmount)
                    : definition.FormatReward());
            }

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

        private void ApplyDim(bool unlocked)
        {
            if (background != null)
                background.color = unlocked ? Color.white : DimColor;

            if (icon != null && !unlocked)
                icon.color = icon.color * DimColor;
        }

        private void OnClick()
        {
            clickHandler?.Invoke(grade);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        /// <summary>카드(Wanted_Bg x2)의 보이는 크기.</summary>
        internal static readonly Vector2 VisibleSize = new Vector2(296f, 396f);

        private static readonly Color CardTextColor = new Color(0x86 / 255f, 0x57 / 255f, 0x31 / 255f, 1f);
        private static readonly Color LockTextColor = new Color(0.83f, 0f, 0.15f, 1f);

        /// <summary>에디터 굽기 전용. 좌표는 카드 중심 기준 로컬이다.</summary>
        internal static UIBountySlot Create(
            Transform parent, int grade, Vector2 position, TMP_FontAsset font, Material plainMaterial)
        {
            Image hit = UIBountyUIFactory.CreateImage("Slot" + grade, parent, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.SetRect(hit.rectTransform, VisibleSize, position);

            var slot = hit.gameObject.AddComponent<UIBountySlot>();
            slot.grade = grade;

            // Wanted_Bg(128²) 는 여백이 좌우 24 · 위아래 34 px 이라 x2 로 키워 보이는 크기에 맞춘다.
            slot.background = UIBountyUIFactory.CreateSprite("Background", hit.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Wanted_Bg"), 2f,
                UIBountyUIFactory.WantedBgRect(VisibleSize.x, VisibleSize.y, 2f), Vector2.zero, true);
            slot.background.raycastTarget = true;

            slot.button = hit.gameObject.AddComponent<Button>();
            slot.button.targetGraphic = slot.background;

            Transform card = hit.transform;
            float top = VisibleSize.y / 2f;

            // 별 — 등급 수만큼. 최대 5개가 카드 폭에 들어가는 간격으로 가운데 정렬해 둔다.
            Sprite star = UIBountyUIFactory.LoadSprite("Upgrade/Icon_Star");
            const int MaxStars = 5;
            const float StarSize = 36f;
            const float StarGap = 4f;
            slot.starImages = new Image[MaxStars];
            for (int i = 0; i < MaxStars; i++)
            {
                float x = (i - (MaxStars - 1) * 0.5f) * (StarSize + StarGap);
                Image s = UIBountyUIFactory.CreateSprite("Star" + i, card, star, 1f,
                    new Vector2(StarSize, StarSize), new Vector2(x, top - 44f), false);
                s.preserveAspect = true;
                slot.starImages[i] = s;
            }

            // 초상 칸(cda280). 몬스터 아이콘을 넣고 칸이 자른다.
            Image portrait = UIBountyUIFactory.CreateImage("Portrait", card, UIBountyUIFactory.Hex(0xCDA280));
            UIBountyUIFactory.SetRect(portrait.rectTransform, new Vector2(160f, 160f), new Vector2(0f, top - 150f));
            portrait.raycastTarget = false;
            portrait.gameObject.AddComponent<RectMask2D>();

            slot.icon = UIBountyUIFactory.CreateImage("Icon", portrait.transform, Color.white);
            UIBountyUIFactory.SetRect(slot.icon.rectTransform, new Vector2(160f, 160f), Vector2.zero);
            slot.icon.preserveAspect = true;
            slot.icon.raycastTarget = false;

            slot.nameText = UIBountyUIFactory.CreateText("Name", card, "이름", 28f,
                TextAlignmentOptions.Center, CardTextColor, font, plainMaterial);
            UIBountyUIFactory.SetRect(slot.nameText.rectTransform, new Vector2(VisibleSize.x - 24f, 38f), new Vector2(0f, top - 258f));

            // HP — 크고 갈색. 스펙이 이 카드의 본론이다.
            slot.hpText = UIBountyUIFactory.CreateText("Hp", card, "HP 0", 34f,
                TextAlignmentOptions.Center, CardTextColor, font, plainMaterial);
            UIBountyUIFactory.SetRect(slot.hpText.rectTransform, new Vector2(VisibleSize.x - 24f, 46f), new Vector2(0f, top - 302f));

            // 보상 — [재화 아이콘] +수량. 흰 글자 + 외곽선.
            slot.rewardIcon = UIBountyUIFactory.CreateImage("RewardIcon", card, Color.white);
            UIBountyUIFactory.SetRect(slot.rewardIcon.rectTransform, new Vector2(40f, 40f), new Vector2(-48f, -top + 38f));
            slot.rewardIcon.preserveAspect = true;
            slot.rewardIcon.raycastTarget = false;

            slot.rewardText = UIBountyUIFactory.CreateText("Reward", card, "+0", 34f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(slot.rewardText.rectTransform, new Vector2(150f, 46f), new Vector2(44f, -top + 38f));

            // 잠김 — 초상 칸 아래쪽에 겹친다.
            slot.lockText = UIBountyUIFactory.CreateText("Lock", card, "앞 등급을 먼저", 26f,
                TextAlignmentOptions.Center, LockTextColor, font, plainMaterial);
            UIBountyUIFactory.SetRect(slot.lockText.rectTransform, new Vector2(VisibleSize.x - 16f, 36f), new Vector2(0f, top - 208f));
            slot.lockText.gameObject.SetActive(false);

            return slot;
        }
    }
}
