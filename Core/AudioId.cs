using System;

namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// A stable 32-bit identifier for a group, bus or custom event, derived from a name via
    /// FNV-1a. Runtime lookups hash once at bake time and compare integers thereafter — never
    /// strings on the hot path. The originating name is intentionally NOT stored here so the
    /// struct stays a single <see cref="uint"/> (zero-alloc, blittable). Editor/debug name
    /// mapping is kept separately in the Unity layer.
    /// </summary>
    [Serializable]
    public readonly struct AudioId : IEquatable<AudioId>
    {
        public readonly uint Value;

        public AudioId(uint value)
        {
            Value = value;
        }

        /// <summary>True for the reserved "none" id (hash 0).</summary>
        public bool IsNone => Value == 0u;

        public static readonly AudioId None = new AudioId(0u);

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// FNV-1a hash of <paramref name="name"/>. Case-sensitive and culture-invariant.
        /// Returns <see cref="None"/> for null/empty so unset names collapse to the none id.
        /// </summary>
        public static AudioId From(string name)
        {
            if (string.IsNullOrEmpty(name))
                return None;

            uint hash = FnvOffsetBasis;
            for (int i = 0; i < name.Length; i++)
            {
                hash ^= name[i];
                hash *= FnvPrime;
            }

            // Never collide with the reserved none id.
            if (hash == 0u)
                hash = FnvPrime;

            return new AudioId(hash);
        }

        public bool Equals(AudioId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is AudioId other && Value == other.Value;

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => "AudioId(0x" + Value.ToString("X8") + ")";

        public static bool operator ==(AudioId a, AudioId b) => a.Value == b.Value;

        public static bool operator !=(AudioId a, AudioId b) => a.Value != b.Value;
    }
}
