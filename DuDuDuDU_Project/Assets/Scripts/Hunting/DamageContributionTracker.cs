using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace OJ.Hunting
{
    /// <summary>다이스 하나가 이 웨이브에서 넣은 피해와 그 비율.</summary>
    public struct DiceDamageShare
    {
        public DiceType DiceType;
        public long Damage;

        /// <summary>전체 대비 비율(0~1).</summary>
        public float Ratio;
    }

    /// <summary>
    /// 이 웨이브에서 어떤 다이스가 얼마나 때렸는가. <b>본편과 무한의 탑이 함께 쓴다.</b>
    ///
    /// <b>원래 탑 전용이었다.</b> 결과 화면의 기여도 막대(기획서 5.6)를 위해
    /// <c>TowerRunManager</c> 안에 있었는데, 본편 전투에서도 보여 주기로 하면서
    /// 여기로 꺼냈다. 탑에 남겨 두고 본편이 그것을 읽게 하면 <b>본편이 탑을 의존</b>하게
    /// 되고, 그건 방향이 거꾸로다 — 탑이 본편 위에 얹힌 콘텐츠지 그 반대가 아니다.
    ///
    /// <b>MonoBehaviour 가 아니다.</b> 만지는 것이 딕셔너리 하나라 씬에 놓을 이유가 없고,
    /// 놓으려면 <c>BattleScene.unity</c> 를 편집해야 한다(AGENTS 절대 규칙 3).
    /// <c>BountyManager</c>·<c>TowerRunManager</c> 와 같은 방식으로 배틀 스코프가 만든다.
    ///
    /// <b>웨이브 단위로 센다.</b> 웨이브가 시작될 때마다 0 으로 돌아간다 —
    /// 알고 싶은 것은 "지금 이 웨이브에서 누가 일하고 있나" 이고, 판 전체로 누적하면
    /// 뒤로 갈수록 앞 웨이브의 몫에 묻혀 <b>방금 바꾼 편성의 효과가 안 보인다.</b>
    ///
    /// <b>탑에서는 둘이 같은 말이다</b> — 한 층이 곧 한 웨이브라(기획서 3.2)
    /// 웨이브 리셋이 그대로 층 리셋이 되고, 결과 화면의 기여도도 그 한 웨이브의 것이다.
    ///
    /// 리셋은 <c>GameManager.ChangeState</c> 의 웨이브 진입 한 곳에서만 한다.
    /// </summary>
    [Preserve]
    public sealed class DamageContributionTracker
    {
        /// <summary>
        /// 누적이 바뀌었다. 실시간 패널이 듣는다.
        ///
        /// <b>매 타격마다 울린다.</b> 듣는 쪽이 그 빈도를 감당할 수 없으므로
        /// (초당 수십 번) 패널은 이 이벤트를 <b>"더러워졌다" 표시로만</b> 쓰고
        /// 실제 다시 그리기는 자기 주기로 한다. 여기서 주기를 정하면 표시 정책이
        /// 데이터 쪽으로 새어 든다.
        /// </summary>
        public event Action OnChanged;

        private readonly Dictionary<DiceType, long> damageByDice = new Dictionary<DiceType, long>();
        private long totalDamage;

        /// <summary>웨이브를 새로 연다. <c>GameManager.ChangeState</c> 가 부른다.</summary>
        public void BeginWave()
        {
            damageByDice.Clear();
            totalDamage = 0;
            OnChanged?.Invoke();
        }

        /// <summary>
        /// 피해를 기록한다. <c>AttackContent.HitMonster</c> 한 곳에서만 불린다.
        ///
        /// <b>그 한 곳이 전부인 것이 중요하다.</b> 다이스 효과 아홉 개가 전부 그 함수를
        /// 지나므로(범위·연쇄·다단 포함) 여기만 잡으면 기여도가 새지 않는다.
        ///
        /// <b>중독 도트는 세지 않는다.</b> <c>Monster.PlayPoison</c> 이
        /// <c>TakeDamage</c> 를 직접 부르는데, 중독을 <i>건</i> 다이스가 무엇인지 개체가
        /// 기억하지 않는다. 세려면 몬스터마다 출처를 들고 다녀야 하고, 기여도는
        /// "어떤 선택이 정답이었나" 를 보여주는 지표이지 회계가 아니다.
        ///
        /// <b>0 이하도 그냥 받는다.</b> 보호막에 막힌 타격이 0 을 돌려주는데, 그것을
        /// 셀지 말지는 <b>여기서</b> 정한다 — 호출부가 거르면 판단이 두 곳으로 갈린다.
        /// </summary>
        public void Record(DiceType diceType, int appliedDamage)
        {
            if (appliedDamage <= 0)
                return;

            damageByDice.TryGetValue(diceType, out long current);
            damageByDice[diceType] = current + appliedDamage;
            totalDamage += appliedDamage;

            OnChanged?.Invoke();
        }

        /// <summary>
        /// 기여도를 큰 순서로 채운다. <b>목록을 넘겨받아 채우는 것이 요점이다</b> —
        /// 실시간 패널이 초당 여러 번 부르므로, 매번 새 <c>List</c> 를 만들면
        /// 그만큼 GC 가 돈다.
        /// </summary>
        public void FillShares(List<DiceDamageShare> buffer)
        {
            if (buffer == null)
                return;

            buffer.Clear();
            if (totalDamage <= 0)
                return;

            foreach (KeyValuePair<DiceType, long> pair in damageByDice)
            {
                buffer.Add(new DiceDamageShare
                {
                    DiceType = pair.Key,
                    Damage = pair.Value,
                    Ratio = (float)pair.Value / totalDamage,
                });
            }

            buffer.Sort(CompareByDamageDescending);
        }

        /// <summary>새 목록으로 받는다. 웨이브가 끝날 때 한 번 부르는 결과 화면용이다.</summary>
        public List<DiceDamageShare> BuildShares()
        {
            var list = new List<DiceDamageShare>(damageByDice.Count);
            FillShares(list);
            return list;
        }

        /// <summary>
        /// 큰 것부터. <b>같은 값일 때 순서를 고정한다</b> — 안 그러면 실시간 패널에서
        /// 비율이 같은 두 다이스가 매 갱신마다 자리를 바꿔 깜빡이는 것처럼 보인다.
        /// </summary>
        private static int CompareByDamageDescending(DiceDamageShare left, DiceDamageShare right)
        {
            int byDamage = right.Damage.CompareTo(left.Damage);
            if (byDamage != 0)
                return byDamage;

            return ((int)left.DiceType).CompareTo((int)right.DiceType);
        }
    }
}
