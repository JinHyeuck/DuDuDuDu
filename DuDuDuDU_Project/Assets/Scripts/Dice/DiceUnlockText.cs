using System.Collections.Generic;
using System.Text;

namespace OJ.Dice
{
    /// <summary>
    /// 언락 조건을 사람 말로 옮긴다.
    ///
    /// <b>한곳에 모으는 이유.</b> 같은 문구를 세 화면이 쓴다 — 탑 편성의 잠긴 칸 토스트,
    /// 로비의 언락 팝업, 층 선택 화면의 해금 진행도. 각자 적으면 "36별" 과 "별 36개" 처럼
    /// 조금씩 다른 말이 생기고, 유저는 그것을 다른 조건으로 읽는다.
    ///
    /// <c>DiceTraitText</c>·<c>TowerDiceText</c> 와 같은 자리의 정적 텍스트 도우미다.
    /// </summary>
    public static class DiceUnlockText
    {
        /// <summary>
        /// 이 다이스를 얻는 컨텐츠 경로들. 없는 경로는 빼고 담는다.
        /// 재화 구매는 <b>담지 않는다</b> — 그건 조건이 아니라 값이라, 가격 줄에서 따로 그린다.
        /// </summary>
        public static void AppendSources(DiceUnlockDefinition definition, List<string> into)
        {
            if (definition == null || into == null)
                return;

            if (definition.starRequirement > 0)
                into.Add("별 " + definition.starRequirement + "개 모으기");

            if (definition.stageRequirement > 0)
                into.Add("스테이지 " + definition.stageRequirement + " 퍼펙트 클리어");

            if (definition.towerFloor > 0)
                into.Add("무한의 탑 " + definition.towerFloor + "층 클리어");
        }

        /// <summary>경로들을 한 줄로 잇는다. 없으면 빈 문자열.</summary>
        public static string DescribeSources(DiceUnlockDefinition definition)
        {
            var sources = new List<string>();
            AppendSources(definition, sources);

            if (sources.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < sources.Count; i++)
            {
                if (i > 0)
                    sb.Append(" · ");

                sb.Append(sources[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 잠긴 다이스를 눌렀을 때의 안내 한 줄.
        ///
        /// <b>이름을 인자로 받는다.</b> 다이스를 어떻게 적을지는 이미 정해져 있고
        /// (<c>TowerDiceText.NameOf</c> = 에셋의 <c>displayName</c>, 없으면 enum 이름),
        /// 그 판정을 여기서 또 하면 표기가 한 벌 더 생긴다. 이 클래스는 <b>조건</b>만 맡는다.
        ///
        /// <b>보상처가 없으면 그렇다고 말한다.</b> 침묵하면 그 칸은 화면의 얼룩이 되고,
        /// "수집 목표를 노출한다" 는 편성 화면의 목적(기획서 8.3)이 반만 이뤄진다.
        /// </summary>
        public static string BuildLockedNotice(string diceName, DiceUnlockDefinition definition)
        {
            string sources = DescribeSources(definition);

            if (!string.IsNullOrEmpty(sources))
                return diceName + " 은(는) " + sources + " 로 열려요";

            if (definition != null && definition.price > 0)
                return diceName + " 은(는) 로비 → 다이스 성장에서 열 수 있어요";

            return diceName + " 은(는) 아직 얻을 수 없어요";
        }
    }
}
