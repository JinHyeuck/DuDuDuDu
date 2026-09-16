using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinball
{
    [Serializable]
    public sealed class SlotPool
    {
        /// <summary>fun 내림차순 정렬. 앞쪽이 화려한 궤적.</summary>
        public uint[] seeds = Array.Empty<uint>();
        public float[] fun = Array.Empty<float>();

        /// <summary>seeds 와 인덱스가 대응한다. 특수 핀에 몇 번 맞은 궤적인지(표시용).</summary>
        public byte[] specialHits = Array.Empty<byte>();

        /// <summary>
        /// seeds 와 인덱스가 대응한다. 어느 특수 태그를 맞았는지의 비트마스크.
        /// 태그별 목표 확률을 맞추는 데 쓰이는 핵심 데이터.
        /// </summary>
        public int[] hitMask = Array.Empty<int>();

        /// <summary>
        /// Keep Per Slot 으로 잘라내기 '전' 에 이 슬롯으로 떨어진 시드 개수.
        /// 자연 분포는 반드시 이 값으로 계산해야 한다. seeds.Length 로 계산하면
        /// 모든 슬롯이 Keep 값으로 잘려서 전부 같은 비율로 보이는 착시가 생긴다.
        /// </summary>
        public int sampled;

        public int Count => seeds.Length;

        public int MaskAt(int i) =>
            hitMask != null && i < hitMask.Length ? hitMask[i] : 0;
    }

    [CreateAssetMenu(fileName = "SeedTable", menuName = "Pinball/Seed Table")]
    public sealed class SeedTable : ScriptableObject
    {
        public PinballBoard board;

        [Tooltip("베이크 당시 판의 지문. 현재 판과 다르면 이 테이블은 무효다.")]
        public string bakedLayoutHash;

        public int sampledCount;
        public int discardedCount;
        public SlotPool[] pools = Array.Empty<SlotPool>();

        public bool IsStale => board == null || board.LayoutHash() != bakedLayoutHash;

        public int SlotCount => pools.Length;

        /// <summary>슬롯 안에서 적중 패턴(마스크)별 시드 개수. 진단과 가중치 계산의 입력.</summary>
        public Dictionary<int, int> MaskHistogram(int slot)
        {
            var hist = new Dictionary<int, int>();
            if (slot < 0 || slot >= pools.Length) return hist;

            var p = pools[slot];
            for (int i = 0; i < p.Count; i++)
            {
                int m = p.MaskAt(i);
                hist.TryGetValue(m, out int n);
                hist[m] = n + 1;
            }
            return hist;
        }

        /// <summary>그 슬롯에서 특정 태그 비트를 맞춘 시드가 몇 개인지.</summary>
        public int CountWithBit(int slot, int bit)
        {
            if (slot < 0 || slot >= pools.Length) return 0;
            var p = pools[slot];
            int mask = 1 << bit, n = 0;
            for (int i = 0; i < p.Count; i++)
                if ((p.MaskAt(i) & mask) != 0) n++;
            return n;
        }

        /// <summary>물리 판이 자연적으로 만들어내는 분포(참고용). 지급 확률과는 무관.</summary>
        public float NaturalRatio(int slot)
        {
            int valid = sampledCount - discardedCount;
            if (valid <= 0) return 0f;

            // sampled 가 0 이면 이 필드가 없던 시절에 구운 오래된 테이블이다.
            int n = pools[slot].sampled > 0 ? pools[slot].sampled : pools[slot].Count;
            return n / (float)valid;
        }

        /// <summary>자연 분포를 믿을 수 있는 테이블인지. false 면 재베이크가 필요하다.</summary>
        public bool HasSampleCounts
        {
            get
            {
                foreach (var p in pools) if (p.sampled <= 0) return false;
                return pools.Length > 0;
            }
        }
    }
}
