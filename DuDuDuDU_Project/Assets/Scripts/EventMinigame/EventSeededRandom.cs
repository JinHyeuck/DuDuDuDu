namespace OJ.EventMinigame
{
    /// <summary>
    /// 시드 하나로 같은 수열을 돌려주는 작은 난수원(xorshift32).
    ///
    /// <b>왜 <c>UnityEngine.Random</c> 이 아닌가.</b> 보물찾기 판은 "시드 + 판 번호"만 저장하고 다시 만들어 낸다.
    /// 엔진 난수는 버전·플랫폼에 따라 수열이 달라질 수 있어 저장한 판이 다른 판으로 복원된다.
    /// 이 난수원은 헤드리스 러너와 기기에서 똑같이 돈다.
    /// </summary>
    public struct EventSeededRandom
    {
        private uint state;

        public EventSeededRandom(int seed)
        {
            uint s = unchecked((uint)seed) ^ 0x9E3779B9u;
            state = s == 0u ? 0x01234567u : s;

            // 시드가 비슷할 때 첫 값이 붙어 나오지 않게 두 번 돌려 둔다.
            Next();
            Next();
        }

        public uint Next()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>0 이상 <paramref name="maxExclusive"/> 미만. 1 이하가 들어오면 0.</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
                return 0;

            return (int)(Next() % (uint)maxExclusive);
        }
    }
}
