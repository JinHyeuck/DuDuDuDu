using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Stage;
using OJ.UI;

namespace OJ.IdleReward
{
    /// <summary>
    /// 방치(자동 전투) 보상 창. (MIGRATION_BASELINE 10.5, 아트 적용 2026-10-01)
    ///
    /// <b>좌표의 정본은 PSD 다</b> — <c>Art/0PSD/고기패스와자동전투보상.psd</c> 의 그룹
    /// '자동전투보상/자동전투'. 수치·결정은 <c>Docs/IdleRewardArtPort.md</c>.
    /// 고기 축제는 로비 위젯(<c>UIMeatFestivalLobbyButton</c>)으로 나갔으므로 탭이 없다.
    ///
    /// <see cref="Create"/> 는 <b>에디터에서 프리팹을 굽는 용도로만</b> 쓴다.
    /// 게임에서는 다른 창과 똑같이 카탈로그에서 나온다.
    ///
    /// <b>버튼 배선은 <see cref="OnLoad"/> 에서 한다.</b> onClick.AddListener 로 붙인
    /// 델리게이트는 프리팹에 직렬화되지 않는다.
    /// </summary>
    public class UIIdleRewardDialog : DialogBase
    {
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color InnerBoxColor = Hex(0x636481);

        // 소탕 문구를 뺀 만큼 안쪽 판·팝업 판 아래를 줄이고 받기 버튼을 올린다(PSD 보다 87px 위).
        // 보상 상자 아래(y 1192)와 안쪽 판 아래 사이를 30px 로 남긴다.
        private const float LiftUp = 87f;
        private static readonly Color RateBoxColor = Hex(0x45465F);
        private static readonly Color LineColor = Hex(0x7B7C96);
        private static readonly Color DarkText = Hex(0x2B2C3F);

        /// <summary>빈 칸은 같은 그림을 어둡게 쓴다(PSD 실측 밝기 비 ≈0.82).</summary>
        private static readonly Color EmptySlotTint = new Color(0.82f, 0.82f, 0.82f, 1f);

        /// <summary>격자에 늘 보이는 칸 수. 보상이 적어도 3줄(PSD)이 채워져 있어야 상자가 비어 보이지 않는다.</summary>
        private const int MinSlotCount = 15;

        // 전부 [SerializeField] 여야 한다. 프리팹으로 구울 때 참조가 함께 저장되고
        // 런타임에는 다시 찾지 않는다. 하나라도 빠뜨리면 그 부분만 조용히 죽는다.
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Button closeButton;

        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text goldRateText;
        [SerializeField] private TMP_Text ticketRateText;
        [SerializeField] private Image ticketRateIcon;
        [SerializeField] private TMP_Text autoTimerText;
        [SerializeField] private RectTransform autoRewardRoot;
        [SerializeField] private Sprite filledSlotSprite;
        [SerializeField] private Sprite emptySlotSprite;


        [SerializeField] private Button autoClaimButton;

        private float nextRefreshTime;
        private string rewardSignature = string.Empty;

        protected override void OnLoad()
        {
            base.OnLoad();

            if (closeButton != null) closeButton.onClick.AddListener(Exit);
            if (autoClaimButton != null) autoClaimButton.onClick.AddListener(ClaimAutoBattle);
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            Refresh(true);
        }

        private void OnEnable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged += OnRewardChanged;
        }

        private void OnDisable()
        {
            if (IdleRewardManager.Instance != null)
                IdleRewardManager.Instance.OnChanged -= OnRewardChanged;
        }

        private void Update()
        {
            // Escape 는 DialogBase + AOSBackBtnManager 스택이 담당한다.
            if (Time.unscaledTime < nextRefreshTime)
                return;

            nextRefreshTime = Time.unscaledTime + 1f;
            Refresh(false);
        }

        private void OnRewardChanged()
        {
            Refresh(true);
        }

        // ── 그리기 ──────────────────────────────────────────────────────

