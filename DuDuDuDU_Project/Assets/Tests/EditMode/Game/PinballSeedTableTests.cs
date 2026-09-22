#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools.Tests
{
    /// <summary>
    /// CI 에 걸어두는 안전장치.
    /// 기획자가 핀을 3픽셀 옮기고 재베이크를 잊으면 여기서 빨간불이 뜬다.
    /// 이걸 안 잡으면 겉보기엔 멀쩡하게 굴러가면서 표기 확률과 실제 지급이 어긋난 채로 라이브에 나간다.
    /// </summary>
#if OJ_HEADLESS_RUNNER
    // 헤드리스 러너는 에디터 밖이라 AssetDatabase 가 없다(내부 호출이 해결 안 돼 터진다).
    // 컴파일은 그대로 되게 두고 실행만 빼다 — 에디터의 Test Runner 에서는 정상으로 돌고,
    // 그곳이 이 테스트의 진짜 집이다.
    [Ignore("AssetDatabase 가 필요해 Unity 에디터에서만 돌다.")]
#endif
    public sealed class PinballSeedTableTests
    {
        private static SeedTable[] LoadAll()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(SeedTable));
            var list = new SeedTable[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                list[i] = AssetDatabase.LoadAssetAtPath<SeedTable>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return list;
        }

        [Test]
        public void LayoutHash_MatchesBakedHash()
        {
            foreach (var t in LoadAll())
            {
                Assert.IsNotNull(t.board, $"{t.name}: board 미지정");
                Assert.AreEqual(t.bakedLayoutHash, t.board.LayoutHash(),
                    $"{t.name}: 판이 베이크 이후 변경됨. 재베이크 필요.");
            }
        }

        [Test]
        public void EverySlot_HasSeeds()
        {
            foreach (var t in LoadAll())
                for (int s = 0; s < t.SlotCount; s++)
                    Assert.Greater(t.pools[s].Count, 0,
                        $"{t.name}: slot {s} 시드 풀이 비어있음 — 이 칸은 당첨돼도 재생할 궤적이 없다.");
        }

        [Test]
        public void DeclaredProbability_SumsToOne()
        {
            foreach (var t in LoadAll())
            {
                var p = t.board.declaredProbability;
                Assert.AreEqual(t.SlotCount, p.Length, $"{t.name}: 확률 배열 길이 불일치");
                float sum = 0f;
                foreach (var v in p) sum += v;
                Assert.AreEqual(1f, sum, 0.001f, $"{t.name}: declaredProbability 합이 {sum}");
            }
        }

        [Test]
        public void StoredSeeds_ReproduceTheirSlot()
        {
            foreach (var t in LoadAll())
            {
                int bad = SeedTableBaker.Verify(t, 64, out string msg);
                Assert.Zero(bad, $"{t.name}: {msg}");
            }
        }
    }
}
#endif
