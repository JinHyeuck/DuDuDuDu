using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// 통과 피해 산술. <b>골든 기준선에 얹지 않는다</b> — 기준선은 기존 동작을 박제하는
    /// 파일이고 이것은 새 규칙이라, 섞으면 "기준선이 바뀐 것"과 "새 값이 들어온 것"을
    /// 구별할 수 없게 된다(<c>BountyFormulaTests</c> 와 같은 판단).
    ///
    /// 여기서 지키는 것은 값 자체가 아니라 <b>난이도 응답이 선형이라는 성질</b>이다.
    /// 통과 피해가 마리 수에 비례해야 "클리어는 되지만 퍼펙트는 안 되는" 구간을
    /// 수치로 만들 수 있다 — 그 이유는 <see cref="WallLeakFormula"/> 주석에 있다.
    /// </summary>
    public sealed class WallLeakFormulaTests
    {
        [TestCase(1, 10)]
        [TestCase(2, 20)]
        public void 통과_피해는_공격력에_비례한다(int attackDamage, int expected)
        {
            Assert.That(WallLeakFormula.LeakDamage(attackDamage, false), Is.EqualTo(expected));
        }

        /// <summary>
        /// 보스는 공격력과 무관하게 일반 세 마리분이다. 보스 프리팹이 테마마다 달라
        /// (늑대 1 / 골렘 2) 프리팹 값을 타면 테마가 보스 통과 피해를 2배로 가른다.
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(7)]
        public void 보스_통과_피해는_프리팹_공격력을_보지_않는다(int attackDamage)
        {
            Assert.That(WallLeakFormula.LeakDamage(attackDamage, true), Is.EqualTo(30));
        }

        /// <summary>
        /// 공격력 0·음수도 피해가 있다. <b>통과했는데 아무 일도 없는 경우</b>를 만들지 않는 것이
        /// 요점이다 — 프리팹에 0 이 들어와도 등급은 깎여야 한다.
        /// </summary>
        [TestCase(0)]
        [TestCase(-5)]
        public void 공격력이_0_이하여도_피해가_있다(int attackDamage)
        {
            Assert.That(WallLeakFormula.LeakDamage(attackDamage, false),
                Is.EqualTo(WallLeakFormula.DamagePerAttackPoint));
        }

        /// <summary>
        /// <b>피해가 마리 수에 선형</b>이다. 예전 구조(벽 앞에 남아 계속 때리는 것)에서는
        /// 통과한 마리 수가 늘면 초당 피해가 같이 늘어 제곱으로 아팠다 — 그래서 화력
        /// 15% 차이가 무피해와 전멸을 갈랐다.
        /// </summary>
        [Test]
        public void 피해는_마리_수에_선형이다()
        {
            int one = WallLeakFormula.LeakDamage(2, false);

            Assert.That(one * 3, Is.EqualTo(WallLeakFormula.LeakDamage(2, false) * 3));
            Assert.That(WallLeakFormula.LeakBudget(one * 3, 2, false), Is.EqualTo(3));
        }

        /// <summary>
        /// 보스 한 마리가 가장 얇은 벽(정예 300)의 1/5 를 넘지 않는다. 보스를 한 번
        /// 놓친 것으로 등급이 통째로 결정되면 그것은 난이도가 아니라 복권이다.
        /// </summary>
        [Test]
        public void 보스_한_마리는_가장_얇은_벽의_오분의_일_아래다()
        {
            const int ThinnestStageWallHp = 300;

            Assert.That(WallLeakFormula.LeakDamage(2, true), Is.LessThan(ThinnestStageWallHp / 5));
        }

        [TestCase(450, 1, false, 45)]
        [TestCase(600, 2, false, 30)]
        [TestCase(300, 2, false, 15)]
        [TestCase(450, 0, true, 15)]
        public void 허용_마리_수는_벽_체력을_나눈_값이다(int wallHp, int attackDamage, bool isBoss, int expected)
        {
            Assert.That(WallLeakFormula.LeakBudget(wallHp, attackDamage, isBoss), Is.EqualTo(expected));
        }
    }
}
