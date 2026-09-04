using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Stage;
using OJ.UI;
using OJ.Utils;

namespace OJ.StageStar
{
    public class UIStageStarStageItem : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text stageTitleText;

        [Header("Visuals")]
        [SerializeField] private Image bannerImage;
        [SerializeField] private Sprite[] bannerSprites;
        [SerializeField] private GameObject lockedRoot;
        [SerializeField] private GameObject perfectRoot;

        [Header("Conditions")]
        [SerializeField] private UIStageStarConditionRow minimumRow;
        [SerializeField] private UIStageStarConditionRow halfRow;
        [SerializeField] private UIStageStarConditionRow perfectRow;
        [SerializeField] private string minimumConditionText = "\uD074\uB9AC\uC5B4";
        [SerializeField] private string halfConditionText = "HP 50% \uC774\uC0C1 \uC0C1\uD0DC\uB85C \uD074\uB9AC\uC5B4";
        [SerializeField] private string perfectConditionText = "HP 100% \uC0C1\uD0DC\uB85C \uD074\uB9AC\uC5B4";

        [Header("Input")]
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonText;

        // 라벨은 세 상태를 구별해야 한다. 예전에는 "게임 시작" / "잠김" 두 가지뿐이었는데,
        // 잠긴 스테이지는 버튼 자체가 숨겨져서 "잠김"이 화면에 나온 적이 없었다.
        [SerializeField] private string startText = "게임 시작";
        [SerializeField] private string replayText = "다시 도전";
        [SerializeField] private string lockedText = "잠김";

