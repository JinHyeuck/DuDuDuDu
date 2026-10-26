using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Point;
using OJ.UI;

namespace OJ.Equipment
{
    /// <summary>
    /// 장비 칸을 누르면 뜨는 "강화 하기" 팝업 (시안 ItemUpgrade_Popup_NewLayout, 2026-10-22).
    ///
    /// 예전에는 화면 위쪽 절반짜리 창이었고 보석 목록·합성 버튼은 장비 페이지의 "보석" 탭에 있었다.
    /// 지금은 <b>한 창에 다 들어 있다</b> — 위에서부터 장비 정보·레벨업, 장착 보석 줄, 이 부위의 보석 격자,
    /// 보석 합성하기. 격자의 보석(또는 장착 줄)을 누르면 미니팝업(시안 ItemUpgrade_Popup_PrsNewLayout)이
    /// 떠서 설명을 보여 주고 장착/해제를 받는다.
    ///
    /// 계층은 <see cref="Build"/> 가 PSD 좌표로 짓고 에디터 메뉴(<c>OJ/개발/장비 강화 팝업 굽기</c>)가
    /// 프리팹에 굽는다. <b>다시 구우면 프리팹 손 수정은 사라진다.</b>
    /// 좌표 정본: <c>Docs/EquipmentUIArtPort.md</c>.
    /// </summary>
    public class UIEquipmentConfirmDialog : DialogBase
    {
        public event Action Hidden;

        /// <summary>장착 보석 한 줄. 굽는 시점에 만들어져 프리팹에 저장된다.</summary>
        [Serializable]
        private class GemRowView
        {
            public Button button;
            public Image background;
            public Image gemFrame;
            public Image gemIcon;
            public TMP_Text nameText;
            public TMP_Text valueText;
            public TMP_Text emptyText;
            public Image lockIcon;
            public TMP_Text lockText;
            public Button unequipButton;
        }

        /// <summary>보석 격자 한 칸. 런타임에 필요한 만큼 만든다.</summary>
        private class GemCellView
        {
            public GameObject root;
            public Image frame;
            public Image icon;
            public TMP_Text countText;
            public string gemId;
        }

        [Header("Font")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Equipment")]
        [SerializeField] private Image equipmentIconImage;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text equipmentTypeText;
        [SerializeField] private TMP_Text attackText;

        [Header("Cost")]
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private TMP_Text scrollCostText;
        [SerializeField] private Image scrollCostIconImage;

        [Header("Gem Rows")]
        [SerializeField] private List<GemRowView> gemRows = new List<GemRowView>();

        [Header("Gem Grid")]
        [SerializeField] private RectTransform gemGridRoot;
        [SerializeField] private TMP_Text gemGridEmptyText;

        [Header("Gem Popup")]
        [SerializeField] private GameObject gemPopup;
        [SerializeField] private Button gemPopupDimButton;
        [SerializeField] private Image gemPopupFrame;
        [SerializeField] private Image gemPopupIcon;
        [SerializeField] private TMP_Text gemPopupCountText;
        [SerializeField] private Image gemPopupTypeIcon;
        [SerializeField] private TMP_Text gemPopupTypeText;
        [SerializeField] private TMP_Text gemPopupRarityText;
        [SerializeField] private TMP_Text gemPopupEffectNameText;
        [SerializeField] private TMP_Text gemPopupEffectValueText;
        [SerializeField] private Button gemPopupActionButton;
        [SerializeField] private TMP_Text gemPopupActionText;

        [Header("Buttons")]
        [SerializeField] private Button levelUpButton;
        [SerializeField] private Button mergeButton;
        [SerializeField] private Button closeButton;

        private readonly List<GemCellView> gemCells = new List<GemCellView>();

        private Action onChanged;
        private EquipmentType equipmentType;
        private bool buttonsBound;

        // 미니팝업이 무엇을 보여 주고 있나. 격자에서 열면 장착, 장착 줄에서 열면 해제다.
        private string popupGemId = string.Empty;
        private int popupSlotIndex = -1;

        // 열려 있는 동안 루트 캔버스 맨 끝으로 옮겨 둔다(LiftToRootCanvas). 닫을 때 돌아갈 자리.
        private Transform homeParent;
        private int homeSiblingIndex;

        protected override void OnLoad()
        {
            TryBindButtons();
            HideGemPopup();
        }

        protected override void OnUnload()
        {
            if (!buttonsBound)
                return;

            if (levelUpButton != null)
                levelUpButton.onClick.RemoveListener(OnClickLevelUp);
            if (mergeButton != null)
                mergeButton.onClick.RemoveListener(OnClickMerge);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnClickClose);
            if (gemPopupDimButton != null)
                gemPopupDimButton.onClick.RemoveListener(HideGemPopup);
            if (gemPopupActionButton != null)
                gemPopupActionButton.onClick.RemoveListener(OnClickGemPopupAction);

            for (int i = 0; i < gemRows.Count; i++)
            {
                if (gemRows[i].button != null)
                    gemRows[i].button.onClick.RemoveAllListeners();
                if (gemRows[i].unequipButton != null)
                    gemRows[i].unequipButton.onClick.RemoveAllListeners();
            }
        }

        protected override void OnExit()
        {
            HideGemPopup();
            ReturnHome();
            Hidden?.Invoke();
        }

        /// <summary>미니팝업이 떠 있으면 백키는 그것만 닫는다.</summary>
        public override void BackKeyCall()
        {
            if (gemPopup != null && gemPopup.activeSelf)
            {
                HideGemPopup();
                KeepOnBackStack();
                return;
            }

            base.BackKeyCall();
        }

        /// <summary>강화 버튼. 로비 가이드가 손가락으로 가리킨다.</summary>
        public Button LevelUpButton => levelUpButton;

