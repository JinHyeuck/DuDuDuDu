using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;

namespace OJ.Dice
{
    /// <summary>
    /// 상점 카드 한 장의 화면. <b>그리기만 한다</b> — 무엇을 살지·살 수 있는지는
    /// <see cref="UICardShopPanel"/> 이 알려 주고, 눌림은 이벤트로 올려보낸다.
    ///
    /// 모양은 현상금 선택 창의 카드(<c>Wanted_Bg</c> 종이 + 갈색 글자)를 그대로 빌렸다 — 같은 화면에
    /// 종이 카드가 둘 다 뜨면 서로 다른 게임처럼 보이지 않게.
    /// </summary>
    public sealed class UIShopCardSlot : MonoBehaviour
    {
        /// <summary>카드(Wanted_Bg x2)의 보이는 크기.</summary>
        internal static readonly Vector2 VisibleSize = new Vector2(296f, 392f);

        private static readonly Color CardTextColor = new Color(0x86 / 255f, 0x57 / 255f, 0x31 / 255f, 1f);
        private static readonly Color LackColor = new Color(0.83f, 0f, 0.15f, 1f);
        private static readonly Color DimColor = new Color(0.62f, 0.62f, 0.62f, 1f);

        // 신화 카드의 바탕. 종이(Wanted_Bg)에 곱해지므로 주황빛 종이가 된다.
        private static readonly Color MythicColor = new Color(1f, 0.58f, 0.18f, 1f);

        [SerializeField] private Button buyButton;
        [SerializeField] private Image background;
        [SerializeField] private Button lockButton;
        [SerializeField] private Image lockIcon;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite unlockedSprite;
        [SerializeField] private TMP_Text kindText;
        [SerializeField] private GameObject ingredientRow;
        [SerializeField] private Image mainIcon;
        [SerializeField] private TMP_Text mainStarText;
        [SerializeField] private Image plusIcon;
        [SerializeField] private Image subIcon;
        [SerializeField] private TMP_Text subStarText;
        [SerializeField] private Image resultIcon;
        [SerializeField] private TMP_Text resultStarText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text mythicText;

        private int index;

        /// <summary>카드를 눌렀다(산다). 인자는 칸 번호.</summary>
        public event Action<int> BuyClicked;

        /// <summary>잠금 버튼을 눌렀다.</summary>
        public event Action<int> LockClicked;

        public void SetIndex(int value)
        {
            index = value;
        }

        private void Awake()
        {
            if (buyButton != null)
                buyButton.onClick.AddListener(() => BuyClicked?.Invoke(index));

            if (lockButton != null)
                lockButton.onClick.AddListener(() => LockClicked?.Invoke(index));
        }

        /// <summary>
        /// 카드를 그린다. <paramref name="canBuy"/> 는 재료·빈 칸이 갖춰졌는가,
        /// <paramref name="affordable"/> 는 재화가 되는가 — 둘 중 하나라도 아니면 회색이다.
        /// </summary>
        public void Bind(ShopCard card, bool canBuy, bool affordable)
        {
            bool empty = card.IsEmpty;
            bool mythic = card.IsMythic;
            bool normal = !empty && !mythic;

            // 신화 카드는 재료·결과를 카드에 적지 않는다. 글자 네 줄이 전부다.
            mythicText.gameObject.SetActive(mythic);
            kindText.gameObject.SetActive(normal || empty);
            ingredientRow.SetActive(normal && card.MainCount > 0);
            ApplyResultLayout(normal && card.MainCount == 0);
            resultIcon.gameObject.SetActive(normal);
            resultStarText.gameObject.SetActive(normal);
            nameText.gameObject.SetActive(normal);
            costText.gameObject.SetActive(normal);
            lockButton.gameObject.SetActive(!empty);

            if (empty)
            {
                kindText.text = "품절";
                background.color = DimColor;
                buyButton.interactable = false;
                return;
            }

            lockIcon.sprite = card.Locked ? lockedSprite : unlockedSprite;

            if (mythic)
            {
                // 눌러서 목록을 여는 카드라 재화가 모자라도 눌린다. 모자란 것은 SP 줄만 붉게 알린다.
                background.color = MythicColor;
                buyButton.interactable = true;

                string cost = "SP" + card.Cost;
                if (!affordable)
                    cost = "<color=#C8001F>" + cost + "</color>";
                mythicText.text = "신화\n다이스\n소환\n" + cost;
                return;
            }

            kindText.text = KindLabel(card.Kind);
            bool usable = canBuy && affordable;
            background.color = usable ? Color.white : DimColor;
            buyButton.interactable = usable;

            // 재료 줄: 지급 카드에는 없다. 머지는 같은 다이스 둘, 조합은 주재료 + 보조.
            if (card.MainCount > 0)
            {
                SetDice(mainIcon, mainStarText, card.MainType, card.MainStar);

                bool hasSub = card.SubCount > 0;
                DiceType subType = hasSub ? card.SubType : card.MainType;
                int subStar = hasSub ? card.SubStar : card.MainStar;
                SetDice(subIcon, subStarText, subType, subStar);
            }

            SetDice(resultIcon, resultStarText, card.Result, card.ResultStar);
            nameText.text = NameOf(card.Result);

            costText.text = card.Cost + " SP";
            costText.color = affordable ? Color.white : LackColor;
        }

