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
        public List<BonusPinReward> pinRewards = new List<BonusPinReward>();

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

            // 판(BonusBoard)의 특수 핀 태그 1~6 에 맞춰 둔다.
            //
            // <b>requiredHits 를 낮게 잡은 것이 핵심이다.</b> 최악의 경우는
            // 노페어만 5번 나오는 것이라 공이 15발이고, 판의 targetHitProbability 가
            // 0.5 라 핀당 기댓값이 7.5회다. 임계치가 그보다 높으면 운이 나쁜 유저는
            // 광고를 봐야만 끝낼 수 있게 되는데, 이 컨텐츠는 그러라고 만든 것이 아니다.
            pinRewards = new List<BonusPinReward>
            {
                new BonusPinReward
                {
                    tag = 1, label = "골드 핀", requiredHits = 6,
                    rewards = new List<PinballReward> { new PinballReward(PointType.Gold, 3000) },
                },
                new BonusPinReward
                {
                    // 강화석이었다. 전투 재화라 로비에서 받아도 판이 시작되면 사라진다
                    // (PinballRewardDatabase.PopulateDefaults 의 폭탄핀 주석 참고).
                    // 동료 핀(골드 3000 / 유물권 2 / 무료젬 100)과 같은 중간 티어로 맞춘다.
                    tag = 2, label = "소환권 핀", requiredHits = 6,
                    rewards = new List<PinballReward> { new PinballReward(PointType.NormalScroll, 5) },
                },
                new BonusPinReward
                {
                    tag = 3, label = "유물권 핀", requiredHits = 6,
                    rewards = new List<PinballReward> { new PinballReward(PointType.RelicTicket, 2) },
                },
                new BonusPinReward
                {
                    tag = 4, label = "젬 핀", requiredHits = 7,
                    rewards = new List<PinballReward> { new PinballReward(PointType.FreeGem, 100) },
                },
                new BonusPinReward
                {
                    tag = 5, label = "골드 대박 핀", requiredHits = 7,
                    rewards = new List<PinballReward> { new PinballReward(PointType.Gold, 10000) },
                },
                new BonusPinReward
                {
                    tag = 6, label = "젬 대박 핀", requiredHits = 8,
                    rewards = new List<PinballReward> { new PinballReward(PointType.FreeGem, 300) },
                },
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

                if (pin.rewards == null || pin.rewards.Count == 0)
                    sb.AppendLine("태그 " + pin.tag + " 에 보상이 없다. 채워도 아무 일도 안 일어난다.");
            }

            return sb.ToString();
        }
    }
}
