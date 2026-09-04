using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OJ.Core;
using OJ.UI;

namespace OJ.Bounty
{
    /// <summary>
    /// 현상금이 나온 순간 화면 위쪽에 잠깐 떴다 사라지는 알림 띠.
    ///
    /// <b>왜 필요한가.</b> 웨이브마다 일반 몬스터가 스무 마리 내려오는 사이에 현상금이
    /// 한 마리 섞여 나온다. 느린 걸음(0.3배)과 큰 덩치로 <b>구분</b>은 되지만 <b>등장 순간</b>은
    /// 놓치기 쉽고, 매 웨이브 반복되면 그냥 배경이 된다. 한 번 짚어 주면 매번 환기된다.
    ///
    /// <b>왜 배너를 재사용하지 않나.</b> <see cref="UIBountyBanner"/> 는 관리 단계 전용이라
    /// 웨이브가 시작되면 꺼진다. 콜아웃은 정확히 그 반대 시점(웨이브 중)에 떠야 한다.
    ///
    /// <b>스스로 사라진다.</b> 닫는 버튼도 백키도 없다 — 몇 초짜리 알림에 조작을 붙이면
    /// 그것을 눌러야 하는 것처럼 보인다. 그래서 레이캐스트도 전부 꺼 두어 아래 조작을
    /// 한 프레임도 가리지 않는다.
    /// </summary>
    public class UIBountyCallout : DialogBase
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text detailText;

        [Tooltip("떠 있는 시간(초). 배속을 타지 않는 실시간이다.")]
        [SerializeField, Min(0.2f)] private float holdSeconds = 1.8f;

        /// <summary>
        /// 이 콜아웃이 몇 번째 요청까지 살아 있는가. 새 요청이 오면 올라가고,
        /// 앞선 대기는 자기 번호가 밀린 것을 보고 조용히 물러난다.
        ///
        /// <b>없으면 겹칠 때 먼저 뜬 쪽이 나중 것을 지운다.</b> 지금은 웨이브당 한 마리라
        /// 겹칠 일이 없지만, 그 사실이 이 파일 밖(<c>BountyManager.ShouldSpawn</c>)에 있어서
        /// 바뀌어도 여기는 모른다.
        /// </summary>
        private int showSequence;

        /// <summary>
        /// 띄운다. 정의가 null 이면 아무것도 하지 않는다 — 등급 0 은 애초에 스폰되지 않으므로
        /// 여기 null 이 오는 것은 데이터 사고이고, 빈 띠를 띄우면 그 사고가 가려진다.
        /// </summary>
        public void Play(BountyDefinition definition, int hp)
        {
            if (definition == null)
                return;

            if (titleText != null)
                titleText.SetText("현상금 등장");

            if (nameText != null)
                nameText.SetText(definition.displayName);

            if (detailText != null)
            {
                detailText.SetText(
                    "HP " + ShortNumberFormat.Format(hp) +
                    "   " + FormatReward(definition));
            }

            if (icon != null)
            {
                icon.enabled = true;
                icon.sprite = definition.icon;
                icon.color = definition.tint;
            }

            Enter();
            HideAfterHold(++showSequence).Forget();
        }

