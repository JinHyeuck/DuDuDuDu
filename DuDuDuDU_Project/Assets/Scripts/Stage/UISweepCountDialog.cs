using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.DI;
using OJ.Hunting;
using OJ.Point;
using OJ.Relic;
using OJ.UI;

namespace OJ.Stage
{
    /// <summary>
    /// 소탕 횟수를 고르는 창.
    ///
    /// <code>
    ///            소탕                 [X]
    ///      N. 스테이지 이름
    ///   소탕 횟수
    ///   [MIN] [-]  12  [+] [MAX]
    ///   필요 재화
    ///        (고기) 60 / 340
    ///           [ 소탕 ]
    /// </code>
    ///
    /// 생김새는 강화 팝업(<c>Art/Layout/Ugrade_Popup.png</c>)·방치 보상 창과 같은 계열이다 —
    /// Ui_Popup_Bg 판, #636481 안쪽 판, #45465F 칸, 초록 큰 버튼, 오른쪽 위 X.
    ///
    /// <b>왜 버튼에서 바로 안 주고 창을 한 번 끼우나.</b> 소탕은 고기를 먹는다
    /// (<see cref="SweepRules.StaminaCostPerSweep"/>). 되돌릴 수 없는 소모를 한 번의
    /// 오터치로 끝내면 안 되고, 몇 회를 돌지도 유저가 정해야 한다.
    ///
    /// <b>지불과 지급이 여기 한곳에 있다.</b> 창이 횟수만 돌려주고 버튼이 지불하는 구조로
    /// 두면 "창이 보여 준 비용"과 "버튼이 뺀 비용"이 두 자리에 생긴다. 표시와 결제가
    /// 어긋나는 사고는 그 틈에서만 난다.
    /// </summary>
    public class UISweepCountDialog : DialogBase
    {
        private const string ArtRoot = "Art/";

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color InnerBoxColor = Hex(0x636481);
        private static readonly Color CellColor = Hex(0x45465F);
        private static readonly Color CaptionColor = Hex(0xD2D3E3);
        private static readonly Color ShortageColor = Hex(0xFF5A5A);
        private static readonly Color DisabledTint = new Color(0.55f, 0.55f, 0.60f, 1f);

        /// <summary>
        /// 이 창과 <see cref="UISweepLobbyButton"/> 이 화면에 낼 수 있는 <b>모든</b> 글자.
        /// <c>SweepCountDialogPrefabBaker</c> 가 굽기 전에 폰트 아틀라스와 대조한다.
        ///
        /// <b>왜 이런 목록이 필요한가.</b> 프로젝트 UI 폰트(<c>BMHANNAProOTF SDF</c>)는
        /// <b>Static</b> 이고 폴백이 비어 있다 — 아틀라스에 없는 글자는 예외도 로그도 없이
        /// <b>빈칸</b>으로 나온다. 실제로 이 창의 "소탕" 에서 '탕'(U+D0D5) 이 그렇게 사라졌다.
        /// 문자 집합은 <c>BMHANNAProOTF_CharacterSet.txt</c> 가 정본이고, 여기 없는 글자를
        /// 쓰려면 그 파일에 더한 뒤 아틀라스를 다시 구워야 한다.
        ///
        /// 서식 문자열로 조립되는 말(<c>"{0} 스테이지"</c> 따위)은 구워진 계층을 훑어서는
        /// 안 보이므로 여기에 손으로 모은다. <b>새 문구를 넣으면 여기도 같이 늘릴 것.</b>
        /// </summary>
        public const string RequiredGlyphs =
            "소탕스테이지클리어한가없다횟수필요재화" +
            "MINAX0123456789,./ +-";

        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text stageText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private Button minButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button maxButton;
        [SerializeField] private Image costIcon;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmText;

        private int targetStageIndex;
        private int maxCount;
        private int selectedCount;

