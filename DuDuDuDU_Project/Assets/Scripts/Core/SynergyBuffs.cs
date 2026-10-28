using System.Collections.Generic;

namespace OJ.Core
{
    /// <summary>
    /// 지금 걸려 있는 시너지 버프의 합. 엔진을 쓰지 않는다.
    ///
    /// <see cref="WaveBuffs"/> 와 같은 <b>정적 상태</b>라, 판이 시작될 때·되감을 때·씬이 사라질 때
    /// 반드시 <see cref="Reset"/> 한다(<c>OJ.Synergy.SynergyRunManager</c> 와 <c>GameManager</c> 가 지킨다).
    /// 아무것도 안 걸렸으면 모든 배수가 1 이라 골든 기준선이 그대로다.
    /// </summary>
    public static class SynergyBuffs
    {
        /// <summary>모든 다이스 공격력 +%.</summary>
        public static int AttackPercent { get; private set; }

        /// <summary>보스가 받는 피해 +%.</summary>
        public static int BossPercent { get; private set; }

        /// <summary>벽이 받는 피해 -%.</summary>
        public static int WallGuardPercent { get; private set; }

        private static readonly Dictionary<int, int> elementPercent = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> dicePercent = new Dictionary<int, int>();

        public static void Reset()
        {
            AttackPercent = 0;
            BossPercent = 0;
            WallGuardPercent = 0;
            elementPercent.Clear();
            dicePercent.Clear();
        }

        public static void AddAttack(int percent) => AttackPercent += percent;
        public static void AddBoss(int percent) => BossPercent += percent;
        public static void AddWallGuard(int percent) => WallGuardPercent += percent;

        public static void AddElement(int element, int percent)
        {
            elementPercent.TryGetValue(element, out int current);
            elementPercent[element] = current + percent;
        }

        public static void AddDice(int dice, int percent)
        {
            dicePercent.TryGetValue(dice, out int current);
            dicePercent[dice] = current + percent;
        }

        /// <summary>
        /// 다이스 한 발의 배수. 공격력·속성·다이스 버프를 <b>더한 뒤 한 번 곱한다</b> — 유물 배수와 같은 방식이다.
        /// <paramref name="elements"/> 는 그 다이스의 속성들(정수).
        /// </summary>
        public static float DamageMultiplier(int dice, IReadOnlyList<int> elements)
        {
            int percent = AttackPercent;
            if (dicePercent.TryGetValue(dice, out int byDice))
                percent += byDice;

            if (elements != null)
            {
                for (int i = 0; i < elements.Count; i++)
                {
                    if (elementPercent.TryGetValue(elements[i], out int byElement))
                    {
                        percent += byElement;
                        break;
                    }
                }
            }

            return percent == 0 ? 1f : 1f + percent / 100f;
        }

        /// <summary>보스에게 들어가는 피해. 보스가 아니거나 버프가 없으면 그대로.</summary>
        public static int ApplyBoss(int damage, bool isBoss)
        {
            if (!isBoss || BossPercent <= 0 || damage <= 0)
                return damage;

            return (int)System.Math.Round(damage * (1.0 + BossPercent / 100.0));
        }

        /// <summary>벽에 들어가는 피해. 상한 90% 감소, 1 미만으로는 안 내린다(피격 자체는 남긴다).</summary>
        public static int ApplyWallGuard(int damage)
        {
            if (WallGuardPercent <= 0 || damage <= 0)
                return damage;

            int guard = System.Math.Min(90, WallGuardPercent);
            return System.Math.Max(1, (int)System.Math.Ceiling(damage * (100 - guard) / 100.0));
        }
    }
}
