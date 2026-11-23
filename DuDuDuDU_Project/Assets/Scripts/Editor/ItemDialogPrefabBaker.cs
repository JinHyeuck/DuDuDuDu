using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using OJ.Bag;
using L = OJ.EditorTools.LobbyPsdLayout;
using Kit = OJ.EditorTools.UiBakeKit;

namespace OJ.EditorTools
{
    /// <summary>
    /// 가방 창(<see cref="UIItemDialog"/>)과 재화 설명 창(<see cref="UIItemDetailPopup"/>) 프리팹을 굽는다.
    ///
    /// <b>두 번 눌러도 안전하다.</b> "굽기"는 이미 있으면 아무것도 하지 않고 말해 준다. 화면 구조를 바꿨다면
    /// "다시 굽기" 를 쓴다 — 같은 경로에 덮어쓰므로 GUID 가 유지되어 카탈로그를 다시 훑을 필요가 없다.
    /// 처음 구웠을 때만 <c>OJ/개발/다이얼로그 카탈로그/훑어서 갱신</c> 을 돌린다.
    ///
    /// <b>메뉴를 연속으로 누르지 말 것.</b> 앞 메뉴의 <c>AssetDatabase.Refresh</c> 가 재컴파일을 시작하면
    /// 그 사이의 클릭은 조용히 무시된다.
    /// </summary>
    public static class ItemDialogPrefabBaker
    {
        private const string Folder = "Assets/Prefab/UI";
        private const string DialogPath = Folder + "/UIItemDialog.prefab";
        private const string DetailPath = Folder + "/UIItemDetailPopup.prefab";

        private const string CloseButtonArt = "OffLineReward/FromPsd/FromPsd_CloseButton";
        private const string SlotArt = "ItemSlot/Itme_Slot_0";
        private const string SlotPressedArt = "ItemSlot/Itme_Slot_Prs";

        [MenuItem("OJ/개발/가방/창 프리팹 굽기")]
        private static void BakeIfMissing() => Bake(overwrite: false);

        [MenuItem("OJ/개발/가방/창 프리팹 다시 굽기 (덮어씀)")]
        private static void Rebake() => Bake(overwrite: true);

        private static void Bake(bool overwrite)
        {
            bool dialogExists = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPath) != null;
            bool detailExists = AssetDatabase.LoadAssetAtPath<GameObject>(DetailPath) != null;
            if (dialogExists && detailExists && !overwrite)
            {
                Debug.LogWarning("[가방] 프리팹이 이미 있어 <b>아무것도 하지 않았다</b>. 화면 구조를 바꿨다면 " +
                                 "OJ/개발/가방/창 프리팹 다시 굽기 (덮어씀) 을 쓸 것.");
                return;
            }

            TMP_FontAsset font = Kit.LoadFont();
            if (font == null)
                return;

