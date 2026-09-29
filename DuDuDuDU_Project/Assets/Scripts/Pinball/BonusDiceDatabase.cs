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
                    rewards = new List<PinballReward> { new PinballReward(PointType.Gold, 300) },
                },
                new BonusPinReward
                {
                    // 강화석 → 소환권 → 레어석으로 두 번 바뀌었다.
                    //
                    // 소환권(NormalScroll)은 티어 3 이라 여기 있을 물건이 아니었다. 그것은
                    // 소탕이 하루 1,152장을 뿌리는 재화라(정책 1.5) 보너스 라운드에서 5장을
                    // 받아 봐야 아무 일도 일어나지 않는다.
                    //
                    // 레어석인 이유: 재화 표가 레어석의 <b>대표 수급처를 Pinball_Bonus</b> 로
                    // 잡고 있는데(정책 2장) 이 표에 레어석이 아예 없었다. 게다가 소탕이
                    // 하루 720개를 뿌리고 있어서 말과 구현이 정반대였다 — 소탕 쪽을 빼고
                    // 이리로 옮겼다(StageRewardCalculator.AddAutoBattleClearRewards 주석).
                    tag = 2, label = "레어석 핀", requiredHits = 6,
                    rewards = new List<PinballReward> { new PinballReward(PointType.RareStone, 25) },
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
                    // 골드 10000 이었다. 골드는 소탕이 하루 14,400 을 주는 재화라(정책 1.5)
                    // 상위 재화 자리를 하나 잡아먹고 있었다. 신화석은 재화 표상 대표 수급처가
                    // 여기인데(정책 2장) 이 표에 없었다 — 레어석과 같은 사정이다.
                    //
                    // 킹 다이스 강화 재료이고 lv20 부근 한 레벨이 75 이므로, 하루 두 라운드면
                    // 킹 1종 +1렙 안팎이 된다. 킹은 탑이 여는 최상위 축이라 이 속도가 맞다.
                    tag = 5, label = "신화석 핀", requiredHits = 7,
                    rewards = new List<PinballReward> { new PinballReward(PointType.MythicStone, 20) },
                },
                new BonusPinReward
                {
                    // 300 이었다. 젬 핀(tag 4)과 합쳐 라운드당 150 이 되도록 낮췄다 —
                    // 하루 두 라운드면 300 이고, 그것이 보석뽑기 1회 값이다(상점 8.2).
                    tag = 6, label = "젬 대박 핀", requiredHits = 8,
                    rewards = new List<PinballReward> { new PinballReward(PointType.FreeGem, 50) },
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