        private static void SetDice(Image icon, TMP_Text starText, DiceType type, int star)
        {
            icon.sprite = DiceMetaDataProvider.GetIcon(type, star);
            icon.enabled = icon.sprite != null;

            // 성급이 그림으로 드러나거나 성급 개념이 없는 다이스(레어·신화)에는 숫자를 달지 않는다.
            bool showStar = DiceMetaDataProvider.ShowStarUI(type) && !DiceMetaDataProvider.HasStarIcons(type);
            starText.gameObject.SetActive(showStar);
            if (showStar)
                starText.text = star + "성";
        }

        internal static string NameOf(DiceType type)
        {
            DiceMetaDataDatabase.DiceMeta meta = DiceMetaDataProvider.GetMeta(type);
            return meta != null && !string.IsNullOrEmpty(meta.displayName) ? meta.displayName : type.ToString();
        }

        private static string KindLabel(ShopCardKind kind)
        {
            switch (kind)
            {
                case ShopCardKind.GrantDice: return "다이스 지급";
                case ShopCardKind.MergeDice: return "다이스 머지";
                case ShopCardKind.CraftRare: return "레어 조합";
                case ShopCardKind.MythicSummon: return "신화 소환";
                default: return string.Empty;
            }
        }

        // ── 결과 칸 배치 ─────────────────────────────────────────────
        //
        // 재료가 있는 카드(머지·조합)는 결과를 아래 왼쪽에, 재료가 없는 카드(지급)는 빈 윗부분까지
        // 써서 크게 가운데에 둔다. 값은 한 곳에 두어 굽기와 런타임이 같은 것을 쓴다.

        private static readonly Vector2 CraftIconPos = new Vector2(-84f, -62f);
        private const float CraftIconSize = 112f;
        private static readonly Vector2 CraftNamePos = new Vector2(58f, -40f);
        private static readonly Vector2 CraftNameSize = new Vector2(150f, 70f);

        private static readonly Vector2 GrantIconPos = new Vector2(0f, 46f);
        private const float GrantIconSize = 168f;
        private static readonly Vector2 GrantNamePos = new Vector2(0f, -62f);
        private static readonly Vector2 GrantNameSize = new Vector2(270f, 50f);

        private void ApplyResultLayout(bool grant)
        {
            Vector2 iconPos = grant ? GrantIconPos : CraftIconPos;
            float iconSize = grant ? GrantIconSize : CraftIconSize;

            SetRect(resultIcon.rectTransform, new Vector2(iconSize, iconSize), iconPos);

            // 성급 글자는 아이콘 오른쪽 아래 귀퉁이에 걸친다.
            SetRect(resultStarText.rectTransform, new Vector2(64f, 30f),
                iconPos + new Vector2(iconSize * 0.34f, -iconSize * 0.42f));

            SetRect(nameText.rectTransform, grant ? GrantNameSize : CraftNameSize, grant ? GrantNamePos : CraftNamePos);
            nameText.alignment = grant ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        }

        private static void SetRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        // ── 에디터 굽기 전용 ──────────────────────────────────────────

