using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OJ;
using OJ.Dice;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 이 어셈블리가 존재하는 이유를 지키는 테스트. (다이스 언락 0단계)
    ///
    /// <b>왜 두 번째 테스트 어셈블리인가.</b> <c>OJ.Core.Tests</c> 는 <c>OJ.Core</c> 만 참조하고
    /// <c>overrideReferences: true</c> 라 <c>DiceType</c>·<c>PointType</c> 을 <b>못 본다</b> —
    /// 그 둘은 <c>Assets/Scripts/Define.cs</c> = <c>OJ.Game</c> 소속이다. 거기에 <c>OJ.Game</c> 을
    /// 더하면 <c>noEngineReferences: true</c> 로 컴파일러가 강제하는 Core 순수성 경계가 흐려진다.
    ///
    /// <b>여기 담긴 세 가지는 전부 "되는지 몰랐던 것"이다.</b> 헤드리스 러너는 Unity 를 열지 않고
    /// Mono 에서 도는데, 그 안에서 엔진 타입이 실제로 살아 움직이는지는 눌러 보기 전에는 알 수 없다.
    /// 하나라도 깨지면 다이스 언락의 나머지 설계가 전부 "에디터를 열어야만 아는" 것이 된다.
    /// </summary>
    public sealed class AssemblyPilotTests
    {
        /// <summary>
        /// 1. <c>OJ.Game</c> 의 열거형과 순수 규칙표가 보인다.
        ///
        /// 티어 판정은 <c>DiceType</c> 의 숫자 구간(0/100/200)만 본다 — 에셋도 엔진도 타지 않는다.
        /// 이게 깨지면 참조 자체가 안 잡힌 것이다.
        /// </summary>
        [Test]
        public void GameEnumsAndPureRulesAreVisible()
        {
            Assert.That(DiceEvolution.GetTier(DiceType.Normal), Is.EqualTo(DiceTier.Base));
            Assert.That(DiceEvolution.GetTier(DiceType.Tornado), Is.EqualTo(DiceTier.Special));
            Assert.That(DiceEvolution.GetTier(DiceType.KingFire), Is.EqualTo(DiceTier.King));
        }

        /// <summary>
        /// 2. 언락 재화 매핑이 이미 코드에 있고 여기서 두드려진다.
        ///
        /// 다이스 언락은 <b>SO 에 재화 종류를 적지 않는다</b> — 적으면 "강화용 재화"와
        /// "언락용 재화"가 갈라진다. 이 매핑이 유일한 정본이라는 것을 여기서 못 박는다.
        /// </summary>
        [Test]
        public void ToScrollTypeMapsSpecialAndKing()
        {
            foreach (DiceType special in DiceEvolution.SpecialTypes)
                Assert.That(PointManager.ToScrollType(special), Is.EqualTo(PointType.SpecialDiceCore), special.ToString());

            foreach (DiceType king in DiceEvolution.KingTypes)
                Assert.That(PointManager.ToScrollType(king), Is.EqualTo(PointType.MythicScroll), king.ToString());
        }

        /// <summary>
        /// 3. <b>러너 안에서 만들 수 있는 것과 없는 것.</b> — 파일럿의 핵심 발견이다.
        ///
        /// <b><c>ScriptableObject.CreateInstance</c> 는 여기서 돌지 않는다.</b> 실제로 넣어 보고
        /// 확인했다: <c>MissingMethodException</c> 과 함께 Mono 가
        /// "out of sync library: UnityEngine.CoreModule.dll" 을 뱉는다. 러너는 Unity 의 <b>관리
        /// 어셈블리</b>만 빌려 쓰고 엔진 네이티브가 없는데, <c>CreateInstance</c> 의 실체가
        /// 그 네이티브 쪽이기 때문이다.
        ///
        /// <b>그래서 SO 를 검사하는 규칙은 SO 에 매달지 않는다.</b> 목록을 인자로 받는
        /// <c>static</c> 으로 두고 SO 의 인스턴스 메서드는 자기 필드를 그리로 넘기기만 한다.
        /// 그러면 규칙은 여기서 잠기고, 에디터를 열어야 하는 것은 "에셋에 값이 실려 있는가"뿐이다.
        /// <c>DiceUnlockDatabase.Validate</c> 와 <c>BuildDefaults</c> 가 그 형태를 따른다.
        ///
        /// 아래는 그 형태가 실제로 되는지 확인한다 — <c>[Serializable]</c> 평범한 클래스는
        /// <c>UnityEngine</c> 애트리뷰트(<c>[Min]</c>)를 달고 있어도 그냥 만들어진다.
        /// </summary>
        [Test]
        public void PlainSerializableGameTypesCanBeConstructed()
        {
            var unlock = new DiceUnlockDefinition { diceType = DiceType.KingFire, price = 200, towerFloor = 30 };

            Assert.That(unlock.towerFloor, Is.EqualTo(30));
            Assert.That(DiceEvolution.GetTier(unlock.diceType), Is.EqualTo(DiceTier.King));

            // 목록에 담아 순수 규칙에 넘기는 것까지 — Validate 가 실제로 받는 모양 그대로다.
            var list = new List<DiceUnlockDefinition> { unlock };
            Assert.That(DiceUnlockDatabase.Validate(list, 0, 0, 0), Is.Not.Empty, "9종이 빠졌으니 문제가 나와야 한다");
        }

        /// <summary>
        /// 4. 진화 배선이 5계통 1:1 이고, 교환 후보가 자기 자신을 뺀다.
        ///
        /// 언락 게이팅이 이 두 지점(<c>TryGetEvolveTarget</c> 의 대상, 교환 후보 목록)에
        /// 필터를 끼우게 되므로, 필터가 없을 때의 답을 먼저 잠가 둔다.
        /// </summary>
        [Test]
        public void EvolveWiringAndExchangeCandidatesAreStable()
        {
            Assert.That(DiceEvolution.BaseTypes.Count, Is.EqualTo(5));
            Assert.That(DiceEvolution.SpecialTypes.Count, Is.EqualTo(5));
            Assert.That(DiceEvolution.KingTypes.Count, Is.EqualTo(5));

            for (int i = 0; i < DiceEvolution.BaseTypes.Count; i++)
            {
                DiceType baseType = DiceEvolution.BaseTypes[i];
                Assert.That(DiceEvolution.TryGetEvolveTarget(baseType, out DiceType special), Is.True, baseType.ToString());
                Assert.That(DiceEvolution.GetTier(special), Is.EqualTo(DiceTier.Special));

                Assert.That(DiceEvolution.TryGetEvolveTarget(special, out DiceType king), Is.True, special.ToString());
                Assert.That(DiceEvolution.GetTier(king), Is.EqualTo(DiceTier.King));

                Assert.That(DiceEvolution.TryGetEvolveTarget(king, out _), Is.False, king.ToString());
            }

            var buffer = new List<DiceType>();
            Assert.That(DiceEvolution.TryGetExchangeCandidates(DiceType.Tornado, buffer), Is.True);
            Assert.That(buffer, Has.Count.EqualTo(4));
            Assert.That(buffer, Has.No.Member(DiceType.Tornado));
        }
    }
}