        public void Open(EquipmentType type, Action changedCallback)
        {
            if (!_isLoaded)
                Load();

            equipmentType = type;
            onChanged = changedCallback;

            LiftToRootCanvas();
            Enter();
            HideGemPopup();
            Refresh();
        }

        /// <summary>
        /// 열려 있는 동안 루트 캔버스의 맨 끝 자식으로 옮긴다.
        ///
        /// <b>왜.</b> 이 창은 장비 페이지 안에 중첩돼 있는데, 로비의 윗 재화 띠와 아래 탭 바가 같은
        /// 루트 캔버스에서 페이지보다 <b>뒤에</b> 그려진다. 예전 절반 창은 그 둘 사이에만 있어 상관없었지만
        /// 화면 전체를 덮는 지금은 위아래가 가려진다. 정렬 순서를 올린 중첩 캔버스로 풀면
        /// <c>UIPopupRoot</c>(합성 창이 뜨는 곳)와 순서가 같아질 수 있다 — 루트는 처음 쓰일 때
        /// "씬 최고 순서 + 1" 로 정해지는데 이 페이지는 탭을 눌러야 생기기 때문이다.
        /// 같은 캔버스 안에서 맨 끝으로 옮기면 탭 바 위·팝업 루트 아래가 확정된다.
        /// </summary>
        private void LiftToRootCanvas()
        {
            if (homeParent != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Transform rootCanvas = canvas != null ? canvas.rootCanvas.transform : null;
            if (rootCanvas == null || transform.parent == rootCanvas)
                return;

            homeParent = transform.parent;
            homeSiblingIndex = transform.GetSiblingIndex();
            transform.SetParent(rootCanvas, false);
            transform.SetAsLastSibling();
        }

        private void ReturnHome()
        {
            if (homeParent == null)
                return;

            transform.SetParent(homeParent, false);
            transform.SetSiblingIndex(homeSiblingIndex);
            homeParent = null;
        }

        private void Refresh()
        {
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null)
                return;

            RefreshEquipment(manager);
            RefreshCost(manager);
            RefreshGemRows(manager);
            RefreshGemGrid(manager);
            RefreshGemPopup(manager);

            if (levelUpButton != null)
                levelUpButton.interactable = !manager.IsMaxLevel(equipmentType);
            if (mergeButton != null)
                mergeButton.interactable = manager.HasMergeMaterials(equipmentType);
        }

        private void RefreshEquipment(EquipmentManager manager)
        {
            if (equipmentIconImage != null)
                equipmentIconImage.sprite = UIEquipmentSpriteResolver.GetEquipmentLargeIconSprite(equipmentType);
            if (levelText != null)
                levelText.SetText("Lv.{0}", manager.GetLevel(equipmentType));
            if (equipmentTypeText != null)
                equipmentTypeText.SetText(UIEquipmentText.GetEquipmentName(equipmentType));
            if (attackText != null)
                attackText.SetText("{0}", manager.GetEquipmentAttack(equipmentType));
        }

        private void RefreshCost(EquipmentManager manager)
        {
            PointType scrollType = PointManager.ToEquipmentScrollType(equipmentType);

            // <b>만렙이면 비용 자리에 MAX 를 적는다.</b> 버튼만 잠그면 유저는 눌러 보고
            // 아무 일도 안 일어나는 것으로 알게 된다 — 이유가 화면에 있어야 한다.
            if (manager.IsMaxLevel(equipmentType))
            {
                goldCostText?.SetText("MAX");
                scrollCostText?.SetText("MAX");
            }
            else
            {
                (int goldCost, int scrollCost) = manager.GetNextUpgradeCost(equipmentType);
                int gold = PointManager.Instance != null ? PointManager.Instance.Get(PointType.Gold) : 0;
                int scroll = PointManager.Instance != null ? PointManager.Instance.Get(scrollType) : 0;

                // 보유/필요 순서. 다이스 강화·해금 창과 같은 순서라야 같은 모양이 같은 뜻이 된다.
                goldCostText?.SetText("{0} / {1}", gold, goldCost);
                scrollCostText?.SetText("{0} / {1}", scroll, scrollCost);
            }

            if (scrollCostIconImage != null)
            {
                Sprite icon = PointRewardUtility.GetPointIcon(scrollType);
                scrollCostIconImage.sprite = icon;
                scrollCostIconImage.enabled = icon != null;
            }
        }

        private void RefreshGemRows(EquipmentManager manager)
        {
            for (int i = 0; i < gemRows.Count; i++)
            {
                GemRowView row = gemRows[i];
                bool unlocked = manager.IsSlotUnlocked(equipmentType, i);
                GemDefinition definition = null;
                string gemId = unlocked ? manager.GetEquippedGemId(equipmentType, i) : string.Empty;
                if (!string.IsNullOrEmpty(gemId))
                    manager.TryGetGemDefinition(gemId, out definition);

                bool equipped = definition != null;
                bool empty = unlocked && !equipped;

                if (row.background != null)
                    row.background.color = unlocked ? RowColor : LockedRowColor;

                SetActive(row.gemFrame, equipped);
                SetActive(row.nameText, equipped);
                SetActive(row.valueText, equipped);
                SetActive(row.unequipButton, equipped);
                SetActive(row.emptyText, empty);
                SetActive(row.lockIcon, !unlocked);
                SetActive(row.lockText, !unlocked);

                if (!unlocked && row.lockText != null)
                    row.lockText.SetText("레벨 {0} 해금", manager.GetSlotUnlockLevel(i));

                if (equipped)
                {
                    row.gemFrame.sprite = UIEquipmentSpriteResolver.GetGemFrameSprite(definition.rarity);
                    row.gemIcon.sprite = UIEquipmentSpriteResolver.GetGemIconSprite(definition.rarity);
                    UIEquipmentEffectTextFormatter.SplitFirstEffect(definition, out string label, out string value);
                    row.nameText.SetText(label);
                    row.valueText.SetText(value);
                }

                if (row.button != null)
                    row.button.interactable = equipped;
            }
        }

