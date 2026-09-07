using System.Collections.Generic;
using NUnit.Framework;
using OJ;
using OJ.Core;
using OJ.Dice;
using OJ.Point;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 다이스 언락의 규칙을 잠근다.
    ///
    /// <b>여기서 못 두드리는 것이 무엇인지 알고 쓴 테스트다.</b> 헤드리스 러너 안에서는
    /// <c>ScriptableObject.CreateInstance</c> 도 <c>MonoSingleton.Instance</c> 도 돌지 않는다
    /// (<see cref="AssemblyPilotTests"/> 주석). 그래서 SO 인스턴스와 <c>StaticResource</c> 를
    /// 타는 경로는 여기 없고, <b>규칙은 전부 그 바깥에 두었다</b> —
    /// <c>DiceUnlockDatabase.Validate</c>/<c>BuildDefaults</c> 는 목록을 받는 <c>static</c> 이고,
    /// 컨텐츠 지급의 결정표는 <c>DiceOwnershipManager.ShouldGrant</c> 라는 상태 없는 <c>static</c> 이다.
    /// 에디터를 열어야만 확인되는 것은 "에셋에 값이 실려 있는가" 하나로 줄어 있다.
    /// </summary>
    public sealed class DiceUnlockTests
    {
        // 실제 환경 수치. 에셋이 아니라 코드 상수·데이터베이스 크기에서 온다.
        private const int StageCount = 30;
        private const int MaxStarCount = 90;   // 30스테이지 × 3별
        private const int StarsPerReward = 3;

        private static List<string> ValidateDefaults()
        {
            return DiceUnlockDatabase.Validate(
                DiceUnlockDatabase.BuildDefaults(), StageCount, MaxStarCount, StarsPerReward);
        }

        // ── 기본 표 ─────────────────────────────────────────────────────

        /// <summary>
        /// 코드 기본값이 스스로의 검사를 통과한다.
        ///
        /// 에셋이 비었을 때 이 표가 그대로 찍히므로, 여기가 깨지면 <b>새 프로젝트가
        /// 잘못된 언락 표로 시작한다.</b>
        /// </summary>
        [Test]
        public void 기본표가_검사를_통과한다()
        {
            Assert.That(ValidateDefaults(), Is.Empty);
        }

        /// <summary>
        /// 특수 5 + 킹 5 가 빠짐없이 있고 기본 5종은 없다.
        ///
        /// 빠진 다이스는 가격도 보상처도 없어 <b>영영 못 얻는다</b>. 기본 다이스가 들어 있으면
        /// 처음부터 보유인 것을 다시 팔게 된다.
        /// </summary>
        [Test]
        public void 기본표가_특수와_킹_열_종을_덮는다()
        {
            List<DiceUnlockDefinition> defaults = DiceUnlockDatabase.BuildDefaults();

            Assert.That(defaults, Has.Count.EqualTo(10));

            var covered = new HashSet<DiceType>();
            foreach (DiceUnlockDefinition definition in defaults)
            {
                Assert.That(DiceEvolution.GetTier(definition.diceType), Is.Not.EqualTo(DiceTier.Base),
                    definition.diceType + " 는 기본 다이스라 언락 대상이 아니다");
                covered.Add(definition.diceType);
            }

            foreach (DiceType special in DiceEvolution.SpecialTypes)
                Assert.That(covered, Contains.Item(special));

            foreach (DiceType king in DiceEvolution.KingTypes)
                Assert.That(covered, Contains.Item(king));
        }

        /// <summary>
        /// 열 종 전부 가격을 갖는다.
        ///
        /// <b>환급 규칙이 이것을 강제한다.</b> 컨텐츠가 이미 보유한 다이스를 줬을 때
        /// 돌려줄 금액이 곧 가격이라, 가격 없는 줄이 하나라도 있으면 그 다이스는
        /// "보상을 받았는데 아무 일도 없는" 상태를 만들 수 있다.
        /// </summary>
        [Test]
        public void 모든_다이스가_가격을_갖는다()
        {
            foreach (DiceUnlockDefinition definition in DiceUnlockDatabase.BuildDefaults())
                Assert.That(definition.price, Is.GreaterThan(0), definition.diceType.ToString());
        }

        /// <summary>
        /// 열 종 전부 컨텐츠 보상처를 하나씩 갖는다.
        ///
        /// 기획의 "다이스 잠금 시 보상처를 보여 줘 컨텐츠 플레이를 유도한다"가 성립하려면
        /// 보여 줄 것이 있어야 한다. 재화만 있는 다이스는 그 화면이 빈칸이 된다.
        /// </summary>
        [Test]
        public void 모든_다이스가_컨텐츠_보상처를_갖는다()
        {
            foreach (DiceUnlockDefinition definition in DiceUnlockDatabase.BuildDefaults())
                Assert.That(definition.HasContentSource, Is.True, definition.diceType.ToString());
        }

        /// <summary>
        /// 기획서에 박힌 값. 5.2 목표 배너와 5.6 해금 진행도가 이 문구를 그린다.
        /// 옮기면 두 화면이 같이 틀어지므로 못 박아 둔다.
        /// </summary>
        [Test]
        public void KingFire_는_삼십층이다()
        {
            var byType = new Dictionary<DiceType, DiceUnlockDefinition>();
            foreach (DiceUnlockDefinition definition in DiceUnlockDatabase.BuildDefaults())
                byType[definition.diceType] = definition;

            Assert.That(byType[DiceType.KingFire].towerFloor, Is.EqualTo(30));
        }

        // ── 검사 ────────────────────────────────────────────────────────

        /// <summary>
        /// 요구 별이 보상 간격의 배수가 아니면 잡는다.
        ///
        /// 별 보상은 3별 간격으로만 존재한다. 20 을 적으면 그 다이스는 <b>영영 그 경로로
        /// 안 열리는데</b>, 화면에는 "별 20개" 라고 멀쩡히 뜬다 — 조용히 죽는 종류다.
        /// </summary>
        [Test]
        public void 요구_별이_보상_간격에_안_맞으면_문제로_잡는다()
        {
            List<DiceUnlockDefinition> definitions = DiceUnlockDatabase.BuildDefaults();
            definitions[0].starRequirement = 20;

            List<string> problems = DiceUnlockDatabase.Validate(
                definitions, StageCount, MaxStarCount, StarsPerReward);

            Assert.That(problems, Is.Not.Empty);
        }

        /// <summary>가격 0 은 환급을 불가능하게 만드는 값이라 문제로 잡는다.</summary>
        [Test]
        public void 가격이_없으면_문제로_잡는다()
        {
            List<DiceUnlockDefinition> definitions = DiceUnlockDatabase.BuildDefaults();
            definitions[0].price = 0;

            Assert.That(
                DiceUnlockDatabase.Validate(definitions, StageCount, MaxStarCount, StarsPerReward),
                Is.Not.Empty);
        }

        /// <summary>같은 층이 둘을 열면 결과 화면이 하나만 보여 준다.</summary>
        [Test]
        public void 해금_층이_겹치면_문제로_잡는다()
        {
            List<DiceUnlockDefinition> definitions = DiceUnlockDatabase.BuildDefaults();
            definitions[5].towerFloor = 100;
            definitions[6].towerFloor = 100;

            Assert.That(
                DiceUnlockDatabase.Validate(definitions, StageCount, MaxStarCount, StarsPerReward),
                Is.Not.Empty);
        }

        /// <summary>한 종이라도 빠지면 그 다이스는 영영 못 얻는다.</summary>
        [Test]
        public void 다이스가_빠지면_문제로_잡는다()
        {
            List<DiceUnlockDefinition> definitions = DiceUnlockDatabase.BuildDefaults();
            definitions.RemoveAt(0);

            Assert.That(
                DiceUnlockDatabase.Validate(definitions, StageCount, MaxStarCount, StarsPerReward),
                Is.Not.Empty);
        }

        /// <summary>바깥 수치를 0 으로 넘기면 그 검사만 건너뛴다 — 나머지는 그대로 돈다.</summary>
        [Test]
        public void 바깥_수치가_0_이면_그_검사만_건너뛴다()
        {
            List<DiceUnlockDefinition> definitions = DiceUnlockDatabase.BuildDefaults();
            definitions[0].starRequirement = 900;   // 최대 별을 한참 넘는 값

            Assert.That(
                DiceUnlockDatabase.Validate(definitions, StageCount, maxStarCount: 0, starsPerReward: StarsPerReward),
                Is.Empty);

            Assert.That(
                DiceUnlockDatabase.Validate(definitions, StageCount, MaxStarCount, StarsPerReward),
                Is.Not.Empty);
        }

        // ── 보유 판정 ───────────────────────────────────────────────────

        private static DiceOwnershipManager MakeOwnership()
        {
            return new DiceOwnershipManager(new PointManager());
        }

        /// <summary>
        /// 기본 5종은 상태 없이 보유다. 세이브가 비어도 참이라 게임이 시작 불가로 떨어지지 않는다.
        /// </summary>
        [Test]
        public void 기본_다이스는_처음부터_보유다()
        {
            DiceOwnershipManager ownership = MakeOwnership();

            foreach (DiceType baseType in DiceEvolution.BaseTypes)
                Assert.That(ownership.IsOwned(baseType), Is.True, baseType.ToString());

            foreach (DiceType special in DiceEvolution.SpecialTypes)
                Assert.That(ownership.IsOwned(special), Is.False, special.ToString());

            foreach (DiceType king in DiceEvolution.KingTypes)
                Assert.That(ownership.IsOwned(king), Is.False, king.ToString());

            Assert.That(ownership.IsOwned(DiceType.Max), Is.False);
        }

        /// <summary>
        /// 컨텐츠 지급의 결정표 — 처음엔 해금, 두 번째부터는 <b>가격만큼 환급</b>.
        /// 이 분기가 기획의 "중복 보상 시 재화 지급" 전부다.
        ///
        /// <c>ShouldGrant</c> 를 두드리는 이유는 실제 지급이 저장(<c>GameContainer.SaveService</c>)을
        /// 밟아 이 러너에서 못 돌기 때문이다. 규칙은 전부 여기 있다 — 매니저 쪽에 남은 것은
        /// "HashSet 에 넣고 저장한다" 뿐이다.
        /// </summary>
        [Test]
        public void 컨텐츠_지급은_처음엔_해금_두번째엔_환급이다()
        {
            var rewards = new List<PointRewardEntry>();

            bool first = DiceOwnershipManager.ShouldGrant(DiceType.Tornado, alreadyOwned: false, price: 120, rewards);

            Assert.That(first, Is.True, "미보유면 지급한다");
            Assert.That(rewards, Is.Empty, "새로 얻었으면 환급할 것이 없다");

            bool second = DiceOwnershipManager.ShouldGrant(DiceType.Tornado, alreadyOwned: true, price: 120, rewards);

            Assert.That(second, Is.False, "이미 보유면 새로 얻은 것이 아니다");
            Assert.That(rewards, Has.Count.EqualTo(1));
            Assert.That(rewards[0].PointType, Is.EqualTo(PointType.SpecialDiceCore));
            Assert.That(rewards[0].Amount, Is.EqualTo(120));
        }

        /// <summary>킹은 신화 재화로 환급된다 — 강화에 쓰는 재화와 같은 것이다.</summary>
        [Test]
        public void 킹_환급은_신화_재화다()
        {
            var rewards = new List<PointRewardEntry>();

            DiceOwnershipManager.ShouldGrant(DiceType.KingFire, alreadyOwned: true, price: 200, rewards);

            Assert.That(rewards, Has.Count.EqualTo(1));
            Assert.That(rewards[0].PointType, Is.EqualTo(PointType.MythicScroll));
            Assert.That(rewards[0].Amount, Is.EqualTo(200));
        }

        /// <summary>기본 다이스와 Max 는 컨텐츠가 줄 것이 없다. 환급도 없다.</summary>
        [Test]
        public void 기본_다이스는_컨텐츠_지급_대상이_아니다()
        {
            var rewards = new List<PointRewardEntry>();

            Assert.That(DiceOwnershipManager.ShouldGrant(DiceType.Normal, alreadyOwned: true, price: 100, rewards), Is.False);
            Assert.That(DiceOwnershipManager.ShouldGrant(DiceType.Max, alreadyOwned: false, price: 100, rewards), Is.False);
            Assert.That(rewards, Is.Empty);
        }

        // ── 저장 ────────────────────────────────────────────────────────

        /// <summary>
        /// 보유가 세이브를 왕복한다. 기본 다이스는 파일에 나가지 않는다.
        ///
        /// 씨를 <c>ReadFrom</c> 으로 뿌린다 — 실제 지급은 저장을 밟아 이 러너에서 못 돈다.
        /// </summary>
        [Test]
        public void 보유가_세이브를_왕복한다()
        {
            var seed = new SaveState();
            seed.OwnedDice["Tornado"] = 1;
            seed.OwnedDice["KingIce"] = 1;

            DiceOwnershipManager written = MakeOwnership();
            written.ReadFrom(seed);

            var state = new SaveState();
            written.WriteTo(state);

            Assert.That(state.OwnedDice, Has.Count.EqualTo(2), "기본 다이스는 파일에 나가지 않는다");
            Assert.That(state.OwnedDice.ContainsKey("Tornado"), Is.True);
            Assert.That(state.OwnedDice.ContainsKey("KingIce"), Is.True);

            DiceOwnershipManager read = MakeOwnership();
            read.ReadFrom(state);

            Assert.That(read.IsOwned(DiceType.Tornado), Is.True);
            Assert.That(read.IsOwned(DiceType.KingIce), Is.True);
            Assert.That(read.IsOwned(DiceType.KingFire), Is.False);
        }

        /// <summary>
        /// 세이브의 쓰레기 줄이 멀쩡한 보유를 같이 죽이지 않는다.
        ///
        /// enum 에서 사라진 이름, 정수 문자열, 기본 다이스, 값 0 — 넷 다 조용히 버린다.
        /// 특히 <b>기본 다이스를 버리는 것이 중요하다</b>: 담으면 <c>WriteTo</c> 가 그것을 다시
        /// 뱉어 "정본이 셋" 인 상태가 파일에 굳는다.
        /// </summary>
        [Test]
        public void 손상된_세이브_줄을_조용히_버린다()
        {
            var state = new SaveState();
            state.OwnedDice["Tornado"] = 1;
            state.OwnedDice["없어진다이스"] = 1;
            state.OwnedDice["999"] = 1;
            state.OwnedDice["Normal"] = 1;      // 기본 다이스
            state.OwnedDice["KingFire"] = 0;    // 값이 0

            DiceOwnershipManager ownership = MakeOwnership();
            ownership.ReadFrom(state);

            Assert.That(ownership.IsOwned(DiceType.Tornado), Is.True);
            Assert.That(ownership.IsOwned(DiceType.KingFire), Is.False);

            var rewritten = new SaveState();
            ownership.WriteTo(rewritten);

            Assert.That(rewritten.OwnedDice, Has.Count.EqualTo(1));
            Assert.That(rewritten.OwnedDice.ContainsKey("Tornado"), Is.True);
        }
    }
}
