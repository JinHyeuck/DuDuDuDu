using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OJ
{
    public static class UIEquipmentRuntimeBuilder
    {
        private static readonly Color PageBackground = new Color32(15, 22, 35, 255);
        private static readonly Color PanelBackground = new Color32(24, 34, 53, 255);
        private static readonly Color CardBackground = new Color32(35, 48, 74, 255);
        private static readonly Color Accent = new Color32(90, 195, 255, 255);
        private static readonly Color AccentSoft = new Color32(56, 88, 124, 255);
        private static readonly Color Dimmed = new Color(14f / 255f, 18f / 255f, 28f / 255f, 210f / 255f);
        private static readonly Color White = new Color32(245, 248, 255, 255);
        private static readonly Color Muted = new Color32(181, 192, 214, 255);
        private static Sprite sharedSprite;

        public static void Build(UIEquipmentPage page)
        {
            if (page == null || page.dialogView == null)
                return;

            RectTransform view = page.dialogView.GetComponent<RectTransform>();
            if (view == null)
                return;

            Transform existingRoot = view.Find("EquipmentRuntimeRoot");
            if (existingRoot != null)
            {
                existingRoot.SetAsLastSibling();
                return;
            }

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            RectTransform root = CreateStretchObject("EquipmentRuntimeRoot", view);
            root.SetAsLastSibling();
            AddImage(root.gameObject, PageBackground);

            RectTransform header = CreateObject("Header", root, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -132f), new Vector2(-24f, -24f));
            AddImage(header.gameObject, PanelBackground);
            CreateText("Title", header, "Equipment Loadout", 40, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(20f, 64f), new Vector2(-20f, 16f), font);
            TMP_Text totalAttackText = CreateText("TotalAttack", header, "총 장비 공격력 0", 30, FontStyles.Bold, Accent, TextAlignmentOptions.Left, new Vector2(20f, 22f), new Vector2(-20f, -14f), font);
            CreateText("Summary", header, "장비 강화와 보석 장착 상태를 한 번에 확인할 수 있습니다.", 22, FontStyles.Normal, Muted, TextAlignmentOptions.Left, new Vector2(20f, -12f), new Vector2(-20f, -52f), font);

            RectTransform content = CreateObject("Content", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 148f), new Vector2(-24f, -148f));
            HorizontalLayoutGroup contentLayout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 18f;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;

            RectTransform equipmentPanel = CreateSection(content, "장비", "장비 레벨과 슬롯 상태", font);
            CreateScrollContent(equipmentPanel, out RectTransform equipmentListRoot);

            RectTransform gemPanel = CreateSection(content, "보석 인벤토리", "장착 가능한 보석 목록", font);
            CreateScrollContent(gemPanel, out RectTransform gemInventoryRoot);

            RectTransform footer = CreateObject("Footer", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 116f));
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 14f;
            footerLayout.childAlignment = TextAnchor.MiddleCenter;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = true;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;

            Button openDetailButton = CreateActionButton(footer, "선택 장비 상세", Accent, White, font);
            Button levelUpAllButton = CreateActionButton(footer, "전체 강화", new Color32(255, 164, 84, 255), White, font);
            Button removeAllGemButton = CreateActionButton(footer, "보석 전체 해제", new Color32(114, 128, 162, 255), White, font);

            RectTransform templates = CreateObject("Templates", root, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, Vector2.zero);
            templates.gameObject.SetActive(false);

            UIEquipmentItem equipmentItemPrefab = CreateEquipmentItemTemplate(templates, font);
            UIGemInventoryItem gemInventoryItemPrefab = CreateGemInventoryItemTemplate(templates, font, "GemInventoryItemTemplate");
            UIEquipmentGemSlotItem slotItemPrefab = CreateGemSlotTemplate(templates, font);
            UIGemInventoryItem gemSelectItemPrefab = CreateGemInventoryItemTemplate(templates, font, "GemSelectItemTemplate");

            UIEquipmentConfirmDialog confirmDialog = CreateConfirmDialog(root, font);
            UIEquipmentGemSelectDialog gemSelectDialog = CreateGemSelectDialog(root, font, gemSelectItemPrefab);
            UIEquipmentDetailDialog detailDialog = CreateDetailDialog(root, font, slotItemPrefab, gemSelectDialog, confirmDialog);

            page.ConfigureRuntime(
                totalAttackText,
                equipmentListRoot,
                equipmentItemPrefab,
                gemInventoryRoot,
                gemInventoryItemPrefab,
                openDetailButton,
                levelUpAllButton,
                removeAllGemButton,
                detailDialog,
                confirmDialog);
        }

        private static RectTransform CreateSection(RectTransform parent, string title, string subtitle, TMP_FontAsset font)
        {
            RectTransform panel = CreateStretchObject(title.Replace(" ", string.Empty) + "Panel", parent);
            AddImage(panel.gameObject, PanelBackground);
            LayoutElement layoutElement = panel.gameObject.AddComponent<LayoutElement>();
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 1f;

            CreateText("Title", panel, title, 30, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(20f, 56f), new Vector2(-20f, 12f), font);
            CreateText("Subtitle", panel, subtitle, 20, FontStyles.Normal, Muted, TextAlignmentOptions.Left, new Vector2(20f, 20f), new Vector2(-20f, -12f), font);
            return panel;
        }

        private static void CreateScrollContent(RectTransform parent, out RectTransform content)
        {
            RectTransform scrollRoot = CreateObject("Scroll", parent, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 20f), new Vector2(-20f, -92f));
            AddImage(scrollRoot.gameObject, AccentSoft);
            ScrollRect scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 32f;

            RectTransform viewport = CreateStretchObject("Viewport", scrollRoot);
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            AddImage(viewport.gameObject, new Color(1f, 1f, 1f, 0.02f));

            content = CreateObject("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -10f), new Vector2(-10f, 0f));
            VerticalLayoutGroup layoutGroup = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 12f;
            layoutGroup.padding = new RectOffset(0, 0, 0, 10);
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = content;
        }

        private static UIEquipmentItem CreateEquipmentItemTemplate(Transform parent, TMP_FontAsset font)
        {
            RectTransform root = CreateTemplateRoot("EquipmentItemTemplate", parent, 164f);
            Button clickButton = root.gameObject.AddComponent<Button>();
            Image selectedFrame = AddOutlineFrame(root, Accent);

            TMP_Text nameText = CreateText("Name", root, "장비", 28, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(18f, 100f), new Vector2(-180f, 60f), font);
            TMP_Text levelText = CreateText("Level", root, "Lv.1", 22, FontStyles.Bold, Accent, TextAlignmentOptions.Left, new Vector2(18f, 62f), new Vector2(-180f, 28f), font);
            TMP_Text attackText = CreateText("Attack", root, "ATK 0", 22, FontStyles.Normal, Muted, TextAlignmentOptions.Left, new Vector2(18f, 28f), new Vector2(-180f, -8f), font);

            RectTransform slotsRoot = CreateObject("Slots", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-310f, -52f), new Vector2(-18f, 52f));
            GridLayoutGroup grid = slotsRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(62f, 32f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            List<Image> slotImages = new List<Image>(Define.MaxEquipmentSlot);
            Sprite slotSprite = GetSharedSprite();
            for (int i = 0; i < Define.MaxEquipmentSlot; i++)
            {
                RectTransform slot = CreateObject($"Slot{i + 1}", slotsRoot, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(62f, 32f));
                slotImages.Add(AddImage(slot.gameObject, new Color32(92, 105, 132, 255), slotSprite));
            }

            UIEquipmentItem item = root.gameObject.AddComponent<UIEquipmentItem>();
            item.ConfigureRuntime(clickButton, nameText, levelText, attackText, selectedFrame, slotImages, slotSprite, slotSprite);
            root.gameObject.SetActive(false);
            return item;
        }

        private static UIGemInventoryItem CreateGemInventoryItemTemplate(Transform parent, TMP_FontAsset font, string name)
        {
            RectTransform root = CreateTemplateRoot(name, parent, 170f);
            Button clickButton = root.gameObject.AddComponent<Button>();
            Image selectedFrame = AddOutlineFrame(root, new Color32(134, 255, 199, 255));

            TMP_Text nameText = CreateText("Name", root, "보석", 26, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(18f, 108f), new Vector2(-160f, 76f), font);
            TMP_Text rarityText = CreateText("Rarity", root, "Rare", 20, FontStyles.Bold, Accent, TextAlignmentOptions.Left, new Vector2(18f, 76f), new Vector2(-160f, 48f), font);
            TMP_Text countText = CreateText("Count", root, "x0", 24, FontStyles.Bold, White, TextAlignmentOptions.Right, new Vector2(-150f, 108f), new Vector2(-18f, 76f), font);
            TMP_Text descText = CreateText("Desc", root, "효과 없음", 19, FontStyles.Normal, Muted, TextAlignmentOptions.TopLeft, new Vector2(18f, 38f), new Vector2(-18f, -18f), font);

            UIGemInventoryItem item = root.gameObject.AddComponent<UIGemInventoryItem>();
            item.ConfigureRuntime(clickButton, nameText, rarityText, countText, descText, selectedFrame);
            root.gameObject.SetActive(false);
            return item;
        }

        private static UIEquipmentGemSlotItem CreateGemSlotTemplate(Transform parent, TMP_FontAsset font)
        {
            RectTransform root = CreateTemplateRoot("GemSlotTemplate", parent, 156f);
            Button clickButton = root.gameObject.AddComponent<Button>();
            Image selectedFrame = AddOutlineFrame(root, new Color32(255, 221, 112, 255));

            TMP_Text titleText = CreateText("Title", root, "슬롯 1", 24, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(18f, 100f), new Vector2(-18f, 68f), font);
            TMP_Text descText = CreateText("Desc", root, "보석을 장착해 보세요.", 18, FontStyles.Normal, Muted, TextAlignmentOptions.TopLeft, new Vector2(18f, 64f), new Vector2(-18f, -18f), font);
            TMP_Text lockText = CreateText("Lock", root, string.Empty, 20, FontStyles.Bold, Accent, TextAlignmentOptions.BottomRight, new Vector2(18f, 18f), new Vector2(-18f, -12f), font);

            UIEquipmentGemSlotItem item = root.gameObject.AddComponent<UIEquipmentGemSlotItem>();
            item.ConfigureRuntime(clickButton, titleText, descText, lockText, selectedFrame);
            root.gameObject.SetActive(false);
            return item;
        }

        private static UIEquipmentDetailDialog CreateDetailDialog(RectTransform parent, TMP_FontAsset font, UIEquipmentGemSlotItem slotItemPrefab, UIEquipmentGemSelectDialog gemSelectDialog, UIEquipmentConfirmDialog confirmDialog)
        {
            RectTransform dialogRoot = CreateDialogRoot("EquipmentDetailDialogRoot", parent);
            UIEquipmentDetailDialog dialog = dialogRoot.gameObject.AddComponent<UIEquipmentDetailDialog>();
            dialog.dialogView = dialogRoot.gameObject;

            RectTransform window = CreateObject("Window", dialogRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, -380f), new Vector2(560f, 380f));
            AddImage(window.gameObject, PanelBackground);

            TMP_Text titleText = CreateText("Title", window, "무기", 34, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(26f, 318f), new Vector2(-220f, 272f), font);
            TMP_Text attackText = CreateText("Attack", window, "공격력 0", 24, FontStyles.Bold, Accent, TextAlignmentOptions.Left, new Vector2(26f, 272f), new Vector2(-220f, 236f), font);
            TMP_Text levelText = CreateText("Level", window, "레벨 1", 22, FontStyles.Normal, Muted, TextAlignmentOptions.Left, new Vector2(26f, 236f), new Vector2(-220f, 204f), font);
            TMP_Text goldCostText = CreateText("GoldCost", window, "Gold 0/0", 20, FontStyles.Normal, White, TextAlignmentOptions.Right, new Vector2(-330f, 318f), new Vector2(-26f, 286f), font);
            TMP_Text scrollCostText = CreateText("ScrollCost", window, "Scroll 0/0", 20, FontStyles.Normal, White, TextAlignmentOptions.Right, new Vector2(-330f, 284f), new Vector2(-26f, 252f), font);

            CreateScrollContent(window, out RectTransform slotRoot);
            RectTransform slotScroll = slotRoot.parent as RectTransform;
            slotScroll.offsetMin = new Vector2(26f, 118f);
            slotScroll.offsetMax = new Vector2(-26f, -154f);

            RectTransform footer = CreateFooter(window);
            Button levelUpButton = CreateActionButton(footer, "강화", Accent, White, font);
            Button levelUpAllButton = CreateActionButton(footer, "전체 강화", new Color32(255, 164, 84, 255), White, font);
            Button closeButton = CreateActionButton(footer, "닫기", new Color32(107, 121, 149, 255), White, font);

            dialog.ConfigureRuntime(titleText, attackText, levelText, slotRoot, slotItemPrefab, goldCostText, scrollCostText, levelUpButton, levelUpAllButton, closeButton, gemSelectDialog, confirmDialog);
            dialog.Load();
            return dialog;
        }

        private static UIEquipmentGemSelectDialog CreateGemSelectDialog(RectTransform parent, TMP_FontAsset font, UIGemInventoryItem gemItemPrefab)
        {
            RectTransform dialogRoot = CreateDialogRoot("EquipmentGemSelectDialogRoot", parent);
            UIEquipmentGemSelectDialog dialog = dialogRoot.gameObject.AddComponent<UIEquipmentGemSelectDialog>();
            dialog.dialogView = dialogRoot.gameObject;

            RectTransform window = CreateObject("Window", dialogRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -360f), new Vector2(520f, 360f));
            AddImage(window.gameObject, PanelBackground);

            TMP_Text titleText = CreateText("Title", window, "슬롯 1 보석", 32, FontStyles.Bold, White, TextAlignmentOptions.Left, new Vector2(26f, 308f), new Vector2(-26f, 264f), font);
            TMP_Text selectedDescText = CreateText("SelectedDesc", window, "장착할 보석을 선택해 주세요.", 20, FontStyles.Normal, Muted, TextAlignmentOptions.TopLeft, new Vector2(26f, 258f), new Vector2(-26f, 198f), font);

            CreateScrollContent(window, out RectTransform listRoot);
            RectTransform gemScroll = listRoot.parent as RectTransform;
            gemScroll.offsetMin = new Vector2(26f, 126f);
            gemScroll.offsetMax = new Vector2(-26f, -176f);

            RectTransform footer = CreateFooter(window);
            Button equipButton = CreateActionButton(footer, "장착", Accent, White, font);
            Button unequipButton = CreateActionButton(footer, "해제", new Color32(255, 164, 84, 255), White, font);
            Button closeButton = CreateActionButton(footer, "닫기", new Color32(107, 121, 149, 255), White, font);

            dialog.ConfigureRuntime(titleText, selectedDescText, listRoot, gemItemPrefab, equipButton, unequipButton, closeButton);
            dialog.Load();
            return dialog;
        }

        private static UIEquipmentConfirmDialog CreateConfirmDialog(RectTransform parent, TMP_FontAsset font)
        {
            RectTransform dialogRoot = CreateDialogRoot("EquipmentConfirmDialogRoot", parent);
            UIEquipmentConfirmDialog dialog = dialogRoot.gameObject.AddComponent<UIEquipmentConfirmDialog>();
            dialog.dialogView = dialogRoot.gameObject;

            RectTransform window = CreateObject("Window", dialogRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-320f, -150f), new Vector2(320f, 150f));
            AddImage(window.gameObject, PanelBackground);

            TMP_Text messageText = CreateText("Message", window, UIEquipmentText.GetLevelUpAllConfirmMessage(), 24, FontStyles.Bold, White, TextAlignmentOptions.Center, new Vector2(26f, 96f), new Vector2(-26f, 26f), font);
            RectTransform footer = CreateFooter(window);
            Button cancelButton = CreateActionButton(footer, "취소", new Color32(107, 121, 149, 255), White, font);
            Button confirmButton = CreateActionButton(footer, "확인", Accent, White, font);

            dialog.ConfigureRuntime(messageText, cancelButton, confirmButton);
            dialog.Load();
            return dialog;
        }

        private static RectTransform CreateTemplateRoot(string name, Transform parent, float height)
        {
            RectTransform root = CreateObject(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, height));
            AddImage(root.gameObject, CardBackground);
            LayoutElement layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
            return root;
        }

        private static RectTransform CreateDialogRoot(string name, RectTransform parent)
        {
            RectTransform dialogRoot = CreateStretchObject(name, parent);
            AddImage(dialogRoot.gameObject, Dimmed);
            dialogRoot.gameObject.SetActive(false);
            return dialogRoot;
        }

        private static RectTransform CreateFooter(RectTransform parent)
        {
            RectTransform footer = CreateObject("Footer", parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(26f, 22f), new Vector2(-26f, 96f));
            HorizontalLayoutGroup footerLayout = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 12f;
            footerLayout.childForceExpandWidth = true;
            footerLayout.childForceExpandHeight = true;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            return footer;
        }

        private static Image AddOutlineFrame(RectTransform parent, Color color)
        {
            RectTransform frame = CreateStretchObject("SelectedFrame", parent);
            Image frameImage = AddImage(frame.gameObject, new Color(0f, 0f, 0f, 0f), GetSharedSprite());
            Outline outline = frame.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, -2f);
            frameImage.enabled = false;
            return frameImage;
        }

        private static Button CreateActionButton(Transform parent, string label, Color backgroundColor, Color textColor, TMP_FontAsset font)
        {
            RectTransform root = CreateObject(label, parent, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(0f, 72f));
            LayoutElement layoutElement = root.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 72f;
            layoutElement.flexibleWidth = 1f;

            Image image = AddImage(root.gameObject, backgroundColor);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            CreateText("Label", root, label, 24, FontStyles.Bold, textColor, TextAlignmentOptions.Center, new Vector2(12f, 12f), new Vector2(-12f, -12f), font);
            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, FontStyles fontStyle, Color color, TextAlignmentOptions alignment, Vector2 offsetMin, Vector2 offsetMax, TMP_FontAsset font)
        {
            RectTransform rect = CreateObject(name, parent, new Vector2(0f, 0f), new Vector2(1f, 1f), offsetMin, offsetMax);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = fontStyle;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        private static RectTransform CreateStretchObject(string name, Transform parent)
        {
            return CreateObject(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static RectTransform CreateObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image AddImage(GameObject go, Color color, Sprite sprite = null)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite ?? GetSharedSprite();
            image.type = Image.Type.Sliced;
            return image;
        }

        private static Sprite GetSharedSprite()
        {
            if (sharedSprite != null)
                return sharedSprite;

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;

            sharedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return sharedSprite;
        }
    }
}