        private void RefreshGemGrid(EquipmentManager manager)
        {
            IReadOnlyList<GemDefinition> definitions = manager.GetGemDefinitions();
            int used = 0;

            for (int i = 0; i < definitions.Count; i++)
            {
                GemDefinition definition = definitions[i];
                if (definition == null || definition.equipableType != equipmentType)
                    continue;

                int count = manager.GetGemCount(definition.gemId);
                if (count <= 0)
                    continue;

                GemCellView cell = GetOrCreateGemCell(used++);
                cell.gemId = definition.gemId;
                cell.root.SetActive(true);
                cell.frame.sprite = UIEquipmentSpriteResolver.GetGemFrameSprite(definition.rarity);
                cell.icon.sprite = UIEquipmentSpriteResolver.GetGemIconSprite(definition.rarity);
                cell.countText.SetText("x{0}", count);
            }

            for (int i = used; i < gemCells.Count; i++)
                gemCells[i].root.SetActive(false);

            if (gemGridEmptyText != null)
                gemGridEmptyText.gameObject.SetActive(used == 0);
        }

        private GemCellView GetOrCreateGemCell(int index)
        {
            while (gemCells.Count <= index)
            {
                GemCellView cell = CreateGemCell(gemGridRoot, font, out Button button);
                button.onClick.AddListener(() => ShowGemPopupForInventory(cell.gemId));
                UIButtonPress.AttachToChildren(cell.root);
                gemCells.Add(cell);
            }

            return gemCells[index];
        }

        // ── 미니팝업 ────────────────────────────────────────────────────

        private void ShowGemPopupForInventory(string gemId)
        {
            popupGemId = gemId;
            popupSlotIndex = -1;
            ShowGemPopup();
        }

        private void ShowGemPopupForSlot(int slotIndex)
        {
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null)
                return;

            string gemId = manager.GetEquippedGemId(equipmentType, slotIndex);
            if (string.IsNullOrEmpty(gemId))
                return;

            popupGemId = gemId;
            popupSlotIndex = slotIndex;
            ShowGemPopup();
        }

        private void ShowGemPopup()
        {
            if (gemPopup == null)
                return;

            gemPopup.SetActive(true);
            RefreshGemPopup(EquipmentManager.Instance);
        }

        private void HideGemPopup()
        {
            popupGemId = string.Empty;
            popupSlotIndex = -1;
            if (gemPopup != null)
                gemPopup.SetActive(false);
        }

        private void RefreshGemPopup(EquipmentManager manager)
        {
            if (gemPopup == null || !gemPopup.activeSelf || manager == null)
                return;

            if (string.IsNullOrEmpty(popupGemId) ||
                !manager.TryGetGemDefinition(popupGemId, out GemDefinition definition) || definition == null)
            {
                HideGemPopup();
                return;
            }

            bool fromSlot = popupSlotIndex >= 0;
            int count = manager.GetGemCount(definition.gemId);

            // 격자에서 연 보석이 합성 등으로 다 사라졌으면 더 보여 줄 것이 없다.
            if (!fromSlot && count <= 0)
            {
                HideGemPopup();
                return;
            }

            gemPopupFrame.sprite = UIEquipmentSpriteResolver.GetGemFrameSprite(definition.rarity);
            gemPopupIcon.sprite = UIEquipmentSpriteResolver.GetGemIconSprite(definition.rarity);
            gemPopupCountText.gameObject.SetActive(!fromSlot);
            gemPopupCountText.SetText("x{0}", count);
            gemPopupTypeIcon.sprite = UIEquipmentSpriteResolver.GetEquipmentSmallIconSprite(definition.equipableType);
            gemPopupTypeText.SetText(UIEquipmentText.GetEquipmentName(definition.equipableType));
            gemPopupRarityText.SetText(UIEquipmentText.GetRarityName(definition.rarity));
            gemPopupRarityText.color = GetRarityColor(definition.rarity);
            UIEquipmentEffectTextFormatter.SplitEffects(definition, out string names, out string values);
            gemPopupEffectNameText.SetText(names);
            gemPopupEffectValueText.SetText(values);

            if (fromSlot)
            {
                gemPopupActionText.SetText("해제");
                gemPopupActionButton.interactable = true;
            }
            else
            {
                bool hasEmptySlot = FindEmptySlotIndex(manager) >= 0;
                gemPopupActionText.SetText(hasEmptySlot ? "장착" : "빈 칸 없음");
                gemPopupActionButton.interactable = hasEmptySlot;
            }
        }

        private int FindEmptySlotIndex(EquipmentManager manager)
        {
            for (int i = 0; i < Define.MaxEquipmentSlot; i++)
            {
                if (manager.IsSlotUnlocked(equipmentType, i) &&
                    string.IsNullOrEmpty(manager.GetEquippedGemId(equipmentType, i)))
                    return i;
            }

            return -1;
        }

        private void OnClickGemPopupAction()
        {
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null)
                return;

            bool changed;
            if (popupSlotIndex >= 0)
            {
                changed = manager.UnequipGem(equipmentType, popupSlotIndex);
            }
            else
            {
                int slotIndex = FindEmptySlotIndex(manager);
                changed = slotIndex >= 0 && manager.TryEquipGem(equipmentType, slotIndex, popupGemId);
            }

            if (!changed)
                return;