        private int stageIndex;
        private Action<int> onStartStage;

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(HandleStartClicked);
        }

        public void Bind(StageData stageData, StageClearGrade bestGrade, bool isUnlocked, Action<int> startCallback)
        {
            stageIndex = stageData != null ? Mathf.Max(1, stageData.stageIndex) : 1;
            onStartStage = startCallback;

            // 클릭 배선을 <c>Awake</c> 가 아니라 여기서 한다.
            //
            // 이 아이템은 비활성 템플릿에서 <c>Instantiate</c> 되고, 활성화·바인딩 순서가
            // 다이얼로그 쪽 흐름에 달려 있다. <c>Awake</c> 에 걸어 두면 "언제 도는가"가
            // 호출 순서에 좌우되고, 안 돌면 <b>버튼은 멀쩡히 보이는데 눌러도 아무 일이 없다.</b>
            // 라벨은 <c>Bind</c> 가 칠하므로 화면상 정상으로 보여서 원인을 찾기가 아주 어렵다.
            //
            // <c>Bind</c> 는 이 아이템이 쓸모를 갖는 지점이고 반드시 불린다. 여기서 배선하면
            // "보이는데 안 눌린다"는 상태 자체가 성립할 수 없다.
            // 중복 등록을 막으려고 먼저 뗀다 — Refresh 가 여러 번 돌면 Bind 도 여러 번 불린다.
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStartClicked);
                startButton.onClick.AddListener(HandleStartClicked);
            }

            if (stageTitleText != null)
                stageTitleText.SetText(StageData.GetStageDisplayName(stageData.stageIndex));

            ApplyBanner(stageData.theme);

            if (minimumRow != null)
                minimumRow.Bind(StageClearGrade.Minimum, bestGrade, minimumConditionText);
            if (halfRow != null)
                halfRow.Bind(StageClearGrade.Half, bestGrade, halfConditionText);
            if (perfectRow != null)
                perfectRow.Bind(StageClearGrade.Perfect, bestGrade, perfectConditionText);

            bool isPerfect = bestGrade >= StageClearGrade.Perfect;
            if (lockedRoot != null)
                lockedRoot.SetActive(!isUnlocked);
            if (perfectRoot != null)
                perfectRoot.SetActive(isPerfect);

            // 버튼은 항상 보인다. 눌리는지만 해금 여부로 갈린다.
            //
            // 예전에는 두 경우에 버튼이 통째로 숨겨졌다 — 잠긴 스테이지, 그리고 이미 퍼펙트로
            // 깬 스테이지(hideStartButtonWhenPerfect). 그런데 이 다이얼로그가 스테이지를 고르는
            // 유일한 경로다(로비의 이전/다음 버튼은 씬에 배선돼 있지 않다). 그래서 퍼펙트를 찍는
            // 순간 그 스테이지에 다시 들어갈 방법이 사라졌다. 카드는 멀쩡히 보이는데 누를 것이
            // 없어서 클릭해도 시작이 안 되는 것처럼 보인다.
            //
            // 잠긴 쪽도 마찬가지였다. lockedRoot 가 프리팹에 연결돼 있지 않아 잠금 표시가 나오지
            // 않는데 버튼까지 없으니, 잠긴 카드와 다 깬 카드가 화면상 구별되지 않았다.
            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                startButton.interactable = isUnlocked;
            }

            // 라벨은 퍼펙트가 아니라 <b>클리어 여부</b>로 갈린다.
            //
            // 처음에는 isPerfect 로 갈랐는데, 그건 지워 버린 hideStartButtonWhenPerfect 의
            // 기준을 그대로 물려받은 것이었지 라벨의 기준이 아니었다. 별 1개로 깬 스테이지는
            // Minimum 등급이라 퍼펙트가 아니고, 그래서 이미 깬 판인데 "게임 시작"이 떴다.
            // 한 번이라도 깼으면 다시 들어가는 것이므로 "다시 도전"이 맞다.
            bool isCleared = bestGrade > StageClearGrade.None;

            if (startButtonText != null)
            {
                if (!isUnlocked)
                    startButtonText.SetText(lockedText);
                else
                    startButtonText.SetText(isCleared ? replayText : startText);
            }
        }

        private void ApplyBanner(StageTheme theme)
        {
            Sprite sprite = StaticResource.Instance.GetStageStarRewardBanner(theme);
            if (sprite != null)
                bannerImage.sprite = sprite;

            // <b>배너는 클릭을 먹으면 안 된다.</b>
            //
            // 이것이 "버튼은 보이는데 눌러도 시작이 안 되던" 원인이었다. 그리고 자기 카드의
            // 배너가 아니라 <b>바로 아래 카드의 배너</b>가 범인이다.
            //
            // 배너 렉트가 1024x1024 인데 카드는 1079.7x525 다. 즉 배너가 카드 위아래로
            // 250 가까이 삐져나온다. 세로 리스트에서 아래 카드가 나중 형제라 위에 그려지므로,
            // <b>N+1번 카드 배너의 윗자락이 N번 카드의 StartButton 을 통째로 덮는다.</b>
            // (피치 525+22=547, 배너 반높이 512 → 윗변이 앞 카드 중심 기준 y=-35,
            //  StartButton 은 y ∈ [-215,-92] 라 100% 안에 들어간다.)
            //
            // 눈에 안 보였던 이유는 배너 스프라이트 위아래 약 25%가 완전 투명이기 때문이다.
            // <b>uGUI 의 Image 는 알파를 무시하고 사각형 전체로 히트테스트한다</b>
            // (alphaHitTestMinimumThreshold 미설정). 그래서 화면은 멀쩡한데 클릭만 먹혔다.
            //
            // 배너에는 Button 이 없으므로 uGUI 는 부모로 거슬러 올라가며 Selectable 을 찾고,
            // 결국 <c>DialogView</c> 의 버튼에 클릭을 배달한다. 그 버튼은 DialogBase 의
            // _exitBtn 에 등록된 딤(dim) 이라 <c>Exit()</c> 를 부른다 — 그래서 카드를 누르면
            // 스테이지가 시작되는 대신 <b>다이얼로그가 닫혔다.</b>
            //
            // 마지막 카드만 뒤에 덮는 것이 없어서 정상 동작했을 것이다.
            //
            // 프리팹에서 체크를 끄는 대신 코드로 못 박는다. 장식용 이미지의 RaycastTarget 은
            // 인스펙터에서 실수로 다시 켜지기 쉽고, 그때 증상이 똑같이 되살아나는데
            // 원인을 찾는 비용이 이 한 줄과 비교가 안 된다.
            //
            // 스크롤은 그대로 된다 — Viewport 와 StageScroll 의 이미지가 여전히
            // RaycastTarget 이라 드래그를 받는다(클릭 추적으로 확인했다).
            bannerImage.raycastTarget = false;
        }

        private static string GetStageDisplayName(int nextStageIndex)
        {
            switch (nextStageIndex)
            {
                case 1:
                    return "\uC5B4\uB460\uC758 \uC232\uC18D";
                case 2:
                    return "\uACA8\uC6B8 \uC232\uC18D";
                case 3:
                    return "\uC0AC\uB9C9 \uB3C4\uC2DC";
                default:
                    return string.Format("Stage {0}", nextStageIndex);
            }
        }

        private void HandleStartClicked()
        {
            onStartStage?.Invoke(stageIndex);
        }
    }
}
