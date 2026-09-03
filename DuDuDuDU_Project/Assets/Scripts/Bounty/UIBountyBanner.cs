using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Bounty
{
    /// <summary>
    /// 관리 단계에 화면 중상위에 떠 있는 현상금 띠. "이번 웨이브에 이게 나온다" 한 줄과
    /// 선택 창을 여는 [변경] 버튼이 전부다.
    ///
    /// <b>웨이브 중에는 뜨지 않는다.</b> 그 시간에는 바꿀 수도 없고, 몬스터가 내려오는
    /// 길 한가운데를 가린다.
    ///
    /// <b>왜 다이얼로그인가.</b> 상시 UI 라면 씬에 놓는 것이 자연스럽지만, 그러려면
    /// <c>BattleScene.unity</c> 를 편집해야 한다(절대 규칙 3). <c>UIService</c> 는 자기
    /// 캔버스를 런타임에 만들어 주므로 씬을 한 글자도 안 건드리고 같은 자리에 띄울 수 있고,
    /// 씬이 바뀌면 캔버스째 사라지는 정리까지 딸려 온다.
    ///
    /// <b>루트를 화면 전체로 늘리지 않는 이유.</b> 늘리면 그 위의 <c>GraphicRaycaster</c> 가
    /// 보드 클릭을 통째로 먹는다. 띠 크기만큼만 차지해야 아래 다이스 조작이 살아 있다.
    /// </summary>
    public class UIBountyBanner : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private Button changeButton;

        private BountyManager subscribed;

        protected override void OnEnter()
        {
            Subscribe();
            Refresh();
        }

        protected override void OnExit()
        {
            Unsubscribe();
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        private void Subscribe()
        {
            BountyManager bounty = battle != null ? battle.Bounty : null;
            if (bounty == null || subscribed == bounty)
                return;

            Unsubscribe();
            bounty.OnChanged += Refresh;
            subscribed = bounty;

            if (changeButton != null)
            {
                changeButton.onClick.RemoveListener(OnClickChange);
                changeButton.onClick.AddListener(OnClickChange);
            }
        }

        private void Unsubscribe()
        {
            if (subscribed != null)
            {
                subscribed.OnChanged -= Refresh;
                subscribed = null;
            }

            if (changeButton != null)
                changeButton.onClick.RemoveListener(OnClickChange);
        }

        /// <summary>
        /// 띠 내용을 다시 그린다. 선택이 바뀔 때(<c>OnChanged</c>)와 열릴 때만 돈다 —
        /// 관리 단계는 매 프레임 도는 화면이라 폴링으로 그리면 그만큼이 그대로 낭비다.
        /// </summary>
        public void Refresh()
        {
            BountyManager bounty = battle != null ? battle.Bounty : null;
            if (bounty == null)
                return;

            int grade = bounty.SelectedGrade;
            BountyDefinition definition = bounty.GetDefinition(grade);

            if (titleText != null)
                titleText.SetText("이번 웨이브 현상금");

            if (definition == null)
            {
                if (nameText != null)
                    nameText.SetText("현상금 선택 X");

                if (detailText != null)
                    detailText.SetText("눌러서 고를 수 있어요");

                if (icon != null)
                    icon.enabled = false;

                return;
            }

            if (nameText != null)
                nameText.SetText(definition.displayName);

            if (detailText != null)
            {
                detailText.SetText(
                    "HP " + ShortNumberFormat.Format(bounty.GetHp(grade)) +
                    "   " + FormatReward(definition));
            }

            if (icon != null)
            {
                icon.enabled = true;
                icon.sprite = definition.icon;
                icon.color = definition.tint;
            }
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

        private void OnClickChange()
        {
            GameContainer.UI?.Show<UIBountySelectDialog>();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        private static readonly Color BannerColor = new Color(0.086f, 0.11f, 0.23f, 0.92f);
        private static readonly Color BannerEdgeColor = new Color(0.42f, 0.56f, 0.85f, 1f);
        private static readonly Color ChangeColor = new Color(0.24f, 0.55f, 0.92f, 1f);
        private static readonly Color MutedTextColor = new Color(0.78f, 0.84f, 0.94f, 1f);

        private const float BannerWidth = 760f;
        private const float BannerHeight = 200f;

        /// <summary>
        /// 띠가 놓이는 높이. 선택 창(<c>UIBountySelectDialog</c>)은 이보다 아래를
        /// 중심으로 펼쳐지므로 둘이 겹치지 않는다.
        /// </summary>
        private const float BannerCenterY = 560f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBountyBanner Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBountyBanner", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            // DialogView 는 <b>늘리지 않는다.</b> 여기가 곧 레이캐스트 면적이라
            // 화면 전체로 늘리면 아래 다이스 보드 클릭을 통째로 먹는다.
            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.SetRect(view.GetComponent<RectTransform>(),
                new Vector2(BannerWidth + 8f, BannerHeight + 8f), new Vector2(0f, BannerCenterY));

            var banner = root.AddComponent<UIBountyBanner>();
            banner.dialogView = view;

            // 백키로 닫히면 안 된다. 상시 표시물이라 닫혀도 다시 여는 길이 없고,
            // 백키 스택에 얹으면 관리 단계에서 뒤로가기가 이 띠를 먼저 먹는다.
            banner.UseBackBtn = false;

            Image edge = UIBountyUIFactory.CreateImage("Edge", view.transform, BannerEdgeColor);
            UIBountyUIFactory.SetRect(edge.rectTransform,
                new Vector2(BannerWidth + 8f, BannerHeight + 8f), Vector2.zero);
            edge.raycastTarget = false;

            Image background = UIBountyUIFactory.CreateImage("Background", view.transform, BannerColor);
            UIBountyUIFactory.SetRect(background.rectTransform,
                new Vector2(BannerWidth, BannerHeight), Vector2.zero);

            banner.icon = UIBountyUIFactory.CreateImage("Icon", background.transform, Color.white);
            UIBountyUIFactory.SetRect(banner.icon.rectTransform,
                new Vector2(128f, 128f), new Vector2(-292f, 0f));
            banner.icon.preserveAspect = true;
            banner.icon.raycastTarget = false;

            banner.titleText = UIBountyUIFactory.CreateText("Title", background.transform,
                "이번 웨이브 현상금", 26f, TextAlignmentOptions.Left, MutedTextColor, font);
            UIBountyUIFactory.SetRect(banner.titleText.rectTransform,
                new Vector2(340f, 34f), new Vector2(-30f, 56f));

            banner.nameText = UIBountyUIFactory.CreateText("Name", background.transform,
                "현상금 선택 X", 38f, TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(banner.nameText.rectTransform,
                new Vector2(340f, 48f), new Vector2(-30f, 8f));

            banner.detailText = UIBountyUIFactory.CreateText("Detail", background.transform,
                "", 26f, TextAlignmentOptions.Left, MutedTextColor, font);
            UIBountyUIFactory.SetRect(banner.detailText.rectTransform,
                new Vector2(340f, 34f), new Vector2(-30f, -42f));

            Image change = UIBountyUIFactory.CreateImage("ChangeButton", background.transform, ChangeColor);
            UIBountyUIFactory.SetRect(change.rectTransform,
                new Vector2(180f, 96f), new Vector2(268f, 0f));
            banner.changeButton = change.gameObject.AddComponent<Button>();
            banner.changeButton.targetGraphic = change;

            TMP_Text changeLabel = UIBountyUIFactory.CreateText("Label", change.transform, "변경", 34f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(changeLabel.rectTransform, new Vector2(180f, 48f), Vector2.zero);

            return banner;
        }
    }
}
