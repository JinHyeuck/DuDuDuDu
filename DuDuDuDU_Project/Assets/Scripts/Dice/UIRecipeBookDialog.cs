using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bounty;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 조합식 목록 팝업 — 레어 5줄 · 신화 5줄. 카드 상점의 [조합식] 버튼이 연다.
    ///
    /// <b>값은 전부 <see cref="DiceRecipes"/> 에서 읽는다.</b> 프리팹에는 칸의 뼈대만 구워 두고
    /// 아이콘·이름·비용은 열 때 채운다 — 식이나 비용을 고쳐도 다시 굽지 않는다.
    /// </summary>
    public sealed class UIRecipeBookDialog : DialogBase
    {
        [Serializable]
        private sealed class Row
        {
            public Image mainIcon;
            public TMP_Text mainText;
            public Image subIcon;
            public TMP_Text subText;
            public Image resultIcon;
            public TMP_Text nameText;
            public TMP_Text costText;
        }

        [SerializeField] private Row[] rareRows;
        [SerializeField] private Row[] mythicRows;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button outsideButton;

        protected override void OnEnter()
        {
            Fill(rareRows, DiceRecipes.Rare, DiceRecipes.RareCostStone);
            Fill(mythicRows, DiceRecipes.Mythic, DiceRecipes.MythicCostStone);
        }

        private static void Fill(Row[] rows, System.Collections.Generic.IReadOnlyList<DiceRecipe> recipes, int cost)
        {
            for (int i = 0; i < rows.Length && i < recipes.Count; i++)
            {
                DiceRecipe r = recipes[i];
                Row row = rows[i];

                SetDice(row.mainIcon, row.mainText, r.MainType, r.MainStar);
                SetDice(row.subIcon, row.subText, r.SubType, r.SubStar);
                SetDice(row.resultIcon, null, r.Result, 1);

                row.nameText.text = UIShopCardSlot.NameOf(r.Result);
                row.costText.text = "마석 " + cost;
            }
        }

        private static void SetDice(Image icon, TMP_Text starText, DiceType type, int star)
        {
            icon.sprite = DiceMetaDataProvider.GetIcon(type, star);
            icon.enabled = icon.sprite != null;

            if (starText == null)
                return;

            // 레어·신화 재료는 성급 개념이 없어 "1성" 이라고 적지 않는다.
            bool showStar = DiceMetaDataProvider.ShowStarUI(type);
            starText.gameObject.SetActive(showStar);
            if (showStar)
                starText.text = star + "성";
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        private static readonly Vector2 PanelSizeVisible = new Vector2(940f, 1340f);
        private const float RowHeight = 80f;
        private const float RowGap = 6f;

        private static readonly Color CardTextColor = new Color(0x86 / 255f, 0x57 / 255f, 0x31 / 255f, 1f);
        private static readonly Color RowColor = new Color(0x63 / 255f, 0x64 / 255f, 0x81 / 255f, 0.9f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UIRecipeBookDialog Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIRecipeBookDialog", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIRecipeBookDialog>();
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            // 바깥을 누르면 닫힌다. 어둡게 깔아 뒤 화면과 구분한다.
            Image blocker = UIBountyUIFactory.CreateImage("OutsideCatcher", view.transform, new Color(0f, 0f, 0f, 0.6f));
            UIBountyUIFactory.Stretch(blocker.rectTransform);
            dialog.outsideButton = blocker.gameObject.AddComponent<Button>();
            dialog.outsideButton.targetGraphic = blocker;
            dialog.outsideButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dialog.outsideButton);

            // Ui_Popup_Bg 는 사방 3px 투명 여백이 있어 x4 로 12px 씩 키운다(현상금 선택 창과 같다).
            Image panelImage = UIBountyUIFactory.CreateSprite("Panel", view.transform,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_Bg"), 4f,
                PanelSizeVisible + new Vector2(24f, 24f), Vector2.zero, true);
            panelImage.raycastTarget = true;
            RectTransform panel = panelImage.rectTransform;

            float top = PanelSizeVisible.y / 2f;

            TMP_Text title = UIBountyUIFactory.CreateText("Title", panel, "조합식", 44f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(title.rectTransform, new Vector2(500f, 64f), new Vector2(0f, top - 56f));

            TMP_Text hint = UIBountyUIFactory.CreateText("Hint", panel,
                "재료는 보드에 있어야 해요. 결과는 첫 재료의 자리에 놓여요.", 24f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(hint.rectTransform, new Vector2(880f, 36f), new Vector2(0f, top - 108f));

            float y = top - 160f;
            dialog.rareRows = BuildSection(panel, "레어 조합", ref y, DiceRecipes.Rare.Count, font, plainMaterial);
            y -= 14f;
            dialog.mythicRows = BuildSection(panel, "신화 조합", ref y, DiceRecipes.Mythic.Count, font, plainMaterial);

            dialog.closeButton = UICardShopPanel.CreateKitButton("CloseButton", panel, "Ingame/Btn_Gray",
                new Vector2(200f, 60f), new Vector2(0f, -top + 52f), "닫기", font, out _);
            dialog.AddExitButton(dialog.closeButton);

            return dialog;
        }

        private static Row[] BuildSection(
            RectTransform panel, string header, ref float y, int count, TMP_FontAsset font, Material plainMaterial)
        {
            TMP_Text headerText = UIBountyUIFactory.CreateText("Header", panel, header, 32f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(headerText.rectTransform, new Vector2(860f, 44f), new Vector2(0f, y - 22f));
            y -= 50f;

            var rows = new Row[count];
            for (int i = 0; i < count; i++)
            {
                rows[i] = BuildRow(panel, new Vector2(0f, y - RowHeight / 2f), font, plainMaterial);
                y -= RowHeight + RowGap;
            }

            return rows;
        }

        private static Row BuildRow(RectTransform panel, Vector2 position, TMP_FontAsset font, Material plainMaterial)
        {
            Image bg = UIBountyUIFactory.CreateSprite("Row", panel,
                UIBountyUIFactory.LoadSprite("Upgrade/Ui_Popup_SmallBox"), 3f,
                new Vector2(860f + 26f, RowHeight + 26f), position, true);
            bg.color = RowColor;

            Transform r = bg.transform;
            var row = new Row();

            row.mainIcon = Icon("Main", r, new Vector2(-340f, 4f), 58f);
            row.mainText = Small("MainStar", r, new Vector2(-340f, -28f), font);

            UIBountyUIFactory.CreateSprite("Plus", r, UIBountyUIFactory.LoadSprite("Ingame/Icon_Plus"), 1f,
                new Vector2(24f, 24f), new Vector2(-270f, 4f), false);

            row.subIcon = Icon("Sub", r, new Vector2(-200f, 4f), 58f);
            row.subText = Small("SubStar", r, new Vector2(-200f, -28f), font);

            // 화살표 — 글꼴에 → 가 없을 수 있어 그림으로 둔다(PORTING.md).
            Image arrow = UIBountyUIFactory.CreateSprite("Arrow", r, UIBountyUIFactory.LoadSprite("Ingame/Icon_Play"), 1f,
                new Vector2(30f, 30f), new Vector2(-124f, 4f), false);
            arrow.preserveAspect = true;

            row.resultIcon = Icon("Result", r, new Vector2(-50f, 2f), 66f);

            row.nameText = UIBountyUIFactory.CreateText("Name", r, "이름", 30f,
                TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(row.nameText.rectTransform, new Vector2(240f, 40f), new Vector2(110f, 0f));

            row.costText = UIBountyUIFactory.CreateText("Cost", r, "마석 10", 30f,
                TextAlignmentOptions.Right, Color.white, font);
            UIBountyUIFactory.SetRect(row.costText.rectTransform, new Vector2(200f, 40f), new Vector2(320f, 0f));

            return row;
        }

        private static Image Icon(string name, Transform parent, Vector2 position, float size)
        {
            Image icon = UIBountyUIFactory.CreateImage(name, parent, Color.white);
            UIBountyUIFactory.SetRect(icon.rectTransform, new Vector2(size, size), position);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static TMP_Text Small(string name, Transform parent, Vector2 position, TMP_FontAsset font)
        {
            TMP_Text text = UIBountyUIFactory.CreateText(name, parent, "1성", 20f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(text.rectTransform, new Vector2(80f, 24f), position);
            return text;
        }
    }
}
