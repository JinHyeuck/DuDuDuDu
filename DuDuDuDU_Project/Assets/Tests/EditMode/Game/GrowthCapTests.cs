using NUnit.Framework;
using OJ;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 성장 상한을 잠근다.
    ///
    /// <b>상한이 없던 시절이 있었다.</b> <c>TryLevelUp</c> 이 <c>SetLevel(current + 1)</c> 만 하고
    /// 검사를 안 해서, 재화만 있으면 무한히 올라갔다. 그러면 "무엇을 목표로 키우는가"가
    /// 사라지고 lv300 에 2년 같은 숫자가 나온다.
    ///
    /// 두 값은 <b>다른 표에서 따라온다</b> — 다이스는 마일스톤의 끝, 장비는 보석 슬롯이
    /// 마지막으로 열리는 레벨이다. 그 표를 고치면 여기가 반응해야 한다.
    /// </summary>
    public sealed class GrowthCapTests
    {
        /// <summary>
        /// 다이스는 12 가 끝이다. <c>DiceMetaDataDatabase</c> 의 마일스톤이 3·6·9·12 이고
        /// 그 위로는 새로 열리는 것이 없다.
        /// </summary>
        [Test]
        public void 다이스_상한은_마일스톤의_끝이다()
        {
            Assert.AreEqual(12, Define.MaxDiceLevel);
        }

        /// <summary>
        /// 장비는 보석 슬롯이 마지막으로 열리는 레벨이 끝이다.
        /// 상수로 40 을 따로 적지 않고 표에서 끌어오므로, 표를 늘리면 상한도 따라 늘어난다.
        /// </summary>
        [Test]
        public void 장비_상한은_보석_슬롯의_마지막이다()
        {
            Assert.AreEqual(40, Define.MaxEquipmentLevel);

            int lastUnlock = 0;
            foreach (int level in Define.EquipmentSlotUnlockLevels)
            {
                if (level > lastUnlock)
                    lastUnlock = level;
            }

            Assert.AreEqual(lastUnlock, Define.MaxEquipmentLevel,
                "상한이 슬롯 해금표와 어긋났다 — 둘 중 하나만 고쳤다는 뜻이다.");
        }

        /// <summary>
        /// 슬롯 해금 레벨이 상한을 넘으면 <b>영원히 못 여는 슬롯</b>이 생긴다.
        /// 표를 늘릴 때 상한을 같이 안 올리면 조용히 그렇게 된다.
        /// </summary>
        [Test]
        public void 모든_보석_슬롯이_상한_안에서_열린다()
        {
            foreach (int level in Define.EquipmentSlotUnlockLevels)
            {
                Assert.LessOrEqual(level, Define.MaxEquipmentLevel,
                    $"해금 레벨 {level} 이 상한 {Define.MaxEquipmentLevel} 을 넘는다.");
            }
        }

        [Test]
        public void 슬롯_수와_해금표_길이가_같다()
        {
            Assert.AreEqual(Define.MaxEquipmentSlot, Define.EquipmentSlotUnlockLevels.Length);
        }
    }
}