        /// <summary>
        /// 에디터 굽기 전용. 좌표는 <b>카드 중심 기준 로컬</b>이다 — 카드가 움직여도 안의 배치는 그대로다.
        /// </summary>
        internal static UIShopCardSlot Create(
            Transform parent, int index, Vector2 position, TMP_FontAsset font, Material plainMaterial)
        {
            Image hit = UIBountyUIFactory.CreateImage("Card" + index, parent, new Color(1f, 1f, 1f, 0f));
            UIBountyUIFactory.SetRect(hit.rectTransform, VisibleSize, position);

            var slot = hit.gameObject.AddComponent<UIShopCardSlot>();
            slot.index = index;

            // Wanted_Bg(128²) 는 여백이 좌우 24 · 위아래 34 px 이라 x2 로 키워 보이는 크기에 맞춘다.
            slot.background = UIBountyUIFactory.CreateSprite("Background", hit.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Wanted_Bg"), 2f,
                UIBountyUIFactory.WantedBgRect(VisibleSize.x, VisibleSize.y, 2f), Vector2.zero, true);
            slot.background.raycastTarget = true;

            slot.buyButton = hit.gameObject.AddComponent<Button>();
            slot.buyButton.targetGraphic = slot.background;

            Transform card = hit.transform;
            float top = VisibleSize.y / 2f;

            slot.kindText = UIBountyUIFactory.CreateText("Kind", card, "다이스 지급", 30f,
                TextAlignmentOptions.Center, CardTextColor, font, plainMaterial);
            UIBountyUIFactory.SetRect(slot.kindText.rectTransform, new Vector2(200f, 40f), new Vector2(-28f, top - 38f));

            // 재료 줄 — [주재료] + [보조].
            GameObject row = UIBountyUIFactory.CreateRect("Ingredients", card);
            UIBountyUIFactory.SetRect(row.GetComponent<RectTransform>(), new Vector2(290f, 130f), new Vector2(0f, 62f));
            slot.ingredientRow = row;

            slot.mainIcon = CreateDiceIcon("Main", row.transform, new Vector2(-78f, 0f), 104f);
            slot.mainStarText = CreateStarText("MainStar", row.transform, new Vector2(-78f + 34f, -42f), font);
            slot.plusIcon = UIBountyUIFactory.CreateSprite("Plus", row.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Icon_Plus"), 1f, new Vector2(34f, 34f), Vector2.zero, false);
            slot.subIcon = CreateDiceIcon("Sub", row.transform, new Vector2(78f, 0f), 104f);
            slot.subStarText = CreateStarText("SubStar", row.transform, new Vector2(78f + 34f, -42f), font);

            // 결과 — 아이콘과 이름. 배치는 ApplyResultLayout 이 정한다.
            slot.resultIcon = CreateDiceIcon("Result", card, Vector2.zero, CraftIconSize);
            slot.resultStarText = CreateStarText("ResultStar", card, Vector2.zero, font);

            slot.nameText = UIBountyUIFactory.CreateText("Name", card, "이름", 30f,
                TextAlignmentOptions.Left, CardTextColor, font, plainMaterial);
            slot.nameText.textWrappingMode = TextWrappingModes.Normal;

            slot.costText = UIBountyUIFactory.CreateText("Cost", card, "2 SP", 38f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(slot.costText.rectTransform, new Vector2(270f, 48f), new Vector2(0f, -top + 50f));

            slot.ApplyResultLayout(false);

            // 신화 카드의 글자 — 네 줄(신화 / 다이스 / 소환 / SP50). 카드를 가득 채운다.
            slot.mythicText = UIBountyUIFactory.CreateText("Mythic", card, "신화\n다이스\n소환\nSP50", 56f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(slot.mythicText.rectTransform, new Vector2(250f, 300f), new Vector2(0f, -8f));
            slot.mythicText.lineSpacing = -10f;
            slot.mythicText.gameObject.SetActive(false);

            // 잠금 — 카드 오른쪽 위. 카드 전체 버튼과 별개라 누르면 구매가 되지 않는다.
            Image lockBg = UIBountyUIFactory.CreateSprite("Lock", card,
                UIBountyUIFactory.LoadSprite("Ingame/Btn_Gray"), 3f, new Vector2(56f + 24f, 56f + 24f),
                new Vector2(VisibleSize.x / 2f - 52f, top - 52f), true);
            lockBg.raycastTarget = true;
            slot.lockButton = lockBg.gameObject.AddComponent<Button>();
            slot.lockButton.targetGraphic = lockBg;

            slot.lockedSprite = UIBountyUIFactory.LoadSprite("Upgrade/Icon_lock");
            slot.unlockedSprite = UIBountyUIFactory.LoadSprite("Upgrade/Icon_Unlock");
            slot.lockIcon = UIBountyUIFactory.CreateSprite("LockIcon", lockBg.transform,
                slot.unlockedSprite, 1f, new Vector2(40f, 40f), Vector2.zero, false);
            slot.lockIcon.preserveAspect = true;

            return slot;
        }

        private static Image CreateDiceIcon(string name, Transform parent, Vector2 position, float size)
        {
            Image icon = UIBountyUIFactory.CreateImage(name, parent, Color.white);
            UIBountyUIFactory.SetRect(icon.rectTransform, new Vector2(size, size), position);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static TMP_Text CreateStarText(string name, Transform parent, Vector2 position, TMP_FontAsset font)
        {
            TMP_Text text = UIBountyUIFactory.CreateText(name, parent, "1성", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(text.rectTransform, new Vector2(64f, 30f), position);
            return text;
        }
    }
}
