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
                titleText.SetText("현재 현상금");

            if (definition == null)
            {
                if (nameText != null)
                    nameText.SetText("현상금 X");

                if (detailText != null)
                    detailText.SetText("현상금을 골라보세요");

                // 빈 칸은 빈 채로 둔다 — 시안의 "현상금 X" 상태가 그렇다.
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
                    "   " + definition.FormatReward());
            }

            if (icon != null)
            {
                icon.enabled = definition.icon != null;
                icon.sprite = definition.icon;
                icon.color = definition.tint;
            }
        }

        private void OnClickChange()
        {
            GameContainer.UI?.Show<UIBountySelectDialog>();
        }

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용. 시안: Art/Layout/Wanted_Layout(2).png — 수치는 Docs/WantedUIArtPort.md
        // ──────────────────────────────────────────────────────────────

        /// <summary>띠(Wanted_Bg x3)의 보이는 사각형 — 시안 x209~887 · y800~971.</summary>
        private static readonly Vector2 BannerCenter = new Vector2(548f, 885.5f);
        private const float BannerVisibleWidth = 679f;
        private const float BannerVisibleHeight = 172f;

        /// <summary>
        /// 몬스터 그림 배율. 128px 그림 안에 몬스터가 작게 들어 있어서 1:1 로 두면 칸이 빈다.
        /// 시안 주석 "몬스터 1.4배 / 클리핑마스크" — 넘치는 것은 칸이 자른다.
        /// </summary>
        internal const float MonsterScale = 1.4f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBountyBanner Create(Transform parent, TMP_FontAsset font, Material plainMaterial)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBountyBanner", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            // DialogView 는 <b>늘리지 않는다.</b> 여기가 곧 레이캐스트 면적이라
            // 화면 전체로 늘리면 아래 다이스 보드 클릭을 통째로 먹는다.
            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.SetRect(view.GetComponent<RectTransform>(),
                new Vector2(BannerVisibleWidth, BannerVisibleHeight),
                UIBountyUIFactory.Pos(BannerCenter.x, BannerCenter.y));

            var banner = root.AddComponent<UIBountyBanner>();
            banner.dialogView = view;

            // 백키로 닫히면 안 된다. 상시 표시물이라 닫혀도 다시 여는 길이 없고,
            // 백키 스택에 얹으면 관리 단계에서 뒤로가기가 이 띠를 먼저 먹는다.
            banner.UseBackBtn = false;

            // 띠 그림. view 기준 (0,0) 이 띠의 보이는 중심이다.
            Image background = UIBountyUIFactory.CreateSprite("Background", view.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Wanted_Bg"), 3f,
                UIBountyUIFactory.WantedBgRect(BannerVisibleWidth, BannerVisibleHeight, 3f), Vector2.zero, true);

            // 띠 바탕은 클릭을 먹는다 — 버튼 옆을 살짝 빗나간 탭이 그 아래 필드로 새지 않게.
            background.raycastTarget = true;

            // 초상 칸(123², cda280). 몬스터는 1.4배로 넣고 칸이 자른다.
            Image portrait = UIBountyUIFactory.CreateImage("Portrait", view.transform, UIBountyUIFactory.Hex(0xCDA280));
            UIBountyUIFactory.SetRect(portrait.rectTransform, new Vector2(123f, 123f),
                UIBountyUIFactory.Local(291.5f, 886.5f, BannerCenter));
            portrait.raycastTarget = false;
            portrait.gameObject.AddComponent<RectMask2D>();

            banner.icon = UIBountyUIFactory.CreateImage("Icon", portrait.transform, Color.white);
            UIBountyUIFactory.SetRect(banner.icon.rectTransform,
                new Vector2(128f * MonsterScale, 128f * MonsterScale), Vector2.zero);
            banner.icon.preserveAspect = true;
            banner.icon.raycastTarget = false;
            banner.icon.enabled = false;

            // 글자 셋. 왼쪽 끝 x365 에 맞춘다.
            const float TextLeft = 365f;
            const float TextWidth = 310f;
            float textCenterX = TextLeft + TextWidth * 0.5f;

            banner.titleText = UIBountyUIFactory.CreateText("Title", view.transform,
                "현재 현상금", 30f, TextAlignmentOptions.Left, UIBountyUIFactory.Hex(0x906544), font, plainMaterial);
            UIBountyUIFactory.SetRect(banner.titleText.rectTransform, new Vector2(TextWidth, 40f),
                UIBountyUIFactory.Local(textCenterX, 843f, BannerCenter));

            banner.nameText = UIBountyUIFactory.CreateText("Name", view.transform,
                "현상금 X", 40f, TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(banner.nameText.rectTransform, new Vector2(TextWidth, 54f),
                UIBountyUIFactory.Local(textCenterX, 887f, BannerCenter));

            banner.detailText = UIBountyUIFactory.CreateText("Detail", view.transform,
                "현상금을 골라보세요", 25f, TextAlignmentOptions.Left, UIBountyUIFactory.Hex(0xB78F6F), font, plainMaterial);
            UIBountyUIFactory.SetRect(banner.detailText.rectTransform, new Vector2(TextWidth, 34f),
                UIBountyUIFactory.Local(textCenterX, 930f, BannerCenter));

            // 선택하기 — Big_Btn_Yellow x3, 보이는 176x103 · 중심 (769.5, 884).
            UIBountyUIFactory.ButtonRect(769.5f, 884f, 176f, 103f, 3f, out Vector2 buttonSize, out Vector2 buttonPos);
            Vector2 bannerPos = UIBountyUIFactory.Pos(BannerCenter.x, BannerCenter.y);
            Image change = UIBountyUIFactory.CreateSprite("ChangeButton", view.transform,
                UIBountyUIFactory.LoadSprite("Ingame/Big_Btn_Yellow"), 3f, buttonSize, buttonPos - bannerPos, true);
            change.raycastTarget = true;
            banner.changeButton = change.gameObject.AddComponent<Button>();
            banner.changeButton.targetGraphic = change;

            TMP_Text changeLabel = UIBountyUIFactory.CreateText("Label", change.transform, "선택하기", 25f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.SetRect(changeLabel.rectTransform, buttonSize, Vector2.zero);

            return banner;
        }
    }
}
