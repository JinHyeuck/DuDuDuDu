using System.Text;
using TMPro;
using UnityEngine;
using VContainer;
using OJ.Bounty;
using OJ.Core;
using OJ.DI;
using OJ.UI;

namespace OJ.Dice
{
    /// <summary>
    /// 소환 버튼 바로 위의 확률 띠 — "1성 40% 2성 35% 3성 25%". 항상 보인다.
    /// 시안(Ingame_NewLayour.png): 바탕 띠 없이 30pt, 성급은 흰색 · 확률은 초록(78fe04).
    ///
    /// <b>왜 소환 버튼 곁인가.</b> 확률은 "뽑는 행동" 옆에 있어야 읽힌다. 확률업 버튼은 레벨과 비용 두 줄로
    /// 이미 차 있어 숫자를 더 싣지 못한다. 0% 인 성급은 적지 않는다(Lv1 은 "1성 100%" 한 칸).
    /// 레벨이 오르면 띠가 짧게 부푼다 — 방금 무엇이 바뀌었는지 눈이 가게 하는 것이다.
    ///
    /// 표는 <see cref="SummonStarRules"/> 하나라 화면과 실제 굴림이 어긋날 수 없다.
    /// </summary>
    public sealed class UISummonRateStrip : DialogBase
    {
        [Inject] private IBattleRefs battle;

        [SerializeField] private TMP_Text text;
        [SerializeField] private RectTransform pulseTarget;

        private const float PulseTime = 0.28f;
        private const float PulseScale = 1.14f;

        private int shownLevel = -1;
        private float pulseElapsed = PulseTime;
        private readonly StringBuilder builder = new StringBuilder(48);

        protected override void OnEnter()
        {
            shownLevel = -1;
            pulseElapsed = PulseTime;
            Refresh();
        }

        private void Update()
        {
            if (!_isEnter)
                return;

            Refresh();
            TickPulse();
        }

        private void Refresh()
        {
            UIDiceSummonSystem summon = battle != null ? battle.Summon : null;
            if (summon == null)
                return;

            int level = summon.ProbLevel;
            if (level == shownLevel)
                return;

            bool first = shownLevel < 0;
            shownLevel = level;

            builder.Clear();
            for (int star = 1; star <= SummonStarRules.MaxStar; star++)
            {
                int percent = SummonStarRules.Chance(level, star);
                if (percent <= 0)
                    continue;

                if (builder.Length > 0)
                    builder.Append(' ');

                builder.Append(star).Append("성 <color=#").Append(PercentColorHex).Append('>')
                    .Append(percent).Append("%</color>");
            }

            text.SetText(builder.ToString());

            // 처음 그릴 때는 부풀리지 않는다 — 올린 순간만 짚는다.
            if (!first)
                pulseElapsed = 0f;
        }

        private void TickPulse()
        {
            if (pulseTarget == null)
                return;

            if (pulseElapsed >= PulseTime)
            {
                pulseTarget.localScale = Vector3.one;
                return;
            }

            pulseElapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(pulseElapsed / PulseTime);
            float bump = Mathf.Sin(k * Mathf.PI);
            pulseTarget.localScale = Vector3.one * Mathf.Lerp(1f, PulseScale, bump);
        }

        // ── 에디터 굽기 ────────────────────────────────────────────────────────────

        /// <summary>확률 숫자 색(시안 실측).</summary>
        private const string PercentColorHex = "78FE04";

        // 보드 아래(화면 아래에서 약 205px)와 소환 버튼 위(약 150px) 사이 여백의 가운데.
        private static readonly Vector2 Size = new Vector2(640f, 46f);
        private const float BottomY = 178f;

        /// <summary>에디터 굽기 전용.</summary>
        public static UISummonRateStrip Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = UIBountyUIFactory.CreateRect("UISummonRateStrip", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = Size;
            rootRect.anchoredPosition = new Vector2(0f, BottomY);

            GameObject view = UIBountyUIFactory.CreateRect("DialogView", root.transform);
            UIBountyUIFactory.Stretch(view.GetComponent<RectTransform>());

            var dialog = root.AddComponent<UISummonRateStrip>();
            dialog.dialogView = view;
            dialog.UseBackBtn = false;
            dialog.BakeOpenStyle(DialogOpenStyle.None);
            dialog.pulseTarget = view.GetComponent<RectTransform>();

            // 바탕 띠는 없다(시안). 글자는 클릭을 먹지 않는다 — 아래 보드·버튼 조작이 살아 있어야 한다.
            dialog.text = UIBountyUIFactory.CreateText("Rates", view.transform, "1성 100%", 30f,
                TextAlignmentOptions.Center, Color.white, font);
            UIBountyUIFactory.Stretch(dialog.text.rectTransform);
            return dialog;
        }
    }
}
