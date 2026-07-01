using System;

namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// Mutable per-group selection cursor. Created once per group at bake time and reused on every
    /// play, so selection allocates nothing on the hot path. Holds the bookkeeping each play mode
    /// needs (last index, cycle cursor, LRU ordinals).
    /// </summary>
    public sealed class SelectionState
    {
        public int LastIndex = -1;
        public int Cursor = -1;
        public bool Initialized;

        // LRU ordinals for PlayMode.Oldest. Length tracks the group's variation count.
        public int[] LastUsedOrdinal = Array.Empty<int>();
        public int OrdinalCounter;

        public void EnsureCapacity(int count)
        {
            if (LastUsedOrdinal.Length < count)
                LastUsedOrdinal = new int[count];
        }

        public void Reset()
        {
            LastIndex = -1;
            Cursor = -1;
            Initialized = false;
            OrdinalCounter = 0;
            for (int i = 0; i < LastUsedOrdinal.Length; i++)
                LastUsedOrdinal[i] = 0;
        }
    }

    /// <summary>
    /// Pure variation-selection logic. Returns the chosen variation index in the local range
    /// [0, count). Deterministic given the injected <see cref="IRandom"/> and the persisted
    /// <see cref="SelectionState"/>. No allocations, no LINQ, no closures.
    /// </summary>
    public static class VariationSelector
    {
        /// <param name="mode">The group's play mode.</param>
        /// <param name="count">Number of variations in the group.</param>
        /// <param name="weights">
        /// Per-variation weights for <see cref="PlayMode.Weighted"/>; may be null/empty for other modes.
        /// Length must be &gt;= <paramref name="count"/> when used.
        /// </param>
        /// <param name="state">Persisted per-group cursor (mutated).</param>
        /// <param name="rng">Deterministic random source.</param>
        /// <returns>Local variation index, or -1 when the group has no variations.</returns>
        public static int Select(PlayMode mode, int count, float[] weights, SelectionState state, IRandom rng)
        {
            if (count <= 0)
                return -1;
            if (count == 1)
            {
                Stamp(state, 0);
                return 0;
            }

            int index;
            switch (mode)
            {
                case PlayMode.Random:
                    index = rng.NextInt(count);
                    break;

                case PlayMode.RandomNoImmediateRepeat:
                    index = NoImmediateRepeat(count, state.LastIndex, rng);
                    break;

                case PlayMode.RoundRobin:
                    if (!state.Initialized)
                    {
                        // Randomize the starting phase once, then cycle deterministically.
                        state.Cursor = rng.NextInt(count) - 1;
                        state.Initialized = true;
                    }
                    state.Cursor = (state.Cursor + 1) % count;
                    index = state.Cursor;
                    break;

                case PlayMode.Sequential:
                    state.Cursor = (state.Cursor + 1) % count; // Cursor starts at -1 → first play is 0.
                    index = state.Cursor;
                    break;

                case PlayMode.Weighted:
                    index = Weighted(count, weights, rng);
                    break;

                case PlayMode.Oldest:
                    index = Oldest(count, state);
                    break;

                default:
                    index = rng.NextInt(count);
                    break;
            }

            Stamp(state, index);
            return index;
        }

        private static void Stamp(SelectionState state, int index)
        {
            state.LastIndex = index;
            state.EnsureCapacity(index + 1);
            if (index < state.LastUsedOrdinal.Length)
                state.LastUsedOrdinal[index] = ++state.OrdinalCounter;
        }

        private static int NoImmediateRepeat(int count, int last, IRandom rng)
        {
            if (last < 0 || last >= count)
                return rng.NextInt(count);
            // Draw uniformly over the count-1 candidates that are not `last`, in one roll.
            int pick = rng.NextInt(count - 1);
            if (pick >= last)
                pick++;
            return pick;
        }

        private static int Weighted(int count, float[] weights, IRandom rng)
        {
            if (weights == null || weights.Length < count)
                return rng.NextInt(count);

            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = weights[i];
                if (w > 0f)
                    total += w;
            }

            if (total <= 0f)
                return rng.NextInt(count); // all weights zero → uniform fallback

            float r = rng.NextFloat01() * total;
            float acc = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = weights[i];
                if (w <= 0f)
                    continue;
                acc += w;
                if (r < acc)
                    return i;
            }
            return count - 1; // floating-point guard
        }

        private static int Oldest(int count, SelectionState state)
        {
            state.EnsureCapacity(count);
            int oldest = 0;
            int oldestOrdinal = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int ord = state.LastUsedOrdinal[i];
                if (ord < oldestOrdinal)
                {
                    oldestOrdinal = ord;
                    oldest = i;
                }
            }
            return oldest;
        }
    }
}
