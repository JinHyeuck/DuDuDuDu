using System;
using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>
    /// 한 판의 <b>이벤트 계획</b> — 이벤트 자리(<see cref="WaveTheme.IsEventSlot"/>)마다 어떤 이벤트가 나오는가.
    /// 판이 시작될 때 한 번 정해 상단 현황의 보상 아이콘과 실제 이벤트가 같은 것을 가리키게 한다(되돌려도 그대로다).
    ///
    /// <b>섞기.</b> 이벤트 목록(<see cref="Pool"/>)을 섞어 순서대로 나눠 준다. 목록이 자리보다 많으면 한 판 안에서 같은 이벤트가
    /// 겹치지 않고, 적으면 다 쓴 뒤 다시 섞는다. 난수는 시드로 받는다 — 같은 시드는 같은 계획이다(엔진을 쓰지 않는다).
    /// </summary>
    public sealed class WaveEventPlan
    {
        /// <summary>이벤트 자리에 나올 수 있는 이벤트들.</summary>
        public static readonly WaveInterlude[] Pool =
        {
            WaveInterlude.SoulBonus,
            WaveInterlude.LuckyDice,
            WaveInterlude.BossChallenge,
            WaveInterlude.WallHeal,
            WaveInterlude.DiceGiftBasic,
            WaveInterlude.DiceGiftRare,
            WaveInterlude.SpGamble,
            WaveInterlude.StoneGamble,
            WaveInterlude.FreeSummons,
            WaveInterlude.ProbUp,
            WaveInterlude.WallUpgrade,
            WaveInterlude.Exchange,
            WaveInterlude.AttackSpeedBuff,
            WaveInterlude.SlowBuff,
            WaveInterlude.WeakenBuff,
            WaveInterlude.RiskyDeal,
            WaveInterlude.Merchant,
        };

        private readonly Dictionary<int, WaveInterlude> events = new Dictionary<int, WaveInterlude>();

        private WaveEventPlan()
        {
        }

        /// <summary>이벤트 자리마다 하나씩 배정한 계획. <paramref name="totalWaves"/> 까지의 자리만 정한다.</summary>
        public static WaveEventPlan Build(int totalWaves, int seed)
        {
            var plan = new WaveEventPlan();
            var random = new Random(seed);
            var bag = new List<WaveInterlude>();

            for (int wave = 1; wave <= totalWaves; wave++)
            {
                if (!WaveTheme.IsEventSlot(wave))
                    continue;

                if (bag.Count == 0)
                    Refill(bag, random);

                plan.events[wave] = bag[bag.Count - 1];
                bag.RemoveAt(bag.Count - 1);
            }

            return plan;
        }

        private static void Refill(List<WaveInterlude> bag, Random random)
        {
            bag.AddRange(Pool);

            // Fisher–Yates.
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                WaveInterlude tmp = bag[i];
                bag[i] = bag[j];
                bag[j] = tmp;
            }
        }

        /// <summary>이 웨이브가 끝난 뒤의 보상. 이벤트 자리가 아니면 고정 보상(<see cref="WaveTheme.FixedAfter"/>).</summary>
        public WaveInterlude After(int wave)
        {
            if (WaveTheme.IsEventSlot(wave))
                return events.TryGetValue(wave, out WaveInterlude e) ? e : WaveTheme.After(wave);

            return WaveTheme.FixedAfter(wave);
        }

        /// <summary>한 번이라도 배정된 이벤트 수(테스트용).</summary>
        public int EventCount => events.Count;
    }
}