            // 장착·해제가 끝나면 닫는다 — 결과는 장착 보석 줄에 바로 보인다.
            HideGemPopup();
            Refresh();
            onChanged?.Invoke();
        }

        // ── 버튼 ──────────────────────────────────────────────────────

        private void OnClickUnequip(int slotIndex)
        {
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null || !manager.UnequipGem(equipmentType, slotIndex))
                return;

            HideGemPopup();
            Refresh();
            onChanged?.Invoke();
        }

        private void OnClickLevelUp()
        {
            EquipmentManager manager = EquipmentManager.Instance;
            if (manager == null || !manager.TryLevelUp(equipmentType))
                return;

            Refresh();
            onChanged?.Invoke();
        }

        /// <summary>
        /// 합성 창은 카탈로그에서 꺼낸다. <c>Show</c> 가 아니라 <c>Get</c> 인 이유는
        /// <c>Open</c> 이 대상 부위와 콜백을 넣은 뒤 스스로 <c>Enter</c> 를 부르기 때문이다.
        /// </summary>
        private void OnClickMerge()
        {
            HideGemPopup();
            GameContainer.UI?.Get<UIMergePopup>()?.Open(equipmentType, OnMergeCompleted);
        }

        private void OnMergeCompleted(IReadOnlyList<string> resultGemIds)
        {
            Refresh();
            onChanged?.Invoke();
        }

        private void OnClickClose()
        {
            Exit();
        }

        private void TryBindButtons()
        {
            if (buttonsBound)
                return;

            if (levelUpButton != null)
                levelUpButton.onClick.AddListener(OnClickLevelUp);
            if (mergeButton != null)
                mergeButton.onClick.AddListener(OnClickMerge);
            if (closeButton != null)
                closeButton.onClick.AddListener(OnClickClose);
            if (gemPopupDimButton != null)
                gemPopupDimButton.onClick.AddListener(HideGemPopup);
            if (gemPopupActionButton != null)
                gemPopupActionButton.onClick.AddListener(OnClickGemPopupAction);

            for (int i = 0; i < gemRows.Count; i++)
            {
                int slotIndex = i;
                if (gemRows[i].button != null)
                    gemRows[i].button.onClick.AddListener(() => ShowGemPopupForSlot(slotIndex));
                if (gemRows[i].unequipButton != null)
                    gemRows[i].unequipButton.onClick.AddListener(() => OnClickUnequip(slotIndex));
            }

            buttonsBound = true;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null && component.gameObject.activeSelf != active)
                component.gameObject.SetActive(active);
        }

        /// <summary>등급 글자색. 프레임(Itme_Slot_0~5) 색에 맞췄다 — 시안의 파란 칸 등급 글자가 #00B4FF.</summary>
        private static Color GetRarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Uncommon: return Hex(0xA6ADD1);
                case Rarity.Common: return Hex(0x00FF18);
                case Rarity.Normal: return Hex(0x00B4FF);
                case Rarity.Rare: return Hex(0xD15BFF);
                case Rarity.Epic: return Hex(0xFF1313);
                case Rarity.Mythic: return Hex(0xFFEA00);
                default: return Color.white;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 좌표는 PSD 레이어 bbox(보이는 픽셀, 1080x1920 좌상단 원점) 그대로 적고,
        // Image 크기에는 스프라이트 투명 여백 x 배율을 더한다(Tools/ui/PORTING.md).
        // ──────────────────────────────────────────────────────────────

        private const string ArtRoot = "Art/";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color PopupDimColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color InnerBoxColor = Hex(0x636481);
        private static readonly Color RowColor = Hex(0x45465F);
        private static readonly Color LockedRowColor = Hex(0x2D2D3D);
        private static readonly Color LineColor = Hex(0x7B7C96);
        private static readonly Color HeaderTextColor = Hex(0xA7A9C1);
        private static readonly Color SubTextColor = Hex(0xA1A3BB);
        private static readonly Color MutedTextColor = Hex(0xA9A5C5);
        private static readonly Color ValueColor = Hex(0x24FF00);
        private static readonly Color PopupBoxColor = Hex(0x7A7A92);
        private static readonly Color PopupPillColor = Hex(0xA1A1BC);
        private static readonly Color PopupAttrTextColor = Hex(0xABACC9);

        /// <summary>장착 보석 줄 간격(PSD 647 → 759 → 871 …).</summary>
        private const float RowPitch = 112f;

        /// <summary>격자 칸의 보이는 크기(PSD 117x116) / Itme_Slot 의 보이는 크기(45px).</summary>
        private const float CellFrameScale = 117f / 45f;

        /// <summary>
        /// 이미 있는 루트(프리팹의 같은 오브젝트) 아래에 계층을 새로 짓는다.
        ///
        /// <b>루트를 새로 만들지 않는 이유.</b> 이 창은 장비 페이지 프리팹에 중첩돼 있고 페이지가
        /// 이 컴포넌트를 직렬화 참조로 들고 있다. 프리팹을 새 오브젝트로 덮어쓰면 루트 컴포넌트의
        /// fileID 가 바뀌어 그 참조가 조용히 None 이 된다. 루트를 남기고 자식만 갈면 참조가 산다.
        /// 호출 전에 자식과 <c>_exitBtn</c> 은 굽기 도구가 비워 둔다.
        /// </summary>
        public void Build(TMP_FontAsset fontAsset)
        {
            font = fontAsset;
            UseBackBtn = true;
            gemRows.Clear();
            BakeOpenStyle(DialogOpenStyle.Popup);

            GameObject view = CreateRect("View", transform);
            Stretch(view.GetComponent<RectTransform>());
            dialogView = view;
            Transform p = view.transform;

            // 딤 — 바깥을 누르면 닫힌다.
            Image dim = CreateImage("Dim", p, DimColor);
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            AddExitButton(dimButton);

            // 팝업 판 — Ui_Popup_Bg x4, 보이는 (86,106)~(1010,1792), 여백 3px
            Image panel = Sprite(p, "Panel", "Upgrade/Ui_Popup_Bg", 4f, 3f, 3f, 3f, 3f, 86f, 106f, 1010f, 1792f, Color.white, true);
            panel.raycastTarget = true; // 판 안을 누르면 딤(닫기)으로 새지 않게
            // 안쪽 판 — SmallBox x4 #636481, 보이는 (120,226)~(977,1753)
            SmallBox(p, "InnerBox", 120f, 226f, 977f, 1753f, InnerBoxColor);

            TMP_Text title = CreateText("Title", p, "강화 하기", 45f, TextAlignmentOptions.Center, Color.white);
            Place(title.rectTransform, 466f, 154f, 638f, 198f, 300f);

            // 닫기 — 바탕+X 를 PSD 에서 한 장으로 잘라 둔 것(방치보상 창과 같은 그림)
            Image close = Picture(p, "CloseButton", "OffLineReward/FromPsd/FromPsd_CloseButton", 866f, 73f, 978f, 184f);
            close.raycastTarget = true;
            closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;

            // 윗 장식 띠 — Stage_PaperPoint_TextBar x5 #7b7c96, 보이는 (134,255)~(964,310). 위 10 · 아래 11 px 여백
            Sprite(p, "TopDecor", "Main/Stage_PaperPoint_TextBar", 5f, 0f, 10f, 0f, 11f, 134f, 255f, 964f, 310f, LineColor, true);

            BuildEquipmentHeader(p);
            BuildGemRows(p);
            BuildGemGrid(p);

            // 보석 합성하기 — Big_Btn_Green x3, 보이는 (346,1570)~(762,1704)
            mergeButton = BigButton(p, "MergeButton", "Ingame/Big_Btn_Green", 346f, 1570f, 762f, 1704f, "보석 합성하기", 40f);

            BuildGemPopup(p);
        }

        private void BuildEquipmentHeader(Transform p)
        {
            // 아이템 칸 — Itme_Slot_0, 보이는 45px 를 151px 로 (x3.36, 시안 "3.5배 비율 맞춰서")
            Sprite(p, "ItemFrame", "ItemSlot/Itme_Slot_0", 151f / 45f, 9f, 9f, 10f, 10f, 175f, 275f, 326f, 426f, Color.white, false);
            // 장비 그림 — 64px 안에 35x34 가 보인다. PSD 의 94x90 이 되게 x2.65 → 170
            equipmentIconImage = Picture(p, "ItemIcon", "ItemSlot/Item_weapon", 0f, 0f, 0f, 0f);
            equipmentIconImage.preserveAspect = true;
            SetRect(equipmentIconImage.rectTransform, new Vector2(170f, 170f), Pos(251f, 346f));

            levelText = CreateText("Level", p, "Lv.1", 35f, TextAlignmentOptions.Left, Color.white);
            Place(levelText.rectTransform, 357f, 281f, 452f, 309f, 240f, TextAnchorX.Left);
            equipmentTypeText = CreateText("TypeName", p, "무기", 30f, TextAlignmentOptions.Left, SubTextColor);
            Place(equipmentTypeText.rectTransform, 354f, 324f, 406f, 353f, 240f, TextAnchorX.Left);

            SmallBox(p, "AttackBox", 346f, 364f, 617f, 426f, RowColor);
            TMP_Text atkLabel = CreateText("AttackLabel", p, "ATK", 30f, TextAlignmentOptions.Left, SubTextColor);
            Place(atkLabel.rectTransform, 365f, 383f, 426f, 407f, 100f, TextAnchorX.Left);
            attackText = CreateText("AttackValue", p, "0", 30f, TextAlignmentOptions.Center, Color.white);
            Place(attackText.rectTransform, 530f, 383f, 566f, 407f, 140f);

            // 비용 칸 — SmallBox x4 #45465f, 보이는 (157,445)~(618,556)
            SmallBox(p, "CostBox", 157f, 445f, 618f, 556f, RowColor);
            // 골드·주문서 — 64px 안에 28~30px 가 보인다. PSD 의 46·50px 가 되게 x1.65 → 106
            Image goldIcon = Picture(p, "GoldIcon", "Gem/Gem_Money", 0f, 0f, 0f, 0f);
            SetRect(goldIcon.rectTransform, new Vector2(106f, 106f), Pos(283f, 472.5f));
            scrollCostIconImage = Picture(p, "ScrollIcon", "Gem/NormalScroll", 0f, 0f, 0f, 0f);
            SetRect(scrollCostIconImage.rectTransform, new Vector2(106f, 106f), Pos(282f, 524.5f));
            goldCostText = CreateText("GoldCost", p, "0 / 0", 35f, TextAlignmentOptions.Center, Color.white);
            Place(goldCostText.rectTransform, 319f, 455f, 530f, 492f, 280f);
            scrollCostText = CreateText("ScrollCost", p, "0 / 0", 35f, TextAlignmentOptions.Center, Color.white);
            Place(scrollCostText.rectTransform, 319f, 505f, 530f, 541f, 280f);

            // LEVEL UP — Big_Btn_Yellow x3, 보이는 (634,432)~(960,558)
            levelUpButton = BigButton(p, "LevelUpButton", "Ingame/Big_Btn_Yellow", 634f, 432f, 960f, 558f, "LEVEL UP", 40f);

            Image line = CreateImage("Line", p, LineColor);
            line.raycastTarget = false;
            Place(line.rectTransform, 139f, 576f, 959f, 581f, 0f);

            TMP_Text header = CreateText("EquippedHeader", p, "장착 보석", 35f, TextAlignmentOptions.Left, HeaderTextColor);
            Place(header.rectTransform, 144f, 601f, 279f, 635f, 240f, TextAnchorX.Left);
        }

        /// <summary>장착 보석 줄 — SmallBox x4 820x99, 112 간격 (첫 줄 y 647).</summary>
        private void BuildGemRows(Transform p)
        {
            for (int i = 0; i < Define.MaxEquipmentSlot; i++)
            {
                float dy = RowPitch * i;
                var row = new GemRowView();
                GameObject rowRoot = CreateRect("GemRow" + (i + 1), p);
                Stretch(rowRoot.GetComponent<RectTransform>());
                Transform r = rowRoot.transform;

                row.background = SmallBox(r, "Bg", 140f, 647f + dy, 960f, 746f + dy, RowColor);
                row.background.raycastTarget = true;
                row.button = row.background.gameObject.AddComponent<Button>();
                row.button.transition = Selectable.Transition.None;

                // 보석 아이콘 — 격자 칸과 같은 그림을 줄인 것. 보이는 (171,664)~(232,724)
                row.gemFrame = Sprite(r, "GemFrame", "ItemSlot/Itme_Slot_0", 61f / 45f, 9f, 9f, 10f, 10f, 171f, 664f + dy, 232f, 724f + dy, Color.white, false);
                row.gemIcon = Picture(row.gemFrame.transform, "GemIcon", "ItemSlot/Slot_Gem_0", 0f, 0f, 0f, 0f);
                SetRect(row.gemIcon.rectTransform, new Vector2(70f, 70f), Vector2.zero);

                row.nameText = CreateText("Name", r, "공격력", 40f, TextAlignmentOptions.Left, Color.white);
                Place(row.nameText.rectTransform, 250f, 674f + dy, 440f, 712f + dy, 335f, TextAnchorX.Left);
                // "[Normal] 공격력" 처럼 대상이 붙으면 길어진다 — 수치(x 594)를 덮지 않게 줄인다.
                row.nameText.enableAutoSizing = true;
                row.nameText.fontSizeMin = 26f;
                row.nameText.fontSizeMax = 40f;
                row.valueText = CreateText("Value", r, "+0%", 40f, TextAlignmentOptions.Left, ValueColor);
                Place(row.valueText.rectTransform, 594f, 671f + dy, 681f, 703f + dy, 160f, TextAnchorX.Left);

                // 해제 — Big_Btn_Red x3, 보이는 (754,651)~(964,743)
                row.unequipButton = BigButton(r, "UnequipButton", "Ingame/Big_Btn_Red", 754f, 651f + dy, 964f, 743f + dy, "해제", 35f);

                row.emptyText = CreateText("Empty", r, "비어있음", 40f, TextAlignmentOptions.Center, MutedTextColor);
                Place(row.emptyText.rectTransform, 140f, 677f + dy, 960f, 716f + dy, 0f);

                // 잠금 — 자물쇠 x2 (PSD 46x56 자리), 글자 #a9a5c5
                row.lockIcon = Sprite(r, "Lock", "Upgrade/Icon_lock", 2f, 1f, 2f, 4f, 2f, 162f, 665f + dy, 216f, 721f + dy, Color.white, false);
                row.lockText = CreateText("LockText", r, "레벨 1 해금", 40f, TextAlignmentOptions.Center, MutedTextColor);
                Place(row.lockText.rectTransform, 140f, 674f + dy, 960f, 713f + dy, 0f);

                gemRows.Add(row);
            }
        }

        /// <summary>
        /// 이 부위의 보석 격자 — 6열, 칸 간격 130 x 120 (PSD 첫 칸 (163,1272)~(280,1388)).
        /// 아래쪽 y 1544 에서 잘리고 세로로 스크롤된다.
        /// </summary>
        private void BuildGemGrid(Transform p)
        {
            TMP_Text header = CreateText("GemHeader", p, "보석 정보", 35f, TextAlignmentOptions.Left, HeaderTextColor);
            Place(header.rectTransform, 147f, 1211f, 283f, 1245f, 240f, TextAnchorX.Left);

            SmallBox(p, "GemBox", 139f, 1260f, 956f, 1728f, RowColor);

            const float left = 150f, right = 946f, top = 1262f, bottom = 1544f;
            GameObject viewport = CreateRect("GemViewport", p);
            SetRect(viewport.GetComponent<RectTransform>(), new Vector2(right - left, bottom - top),
                Pos((left + right) * 0.5f, (top + bottom) * 0.5f));
            viewport.AddComponent<RectMask2D>();

            // 스크롤이 잡히게 뷰포트에 투명 판을 깐다(방치보상 창과 같은 이유).
            Image hitArea = viewport.AddComponent<Image>();
            hitArea.color = Color.clear;

            GameObject content = CreateRect("GemGrid", viewport.transform);
            gemGridRoot = content.GetComponent<RectTransform>();
            gemGridRoot.anchorMin = new Vector2(0f, 1f);
            gemGridRoot.anchorMax = new Vector2(1f, 1f);
            gemGridRoot.pivot = new Vector2(0.5f, 1f);
            gemGridRoot.anchoredPosition = Vector2.zero;
            gemGridRoot.sizeDelta = Vector2.zero;

            // 첫 칸 중심 (221.5,1330) = 뷰포트 왼쪽 150 + 패딩 6.5 + 칸 반폭 65
            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(7, 7, 8, 8);
            grid.cellSize = new Vector2(130f, 120f);
            grid.spacing = Vector2.zero;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.UpperLeft;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = gemGridRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            gemGridEmptyText = CreateText("GemEmpty", p, "보유한 보석이 없습니다", 35f, TextAlignmentOptions.Center, MutedTextColor);
            Place(gemGridEmptyText.rectTransform, 139f, 1380f, 956f, 1420f, 0f);
        }

        /// <summary>
        /// 보석 미니팝업 — 본 팝업 위에 딤 50% 를 깔고 SmallBox x4 #7a7a92 (371,1093)~(752,1531).
        /// 시안의 자물쇠 원(677,1113)은 대응하는 기능(보석 잠금)이 없어 넣지 않았다.
        /// </summary>
        private void BuildGemPopup(Transform p)
        {
            gemPopup = CreateRect("GemPopup", p);
            Stretch(gemPopup.GetComponent<RectTransform>());
            Transform g = gemPopup.transform;

            Image dim = CreateImage("Dim", g, PopupDimColor);
            Stretch(dim.rectTransform);
            gemPopupDimButton = dim.gameObject.AddComponent<Button>();
            gemPopupDimButton.transition = Selectable.Transition.None;

            Image box = SmallBox(g, "Box", 371f, 1093f, 752f, 1531f, PopupBoxColor);
            box.raycastTarget = true; // 상자 안을 누르면 딤으로 새지 않게

            // 보석 칸 — 격자 칸과 같은 크기. 보이는 (379,1106)~(496,1222)
            gemPopupFrame = Sprite(g, "GemFrame", "ItemSlot/Itme_Slot_0", CellFrameScale, 9f, 9f, 10f, 10f, 379f, 1106f, 496f, 1222f, Color.white, false);
            gemPopupIcon = Picture(g, "GemIcon", "ItemSlot/Slot_Gem_0", 0f, 0f, 0f, 0f);
            SetRect(gemPopupIcon.rectTransform, new Vector2(132f, 132f), Pos(438f, 1165.5f));
            gemPopupCountText = CreateText("Count", g, "x1", 25f, TextAlignmentOptions.Right, Color.white);
            Place(gemPopupCountText.rectTransform, 457f, 1184f, 486f, 1204f, 100f, TextAnchorX.Right);

            // 부위 — SmallBox x4 #a1a1bc (505,1113)~(669,1173), 작은 아이콘 + 이름
            SmallBox(g, "TypeBox", 505f, 1113f, 669f, 1173f, PopupPillColor);
            gemPopupTypeIcon = Picture(g, "TypeIcon", "ItemSlot/Icon_Weapon", 0f, 0f, 0f, 0f);
            SetRect(gemPopupTypeIcon.rectTransform, new Vector2(40f, 40f), Pos(548f, 1142f));
            gemPopupTypeText = CreateText("TypeName", g, "무기", 25f, TextAlignmentOptions.Center, Color.white);
            Place(gemPopupTypeText.rectTransform, 576f, 1129f, 662f, 1154f, 0f);

            gemPopupRarityText = CreateText("Rarity", g, "레어", 25f, TextAlignmentOptions.Left, GetRarityColor(Rarity.Normal));
            Place(gemPopupRarityText.rectTransform, 514f, 1188f, 557f, 1213f, 200f, TextAnchorX.Left);

            // 보석 속성 띠 — SmallBox x4 #45465f (377,1238)~(746,1298)
            SmallBox(g, "AttrBar", 377f, 1238f, 746f, 1298f, RowColor);
            TMP_Text attr = CreateText("AttrLabel", g, "보석 속성", 30f, TextAlignmentOptions.Left, PopupAttrTextColor);
            Place(attr.rectTransform, 397f, 1252f, 513f, 1282f, 200f, TextAnchorX.Left);

            // 효과 — 이름은 오른쪽 맞춤, 수치는 왼쪽 맞춤으로 x 600 에서 맞붙는다(PSD 이름 ~570, 수치 612~).
            // 여러 효과면 아래로 한 줄씩 는다. 첫 줄 가운데 y 1379.
            gemPopupEffectNameText = CreateText("EffectNames", g, "공격력", 30f, TextAlignmentOptions.TopRight, Color.white);
            gemPopupEffectNameText.lineSpacing = 10f;
            SetRect(gemPopupEffectNameText.rectTransform, new Vector2(210f, 150f), Pos(485f, 1435f));
            gemPopupEffectValueText = CreateText("EffectValues", g, "+0%", 30f, TextAlignmentOptions.TopLeft, ValueColor);
            gemPopupEffectValueText.lineSpacing = 10f;
            SetRect(gemPopupEffectValueText.rectTransform, new Vector2(130f, 150f), Pos(677f, 1435f));

            // 장착/해제 — Big_Btn_Yellow x3, 보이는 (394,1544)~(720,1670)
            gemPopupActionButton = BigButton(g, "ActionButton", "Ingame/Big_Btn_Yellow", 394f, 1544f, 720f, 1670f, "장착", 40f);
            gemPopupActionText = gemPopupActionButton.GetComponentInChildren<TMP_Text>();

            gemPopup.SetActive(false);
        }

        /// <summary>
        /// 격자 한 칸 — Itme_Slot x2.6 (보이는 117) 위에 Slot_Gem, 오른쪽 아래 개수.
        /// 누르는 판은 보이는 크기(117)로 따로 둔다 — 그림 Image 는 투명 여백까지 166 이라
        /// 그것을 누름 판으로 쓰면 이웃 칸(간격 130)과 겹쳐 옆 칸이 눌린다.
        /// </summary>
        private static GemCellView CreateGemCell(Transform parent, TMP_FontAsset fontAsset, out Button button)
        {
            var cell = new GemCellView();
            cell.root = CreateRect("GemCell", parent);
            Transform c = cell.root.transform;

            Image hit = CreateImage("Hit", c, Color.clear);
            SetRect(hit.rectTransform, new Vector2(117f, 116f), Vector2.zero);
            button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            float frameSize = 64f * CellFrameScale;
            cell.frame = CreateImage("Frame", c, Color.white);
            cell.frame.sprite = LoadSprite("ItemSlot/Itme_Slot_0");
            cell.frame.raycastTarget = false;
            SetRect(cell.frame.rectTransform, new Vector2(frameSize, frameSize), new Vector2(0.5f * CellFrameScale, -0.5f * CellFrameScale));

            cell.icon = CreateImage("Icon", c, Color.white);
            cell.icon.sprite = LoadSprite("ItemSlot/Slot_Gem_0");
            cell.icon.raycastTarget = false;
            SetRect(cell.icon.rectTransform, new Vector2(132f, 132f), new Vector2(0f, 2f));

            cell.countText = CreateTextStatic("Count", c, "x1", 25f, TextAlignmentOptions.BottomRight, Color.white, fontAsset);
            SetRect(cell.countText.rectTransform, new Vector2(100f, 40f), new Vector2(1f, -31f));
            return cell;
        }

        // ── 조립 헬퍼 ────────────────────────────────────────────────────

        private enum TextAnchorX { Center, Left, Right }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>PSD 좌표(좌상단 원점, 1080x1920) → 화면 중앙 기준 anchoredPosition.</summary>
        private static Vector2 Pos(float x, float y)
        {
            return new Vector2(x - 540f, 960f - y);
        }

        /// <summary>스프라이트를 읽는다. <b>없으면 멈춘다</b> — null 로 구우면 흰 사각형이 조용히 저장된다.</summary>
        private static Sprite LoadSprite(string pathUnderArt)
        {
            Sprite sprite = Resources.Load<Sprite>(ArtRoot + pathUnderArt);
            if (sprite == null)
                throw new InvalidOperationException("[장비 강화 팝업] 스프라이트가 없다: Resources/" + ArtRoot + pathUnderArt);
            return sprite;
        }

        /// <summary>보이는 경계 (x0,y0)~(x1,y1) 에 그대로 놓는 그림(여백 없는 그림용).</summary>
        private static Image Picture(Transform parent, string name, string path, float x0, float y0, float x1, float y1)
        {
            Image image = CreateImage(name, parent, Color.white);
            image.sprite = LoadSprite(path);
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(x1 - x0, y1 - y0), Pos((x0 + x1) * 0.5f, (y0 + y1) * 0.5f));
            return image;
        }

        /// <summary>
        /// 투명 여백(스프라이트 픽셀, 좌·위·우·아래)이 있는 그림을 보이는 경계에 맞춰 놓는다.
        /// Image 는 여백 x 배율만큼 커지고, 여백이 비대칭이면 중심도 그만큼 옮긴다.
        /// </summary>
        private static Image Sprite(Transform parent, string name, string path, float scale,
            float padL, float padT, float padR, float padB,
            float x0, float y0, float x1, float y1, Color color, bool sliced)
        {
            Image image = CreateImage(name, parent, color);
            image.sprite = LoadSprite(path);
            image.raycastTarget = false;
            if (sliced)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f / scale;
            }

            float left = x0 - padL * scale, right = x1 + padR * scale;
            float top = y0 - padT * scale, bottom = y1 + padB * scale;
            SetRect(image.rectTransform, new Vector2(right - left, bottom - top), Pos((left + right) * 0.5f, (top + bottom) * 0.5f));
            return image;
        }

        /// <summary>Ui_Popup_SmallBox x4 — 여백 6/6/7/7 px.</summary>
        private static Image SmallBox(Transform parent, string name, float x0, float y0, float x1, float y1, Color color)
        {
            return Sprite(parent, name, "Upgrade/Ui_Popup_SmallBox", 4f, 6f, 6f, 7f, 7f, x0, y0, x1, y1, color, true);
        }

        /// <summary>
        /// Big_Btn_* x3 — 여백 좌우 4 · 위 6 · 아래 2 px. 글자는 아래 턱(약 12px)을 뺀 윗면 가운데.
        /// </summary>
        private Button BigButton(Transform parent, string name, string path,
            float x0, float y0, float x1, float y1, string label, float size)
        {
            Image image = Sprite(parent, name, path, 3f, 4f, 6f, 4f, 2f, x0, y0, x1, y1, Color.white, true);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText("Label", image.transform, label, size, TextAlignmentOptions.Center, Color.white);
            float faceCenter = (y0 + y1 - 12f) * 0.5f;
            float imageCenter = ((y0 - 18f) + (y1 + 6f)) * 0.5f;
            SetRect(text.rectTransform, new Vector2(x1 - x0, size + 16f), new Vector2(0f, imageCenter - faceCenter));
            return button;
        }

        /// <summary>
        /// PSD bbox 에 놓는다. 세로는 bbox 가운데, 가로는 정렬 기준에 따라 왼쪽 끝·오른쪽 끝·가운데를
        /// 맞춘다. <paramref name="width"/> 가 0 이면 bbox 폭을 쓴다. 글자는 위아래로 여유를 준다.
        /// </summary>
        private static void Place(RectTransform rect, float x0, float y0, float x1, float y1, float width,
            TextAnchorX anchor = TextAnchorX.Center)
        {
            float w = width > 0f ? width : x1 - x0;
            float h = rect.GetComponent<TMP_Text>() != null ? (y1 - y0) + 16f : (y1 - y0);

            float cx;
            switch (anchor)
            {
                case TextAnchorX.Left: cx = x0 + w * 0.5f; break;
                case TextAnchorX.Right: cx = x1 - w * 0.5f; break;
                default: cx = (x0 + x1) * 0.5f; break;
            }

            SetRect(rect, new Vector2(w, h), Pos(cx, (y0 + y1) * 0.5f));
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = 5;
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject gameObject = CreateRect(name, parent);
            Image image = gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Color color)
        {
            return CreateTextStatic(name, parent, value, size, alignment, color, font);
        }

        private static TMP_Text CreateTextStatic(string name, Transform parent, string value, float size,
            TextAlignmentOptions alignment, Color color, TMP_FontAsset fontAsset)
        {
            GameObject gameObject = CreateRect(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null)
                text.font = fontAsset;
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rectTransform, Vector2 size, Vector2 anchoredPosition)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }
}
