namespace Pinball
{
    /// <summary>
    /// xorshift32 + murmur3 finalizer.
    /// UnityEngine.Random / System.Random 은 구현이 바뀔 수 있으므로 절대 쓰지 않는다.
    /// 이 구조체만이 시뮬레이션의 유일한 무작위성 원천이다.
    /// </summary>
    public struct PinballRng
    {
        private uint _s;

        public PinballRng(uint seed)
        {
            // 작은 정수 시드(1,2,3...)도 충분히 흩어지도록 finalizer를 한 번 먹인다.
            _s = Mix(seed == 0u ? 0x9E3779B9u : seed);
        }

        public static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return x == 0u ? 0x9E3779B9u : x;
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint x = _s;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _s = x;
                return x;
            }
        }

        /// <summary>[0, 1) — 24bit 정밀도. 나눗셈 대신 곱셈이라 결과가 안정적이다.</summary>
        public float NextFloat()
        {
            return (NextUInt() >> 8) * (1f / 16777216f);
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        public int RangeInt(int minInclusive, int maxExclusive)
        {
            int span = maxExclusive - minInclusive;
            if (span <= 0) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)span);
        }
    }
}
