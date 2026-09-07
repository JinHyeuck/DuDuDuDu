using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using OJ.Bounty;
using OJ.DI;
using OJ.UI;

namespace OJ.Rewind
{
    /// <summary>
    /// 웨이브 중에 떠 있는 되돌리기 버튼. 남은 횟수를 함께 띄운다.
    ///
    /// <b>남은 횟수가 라벨에 있어야 하는 이유.</b> 되돌리기의 값어치는 유한하다는 데서
    /// 나온다 — 무제한이면 패배가 영영 안 와서 "더 강해져야겠다" 의 방아쇠가 사라진다.
    /// 몇 번 남았는지가 안 보이면 유저는 그것이 유한한 줄 모르고, 그러면 마지막 한 번의
    /// 긴장도 생기지 않는다.
    ///
    /// <b>왜 다이얼로그인가.</b> 전투 HUD 라면 씬에 놓는 것이 자연스럽지만, 그러려면
    /// <c>BattleScene.unity</c> 를 편집해야 한다(AGENTS 절대 규칙 3).
    /// <c>UIBountyBanner</c>·<c>UIDamageContributionPanel</c> 이 같은 이유로 같은 모양이다.
    ///
    /// <b>루트를 화면 전체로 늘리지 않는다.</b> 늘리면 그 위의 <c>GraphicRaycaster</c> 가
    /// 보드 클릭을 통째로 먹는다.
    ///
    /// <b>여기에 판단을 두지 않는다.</b> 누르면 <c>GameManager.OnClick_Rewind</c> 로 넘긴다 —
    /// 확인 창을 띄우고 배속을 멈췄다 되돌리는 일은 <c>timeSpeed</c> 를 아는 쪽이 해야 하고,
    /// 그건 <c>GameManager</c> 다. <c>OnClick_Pause</c> 가 이미 그 자리에 있다.
    /// </summary>
    public class UIWaveRewindButton : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private Button rewindButton;
        [SerializeField] private TMP_Text label;

        private WaveRewindManager subscribed;

        protected override void OnLoad()
        {
            if (rewindButton != null)
                rewindButton.onClick.AddListener(OnClick);
        }

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

            if (rewindButton != null)
                rewindButton.onClick.RemoveListener(OnClick);

            base.OnDestroy();
        }

        private void Subscribe()
        {
            WaveRewindManager rewind = battle != null ? battle.Rewind : null;
            if (rewind == null || subscribed == rewind)
                return;

            Unsubscribe();
            rewind.OnAvailabilityChanged += Refresh;
            subscribed = rewind;
        }

        private void Unsubscribe()
        {
            if (subscribed == null)
                return;

            subscribed.OnAvailabilityChanged -= Refresh;
            subscribed = null;
        }

        private void Refresh()
        {
            WaveRewindManager rewind = battle != null ? battle.Rewind : null;
            if (rewind == null || label == null)
                return;

            label.SetText("되돌리기 {0}", rewind.RemainingCount);
        }

        private void OnClick()
        {
            // 창구가 비었으면 전투 밖이다. 그 상황에서 이 버튼이 눌릴 경로는 없지만,
            // 눌렸다면 조용히 넘어가는 편이 낫다 — 여기서 터뜨려도 알려 줄 사람이 없다.
            battle?.Game?.OnClick_Rewind();
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────
        //
        // 조립 헬퍼는 UIBountyUIFactory 를 그대로 쓴다. 같은 다섯 함수가 이미 이 어셈블리에
        // 세 벌(UIBountyUIFactory · UIBattleDiceDetailPanel · UIDamageContributionPanel)
        // 있는데, 네 번째를 만들면 여백이 서로 어긋나는 화면이 나오고 그 원인은 눈으로
        // 못 찾는다. 이름에 Bounty 가 붙어 있지만 그 파일의 주석이 스스로를
        // "코드로 UI 를 굽는 클래스들이 함께 쓰는 조립 도구" 라고 적어 두었다.

        private const float ButtonWidth = 200f;
        private const float ButtonHeight = 76f;
        private const float RightMargin = 16f;

        /// <summary>
        /// 화면 세로 중심에서 얼마나 올릴지.
        ///
        /// <c>UIDamageContributionPanel</c> 과 <b>같은 띠의 반대쪽</b>이다. 그 패널
        /// 주석이 적어 둔 대로 위로는 웨이브 게이지, 아래로는 다이스 보드가 있고 그 사이가
        /// 비어 있다 — 왼쪽은 기여도가 쓰고 있으므로 오른쪽을 쓴다.
        /// </summary>
        private const float CenterY = 430f;

        private static readonly Color ButtonColor = new Color(0.55f, 0.24f, 0.24f, 0.92f);

        /// <summary>에디터 굽기 전용.</summary>
        public static UIWaveRewindButton Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIWaveRewindButton", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();

            rootRect.anchorMin = new Vector2(1f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rootRect.anchoredPosition = new Vector2(-RightMargin, CenterY);

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var panel = root.AddComponent<UIWaveRewindButton>();
            panel.dialogView = view;

            // 백키로 닫지 않는다. 전투 중 백키는 다른 뜻이고(일시정지), 이 버튼은
            // 닫는 것이 아니라 <b>웨이브가 끝나면 저절로 사라지는</b> 종류다.
            panel.UseBackBtn = false;

            Image background = UIBountyUIFactory.CreateImage("Background", view.transform, ButtonColor);
            UIBountyUIFactory.Stretch(background.rectTransform);

            panel.rewindButton = background.gameObject.AddComponent<Button>();
            panel.rewindButton.targetGraphic = background;

            panel.label = UIBountyUIFactory.CreateText(
                "Label", background.transform, "되돌리기 1", 26f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.Stretch(panel.label.rectTransform);

            return panel;
        }
    }
}
