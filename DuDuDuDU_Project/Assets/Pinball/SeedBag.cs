using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    /// <summary>
    /// 슬롯별 셔플백. 풀을 한 바퀴 다 쓰기 전에는 같은 시드가 재등장하지 않는다.
    /// 순수 랜덤으로 뽑으면 유저가 "아까 그 궤적"을 금방 알아챈다.
    ///
    /// 특수 핀 적중 횟수까지 지정해서 뽑을 수 있다.
    /// 이 경우 (슬롯, 버킷) 조합마다 별도의 셔플백을 쓴다.
    /// </summary>
    public sealed class SeedBag
    {
        private readonly SeedTable _table;
        private readonly System.Random _rand;

        // key = slot * 512 + (mask + 1). mask = -1 은 "가리지 않음" 자리.
        private readonly Dictionary<int, int[]> _order = new Dictionary<int, int[]>();
        private readonly Dictionary<int, int> _cursor = new Dictionary<int, int>();

        /// <summary>fun 상위 몇 %만 쓸지. 1f = 전부. 0.35f = 화려한 궤적만.</summary>
        public float TopFunFraction = 1f;

        public SeedBag(SeedTable table, int shuffleSeed = 0)
        {
            _table = table ? table : throw new ArgumentNullException(nameof(table));
            _rand = shuffleSeed == 0 ? new System.Random() : new System.Random(shuffleSeed);
        }

        /// <summary>적중 패턴을 가리지 않고 그 슬롯에서 아무거나 하나.</summary>
        public bool TryTake(int slot, out uint seed) => TryTake(slot, -1, out seed);

        /// <param name="mask">특수 핀 적중 패턴. -1 이면 가리지 않는다.</param>
        public bool TryTake(int slot, int mask, out uint seed)
        {
            seed = 0u;
            if (slot < 0 || slot >= _table.SlotCount) return false;

            var pool = _table.pools[slot];
            if (pool.Count == 0) return false;

            int key = slot * 512 + (mask + 1);

            if (!_cursor.TryGetValue(key, out int cur) ||
                !_order.TryGetValue(key, out int[] order) ||
                cur >= order.Length)
            {
                order = BuildOrder(pool, mask);
                if (order.Length == 0) return false;      // 그 조합의 궤적이 아예 없다
                _order[key] = order;
                _cursor[key] = 0;
                cur = 0;
            }

            seed = pool.seeds[order[cur]];
            _cursor[key] = cur + 1;
            return true;
        }

        /// <summary>그 (슬롯, 패턴) 조합에 쓸 수 있는 궤적이 있는지. 폴백 판단에 쓴다.</summary>
        public bool HasSeeds(int slot, int mask)
        {
            if (slot < 0 || slot >= _table.SlotCount) return false;
            var pool = _table.pools[slot];
            if (mask < 0) return pool.Count > 0;

            for (int i = 0; i < pool.Count; i++)
                if (pool.MaskAt(i) == mask) return true;
            return false;
        }

        private int[] BuildOrder(SlotPool pool, int mask)
        {
            // pool 은 fun 내림차순이므로 앞쪽일수록 화려하다.
            // TopFunFraction 은 '패턴 필터를 통과한 것들' 중에서의 상위 비율로 적용한다.
            var candidates = new List<int>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                if (mask < 0 || pool.MaskAt(i) == mask) candidates.Add(i);

            if (candidates.Count == 0) return Array.Empty<int>();

            int usable = Mathf.Max(1, Mathf.RoundToInt(candidates.Count * Mathf.Clamp01(TopFunFraction)));
            var idx = candidates.GetRange(0, usable).ToArray();

            for (int i = idx.Length - 1; i > 0; i--)
            {
                int j = _rand.Next(i + 1);
                (idx[i], idx[j]) = (idx[j], idx[i]);
            }
            return idx;
        }
    }
}