        public static UISweepCountDialog Create(Transform parent, TMP_FontAsset fontAsset)
        {
            GameObject root = CreateRect("UISweepCountDialog", parent);
            Stretch(root.GetComponent<RectTransform>());

            GameObject view = CreateRect("DialogView", root.transform);
            Stretch(view.GetComponent<RectTransform>());

            // 딤 — 누르면 닫힌다.
            Image dim = CreateImage("Dim", view.transform, DimColor);
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;

            UISweepCountDialog dialog = root.AddComponent<UISweepCountDialog>();
            dialog.font = fontAsset;
            dialog.dialogView = view;
            dialog.UseBackBtn = true;
            dialog.Build(view.transform);
            dialog.AddExitButton(dimButton);
            return dialog;
        }

        /// <summary>
        /// 좌표는 1080x1920 화면 기준(<see cref="Pos"/>) — 판이 화면 가운데라 판 안 좌표와 같다.
        /// 판 크기 = 보이는 크기 + 투명 여백 x 배율(<c>Tools/ui/PORTING.md</c>). 판은 x4, 버튼은 x3 픽셀 아트다.
        /// </summary>
        private void Build(Transform viewRoot)
        {
            // 팝업 판 — 보이는 924x776 (572~1348) + 여백 3px x4
            panel = Sliced(viewRoot, "Panel", "Upgrade/Ui_Popup_Bg", 4f, new Vector2(948f, 788f), Pos(540f, 960f), Color.white).gameObject;
            Transform p = panel.transform;

            TMP_Text title = CreateText("Title", p, "소탕", 45f, TextAlignmentOptions.Center, Color.white);
            SetRect(title.rectTransform, new Vector2(600f, 60f), Pos(540f, 642f));

            // 안쪽 판 — 보이는 857x432 (693~1125) + 여백 6px x4
            Sliced(p, "InnerBox", "Upgrade/Ui_Popup_SmallBox", 4f, new Vector2(909f, 456f), Pos(540f, 909f), InnerBoxColor);
            Picture(p, "TopDecor", "OffLineReward/FromPsd/FromPsd_TopDecor", new Vector2(830f, 55f), Pos(541f, 734.5f));

            stageText = CreateText("StageText", p, "1. 스테이지", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(stageText.rectTransform, new Vector2(760f, 48f), Pos(540f, 745f));

            // 횟수 줄 — [MIN] [-] 수량 [+] [MAX]. 버튼은 Btn_Gray x3 (보이는 24px, 여백 4/6/4/2 → 중심 6px 위)
            Caption(p, "CountCaption", "소탕 횟수", 815f);
            minButton = GrayButton(p, "MinButton", "MIN", new Vector2(154f, 114f), Pos(215f, 879f));
            decreaseButton = GrayButton(p, "DecreaseButton", "-", new Vector2(124f, 114f), Pos(350f, 879f));
            Image countBox = Sliced(p, "CountBox", "Upgrade/Ui_Popup_SmallBox", 4f, new Vector2(284f, 124f), Pos(540f, 885f), CellColor);
            countText = CreateText("CountText", countBox.transform, "1", 45f, TextAlignmentOptions.Center, Color.white);
            Stretch(countText.rectTransform);
            increaseButton = GrayButton(p, "IncreaseButton", "+", new Vector2(124f, 114f), Pos(730f, 879f));
            maxButton = GrayButton(p, "MaxButton", "MAX", new Vector2(154f, 114f), Pos(865f, 879f));

            // 필요 재화 — (고기) 필요 / 보유
            Caption(p, "CostCaption", "필요 재화", 965f);
            Sliced(p, "CostBox", "Upgrade/Ui_Popup_SmallBox", 4f, new Vector2(804f, 114f), Pos(540f, 1035f), CellColor);
            costIcon = CreateImage("CostIcon", p, Color.white);
            costIcon.preserveAspect = true;
            costIcon.raycastTarget = false;
            SetRect(costIcon.rectTransform, new Vector2(76f, 76f), Pos(405f, 1035f));
            costText = CreateText("CostText", p, "5 / 0", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(costText.rectTransform, new Vector2(320f, 48f), Pos(570f, 1035f));

            // 소탕 — Big_Btn_Green x3, 보이는 331x145 + 여백 4/6/4/2 x3 → 355x169, 중심이 6px 위
            confirmButton = SpriteButton(p, "ConfirmButton", "Ingame/Big_Btn_Green", 3f, new Vector2(355f, 169f), Pos(540f, 1223.5f));
            confirmText = CreateText("Label", confirmButton.transform, "소탕", 35f, TextAlignmentOptions.Center, Color.white);
            SetRect(confirmText.rectTransform, new Vector2(300f, 46f), new Vector2(0f, 4.5f));

            // 닫기 — 방치 보상 창과 같은 그림(FromPsd 임시). 판 오른쪽 위 모서리에 걸친다.
            closeButton = SpriteButton(p, "CloseButton", "OffLineReward/FromPsd/FromPsd_CloseButton", 1f, new Vector2(112f, 111f), Pos(914f, 595f));
            closeButton.GetComponent<Image>().type = Image.Type.Simple;
        }

        private void Caption(Transform parent, string name, string value, float designY)
        {
            TMP_Text caption = CreateText(name, parent, value, 30f, TextAlignmentOptions.MidlineLeft, CaptionColor);
            SetRect(caption.rectTransform, new Vector2(780f, 42f), Pos(540f, designY));
        }

        private Button GrayButton(Transform parent, string name, string label, Vector2 size, Vector2 position)
        {
            Button button = SpriteButton(parent, name, "Ingame/Btn_Gray", 3f, size, position);
            TMP_Text text = CreateText("Label", button.transform, label, label.Length == 1 ? 50f : 32f, TextAlignmentOptions.Center, Color.white);
            SetRect(text.rectTransform, size - new Vector2(24f, 24f), new Vector2(0f, 6f));
            return button;
        }

        /// <summary>
        /// 버튼 배선. 프리팹에는 델리게이트가 저장되지 않으므로 여기서 다시 붙인다.
        /// <see cref="DialogBase.Load"/> 가 최초 1회만 부른다.
        /// </summary>
        protected override void OnLoad()
        {
            base.OnLoad();

            if (minButton != null) minButton.onClick.AddListener(() => SetCount(1));
            if (decreaseButton != null) decreaseButton.onClick.AddListener(() => SetCount(selectedCount - 1));
            if (increaseButton != null) increaseButton.onClick.AddListener(() => SetCount(selectedCount + 1));
            if (maxButton != null) maxButton.onClick.AddListener(() => SetCount(maxCount));
            if (closeButton != null) closeButton.onClick.AddListener(Exit);
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        }

        protected override void OnEnter()
        {
            base.OnEnter();

            StageProgressManager progress = StageProgressManager.Instance;
            int lastCleared = progress != null ? progress.GetLastClearedStageIndex() : 0;
            targetStageIndex = SweepRules.ResolveTargetStageIndex(lastCleared);

            PointManager points = PointManager.Instance;
            int ownedStamina = points != null ? points.Get(PointType.Stamina) : 0;
            maxCount = SweepRules.ResolveMaxCount(ownedStamina);

            // 열 때는 최대치로 시작한다. 소탕은 "쌓인 것을 한 번에 턴다"가 기본 동작이라
            // 매번 MAX 를 한 번 더 누르게 하면 그 탭이 순수한 군더더기가 된다.
            SetCount(maxCount);
        }

        private void SetCount(int value)
        {
            selectedCount = SweepRules.ClampToMax(value, maxCount);
            Refresh();
        }

        private void Refresh()
        {
            PointManager points = PointManager.Instance;
            int ownedStamina = points != null ? points.Get(PointType.Stamina) : 0;
            bool canSweep = targetStageIndex >= 1 && selectedCount >= 1;

            if (stageText != null)
            {
                stageText.SetText(targetStageIndex >= 1
                    ? targetStageIndex + ". " + StageData.GetStageDisplayName(targetStageIndex)
                    : "클리어한 스테이지가 없다");
            }

            if (countText != null)
                countText.SetText(selectedCount.ToString("#,##0"));

            if (costText != null)
            {
                // 0 회일 때도 1회분 비용을 적는다. "5 / 3" 이 빨갛게 보여야
                // 왜 못 누르는지가 한 줄로 읽힌다. (필요 / 보유)
                int shownCount = canSweep ? selectedCount : 1;
                int cost = SweepRules.TotalStaminaCost(shownCount);
                costText.color = canSweep ? Color.white : ShortageColor;
                costText.SetText($"{cost:#,##0} / {ownedStamina:#,##0}");
            }

            if (costIcon != null && costIcon.sprite == null)
                costIcon.sprite = PointRewardUtility.GetPointIcon(PointType.Stamina);
            if (costIcon != null)
                costIcon.enabled = costIcon.sprite != null;

            if (confirmButton != null)
                confirmButton.interactable = canSweep;

            if (minButton != null) minButton.interactable = canSweep && selectedCount > 1;
            if (decreaseButton != null) decreaseButton.interactable = canSweep && selectedCount > 1;
            if (increaseButton != null) increaseButton.interactable = canSweep && selectedCount < maxCount;
            if (maxButton != null) maxButton.interactable = canSweep && selectedCount < maxCount;
        }

        private void Confirm()
        {
            if (targetStageIndex < 1 || selectedCount < 1)
                return;

            PointManager points = PointManager.Instance;
            if (points == null)
            {
                Debug.LogWarning("[소탕] PointManager 가 없다. 소탕을 돌리지 않는다.");
                return;
            }

            int cost = SweepRules.TotalStaminaCost(selectedCount);

            // 화면을 믿지 않고 여기서 한 번 더 뺀다. 창을 열어 둔 사이에 다른 경로가
            // 고기를 쓰면 maxCount 는 낡은 값이 된다 — TrySpend 만이 최종 판정이다.
            if (!points.TrySpend(PointType.Stamina, cost))
            {
                Debug.Log($"[소탕] 고기가 모자라 취소했다. 필요 {cost} / 보유 {points.Get(PointType.Stamina)}");
                Refresh();
                return;
            }

            int count = selectedCount;
            List<PointRewardEntry> rewards = SweepRules.BuildRewards(targetStageIndex, count);

            // 클리어와 같은 보상이므로 유물 보너스도 같이 탄다
            // (GameManager.ClearStage 와 같은 순서 — 지급 전에 곱한다).
            if (RelicManager.Instance != null)
                rewards = RelicManager.Instance.ApplyStageClearRewardBonus(rewards);

            PointRewardUtility.GrantRewards(rewards);

            Debug.Log($"Sweep Stage {targetStageIndex} x{count} | 고기 -{cost} | " +
                      $"{PointRewardUtility.BuildRewardSummary(rewards)}");

            // 결과창을 이 창 위에 겹치지 않는다. 먼저 닫고 띄운다.
            Exit();

            UIRewardResultDialog resultDialog = GameContainer.UI?.Get<UIRewardResultDialog>();
            if (resultDialog != null)
                resultDialog.Open(rewards, $"스테이지 {targetStageIndex} 소탕 {count}회");
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

        private TMP_Text CreateText(
            string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Color color)
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

        private static Vector2 Pos(float x, float y)
        {
            return new Vector2(x - 540f, 960f - y);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        /// <summary>스프라이트를 읽는다. <b>없으면 멈춘다</b> — null 로 구우면 흰 사각형이 조용히 저장된다.</summary>
        private static Sprite LoadSprite(string pathUnderArt)
        {
            Sprite sprite = Resources.Load<Sprite>(ArtRoot + pathUnderArt);
            if (sprite == null)
                throw new System.InvalidOperationException("[소탕 창 굽기] 스프라이트가 없다: Resources/" + ArtRoot + pathUnderArt);
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

        private static Button SpriteButton(Transform parent, string name, string path, float pixelScale, Vector2 size, Vector2 position)
        {
            Image image = Sliced(parent, name, path, pixelScale, size, position, Color.white);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = DisabledTint;
            button.colors = colors;
            return button;
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
            SetAnchored(rectTransform, new Vector2(0.5f, 0.5f), anchoredPosition, size);
        }

        private static void SetAnchored(RectTransform rectTransform, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }
    }
}
