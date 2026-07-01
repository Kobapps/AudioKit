namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// Fixed-capacity accounting of live voices. Tracks which group/bus each voice belongs to, its
    /// age (acquire order) and current volume, and enforces per-group voice limits with three
    /// stealing policies plus per-bus limits and a global LRU reclaim when the pool is exhausted.
    ///
    /// Voices are addressed by a (slot, generation) pair; <see cref="Release"/> bumps the slot's
    /// generation so stale <c>SoundHandle</c>s are detected. All operations are allocation free.
    /// </summary>
    public sealed class VoiceTable
    {
        private bool[] _active;
        private int[] _generation;
        private int[] _group;
        private int[] _bus;
        private int[] _order;   // acquire order; larger = newer
        private float[] _volume;

        private int[] _groupCount;
        private int[] _busCount;

        private int[] _free;
        private int _freeTop;

        private int _capacity;
        private int _orderCounter;

        public int Capacity => _capacity;

        public void Init(int capacity, int groupCount, int busCount)
        {
            if (capacity < 1) capacity = 1;
            _capacity = capacity;
            _active = new bool[capacity];
            _generation = new int[capacity];
            _group = new int[capacity];
            _bus = new int[capacity];
            _order = new int[capacity];
            _volume = new float[capacity];
            _free = new int[capacity];
            _groupCount = new int[groupCount < 0 ? 0 : groupCount];
            _busCount = new int[busCount < 0 ? 0 : busCount];
            Reset();
        }

        public void Reset()
        {
            _freeTop = 0;
            _orderCounter = 0;
            for (int i = 0; i < _capacity; i++)
            {
                _active[i] = false;
                _group[i] = -1;
                _bus[i] = -1;
                _order[i] = 0;
                _volume[i] = 0f;
                // generation is intentionally preserved across Reset so old handles stay invalid,
                // but bumped to be safe.
                _generation[i]++;
                _free[_freeTop++] = _capacity - 1 - i; // fill so slot 0 is popped first
            }
            for (int i = 0; i < _groupCount.Length; i++) _groupCount[i] = 0;
            for (int i = 0; i < _busCount.Length; i++) _busCount[i] = 0;
        }

        /// <summary>Grow the per-group voice counters to accommodate dynamically registered groups.</summary>
        public void EnsureGroupCapacity(int groups)
        {
            if (_groupCount != null && _groupCount.Length >= groups) return;
            var bigger = new int[groups];
            if (_groupCount != null)
                System.Array.Copy(_groupCount, bigger, _groupCount.Length);
            _groupCount = bigger;
        }

        public int GroupVoiceCount(int group) =>
            (uint)group < (uint)_groupCount.Length ? _groupCount[group] : 0;

        public int BusVoiceCount(int bus) =>
            (uint)bus < (uint)_busCount.Length ? _busCount[bus] : 0;

        public int Generation(int slot) => (uint)slot < (uint)_capacity ? _generation[slot] : -1;

        public bool IsActive(int slot) => (uint)slot < (uint)_capacity && _active[slot];

        public bool IsValid(int slot, int generation) =>
            (uint)slot < (uint)_capacity && _active[slot] && _generation[slot] == generation;

        public int GroupOf(int slot) => (uint)slot < (uint)_capacity ? _group[slot] : -1;
        public int BusOf(int slot) => (uint)slot < (uint)_capacity ? _bus[slot] : -1;

        public void SetVolume(int slot, float volume)
        {
            if ((uint)slot < (uint)_capacity && _active[slot])
                _volume[slot] = volume;
        }

        public float GetVolume(int slot) =>
            (uint)slot < (uint)_capacity && _active[slot] ? _volume[slot] : 0f;

        /// <summary>
        /// Try to acquire a voice slot for a group/bus. On success <paramref name="slot"/> is the
        /// slot to bind, and <paramref name="stolenSlot"/> is the same slot if an existing voice was
        /// reclaimed (so the caller can stop/reconfigure it), or -1 if a fresh slot was used.
        /// Returns false only when the group's policy is <see cref="VoiceStealPolicy.Reject"/> and
        /// the group is full, or there are no voices anywhere to reclaim.
        /// </summary>
        public bool TryAcquire(
            int group, int bus, int groupLimit, int busLimit,
            VoiceStealPolicy policy, float incomingVolume,
            out int slot, out int stolenSlot)
        {
            slot = -1;
            stolenSlot = -1;

            if (groupLimit < 1) groupLimit = 1;

            bool groupFull = (uint)group < (uint)_groupCount.Length && _groupCount[group] >= groupLimit;
            bool busFull = bus >= 0 && busLimit > 0 &&
                           (uint)bus < (uint)_busCount.Length && _busCount[bus] >= busLimit;

            if (groupFull)
            {
                if (policy == VoiceStealPolicy.Reject)
                    return false;

                int victim = policy == VoiceStealPolicy.StealQuietest
                    ? QuietestInGroup(group)
                    : OldestInGroup(group);

                if (victim < 0)
                    return false;

                Repurpose(victim, group, bus, incomingVolume);
                slot = victim;
                stolenSlot = victim;
                return true;
            }

            if (busFull)
            {
                int victim = OldestInBus(bus);
                if (victim < 0)
                    return false;

                Repurpose(victim, group, bus, incomingVolume);
                slot = victim;
                stolenSlot = victim;
                return true;
            }

            // Under all limits: prefer a fresh slot, else LRU-reclaim the globally oldest voice.
            if (_freeTop > 0)
            {
                int s = _free[--_freeTop];
                Activate(s, group, bus, incomingVolume);
                slot = s;
                stolenSlot = -1;
                return true;
            }

            int oldest = GlobalOldest();
            if (oldest < 0)
                return false;

            Repurpose(oldest, group, bus, incomingVolume);
            slot = oldest;
            stolenSlot = oldest;
            return true;
        }

        /// <summary>Release a voice. Bumps generation and returns the slot to the free list.</summary>
        public void Release(int slot)
        {
            if ((uint)slot >= (uint)_capacity || !_active[slot])
                return;

            int g = _group[slot];
            int b = _bus[slot];
            if ((uint)g < (uint)_groupCount.Length && _groupCount[g] > 0) _groupCount[g]--;
            if ((uint)b < (uint)_busCount.Length && _busCount[b] > 0) _busCount[b]--;

            _active[slot] = false;
            _group[slot] = -1;
            _bus[slot] = -1;
            _volume[slot] = 0f;
            _generation[slot]++;
            _free[_freeTop++] = slot;
        }

        private void Activate(int slot, int group, int bus, float volume)
        {
            _active[slot] = true;
            _group[slot] = group;
            _bus[slot] = bus;
            _volume[slot] = volume;
            _order[slot] = ++_orderCounter;
            if ((uint)group < (uint)_groupCount.Length) _groupCount[group]++;
            if ((uint)bus < (uint)_busCount.Length) _busCount[bus]++;
        }

        private void Repurpose(int slot, int group, int bus, float volume)
        {
            int oldG = _group[slot];
            int oldB = _bus[slot];
            if ((uint)oldG < (uint)_groupCount.Length && _groupCount[oldG] > 0) _groupCount[oldG]--;
            if ((uint)oldB < (uint)_busCount.Length && _busCount[oldB] > 0) _busCount[oldB]--;

            _generation[slot]++; // invalidate the stolen voice's handle
            _active[slot] = true;
            _group[slot] = group;
            _bus[slot] = bus;
            _volume[slot] = volume;
            _order[slot] = ++_orderCounter;
            if ((uint)group < (uint)_groupCount.Length) _groupCount[group]++;
            if ((uint)bus < (uint)_busCount.Length) _busCount[bus]++;
        }

        private int OldestInGroup(int group)
        {
            int best = -1;
            int bestOrder = int.MaxValue;
            for (int i = 0; i < _capacity; i++)
            {
                if (_active[i] && _group[i] == group && _order[i] < bestOrder)
                {
                    bestOrder = _order[i];
                    best = i;
                }
            }
            return best;
        }

        private int QuietestInGroup(int group)
        {
            int best = -1;
            float bestVol = float.MaxValue;
            for (int i = 0; i < _capacity; i++)
            {
                if (_active[i] && _group[i] == group && _volume[i] < bestVol)
                {
                    bestVol = _volume[i];
                    best = i;
                }
            }
            return best;
        }

        private int OldestInBus(int bus)
        {
            int best = -1;
            int bestOrder = int.MaxValue;
            for (int i = 0; i < _capacity; i++)
            {
                if (_active[i] && _bus[i] == bus && _order[i] < bestOrder)
                {
                    bestOrder = _order[i];
                    best = i;
                }
            }
            return best;
        }

        private int GlobalOldest()
        {
            int best = -1;
            int bestOrder = int.MaxValue;
            for (int i = 0; i < _capacity; i++)
            {
                if (_active[i] && _order[i] < bestOrder)
                {
                    bestOrder = _order[i];
                    best = i;
                }
            }
            return best;
        }
    }
}
