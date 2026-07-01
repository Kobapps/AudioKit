namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// Deterministic random source seam. Core never touches <c>UnityEngine.Random</c> so all
    /// selection logic is reproducible and headless-testable. Implementations must be allocation
    /// free per call.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Next raw 32-bit value.</summary>
        uint NextUInt();

        /// <summary>Uniform integer in [0, maxExclusive). Returns 0 when maxExclusive &lt;= 1.</summary>
        int NextInt(int maxExclusive);

        /// <summary>Uniform float in [0, 1).</summary>
        float NextFloat01();

        /// <summary>Uniform float in [min, max].</summary>
        float NextRange(float min, float max);
    }

    /// <summary>
    /// Tiny, fast, allocation-free xorshift32 generator. Deterministic given a seed. Not for
    /// cryptography — for variation selection and parameter jitter only.
    /// </summary>
    public sealed class XorShiftRandom : IRandom
    {
        private uint _state;

        public XorShiftRandom(uint seed = 0x9E3779B9u)
        {
            // Avoid the degenerate all-zero state.
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public void Reseed(uint seed)
        {
            _state = seed == 0u ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 1)
                return 0;
            // Lemire-style unbiased-enough reduction; cheap and good for audio variation.
            return (int)(((ulong)NextUInt() * (ulong)(uint)maxExclusive) >> 32);
        }

        public float NextFloat01()
        {
            // 24 mantissa bits → [0,1).
            return (NextUInt() >> 8) * (1.0f / 16777216.0f);
        }

        public float NextRange(float min, float max)
        {
            if (max <= min)
                return min;
            return min + NextFloat01() * (max - min);
        }
    }
}
