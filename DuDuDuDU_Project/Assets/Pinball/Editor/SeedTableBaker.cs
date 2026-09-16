using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Pinball.EditorTools
{
    public struct BakeReport
    {
        public int sampled;
        public int discarded;
        public int[] perSlot;
        public double seconds;
    }

    public static class SeedTableBaker
    {
        private const int Chunk = 20000;   // 청크마다 메인스레드로 돌아와 진행률을 갱신한다

        /// <summary>
        /// seed = 1..sampleCount 를 전부 돌려 슬롯별로 분류하고, fun 내림차순으로 잘라 저장한다.
        /// </summary>
        public static BakeReport Bake(SeedTable table, int sampleCount, int keepPerSlot, bool showProgress = true)
        {
            if (table == null || table.board == null)
                throw new ArgumentException("SeedTable 과 Board 를 먼저 지정하세요.");

            var t0 = DateTime.UtcNow;

            // ★ 스레드에 넘기기 전에 반드시 순수 C# 스냅샷으로 복사
            var snapshot = table.board.CreateSnapshot();
            var sim = new PinballSimulator(snapshot);
            int slotCount = snapshot.SlotCount;

            var slotOf = new int[sampleCount];
            var funOf = new float[sampleCount];
            var specialOf = new byte[sampleCount];
            var maskOf = new int[sampleCount];

            try
            {
                for (int start = 0; start < sampleCount; start += Chunk)
                {
                    int end = Mathf.Min(start + Chunk, sampleCount);

                    Parallel.For(start, end, i =>
                    {
                        var r = sim.Run((uint)(i + 1));   // 시드는 1-based, 결과는 인덱스로 직접 기록 -> 락 불필요
                        slotOf[i] = r.slot;
                        funOf[i] = r.fun;
                        specialOf[i] = (byte)Mathf.Min(255, r.specialHits);
                        maskOf[i] = r.specialMask;
                    });

                    if (showProgress &&
                        EditorUtility.DisplayCancelableProgressBar(
                            "Baking seed table",
                            $"{end:N0} / {sampleCount:N0}",
                            end / (float)sampleCount))
                    {
                        throw new OperationCanceledException();
                    }
                }
            }
            finally
            {
                if (showProgress) EditorUtility.ClearProgressBar();
            }

            // 슬롯별 수집
            var buckets = new List<(uint seed, float fun, byte special, int mask)>[slotCount];
            for (int i = 0; i < slotCount; i++) buckets[i] = new List<(uint, float, byte, int)>();

            int discarded = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                if (slotOf[i] < 0) { discarded++; continue; }
                buckets[slotOf[i]].Add(((uint)(i + 1), funOf[i], specialOf[i], maskOf[i]));
            }

            var pools = new SlotPool[slotCount];
            for (int s = 0; s < slotCount; s++)
            {
                buckets[s].Sort((a, b) => b.fun.CompareTo(a.fun));   // fun 내림차순
                int keep = keepPerSlot <= 0 ? buckets[s].Count : Mathf.Min(keepPerSlot, buckets[s].Count);

                var pool = new SlotPool
                {
                    seeds = new uint[keep],
                    fun = new float[keep],
                    specialHits = new byte[keep],
                    hitMask = new int[keep],
                    sampled = buckets[s].Count      // 자르기 전 원본 개수
                };
                for (int k = 0; k < keep; k++)
                {
                    pool.seeds[k] = buckets[s][k].seed;
                    pool.fun[k] = buckets[s][k].fun;
                    pool.specialHits[k] = buckets[s][k].special;
                    pool.hitMask[k] = buckets[s][k].mask;
                }
                pools[s] = pool;
            }

            table.pools = pools;
            table.sampledCount = sampleCount;
            table.discardedCount = discarded;
            table.bakedLayoutHash = table.board.LayoutHash();

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();

            var report = new BakeReport
            {
                sampled = sampleCount,
                discarded = discarded,
                perSlot = new int[slotCount],
                seconds = (DateTime.UtcNow - t0).TotalSeconds
            };
            for (int s = 0; s < slotCount; s++) report.perSlot[s] = pools[s].Count;
            return report;
        }

        /// <summary>
        /// 저장된 시드를 다시 돌려 슬롯이 일치하는지 검사한다.
        /// CI 에 이걸 걸어두면 판을 건드렸을 때 조용히 확률이 틀어지는 사고를 막을 수 있다.
        /// </summary>
        public static int Verify(SeedTable table, int samplesPerSlot, out string message)
        {
            var sim = new PinballSimulator(table.board.CreateSnapshot());
            int mismatch = 0;
            var log = new System.Text.StringBuilder();

            if (table.IsStale)
                log.AppendLine($"레이아웃 해시 불일치: baked={table.bakedLayoutHash} current={table.board.LayoutHash()}");

            for (int s = 0; s < table.SlotCount; s++)
            {
                var pool = table.pools[s];
                if (pool.Count == 0)
                {
                    log.AppendLine($"slot {s}: 풀이 비어있음 (판 배치 또는 발사 지터 조정 필요)");
                    mismatch++;
                    continue;
                }

                int n = Mathf.Min(samplesPerSlot, pool.Count);
                int stride = Mathf.Max(1, pool.Count / n);

                for (int k = 0; k < pool.Count && k < n * stride; k += stride)
                {
                    var r = sim.Run(pool.seeds[k]);
                    if (r.slot != s)
                    {
                        mismatch++;
                        if (mismatch <= 10)
                            log.AppendLine($"slot {s}: seed {pool.seeds[k]} -> {r.slot}");
                    }
                }
            }

            message = mismatch == 0
                ? "OK — 모든 샘플이 기록된 슬롯으로 재현됨."
                : $"불일치 {mismatch}건\n{log}";
            return mismatch;
        }
    }

    /// <summary>
    /// 판을 바꿀 때마다 돌리는 빠른 진단. 에셋에 저장하지 않고 통계만 낸다.
    /// 레이아웃 편집 -> Quick Test -> 수치 확인 의 루프를 짧게 돌리는 게 목적.
    /// </summary>
    public struct ProbeReport
    {
        public int sampled;
        public int discarded;
        public int[] perSlot;
        public float avgPegHits;
        public float avgSwitches;
        public float avgSeconds;
        public float nearMissRatio;   // 칸을 2회 이상 넘나든 궤적 비율

        /// <summary>[슬롯, 태그비트] 그 태그를 한 번이라도 맞은 표본 수.</summary>
        public int[,] slotTagHits;

        /// <summary>태그비트별 전체 적중 표본 수.</summary>
        public int[] tagHits;

        /// <summary>비트 인덱스 -> 태그 값.</summary>
        public int[] tagByBit;

        public float avgSpecialHits;
        public bool hasSpecialPegs;

        public double seconds;

        public int Valid => sampled - discarded;
        public float DiscardRatio => sampled > 0 ? discarded / (float)sampled : 0f;
        public int MinPool
        {
            get
            {
                int m = int.MaxValue;
                foreach (var c in perSlot) m = Mathf.Min(m, c);
                return m == int.MaxValue ? 0 : m;
            }
        }
    }

    public static class SeedTableProbe
    {
        public static ProbeReport Run(PinballBoard board, int sampleCount)
        {
            var t0 = DateTime.UtcNow;
            var snapshot = board.CreateSnapshot();
            var sim = new PinballSimulator(snapshot);
            int slotCount = snapshot.SlotCount;

            var slots = new int[sampleCount];
            var pegHits = new int[sampleCount];
            var switches = new int[sampleCount];
            var steps = new int[sampleCount];
            var special = new int[sampleCount];
            var masks = new int[sampleCount];

            Parallel.For(0, sampleCount, i =>
            {
                var r = sim.Run((uint)(i + 1));
                slots[i] = r.slot;
                pegHits[i] = r.pegHits;
                switches[i] = r.slotSwitches;
                steps[i] = r.steps;
                special[i] = r.specialHits;
                masks[i] = r.specialMask;
            });

            int bitCount = snapshot.tagByBit != null ? snapshot.tagByBit.Length : 0;

            var rep = new ProbeReport
            {
                sampled = sampleCount,
                perSlot = new int[slotCount],
                slotTagHits = new int[slotCount, Mathf.Max(1, bitCount)],
                tagHits = new int[Mathf.Max(1, bitCount)],
                tagByBit = snapshot.tagByBit,
                hasSpecialPegs = bitCount > 0
            };
            long sp = 0, ss = 0, st = 0, sh = 0;
            int near = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                if (slots[i] < 0) { rep.discarded++; continue; }
                rep.perSlot[slots[i]]++;
                sp += pegHits[i]; ss += switches[i]; st += steps[i]; sh += special[i];
                if (switches[i] >= 2) near++;

                for (int b = 0; b < bitCount; b++)
                {
                    if ((masks[i] & (1 << b)) == 0) continue;
                    rep.slotTagHits[slots[i], b]++;
                    rep.tagHits[b]++;
                }
            }

            int valid = Mathf.Max(1, rep.Valid);
            rep.avgPegHits = sp / (float)valid;
            rep.avgSwitches = ss / (float)valid;
            rep.avgSeconds = st / (float)valid * snapshot.dt;
            rep.nearMissRatio = near / (float)valid;
            rep.avgSpecialHits = sh / (float)valid;
            rep.seconds = (DateTime.UtcNow - t0).TotalSeconds;
            return rep;
        }

        /// <summary>합격 기준 점검. 문제가 있으면 사람이 읽을 수 있는 진단 문장을 돌려준다.</summary>
        public static List<string> Diagnose(ProbeReport r, int minPoolPerSlot = 40)
        {
            var msgs = new List<string>();

            if (r.DiscardRatio > 0.85f)
                msgs.Add($"폐기율 {r.DiscardRatio * 100:0.#}% — 구슬이 거의 착지하지 못합니다. " +
                         "발사 레인을 못 벗어나고 있을 가능성이 큽니다. launchSpeed 를 올리거나 상단 디플렉터 램프를 확인하세요.");
            else if (r.DiscardRatio > 0.30f)
                msgs.Add($"폐기율 {r.DiscardRatio * 100:0.#}% — 다소 높습니다. maxSteps 를 늘리거나 linearDrag 를 올려 끼임을 줄이세요.");

            for (int i = 0; i < r.perSlot.Length; i++)
                if (r.perSlot[i] < minPoolPerSlot)
                    msgs.Add($"slot {i} 표본 {r.perSlot[i]}개 — 궤적이 부족합니다. " +
                             "angleJitterDeg 를 넓히거나 이 칸 위쪽 핀 배치를 조정하세요.");

            if (r.avgPegHits < 12f)
                msgs.Add($"평균 핀 충돌 {r.avgPegHits:0.#}회 — 밋밋합니다. 핀을 더 촘촘히 하거나 linearDrag 를 낮추세요.");

            if (r.avgSeconds > 5f)
                msgs.Add($"평균 {r.avgSeconds:0.#}초 — 깁니다. gravity 와 linearDrag 를 함께 올리세요(launchSpeed 도 같이).");
            else if (r.avgSeconds < 1.5f)
                msgs.Add($"평균 {r.avgSeconds:0.#}초 — 너무 짧아 연출이 안 살아납니다.");

            if (r.hasSpecialPegs)
            {
                // 태그별로: 이 슬롯에서 아예 못 맞추면 그 슬롯에서는 목표 확률을 올릴 수 없고,
                // 100% 맞으면 내릴 수 없다. 가중치 조정의 한계가 여기서 정해진다.
                for (int slot = 0; slot < r.perSlot.Length; slot++)
                {
                    if (r.perSlot[slot] == 0) continue;

                    for (int b = 0; b < r.tagHits.Length; b++)
                    {
                        int tag = r.tagByBit != null && b < r.tagByBit.Length ? r.tagByBit[b] : b;
                        float ratio = r.slotTagHits[slot, b] / (float)r.perSlot[slot];

                        if (r.slotTagHits[slot, b] == 0)
                            msgs.Add($"slot {slot} 로 가면서 태그 {tag} 핀을 맞는 궤적이 없습니다. " +
                                     "그 슬롯에서는 이 태그의 적중 확률을 올릴 수 없습니다.");
                        else if (ratio < 0.02f)
                            msgs.Add($"slot {slot} 에서 태그 {tag} 적중이 {ratio * 100:0.#}% 뿐입니다. " +
                                     "목표를 높게 잡으면 같은 궤적이 반복됩니다.");
                    }
                }
            }

            if (r.nearMissRatio < 0.08f)
                msgs.Add($"니어미스 궤적 {r.nearMissRatio * 100:0.#}% — 아슬아슬한 연출이 부족합니다. dividerTopY 를 낮춰보세요.");

            return msgs;
        }
    }
}