        private void Refresh(bool forceCards)
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null)
                return;

            int stageIndex = manager.GetAutoBattleStageIndex();
            TimeSpan elapsed = manager.GetAutoBattleElapsed();
            List<PointRewardEntry> rewards = manager.GetAutoBattleRewards();

            stageText.SetText(stageIndex > 0
                ? stageIndex + ". " + StageData.GetStageDisplayName(stageIndex)
                : "클리어한 스테이지가 없습니다");

            // 시간당 보상 — 자동 전투는 1시간에 1회 클리어(IdleRewardManager.SecondsPerAutoBattleClear).
            // 골드는 보장분, 오른쪽은 회당 고정인 핀볼 티켓이다. 보석은 자동 전투 보상에 없다
            // (PSD 의 "+10/시간" 보석은 예시 숫자 — 결정표 참조).
            int goldPerHour = stageIndex > 0 ? StageRewardCalculator.GetGuaranteedNormalGold(stageIndex) : 0;
            int ticketPerHour = stageIndex > 0 ? StageRewardCalculator.PinballTicketPerClear : 0;
            goldRateText.SetText("+{0}/시간", goldPerHour);
            ticketRateText.SetText("+{0}/시간", ticketPerHour);
            // 핀볼 티켓은 PointMetadataDatabase 에 아이콘이 아직 없다 — 흰 사각형 대신 숨긴다.
            if (ticketRateIcon != null && ticketRateIcon.sprite == null)
                ticketRateIcon.sprite = PointRewardUtility.GetPointIcon(PointType.PinballTicket);
            if (ticketRateIcon != null)
                ticketRateIcon.enabled = ticketRateIcon.sprite != null;

            bool full = manager.GetAutoBattleProgress01() >= 1f;
            autoTimerText.SetText(full
                ? "이미 한도 시간이 되었습니다. " + FormatTime(elapsed)
                : "누적 " + FormatTime(elapsed) + " / " + FormatTime(TimeSpan.FromSeconds(IdleRewardManager.AutoBattleMaxSeconds)));

            autoClaimButton.interactable = rewards.Count > 0;
            RefreshRewardCards(rewards, forceCards);
        }

        private void RefreshRewardCards(IReadOnlyList<PointRewardEntry> rewards, bool force)
        {
            string signature = BuildRewardSignature(rewards);
            if (!force && signature == rewardSignature)
                return;

            rewardSignature = signature;
            for (int i = autoRewardRoot.childCount - 1; i >= 0; i--)
                Destroy(autoRewardRoot.GetChild(i).gameObject);

            int count = Mathf.Max(MinSlotCount, Mathf.CeilToInt(rewards.Count / 5f) * 5);
            for (int i = 0; i < count; i++)
            {
                bool filled = i < rewards.Count;
                GameObject cell = CreateRect("Slot", autoRewardRoot);

                // 칸 그림(x3, 192)은 격자 칸(148x150)보다 크다 — 시안의 칸 사이 간격이 그림 여백이다.
                Image slot = CreateImage("Bg", cell.transform, Color.white);
                slot.sprite = filled ? filledSlotSprite : emptySlotSprite;
                slot.color = filled ? Color.white : EmptySlotTint;
                slot.raycastTarget = false;
                SetRect(slot.rectTransform, new Vector2(192f, 192f), Vector2.zero);

                if (!filled)
                    continue;

                PointRewardEntry reward = rewards[i];
                Image icon = CreateImage("Icon", cell.transform, Color.white);
                icon.sprite = PointRewardUtility.GetPointIcon(reward.PointType);
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                SetRect(icon.rectTransform, new Vector2(84f, 84f), new Vector2(0f, 10f));

                TMP_Text amount = CreateText("Amount", cell.transform, "x" + reward.Amount.ToString("#,##0"), 28f,
                    TextAlignmentOptions.Center, Color.white);
                SetRect(amount.rectTransform, new Vector2(140f, 38f), new Vector2(0f, -44f));
            }
        }

        // ── 입력 ────────────────────────────────────────────────────────

        private void ClaimAutoBattle()
        {
            IdleRewardManager manager = IdleRewardManager.Instance;
            if (manager == null || !manager.TryClaimAutoBattle(out List<PointRewardEntry> rewards, out int stageIndex))
                return;

            ShowRewardResult(rewards, "스테이지 " + stageIndex + " 자동전투 보상을 획득했습니다.");
            Refresh(true);
        }

        /// <summary>
        /// 보상 결과창을 띄운다. (10.4) 팝업 루트 안에서 맨 위로 올린다.
        /// </summary>
        private void ShowRewardResult(IReadOnlyList<PointRewardEntry> rewards, string message)
        {
            UIRewardResultDialog resultDialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (resultDialog == null)
                return;

            resultDialog.transform.SetAsLastSibling();
            resultDialog.Open(rewards, message);
        }

        private static string BuildRewardSignature(IReadOnlyList<PointRewardEntry> rewards)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                builder.Append((int)rewards[i].PointType);
                builder.Append(':');
                builder.Append(rewards[i].Amount);
                builder.Append('|');
            }
            return builder.ToString();
        }

        private static string FormatTime(TimeSpan time)
        {
            int totalHours = Mathf.FloorToInt((float)time.TotalHours);
            return string.Format("{0:00}:{1:00}:{2:00}", totalHours, time.Minutes, time.Seconds);
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 좌표는 PSD 레이어 bbox(보이는 픽셀) 그대로 적고, Image 크기에는
        // 스프라이트 투명 여백 x 배율을 더한다(Tools/ui/PORTING.md).
        // ──────────────────────────────────────────────────────────────

        private const string ArtRoot = "Art/";

        /// <summary>
        /// 계층을 짓는다. <b>에디터에서 프리팹을 굽는 용도다</b> — 게임에서는 부르지 않는다.
        /// 루트에는 아무것도 그리지 않고 DialogView 자식이 딤과 판을 갖는다 —
        /// <see cref="DialogBase"/> 가 그 자식만 켜고 끄기 때문이다.
        /// </summary>
        public static UIIdleRewardDialog Create(Transform parent, TMP_FontAsset fontAsset)
        {
            GameObject root = CreateRect("UIIdleRewardDialog", parent);
            Stretch(root.GetComponent<RectTransform>());

            GameObject view = CreateRect("DialogView", root.transform);
            Stretch(view.GetComponent<RectTransform>());

            UIIdleRewardDialog dialog = root.AddComponent<UIIdleRewardDialog>();
            dialog.font = fontAsset;
            dialog.dialogView = view;
            dialog.UseBackBtn = true;

            // 딤 — 바깥을 누르면 닫힌다(예전과 같다).
            Image dim = CreateImage("Dim", view.transform, DimColor);
            Stretch(dim.rectTransform);
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dialog.AddExitButton(dimButton);

            dialog.Build(view.transform);
            return dialog;
        }

        private void Build(Transform p)
        {
            // 윗 그림 — 배경(Banner_1 x2) 위에 PSD 에서 잘라 온 헌터·슬라임·해골(FromPsd, 정식 파일 대기).
            // Banner_1 은 512 안에 (16,154)~(495,358) 이 보인다 → 보이는 좌상단 (62,0) 이 되게 놓는다.
            Picture(p, "Banner", "OffLineReward/OfflineReward_Banner_1", new Vector2(1024f, 1024f), Pos(542f, 204f));
            Picture(p, "Hunter", "OffLineReward/FromPsd/FromPsd_Hunter", new Vector2(315f, 322f), Pos(314.5f, 216f));
            Picture(p, "Slime", "OffLineReward/FromPsd/FromPsd_Slime", new Vector2(184f, 165f), Pos(803f, 258.5f));
            Picture(p, "Skeleton", "OffLineReward/FromPsd/FromPsd_Skeleton", new Vector2(173f, 248f), Pos(653.5f, 217f));

            // 팝업 판 — Ui_Popup_Bg x4, 보이는 924x1173 (75,359) + 여백 3px x4
            Sliced(p, "Panel", "Upgrade/Ui_Popup_Bg", 4f, new Vector2(948f, 1197f - LiftUp), Pos(537f, 945.5f - LiftUp / 2f), Color.white);

            // 닫기 — PSD 에서 잘라 온 버튼 한 장(FromPsd, 정식 파일 대기)
            Image close = Picture(p, "CloseButton", "OffLineReward/FromPsd/FromPsd_CloseButton", new Vector2(112f, 111f), Pos(911f, 381.5f));
            close.raycastTarget = true;
            closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close;

            TMP_Text title = CreateText("Title", p, "자동 전투 보상", 45f, TextAlignmentOptions.Center, Color.white);
            SetRect(title.rectTransform, new Vector2(600f, 60f), Pos(536.5f, 429f));

            // 안쪽 판 — SmallBox x4 #636481, 보이는 857x829 (109,480). SmallBox 여백 6/6/7/7 px x4
            Sliced(p, "InnerBox", "Upgrade/Ui_Popup_SmallBox", 4f, new Vector2(909f, 881f - LiftUp), Pos(539.5f, 896.5f - LiftUp / 2f), InnerBoxColor);
            Picture(p, "TopDecor", "OffLineReward/FromPsd/FromPsd_TopDecor", new Vector2(830f, 55f), Pos(541f, 521.5f));

            stageText = CreateText("StageText", p, "1. 어둠의 숲속", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(stageText.rectTransform, new Vector2(700f, 48f), Pos(537f, 532f));

            // 시간당 보상 칸 둘 — SmallBox x4 #45465f, 보이는 361x62
            Sliced(p, "GoldRateBox", "Upgrade/Ui_Popup_SmallBox", 4f, new Vector2(413f, 114f), Pos(339.5f, 602f), RateBoxColor);
            Sliced(p, "TicketRateBox", "Upgrade/Ui_Popup_SmallBox", 4f, new Vector2(413f, 114f), Pos(732.5f, 605f), RateBoxColor);

            Picture(p, "GoldIcon", "Gem/Gem_Money", new Vector2(128f, 128f), Pos(195f, 600f));
            ticketRateIcon = CreateImage("TicketIcon", p, Color.white);
            ticketRateIcon.preserveAspect = true;
            ticketRateIcon.raycastTarget = false;
            SetRect(ticketRateIcon.rectTransform, new Vector2(64f, 64f), Pos(592f, 603f));

            goldRateText = CreateText("GoldRate", p, "+200/시간", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(goldRateText.rectTransform, new Vector2(260f, 48f), Pos(346f, 599.5f));
            ticketRateText = CreateText("TicketRate", p, "+1/시간", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(ticketRateText.rectTransform, new Vector2(260f, 48f), Pos(746.5f, 602.5f));

            Image line = CreateImage("Line", p, LineColor);
            line.raycastTarget = false;
            SetRect(line.rectTransform, new Vector2(820f, 5f), Pos(541f, 648.5f));

            // 보상 상자 — Ui_offlineRewardBox x4, 보이는 817x512 (132,680), 가로 여백 15px x4
            Sliced(p, "RewardBox", "OffLineReward/Ui_offlineRewardBox", 4f, new Vector2(937f, 512f), Pos(540.5f, 936f), Color.white);

            // 윗 밝은 띠는 상자 그림 자체의 위 20px 이다(#8485A1, 아래는 #45465F) — 9슬라이스 위 테두리 20 x4 = 80.
            // PSD 의 도형(사각형 1624)이 같은 자리를 덮지만 따로 그리지 않는다.

            autoTimerText = CreateText("TimerText", p, "이미 한도 시간이 되었습니다. 00:00:00", 30f, TextAlignmentOptions.Center, Color.white);
            SetRect(autoTimerText.rectTransform, new Vector2(800f, 42f), Pos(531f, 719f));

            BuildRewardGrid(p);

            // 받기 버튼 하나만 둔다 — 이 창에 소탕 UX 는 두지 않는다(사용자 지시 2026-10-01).
            // PSD 의 두 버튼 자리(보이는 178~911)를 한 버튼이 차지한다. 여백 4px x3 씩 → 757x169
            autoClaimButton = SpriteButton(p, "ClaimButton", "Ingame/Big_Btn_Green", new Vector2(757f, 169f), Pos(544.5f, 1407.5f - LiftUp));
            TMP_Text claimLabel = CreateText("Label", autoClaimButton.transform, "받기", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(claimLabel.rectTransform, new Vector2(300f, 46f), new Vector2(0f, 4.5f));
        }

        /// <summary>
        /// 보상 격자 — 5열, 칸 간격 148.5 x 150 (PSD 열 중심 235/381/533/679/829, 행 중심 852/1002/…).
        /// 상자 아래쪽(y 1173)에서 잘리고 세로로 스크롤된다.
        /// </summary>
        private void BuildRewardGrid(Transform p)
        {
            filledSlotSprite = LoadSprite("ItemSlot/Itme_Slot_3");
            emptySlotSprite = LoadSprite("ItemSlot/Itme_Slot_0");

            const float top = 762f, bottom = 1173f, left = 160.75f, width = 742.5f;
            GameObject viewport = CreateRect("RewardViewport", p);
            SetRect(viewport.GetComponent<RectTransform>(), new Vector2(width + 60f, bottom - top),
                Pos(left + width * 0.5f, (top + bottom) * 0.5f));
            viewport.AddComponent<RectMask2D>();

            GameObject content = CreateRect("RewardGrid", viewport.transform);
            autoRewardRoot = content.GetComponent<RectTransform>();
            autoRewardRoot.anchorMin = new Vector2(0f, 1f);
            autoRewardRoot.anchorMax = new Vector2(1f, 1f);
            autoRewardRoot.pivot = new Vector2(0.5f, 1f);
            autoRewardRoot.anchoredPosition = Vector2.zero;
            autoRewardRoot.sizeDelta = Vector2.zero;

            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(30, 30, 15, 15);
            grid.cellSize = new Vector2(148.5f, 150f);
            grid.spacing = Vector2.zero;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = autoRewardRoot;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // 굽는 시점의 모습: 빈 칸 3줄. 런타임 Refresh 가 실제 보상으로 다시 채운다.
            for (int i = 0; i < MinSlotCount; i++)
            {
                GameObject cell = CreateRect("Slot", autoRewardRoot);
                Image slot = CreateImage("Bg", cell.transform, Color.white);
                slot.sprite = emptySlotSprite;
                slot.color = EmptySlotTint;
                slot.raycastTarget = false;
                SetRect(slot.rectTransform, new Vector2(192f, 192f), Vector2.zero);
            }
        }

        // ── 조립 헬퍼 ────────────────────────────────────────────────────

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
                throw new InvalidOperationException("[방치보상 굽기] 스프라이트가 없다: Resources/" + ArtRoot + pathUnderArt);
            return sprite;
        }

        private static Image Picture(Transform parent, string name, string path, Vector2 size, Vector2 position)
        {
            Image image = CreateImage(name, parent, Color.white);
            image.sprite = LoadSprite(path);
            image.raycastTarget = false;
            SetRect(image.rectTransform, size, position);
            return image;
        }

        /// <summary>9슬라이스 판. 테두리는 사람이 정한다 — 여기서는 Sliced 와 배율만 지정한다.</summary>
        private static Image Sliced(Transform parent, string name, string path, float pixelScale, Vector2 size, Vector2 position, Color color)
        {
            Image image = Picture(parent, name, path, size, position);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f / pixelScale;
            image.color = color;
            return image;
        }

        private static Button SpriteButton(Transform parent, string name, string path, Vector2 size, Vector2 position)
        {
            Image image = Sliced(parent, name, path, 3f, size, position, Color.white);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
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
            GameObject gameObject = CreateRect(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
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
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }
    }
}
