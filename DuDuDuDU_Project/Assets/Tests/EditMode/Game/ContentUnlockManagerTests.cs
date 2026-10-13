using NUnit.Framework;
using OJ.ContentUnlock;
using OJ.Core;

namespace OJ.Game.Tests
{
    /// <summary>
    /// 해금 연출 기록의 저장 왕복을 잠근다. (Docs/ContentUnlock.md)
    ///
    /// <b>왕복이 어긋나면 로드마다 에러가 찍힌다.</b> <c>SaveService</c> 가 읽은 것과 다시 쓴 것을
    /// 문자열로 대조하기 때문이다. 특히 <c>Initialized</c> 는 "기록 없는 옛 세이브" 를 가르는 표식이라
    /// 읽자마자 true 로 바꿔 쓰면 채우기가 영영 안 일어나 연출이 쏟아진다.
    ///
    /// 열림·보임 판정은 <c>StageProgressManager</c>·SO 를 타서 여기서 못 두드린다 —
    /// 그 규칙은 <c>ContentUnlockRulesTests</c> 가 잠근다.
    /// </summary>
    public sealed class ContentUnlockManagerTests
    {
        private static SaveState RoundTrip(SaveState loaded)
        {
            var manager = new ContentUnlockManager();
            manager.ReadFrom(loaded);

            var written = new SaveState();
            manager.WriteTo(written);
            return written;
        }

        [Test]
        public void 옛_세이브는_기록_없음_그대로_다시_쓴다()
        {
            var old = new SaveState();
            old.Stage.Records["6"] = new StageRecordSave { BestClearGrade = 1 };

            SaveState written = RoundTrip(old);

            Assert.IsFalse(written.ContentUnlock.Initialized);
            CollectionAssert.IsEmpty(written.ContentUnlock.Announced);
        }

        [Test]
        public void 기록은_이름_오름차순으로_그대로_돌아온다()
        {
            var loaded = new SaveState();
            loaded.ContentUnlock.Initialized = true;
            loaded.ContentUnlock.Announced.Add("Dice");
            loaded.ContentUnlock.Announced.Add("Equipment");
            loaded.ContentUnlock.Announced.Add("IdleReward");

            SaveState written = RoundTrip(loaded);

            Assert.IsTrue(written.ContentUnlock.Initialized);
            CollectionAssert.AreEqual(loaded.ContentUnlock.Announced, written.ContentUnlock.Announced);
        }

        /// <summary>표에서 빠진 컨텐츠의 기록은 버린다 — 왕복 검사가 그 차이를 크게 알린다.</summary>
        [Test]
        public void 모르는_이름은_버린다()
        {
            var loaded = new SaveState();
            loaded.ContentUnlock.Initialized = true;
            loaded.ContentUnlock.Announced.Add("Dice");
            loaded.ContentUnlock.Announced.Add("NoSuchContent");

            SaveState written = RoundTrip(loaded);

            CollectionAssert.AreEqual(new[] { "Dice" }, written.ContentUnlock.Announced);
        }

        /// <summary>세이브 파일이 없는 첫 실행(ReadFrom 이 안 불린다)은 처음부터 기록을 쌓는다.</summary>
        [Test]
        public void 첫_실행은_기록하는_세이브로_시작한다()
        {
            var written = new SaveState();
            new ContentUnlockManager().WriteTo(written);

            Assert.IsTrue(written.ContentUnlock.Initialized);
        }
    }
}
