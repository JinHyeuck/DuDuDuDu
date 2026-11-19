using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OJ.Pinball
{
    /// <summary>
    /// 보상판 특수 핀 하나의 규칙. <c>PinballBoard</c> 의 <c>Peg.specialTag</c> 와 태그로 짝짓는다.
    ///
    /// <b><see cref="PinballSpecialReward"/> 와 생김새가 닮았지만 뜻이 다르다.</b> 저쪽은 판을 넘어
    /// 계속 도는 게이지라 임계치를 넘으면 되감겨 <i>반복</i> 지급되고, 이쪽은 <b>한 라운드에 한 번</b>
    /// 채워지고 끝난다.
    /// </summary>
    [Serializable]
    public sealed class BonusPinReward
    {
        [Tooltip("PinballBoard 의 Peg.specialTag 와 같은 값.")]
        public int tag;

        [Tooltip("화면에 쓸 이름. 비워도 동작한다.")]
        public string label;

        [Tooltip("이 핀을 몇 번 맞혀야 보상이 나오는가.")]
        [Min(1)] public int requiredHits = 10;

        public List<PinballReward> rewards = new List<PinballReward>();
    }

    /// <summary>
    /// 보상 풀의 한 줄. 라운드를 열 때 핀마다 이 풀에서 하나씩 <b>가중치대로</b> 뽑아 걸고,
    /// 핀이 채워지면 그 줄의 <see cref="rewards"/> 를 <b>전부</b> 준다.
    ///
    /// <b>가중치는 "하루 목표 개수 ÷ 하루 추첨 수" 에서 왔다</b> (가치 장부 2026-11). 보너스가 하루 4번 열리고
    /// 핀이 6개라 하루 24번 뽑으므로, 하루 4장이 목표인 재화는 4/24 = 16.7% 다.
    /// </summary>
    [Serializable]
    public sealed class BonusRewardDraw
    {
        [Tooltip("화면·로그에 쓸 이름. 비워도 동작한다.")]
        public string label;

        [Tooltip("상대 가중치. 합이 1000 이 되게 적으면 퍼밀(‰)로 읽힌다. 0 이하는 뽑히지 않는다.")]
        [Min(0)] public int weight = 100;

        public List<PinballReward> rewards = new List<PinballReward>();
    }

    /// <summary>
    /// 핀볼 보상 라운드(주사위 족보 → 공 발사)의 수치 정본.
    ///
    /// <b>이 표는 "쉬움"을 만드는 손잡이다.</b> 보상 라운드는 센터핀을 많이 맞힌 것에 대한
    /// 칭찬이고, 조금만 참여해도 좋은 것을 거의 다 가져가는 것이 설계 목표다. 그래서
    /// <see cref="pinRewards"/> 의 <c>requiredHits</c> 합계는 <b>라운드 전체에서 나가는 공의
    /// 기댓값 안에 넉넉히 들어가야 한다</b> — 그러지 않으면 유저가 빈손으로 나가고,
    /// 그 순간 이 컨텐츠는 존재 이유를 잃는다.
    /// </summary>
    [CreateAssetMenu(fileName = "BonusDiceDatabase", menuName = "OJ/Bonus Dice Database")]
    public sealed class BonusDiceDatabase : ScriptableObject
    {
        [Header("여는 조건")]
        [Tooltip("핀볼 판의 이 태그(센터핀) 게이지가 다 차면 보상 라운드가 열린다.")]
        public int pinballTriggerTag = 3;

        [Header("주사위")]
        [Tooltip("한 번에 굴리는 주사위 수.")]
        [Min(1)] public int diceCount = 5;

        [Tooltip("사이클당 다시 굴릴 수 있는 횟수. 사이클 시작의 자동 굴림은 여기 포함되지 않는다.")]
        [Min(0)] public int rerollsPerCycle = 5;

        [Tooltip("한 라운드에 굴리고 쏘는 횟수.")]
        [Min(1)] public int cyclesPerRound = 5;

        [Header("족보 → 공 개수")]
        [Tooltip("인덱스가 DiceHand 값이다. 0 인 자리는 BonusDiceRules.DefaultBallTable 로 내려간다.")]
        public int[] ballTable = (int[])BonusDiceRules.DefaultBallTable.Clone();

        [Header("특수 핀")]
        [Tooltip("핀의 구조(태그·필요 적중 수). 보상 풀이 있으면 핀 위의 보상은 풀에서 뽑고, 여기 적힌 rewards 는 풀이 비었을 때의 폴백이다.")]
        public List<BonusPinReward> pinRewards = new List<BonusPinReward>();

        [Header("보상 풀 — 라운드를 열 때 핀마다 하나씩 가중치대로 뽑는다")]
        public List<BonusRewardDraw> rewardPool = new List<BonusRewardDraw>();

        /// <summary>풀의 가중치만 뽑아 낸 목록. 추첨 규칙(<see cref="BonusDiceRules.PickWeightedIndex"/>)에 넘긴다.</summary>
        public List<int> PoolWeights()
        {
            var weights = new List<int>(rewardPool != null ? rewardPool.Count : 0);
            if (rewardPool != null)
            {
                for (int i = 0; i < rewardPool.Count; i++)
                    weights.Add(rewardPool[i] != null ? rewardPool[i].weight : 0);
            }
            return weights;
        }

        /// <summary>풀의 한 줄. 범위를 벗어나면 null.</summary>
        public BonusRewardDraw GetDraw(int index)
        {
            if (rewardPool == null || index < 0 || index >= rewardPool.Count)
                return null;

            return rewardPool[index];
        }

        /// <summary>그 태그의 규칙. 없으면 null — 보상이 안 걸린 핀이라는 뜻이다.</summary>
        public BonusPinReward GetPin(int tag)
        {
            if (pinRewards == null)
                return null;

            for (int i = 0; i < pinRewards.Count; i++)
            {
                if (pinRewards[i] != null && pinRewards[i].tag == tag)
                    return pinRewards[i];
            }
            return null;
        }

        /// <summary>
        /// 에셋이 비었을 때 채우는 기본값. <b>수치의 정본은 에셋이고 이것은 바닥이다</b> —
        /// 에디터 도구가 처음 만들 때와, 에셋을 못 찾았을 때만 쓰인다.
        /// </summary>
        public void PopulateDefaults()
        {
            diceCount = 5;
            rerollsPerCycle = 5;
            cyclesPerRound = 5;
            ballTable = (int[])BonusDiceRules.DefaultBallTable.Clone();

            // 판(BonusBoard)의 특수 핀 태그 1~6 에 맞춰 둔다. 핀은 <b>구조</b>(태그·필요 적중 수)만 갖고,
            // 핀 위의 보상은 라운드를 열 때 아래 <see cref="rewardPool"/> 에서 가중치대로 뽑는다(가치 장부 2026-11).
            //
            // <b>requiredHits 를 낮게 잡은 것이 핵심이다.</b> 최악의 경우는
            // 노페어만 5번 나오는 것이라 공이 15발이고, 판의 targetHitProbability 가
            // 0.5 라 핀당 기댓값이 7.5회다. 임계치가 그보다 높으면 운이 나쁜 유저는
            // 광고를 봐야만 끝낼 수 있게 되는데, 이 컨텐츠는 그러라고 만든 것이 아니다.
            //
            // 여기 적힌 rewards(골드 20)는 <b>풀이 비었을 때의 폴백</b>이다 — 표가 깨져도 핀이 빈손이 되지 않게 한다.
            int[] requiredHits = { 6, 6, 6, 7, 7, 8 };
            pinRewards = new List<BonusPinReward>();
            for (int i = 0; i < requiredHits.Length; i++)
            {
                pinRewards.Add(new BonusPinReward
                {
                    tag = i + 1, label = "보상 핀 " + (i + 1), requiredHits = requiredHits[i],
                    rewards = new List<PinballReward> { new PinballReward(PointType.Gold, 20) },
                });
            }

            // 보상 풀. 가중치는 퍼밀(합 1000). <b>하루 4번 열리고(500발당 1회 × 하루 2,000발) 핀이 6개</b>라 하루 24번 뽑는다 —
            // 하루 목표 개수 ÷ 24 가 확률이다.
            //
            //   랜덤 레어·킹 카피 1장   16.7%  (하루 4장)       랜덤 기본 카피 1장      16.7%  (하루 4장)
            //   일반 보석 뽑기권 1장     8.3%  (하루 2장)       선택권·유물권·최고급 뽑기권 각 4.2%  (하루 1장씩)
            //   시너지 티켓 5장         15.0%                    이벤트 재화 5개          15.7%  (열린 이벤트의 재화로 풀린다)
            //   골드 20                 15.0%
            rewardPool = BuildDefaultPool();
        }

        /// <summary>
        /// 기본 보상 풀. <b>목록을 돌려주는 static 이다</b> — 헤드리스 러너에서는 <c>ScriptableObject</c> 를 못 만들어
        /// 인스턴스 메서드(<see cref="PopulateDefaults"/>)로만 두면 이 표의 검산(가중치 합, 하루 기대량)에 테스트가 닿지 못한다.
        /// </summary>
        public static List<BonusRewardDraw> BuildDefaultPool()
        {
            return new List<BonusRewardDraw>
            {
                Draw("레어·킹 카피", 167, PointType.RandomRareKingCopy, 1),
                Draw("기본 카피", 167, PointType.RandomBasicCopy, 1),
                Draw("일반 보석 뽑기권", 83, PointType.NormalGemTicket, 1),
                Draw("선택권", 42, PointType.SelectTicket, 1),
                Draw("유물권", 42, PointType.RelicTicket, 1),
                Draw("최고급 보석 뽑기권", 42, PointType.PremiumGemTicket, 1),
                Draw("시너지 티켓", 150, PointType.SynergyTicket, 5),
                Draw("이벤트 재화", 157, PointType.OpenEventCurrency, 5),
                Draw("골드", 150, PointType.Gold, 20),
            };
        }

        private static BonusRewardDraw Draw(string label, int weight, PointType type, int amount)
        {
            return new BonusRewardDraw
            {
                label = label,
                weight = weight,
                rewards = new List<PinballReward> { new PinballReward(type, amount) },
            };
        }

        /// <summary>
        /// 표가 앞뒤가 맞는지. 문제가 없으면 빈 문자열.
        /// 에디터 메뉴와 EditMode 테스트가 같이 쓴다.
        /// </summary>
        public string Validate()
        {
            var sb = new StringBuilder();

            if (diceCount < 1)
                sb.AppendLine("주사위 수가 1 미만이다.");
            if (cyclesPerRound < 1)
                sb.AppendLine("사이클 수가 1 미만이다.");

            if (pinballTriggerTag <= 0)
            {
                sb.AppendLine(
                    "pinballTriggerTag 가 0 이하다. 핀볼에서 이 라운드를 여는 길이 없다.");
            }

            if (pinRewards == null || pinRewards.Count == 0)
            {
                sb.AppendLine("특수 핀이 하나도 없다. 이 라운드는 보상이 0 이다.");
                return sb.ToString();
            }

            // 보상 풀이 있으면 그것이 핀 위의 보상을 정한다. 풀은 뽑을 수 있어야 하고(가중치 합 > 0),
            // 줄마다 줄 것이 있어야 한다.
            bool hasPool = rewardPool != null && rewardPool.Count > 0;
            if (hasPool)
            {
                long totalWeight = 0;
                for (int i = 0; i < rewardPool.Count; i++)
                {
                    BonusRewardDraw draw = rewardPool[i];
                    if (draw == null)
                    {
                        sb.AppendLine("보상 풀 " + i + "번 줄이 비어 있다.");
                        continue;
                    }

                    if (draw.weight > 0)
                        totalWeight += draw.weight;

                    if (draw.rewards == null || draw.rewards.Count == 0)
                        sb.AppendLine("보상 풀 " + i + "번 줄에 보상이 없다. 뽑혀도 아무 일도 안 일어난다.");
                }

                if (totalWeight <= 0)
                    sb.AppendLine("보상 풀의 가중치 합이 0 이다. 아무것도 뽑을 수 없다.");
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < pinRewards.Count; i++)
            {
                BonusPinReward pin = pinRewards[i];
                if (pin == null)
                {
                    sb.AppendLine(i + "번 핀이 비어 있다.");
                    continue;
                }

                if (!seen.Add(pin.tag))
                    sb.AppendLine("태그 " + pin.tag + " 가 두 번 나온다. 뒤엣것은 절대 안 쓰인다.");

                if (pin.requiredHits < 1)
                    sb.AppendLine("태그 " + pin.tag + " 의 requiredHits 가 1 미만이라 영원히 안 채워진다.");

                if ((pin.rewards == null || pin.rewards.Count == 0) && !hasPool)
                    sb.AppendLine("태그 " + pin.tag + " 에 보상이 없다. 채워도 아무 일도 안 일어난다.");
            }

            return sb.ToString();
        }
    }
}
