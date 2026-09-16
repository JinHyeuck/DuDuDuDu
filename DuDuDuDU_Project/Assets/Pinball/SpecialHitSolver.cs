using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 태그별 목표 적중 확률을 만족하도록, 슬롯 안의 "적중 패턴"별 선택 가중치를 계산한다.
    ///
    /// 왜 패턴 가중치인가:
    ///   적중 횟수로 풀을 쪼개면(0회/1회/2회...) 조합마다 시드가 말라붙는다.
    ///   대신 풀은 통째로 두고, 어느 패턴을 얼마나 자주 뽑을지만 조정한다.
    ///   "핀A를 맞는다"는 요구는 패턴 {A} 와 {A,B} 양쪽으로 분산될 수 있어 훨씬 여유롭다.
    ///
    /// 방법: IPF(반복 비례 조정).
    ///   자연 분포에서 출발해, 태그마다 현재 주변확률을 목표에 맞추는 스케일링을 반복한다.
    ///   목표가 달성 가능하면 수렴한다.
    /// </summary>
    public sealed class SpecialHitSolver
    {
        private const int Iterations = 200;

        /// <summary>슬롯별로 (패턴 -> 누적확률) 테이블. 뽑을 때 이진탐색 없이 선형 스캔해도 충분히 작다.</summary>
        private readonly Dictionary<int, (int[] masks, float[] cumulative)> _cache =
            new Dictionary<int, (int[], float[])>();

        private readonly SeedTable _table;
        private readonly float[] _targets;      // 비트 인덱스별 목표 확률
        private readonly int _bitCount;

        public SpecialHitSolver(SeedTable table, PinballBoard board)
        {
            _table = table;

            var tags = board.SpecialTags();
            _bitCount = tags.Count;
            _targets = new float[_bitCount];

            for (int b = 0; b < _bitCount; b++)
            {
                _targets[b] = -1f;              // 규칙이 없는 태그는 제어하지 않는다
                if (board.specialRules == null) continue;

                foreach (var rule in board.specialRules)
                    if (rule.tag == tags[b]) { _targets[b] = Mathf.Clamp01(rule.targetHitProbability); break; }
            }
        }

        public int BitCount => _bitCount;

        /// <summary>목표 확률에 맞춰 패턴 하나를 뽑는다. 제어할 게 없으면 -1(가리지 않음).</summary>
        public int PickMask(int slot, System.Random rand)
        {
            if (_bitCount == 0) return -1;

            var table = GetOrBuild(slot);
            if (table.masks == null || table.masks.Length == 0) return -1;

            float roll = (float)rand.NextDouble();
            for (int i = 0; i < table.masks.Length; i++)
                if (roll <= table.cumulative[i]) return table.masks[i];

            return table.masks[table.masks.Length - 1];
        }

        /// <summary>진단용. 계산된 가중치로 실제 달성되는 태그별 적중 확률.</summary>
        public float AchievedProbability(int slot, int bit)
        {
            var t = GetOrBuild(slot);
            if (t.masks == null) return 0f;

            int mask = 1 << bit;
            float sum = 0f, prev = 0f;
            for (int i = 0; i < t.masks.Length; i++)
            {
                float w = t.cumulative[i] - prev;
                prev = t.cumulative[i];
                if ((t.masks[i] & mask) != 0) sum += w;
            }
            return sum;
        }

        /// <summary>
        /// 가장 심하게 재사용되는 패턴의 배율.
        /// 1.0 이면 자연 분포 그대로, 3.0 이면 그 패턴의 시드들이 3배 자주 나온다는 뜻.
        /// 높을수록 유저가 같은 궤적을 자주 보게 된다.
        /// </summary>
        public float WorstReuseFactor(int slot)
        {
            var t = GetOrBuild(slot);
            if (t.masks == null) return 1f;

            var hist = _table.MaskHistogram(slot);
            int total = 0;
            foreach (var kv in hist) total += kv.Value;
            if (total == 0) return 1f;

            float worst = 0f, prev = 0f;
            for (int i = 0; i < t.masks.Length; i++)
            {
                float w = t.cumulative[i] - prev;
                prev = t.cumulative[i];

                if (!hist.TryGetValue(t.masks[i], out int n) || n == 0) continue;
                float natural = n / (float)total;
                worst = Mathf.Max(worst, w / natural);
            }
            return worst;
        }

        public void Clear() => _cache.Clear();

        // ────────────────────────────────────────────────

        private (int[] masks, float[] cumulative) GetOrBuild(int slot)
        {
            if (_cache.TryGetValue(slot, out var cached)) return cached;

            var built = Build(slot);
            _cache[slot] = built;
            return built;
        }

        private (int[] masks, float[] cumulative) Build(int slot)
        {
            var hist = _table.MaskHistogram(slot);
            if (hist.Count == 0) return (null, null);

            var masks = new int[hist.Count];
            var w = new float[hist.Count];
            var natural = new float[hist.Count];

            int total = 0;
            foreach (var kv in hist) total += kv.Value;

            int k = 0;
            foreach (var kv in hist)
            {
                masks[k] = kv.Key;
                natural[k] = kv.Value / (float)total;
                w[k] = natural[k];                  // 자연 분포에서 출발
                k++;
            }

            // IPF
            for (int iter = 0; iter < Iterations; iter++)
            {
                for (int bit = 0; bit < _bitCount; bit++)
                {
                    float target = _targets[bit];
                    if (target < 0f) continue;      // 이 태그는 제어하지 않음

                    int m = 1 << bit;
                    float cur = 0f;
                    for (int i = 0; i < masks.Length; i++)
                        if ((masks[i] & m) != 0) cur += w[i];

                    // 모두 맞거나 아무도 안 맞는 슬롯이면 조정이 불가능하다. 그냥 둔다.
                    if (cur <= 1e-6f || cur >= 1f - 1e-6f) continue;

                    float up = target / cur;
                    float down = (1f - target) / (1f - cur);
                    for (int i = 0; i < masks.Length; i++)
                        w[i] *= (masks[i] & m) != 0 ? up : down;
                }

                float z = 0f;
                for (int i = 0; i < w.Length; i++) z += w[i];
                if (z <= 1e-12f) break;
                for (int i = 0; i < w.Length; i++) w[i] /= z;
            }

            var cum = new float[masks.Length];
            float acc = 0f;
            for (int i = 0; i < masks.Length; i++)
            {
                acc += w[i];
                cum[i] = acc;
            }
            if (cum.Length > 0) cum[cum.Length - 1] = 1f;   // 부동소수 오차 보정

            return (masks, cum);
        }
    }
}
