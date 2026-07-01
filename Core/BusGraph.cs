namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// Computes effective linear gain per bus from each bus's smoothed volume, mute/solo state and
    /// ducking gain. Flat (groups → bus → master); no nested buses. Solo is global: if any bus is
    /// soloed, non-soloed buses are silenced. Operates over caller-owned parallel arrays so it
    /// allocates nothing and can be ticked every frame.
    /// </summary>
    public sealed class BusGraph
    {
        private float[] _volume;   // smoothed current volume per bus
        private float[] _duckGain; // current duck multiplier per bus (1 = no duck)
        private bool[] _muted;
        private bool[] _soloed;
        private float _masterVolume = 1f;
        private int _count;
        private int _soloCount; // cached: number of soloed buses (avoids rescanning per gain query)

        public int Count => _count;
        public float MasterVolume { get => _masterVolume; set => _masterVolume = value < 0f ? 0f : value; }

        /// <summary>(Re)bind the backing arrays. All arrays must be at least <paramref name="count"/> long.</summary>
        public void Bind(float[] volume, float[] duckGain, bool[] muted, bool[] soloed, int count)
        {
            _volume = volume;
            _duckGain = duckGain;
            _muted = muted;
            _soloed = soloed;
            _count = count;
            RefreshSolo();
        }

        /// <summary>Recompute the cached solo count from the bound array (after external array writes).</summary>
        public void RefreshSolo()
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
                if (_soloed[i]) n++;
            _soloCount = n;
        }

        public void SetVolume(int bus, float v)
        {
            if ((uint)bus < (uint)_count) _volume[bus] = v < 0f ? 0f : v;
        }

        public float GetVolume(int bus) => (uint)bus < (uint)_count ? _volume[bus] : 1f;

        public void SetDuckGain(int bus, float g)
        {
            if ((uint)bus < (uint)_count) _duckGain[bus] = g;
        }

        public void SetMuted(int bus, bool muted)
        {
            if ((uint)bus < (uint)_count) _muted[bus] = muted;
        }

        public void SetSoloed(int bus, bool soloed)
        {
            if ((uint)bus >= (uint)_count || _soloed[bus] == soloed) return;
            _soloed[bus] = soloed;
            _soloCount += soloed ? 1 : -1;
            if (_soloCount < 0) _soloCount = 0;
        }

        public bool AnySoloed() => _soloCount > 0;

        /// <summary>
        /// Effective linear gain for <paramref name="bus"/>, including master, mute, solo and duck.
        /// Pass bus = -1 for the implicit master bus (master volume only).
        /// </summary>
        public float EffectiveGain(int bus)
        {
            if (bus < 0)
                return _masterVolume; // implicit master bus

            if ((uint)bus >= (uint)_count)
                return _masterVolume;

            if (_muted[bus])
                return 0f;

            if (AnySoloed() && !_soloed[bus])
                return 0f;

            float g = _volume[bus] * _duckGain[bus] * _masterVolume;
            return g < 0f ? 0f : g;
        }
    }
}
