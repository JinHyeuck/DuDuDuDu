using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OJ.Core;

namespace OJ.Core.Tests
{
    /// <summary>
    /// <see cref="RunState"/> 의 <b>필드 커버리지</b>를 잠근다. (웨이브 되돌리기 1단계)
    ///
    /// <b>왜 값 비교가 아니라 리플렉션인가.</b> 이 클래스의 존재 이유가
    /// "필드를 늘리면 반드시 한 곳도 빠뜨리지 않는다" 인데, 손으로 쓴 단언은
    /// <b>새로 생긴 필드를 모른다</b> — 필드를 추가하고 <c>BeginRun</c> 이나
    /// <c>CaptureWaveSnapshot</c> 에 넣는 것을 잊어도 테스트는 그대로 초록이다.
    /// 그 사고가 실제로 나면 이전 판 값이 다음 판에 새거나, 되돌리기가 일부만 되감는
    /// 형태로만 드러난다 — 둘 다 화면에서 원인을 못 찾는 종류다.
    ///
    /// 그래서 필드를 <b>세지 않고 훑는다.</b> 필드가 늘면 이 테스트가 자동으로 그것까지
    /// 검사하고, 세 메서드 중 하나라도 빠뜨리면 그 필드 이름을 대고 실패한다.
    /// </summary>
    [TestFixture]
    public sealed class RunStateSnapshotTests
    {
        /// <summary>
        /// 자동 프로퍼티의 백킹 필드까지 전부 집는다 — <c>NonPublic</c> 이 그것을 위한 것이다.
        /// <c>Seed</c>·<c>StageIndex</c> 는 <c>private set</c> 이라 프로퍼티로는 못 쓴다.
        /// </summary>
        private static FieldInfo[] InstanceFields =>
            typeof(RunState).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        /// <summary>모든 필드에 salt 로 갈리는 값을 채운다. bool 은 salt 의 홀짝을 탄다.</summary>
        private static void FillAll(RunState state, int salt)
        {
            FieldInfo[] fields = InstanceFields;
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];

                if (field.FieldType == typeof(int))
                    field.SetValue(state, salt + i);
                else if (field.FieldType == typeof(bool))
                    field.SetValue(state, (salt + i) % 2 == 0);
                else
                    Assert.Fail(
                        $"{field.Name} 은 {field.FieldType} 이라 이 테스트가 채울 줄 모른다. " +
                        "RunState 에 새 타입을 들였다면 FillAll 도 같이 늘려야 한다.");
            }
        }

        private static Dictionary<string, object> ReadAll(RunState state)
        {
            var values = new Dictionary<string, object>();
            FieldInfo[] fields = InstanceFields;
            for (int i = 0; i < fields.Length; i++)
                values.Add(fields[i].Name, fields[i].GetValue(state));

            return values;
        }

        [Test]
        public void 필드가_하나라도_있다()
        {
            // 리플렉션이 아무것도 못 집으면 아래 두 테스트가 전부 공짜로 통과한다.
            // 그 상태를 초록으로 두면 이 파일 전체가 아무것도 안 지키게 된다.
            Assert.That(InstanceFields.Length, Is.GreaterThan(0));
        }

        [Test]
        public void 스냅샷은_모든_필드를_왕복시킨다()
        {
            var state = new RunState();
            FillAll(state, 100);
            Dictionary<string, object> before = ReadAll(state);

            RunState.WaveSnapshot captured = state.CaptureWaveSnapshot();

            // 101 은 100 과 홀짝이 달라 bool 까지 뒤집힌다. 같은 홀짝을 쓰면
            // bool 필드가 흩뜨려지지 않아 복원 여부를 판정할 수 없다.
            FillAll(state, 101);
            Dictionary<string, object> scrambled = ReadAll(state);
            foreach (KeyValuePair<string, object> pair in before)
            {
                Assert.AreNotEqual(
                    pair.Value, scrambled[pair.Key],
                    $"{pair.Key} 가 흩뜨려지지 않았다. 이 테스트가 그 필드에 대해선 아무것도 검사하지 못한다.");
            }

            state.RestoreWaveSnapshot(captured);

            Dictionary<string, object> after = ReadAll(state);
            foreach (KeyValuePair<string, object> pair in before)
            {
                Assert.AreEqual(
                    pair.Value, after[pair.Key],
                    $"{pair.Key} 가 되돌아오지 않았다. WaveSnapshot · CaptureWaveSnapshot · " +
                    "RestoreWaveSnapshot 셋 중 하나에서 빠졌다.");
            }
        }

        [Test]
        public void BeginRun_은_모든_필드를_새로_세운다()
        {
            var state = new RunState();

            // 인자로 넘길 여섯 값과 절대 겹치지 않는 표식이다. BeginRun 이 안 건드린
            // 필드만 이 값을 그대로 들고 남는다.
            const int Marker = 999999;
            FieldInfo[] fields = InstanceFields;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == typeof(int))
                    fields[i].SetValue(state, Marker);
                else if (fields[i].FieldType == typeof(bool))
                    fields[i].SetValue(state, true);
            }

            state.BeginRun(
                seed: 1,
                stageIndex: 2,
                wallMaxHp: 3,
                monstersPerWave: 4,
                initialSummonPoint: 5,
                initialSummonCost: 6);

            for (int i = 0; i < fields.Length; i++)
            {
                object value = fields[i].GetValue(state);

                if (fields[i].FieldType == typeof(int))
                {
                    Assert.AreNotEqual(
                        Marker, value,
                        $"{fields[i].Name} 을 BeginRun 이 세우지 않는다. 이전 판 값이 다음 판으로 샌다.");
                }
                else if (fields[i].FieldType == typeof(bool))
                {
                    // 지금 bool 은 IsGameOver 하나뿐이고 BeginRun 이 false 로 되돌린다.
                    // "판이 끝난 상태로 새 판을 시작"하는 것을 막는 유일한 줄이다.
                    Assert.IsFalse(
                        (bool)value,
                        $"{fields[i].Name} 을 BeginRun 이 세우지 않는다. 이전 판 값이 다음 판으로 샌다.");
                }
            }
        }
    }
}