            Kit.ClearCache();
            try
            {
                if (!Kit.SavePrefab(AssembleDialog(font), DialogPath) ||
                    !Kit.SavePrefab(AssembleDetail(font), DetailPath))
                {
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[가방] 프리팹을 굽다 멈췄다 — 저장하지 않았다.\n" + e);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool firstTime = !dialogExists || !detailExists;
            Debug.Log("[가방] 프리팹을 구웠다: " + DialogPath + " , " + DetailPath + "\n" +
                      (firstTime
                          ? "  다음: OJ/개발/다이얼로그 카탈로그/훑어서 갱신 을 돌려야 창이 열린다."
                          : "  덮어썼다. GUID 가 그대로라 카탈로그는 다시 안 훑어도 된다."));
        }

        // ──────────────────────────────────────────────── 가방 창

        private static GameObject AssembleDialog(TMP_FontAsset font)
        {
            GameObject root = Kit.NewRect("UIItemDialog", null);
            Kit.Stretch(root.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UIItemDialog>();

            // dialogView 는 루트가 아니라 자식이어야 한다. DialogBase 가 이것을 끄고 켜는데,
            // 루트를 끄면 컴포넌트까지 같이 잠들어 다시 열 수 없다.
            GameObject view = Kit.NewRect("View", root.transform);
            Kit.Stretch(view.GetComponent<RectTransform>());
            dialog.dialogView = view;
            Transform p = view.transform;

            Kit.AddBackdrop(dialog, p, Kit.Backdrop);

            // 팝업 판. 판 위의 빈 곳을 눌러도 바깥으로 안 새게 레이캐스트를 받는다(딤 버튼이 닫아 버리면 안 된다).
            Kit.PopupBgRect(L.Bag.PanelX, L.Bag.PanelY, L.Bag.PanelW, L.Bag.PanelH, out Vector2 size, out Vector2 pos);
            Kit.Sliced(p, "Panel", "Upgrade/Ui_Popup_Bg", size, pos, Color.white).raycastTarget = true;

            TMP_Text title = Kit.NewText("Title", p, "아이템", L.Bag.TitleSize, Color.white, font);
            Kit.SetRect(title.rectTransform, new Vector2(600f, 60f), Kit.Pos(L.Bag.TitleCx, L.Bag.TitleCy));

            Image close = Kit.NewImage("CloseButton", p, Color.white);
            close.sprite = Kit.LoadSprite(CloseButtonArt);
            Kit.SetRect(close.rectTransform, new Vector2(L.Bag.CloseW, L.Bag.CloseH),
                        Kit.Pos(L.Bag.CloseCx, L.Bag.CloseCy));
            dialog.AddExitButton(Kit.SpriteButton(close));

            Kit.SmallBoxRect(L.Bag.InnerX, L.Bag.InnerY, L.Bag.InnerW, L.Bag.InnerH, out size, out pos);
            Kit.Sliced(p, "InnerBox", "Upgrade/Ui_Popup_SmallBox", size, pos, Kit.InnerBoxColor);

            BuildScroll(dialog, p, font, out RectTransform content, out UIItemSlot template);

            var so = new SerializedObject(dialog);
            Kit.SetRef(so, "gridContent", content);
            Kit.SetRef(so, "slotTemplate", template);
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void BuildScroll(
            UIItemDialog dialog, Transform p, TMP_FontAsset font, out RectTransform content, out UIItemSlot template)
        {
            float viewportHeight = L.Bag.ViewportBottom - L.Bag.ViewportTop;
            Vector2 viewportCenter = Kit.Pos(L.Bag.InnerX + L.Bag.InnerW * 0.5f,
                                             (L.Bag.ViewportTop + L.Bag.ViewportBottom) * 0.5f);

            // 칸 사이 빈 곳에서도 끌려야 해서, 스크롤 영역 자체가 안 보이는 레이캐스트 면을 갖는다.
            Image scrollImage = Kit.NewImage("Scroll", p, new Color(1f, 1f, 1f, 0f));
            scrollImage.raycastTarget = true;
            Kit.SetRect(scrollImage.rectTransform, new Vector2(L.Bag.InnerW, viewportHeight), viewportCenter);

            GameObject viewport = Kit.NewRect("Viewport", scrollImage.transform);
            Kit.Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();

            GameObject contentObject = Kit.NewRect("Content", viewport.transform);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var grid = contentObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(L.Bag.SlotSize, L.Bag.SlotSize);
            grid.spacing = new Vector2(L.Bag.SpacingX, L.Bag.SpacingY);
            grid.padding = new RectOffset(
                Mathf.RoundToInt(L.Bag.PadLeft), Mathf.RoundToInt(L.Bag.PadRight),
                Mathf.RoundToInt(L.Bag.PadTop), Mathf.RoundToInt(L.Bag.PadBottom));
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = L.Bag.Columns;

            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollImage.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            template = BuildSlot(content, font);
        }

        /// <summary>
        /// 칸 하나. 뿌리는 188 의 안 보이는 눌림 면이고, 틀 그림은 그보다 크게 얹는다 —
        /// 킷 그림(Itme_Slot_0)은 64px 안에 45px 만 보여서, 보이는 틀이 188 이 되려면 Image 는 그보다 커야 한다.
        /// </summary>
        private static UIItemSlot BuildSlot(Transform parent, TMP_FontAsset font)
        {
            float size = L.Bag.SlotSize;

            Image root = Kit.NewImage("ItemSlot", parent, new Color(1f, 1f, 1f, 0f));
            root.raycastTarget = true;
            Kit.SetRect(root.rectTransform, new Vector2(size, size), Vector2.zero);

            // 눌렀을 때 금색 테두리 그림으로 바뀐다. 두 그림은 같은 Image 크기·같은 중심을 쓴다.
            Kit.Art slotArt = Kit.LoadArt(SlotArt);
            Image frame = Kit.PlaceVisible(root.transform, "Frame", slotArt, 0f, 0f, size, size);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { pressedSprite = Kit.LoadSprite(SlotPressedArt) };
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Image icon = Kit.NewImage("Icon", root.transform, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Kit.SetRect(icon.rectTransform, new Vector2(L.Bag.IconSize, L.Bag.IconSize), new Vector2(0f, L.Bag.IconUp));

            // 그림이 없는 재화는 이름 글자로 대신한다. 평소에는 꺼져 있다.
            TMP_Text nameText = Kit.NewText("Name", root.transform, string.Empty, 28f, Color.white, font);
            nameText.textWrappingMode = TextWrappingModes.Normal;
            Kit.AutoSize(nameText, 18f, 28f);
            Kit.SetRect(nameText.rectTransform, new Vector2(164f, 86f), new Vector2(0f, L.Bag.IconUp));
            nameText.gameObject.SetActive(false);

            TMP_Text count = Kit.NewText("Count", root.transform, "X0", L.Bag.CountSize, Color.white, font);
            Kit.AutoSize(count, L.Bag.CountMin, L.Bag.CountSize);
            Kit.SetRect(count.rectTransform, new Vector2(168f, 48f), new Vector2(0f, -L.Bag.CountDown));

            var slot = root.gameObject.AddComponent<UIItemSlot>();
            var so = new SerializedObject(slot);
            Kit.SetRef(so, "button", button);
            Kit.SetRef(so, "icon", icon);
            Kit.SetRef(so, "countText", count);
            Kit.SetRef(so, "nameText", nameText);
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return slot;
        }

        // ──────────────────────────────────────────────── 재화 설명 창

        private static GameObject AssembleDetail(TMP_FontAsset font)
        {
            GameObject root = Kit.NewRect("UIItemDetailPopup", null);
            Kit.Stretch(root.GetComponent<RectTransform>());

            var popup = root.AddComponent<UIItemDetailPopup>();

            GameObject view = Kit.NewRect("View", root.transform);
            Kit.Stretch(view.GetComponent<RectTransform>());
            popup.dialogView = view;
            Transform p = view.transform;

            // 가방 위에 겹쳐 뜨므로 딤을 얇게 한다. 가방 자신의 딤이 이미 깔려 있다.
            Kit.AddBackdrop(popup, p, new Color(0f, 0f, 0f, 0.35f));

            // 시안에서 이 창은 캔버스 위쪽에 따로 떠 있다. 화면 한가운데로 옮긴다.
            float ox = 540f - (L.Detail.PanelX + L.Detail.PanelW * 0.5f);
            float oy = 960f - (L.Detail.PanelY + L.Detail.PanelH * 0.5f);
            Vector2 At(float x, float y) => Kit.Pos(x + ox, y + oy);

            Kit.PopupBgRect(L.Detail.PanelX + ox, L.Detail.PanelY + oy, L.Detail.PanelW, L.Detail.PanelH,
                            out Vector2 size, out Vector2 pos);
            Kit.Sliced(p, "Panel", "Upgrade/Ui_Popup_Bg", size, pos, Color.white).raycastTarget = true;

            TMP_Text nameText = Kit.NewText("Name", p, string.Empty, L.Detail.NameSize, Color.white, font);
            Kit.SetRect(nameText.rectTransform, new Vector2(620f, 50f), At(L.Detail.NameCx, L.Detail.NameCy));

            Kit.SmallBoxRect(L.Detail.InnerX + ox, L.Detail.InnerY + oy, L.Detail.InnerW, L.Detail.InnerH,
                             out size, out pos);
            Kit.Sliced(p, "InnerBox", "Upgrade/Ui_Popup_SmallBox", size, pos, Kit.InnerBoxColor);

            Kit.PlaceDesign(p, "IconSlot", Kit.LoadArt(SlotArt),
                            L.Detail.SlotX + ox, L.Detail.SlotY + oy, L.Detail.SlotSize, L.Detail.SlotSize, Vector2.zero);

            Image icon = Kit.NewImage("Icon", p, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            Kit.SetRect(icon.rectTransform, new Vector2(L.Detail.IconSize, L.Detail.IconSize),
                        At(L.Detail.IconCx, L.Detail.IconCy));

            TMP_Text amount = Kit.NewText("Amount", p, string.Empty, L.Detail.AmountSize, Kit.Hex(0xb5b7e5), font);
            Kit.SetRect(amount.rectTransform, new Vector2(560f, 44f), At(L.Detail.AmountCx, L.Detail.AmountCy));

            Image divider = Kit.NewImage("Divider", p, Kit.Hex(0x7b7c96));
            divider.raycastTarget = false;
            Kit.SetRect(divider.rectTransform, new Vector2(L.Detail.DividerW, L.Detail.DividerH),
                        At(L.Detail.DividerX + L.Detail.DividerW * 0.5f, L.Detail.DividerY + L.Detail.DividerH * 0.5f));

            TMP_Text description = Kit.NewText("Description", p, string.Empty, L.Detail.DescriptionSize,
                                               Color.white, font);
            Kit.AutoSize(description, 20f, L.Detail.DescriptionSize);
            Kit.SetRect(description.rectTransform, new Vector2(640f, 44f),
                        At(L.Detail.DescriptionCx, L.Detail.DescriptionCy));

            var so = new SerializedObject(popup);
            Kit.SetRef(so, "nameText", nameText);
            Kit.SetRef(so, "icon", icon);
            Kit.SetRef(so, "amountText", amount);
            Kit.SetRef(so, "descriptionText", description);
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }
    }
}