        /// <summary>
        /// <c>UnscaledDeltaTime</c> 을 쓴다. 배속 3배에서 알림이 0.6초로 줄면 읽기 전에
        /// 사라지는데, 배속은 <b>전투를 빨리 돌리라</b>는 뜻이지 글을 빨리 읽으라는 뜻이 아니다.
        /// (몬스터 상태이상이 게임 시간을 쓰는 것과 반대인 이유가 이것이다.)
        /// </summary>
        private async UniTaskVoid HideAfterHold(int sequence)
        {
            await UniTask.Delay(
                Mathf.RoundToInt(holdSeconds * 1000f),
                DelayType.UnscaledDeltaTime,
                PlayerLoopTiming.Update);

            // 기다리는 사이에 씬이 내려갔을 수 있다. 파괴된 오브젝트는 == null 이 true 인
            // 가짜 null 이라 이 검사에 걸린다.
            if (this == null || sequence != showSequence)
                return;

            Exit();
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

        // ──────────────────────────────────────────────────────────────
        // 에디터 굽기 전용.
        // ──────────────────────────────────────────────────────────────

        private static readonly Color CardColor = new Color(0.14f, 0.09f, 0.11f, 0.94f);
        private static readonly Color CardEdgeColor = new Color(1f, 0.72f, 0.32f, 1f);
        private static readonly Color TitleColor = new Color(1f, 0.72f, 0.32f, 1f);
        private static readonly Color MutedTextColor = new Color(0.85f, 0.88f, 0.94f, 1f);

        private const float CardWidth = 620f;
        private const float CardHeight = 150f;

        /// <summary>
        /// 콜아웃이 뜨는 높이. <b>웨이브 게이지 바로 아래, 몬스터가 내려오는 길 위</b>다.
        /// 관리 단계 배너(y 560)보다 위에 두어 둘을 눈이 다른 것으로 읽게 한다.
        /// </summary>
        private const float CardCenterY = 720f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UIBountyCallout Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UIBountyCallout", parent);
            UIBountyUIFactory.Stretch(root.GetComponent<RectTransform>());

            // 화면 전체로 늘리지 않는다. 늘리면 그 아래 다이스 조작을 가릴 수 있고,
            // 이 띠는 읽히기만 하면 되는 물건이다.
            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.SetRect(view.GetComponent<RectTransform>(),
                new Vector2(CardWidth + 8f, CardHeight + 8f), new Vector2(0f, CardCenterY));

            var callout = root.AddComponent<UIBountyCallout>();
            callout.dialogView = view;

            // 백키 스택에 얹지 않는다. 스스로 사라지는 물건이 스택에 남으면 이미 없어진
            // 창이 백키 한 번을 조용히 먹는다(DialogBase.Unload 주석의 그 문제다).
            callout.UseBackBtn = false;

            Image edge = UIBountyUIFactory.CreateImage("Edge", view.transform, CardEdgeColor);
            UIBountyUIFactory.SetRect(edge.rectTransform,
                new Vector2(CardWidth + 8f, CardHeight + 8f), Vector2.zero);
            edge.raycastTarget = false;

            Image background = UIBountyUIFactory.CreateImage("Background", view.transform, CardColor);
            UIBountyUIFactory.SetRect(background.rectTransform,
                new Vector2(CardWidth, CardHeight), Vector2.zero);

            // 알림은 <b>읽히기만</b> 하면 된다. 레이캐스트를 켜 두면 전투 중에 이 띠가
            // 뜬 동안 그 아래를 못 누른다 — 하필 가장 바쁜 순간이다.
            background.raycastTarget = false;
            callout.card = background.rectTransform;

            callout.icon = UIBountyUIFactory.CreateImage("Icon", background.transform, Color.white);
            UIBountyUIFactory.SetRect(callout.icon.rectTransform,
                new Vector2(104f, 104f), new Vector2(-238f, 0f));
            callout.icon.preserveAspect = true;
            callout.icon.raycastTarget = false;

            callout.titleText = UIBountyUIFactory.CreateText("Title", background.transform,
                "현상금 등장", 26f, TextAlignmentOptions.Left, TitleColor, font);
            UIBountyUIFactory.SetRect(callout.titleText.rectTransform,
                new Vector2(400f, 32f), new Vector2(38f, 44f));

            callout.nameText = UIBountyUIFactory.CreateText("Name", background.transform,
                "이름", 38f, TextAlignmentOptions.Left, Color.white, font);
            UIBountyUIFactory.SetRect(callout.nameText.rectTransform,
                new Vector2(400f, 46f), new Vector2(38f, 4f));

            callout.detailText = UIBountyUIFactory.CreateText("Detail", background.transform,
                "", 26f, TextAlignmentOptions.Left, MutedTextColor, font);
            UIBountyUIFactory.SetRect(callout.detailText.rectTransform,
                new Vector2(400f, 32f), new Vector2(38f, -42f));

            return callout;
        }
    }
}
