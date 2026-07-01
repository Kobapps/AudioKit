using System.Collections.Generic;

namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// The headless heart of AudioKit. Owns the baked group/bus/variation tables and the runtime
    /// state (selection cursors, voice accounting, bus volume fades and ducking). It makes every
    /// playback decision as pure numbers; the Unity layer is a thin adapter that feeds it
    /// <c>deltaTime</c> and applies its outputs to <c>AudioSource</c>s.
    ///
    /// Contains zero <c>UnityEngine</c> references and allocates only at <see cref="Configure"/>
    /// time, never on the play/tick hot path.
    /// </summary>
    public sealed class AudioKitCoreEngine
    {
        private readonly IRandom _rng;

        private GroupData[] _groups = System.Array.Empty<GroupData>();
        private BusData[] _buses = System.Array.Empty<BusData>();
        private VariationData[] _variations = System.Array.Empty<VariationData>();

        private float[][] _groupWeights = System.Array.Empty<float[]>();
        private SelectionState[] _selection = System.Array.Empty<SelectionState>();

        // Per-group runtime volume / mute (applied on top of variation + group-random + bus gain).
        private float[] _groupVolume = System.Array.Empty<float>();
        private bool[] _groupMuted = System.Array.Empty<bool>();

        // Free lists for dynamic (runtime) group registration. Group/variation indices must stay
        // stable while voices are live, so unregister frees slots into these pools for reuse rather
        // than compacting the arrays.
        private readonly Stack<int> _freeGroupSlots = new Stack<int>();
        private readonly Dictionary<int, Stack<int>> _freeVarRanges = new Dictionary<int, Stack<int>>();

        private readonly Dictionary<uint, int> _groupIndex = new Dictionary<uint, int>(64);
        private readonly Dictionary<uint, int> _busIndex = new Dictionary<uint, int>(16);

        private readonly VoiceTable _voices = new VoiceTable();
        private readonly BusGraph _busGraph = new BusGraph();

        // Per-bus runtime state (shared by reference with the BusGraph).
        private float[] _busVolume = System.Array.Empty<float>();
        private float[] _busDuckGain = System.Array.Empty<float>();
        private bool[] _busMuted = System.Array.Empty<bool>();
        private bool[] _busSoloed = System.Array.Empty<bool>();
        private FadeEnvelope[] _busFade = System.Array.Empty<FadeEnvelope>();
        private DuckEnvelope[] _busDuck = System.Array.Empty<DuckEnvelope>();

        public AudioKitCoreEngine(IRandom rng = null)
        {
            _rng = rng ?? new XorShiftRandom();
        }

        public int GroupCount => _groups.Length;
        public int BusCount => _buses.Length;

        /// <summary>Total variation slots allocated (including holes from unregistered groups).</summary>
        public int VariationTotal => _variations.Length;
        /// <summary>Total group slots allocated (including holes from unregistered groups).</summary>
        public int GroupTotal => _groups.Length;
        public int VoiceCapacity => _voices.Capacity;
        public VoiceTable Voices => _voices;
        public BusGraph Buses => _busGraph;

        public float MasterVolume
        {
            get => _busGraph.MasterVolume;
            set => _busGraph.MasterVolume = value;
        }

        /// <summary>
        /// Bake the design-time tables into runtime state. <paramref name="variations"/> is the flat
        /// array indexed by each group's <see cref="GroupData.VariationOffset"/>/Count slice.
        /// </summary>
        public void Configure(GroupData[] groups, BusData[] buses, VariationData[] variations, int voiceCapacity)
        {
            _groups = groups ?? System.Array.Empty<GroupData>();
            _buses = buses ?? System.Array.Empty<BusData>();
            _variations = variations ?? System.Array.Empty<VariationData>();

            int groupN = _groups.Length;
            int busN = _buses.Length;

            _freeGroupSlots.Clear();
            _freeVarRanges.Clear();

            // Index maps.
            _groupIndex.Clear();
            for (int g = 0; g < groupN; g++)
                _groupIndex[_groups[g].Id.Value] = g;
            _busIndex.Clear();
            for (int b = 0; b < busN; b++)
                _busIndex[_buses[b].Id.Value] = b;

            // Per-group weights + selection state.
            _groupWeights = new float[groupN][];
            _selection = new SelectionState[groupN];
            for (int g = 0; g < groupN; g++)
            {
                int count = _groups[g].VariationCount;
                var w = new float[count < 0 ? 0 : count];
                for (int i = 0; i < w.Length; i++)
                {
                    int vi = _groups[g].VariationOffset + i;
                    w[i] = (uint)vi < (uint)_variations.Length ? _variations[vi].Weight : 1f;
                }
                _groupWeights[g] = w;
                var s = new SelectionState();
                s.EnsureCapacity(w.Length);
                _selection[g] = s;
            }

            _groupVolume = new float[groupN];
            _groupMuted = new bool[groupN];
            for (int g = 0; g < groupN; g++) _groupVolume[g] = 1f;

            // Bus runtime arrays.
            _busVolume = new float[busN];
            _busDuckGain = new float[busN];
            _busMuted = new bool[busN];
            _busSoloed = new bool[busN];
            _busFade = new FadeEnvelope[busN];
            _busDuck = new DuckEnvelope[busN];
            for (int b = 0; b < busN; b++)
            {
                _busVolume[b] = _buses[b].Volume;
                _busDuckGain[b] = 1f;
                _busMuted[b] = _buses[b].Muted;
                _busSoloed[b] = _buses[b].Soloed;
                _busFade[b].SnapTo(_buses[b].Volume);
                _busDuck[b].Reset();
                _busDuck[b].Configure(_buses[b].Duck);
            }

            _busGraph.Bind(_busVolume, _busDuckGain, _busMuted, _busSoloed, busN);

            int cap = voiceCapacity < 1 ? 1 : voiceCapacity;
            _voices.Init(cap, groupN, busN);
        }

        public bool TryGetGroupIndex(AudioId id, out int index) => _groupIndex.TryGetValue(id.Value, out index);
        public bool TryGetBusIndex(AudioId id, out int index) => _busIndex.TryGetValue(id.Value, out index);

        public int GetGroupIndex(AudioId id) => _groupIndex.TryGetValue(id.Value, out int i) ? i : -1;
        public int GetBusIndex(AudioId id) => _busIndex.TryGetValue(id.Value, out int i) ? i : -1;

        public GroupData GetGroup(int index) => (uint)index < (uint)_groups.Length ? _groups[index] : GroupData.Default;
        public BusData GetBus(int index) => (uint)index < (uint)_buses.Length ? _buses[index] : BusData.Default;

        // --- Dynamic group registration --------------------------------------------------------

        /// <summary>
        /// Register a group at runtime. The group's <see cref="GroupData.VariationOffset"/>/Count are
        /// assigned by the engine (reusing a freed variation range of the same size when available).
        /// Returns the stable group index, or -1 if the id is empty or already registered. The Unity
        /// layer must grow its parallel clip arrays to <see cref="VariationTotal"/> and write clips at
        /// the returned group's variation range.
        /// </summary>
        public int RegisterGroup(GroupData group, VariationData[] variations)
        {
            if (group.Id.IsNone || _groupIndex.ContainsKey(group.Id.Value))
                return -1;

            int n = variations != null ? variations.Length : 0;

            // Allocate a variation range: reuse a freed range of the same size, else append.
            int varOffset;
            if (n > 0 && _freeVarRanges.TryGetValue(n, out var ranges) && ranges.Count > 0)
            {
                varOffset = ranges.Pop();
            }
            else
            {
                varOffset = _variations.Length;
                if (n > 0) System.Array.Resize(ref _variations, varOffset + n);
            }
            for (int i = 0; i < n; i++)
                _variations[varOffset + i] = variations[i];

            // Allocate a group slot: reuse a freed slot, else append.
            int slot;
            if (_freeGroupSlots.Count > 0)
            {
                slot = _freeGroupSlots.Pop();
            }
            else
            {
                slot = _groups.Length;
                System.Array.Resize(ref _groups, slot + 1);
                System.Array.Resize(ref _groupWeights, slot + 1);
                System.Array.Resize(ref _selection, slot + 1);
                System.Array.Resize(ref _groupVolume, slot + 1);
                System.Array.Resize(ref _groupMuted, slot + 1);
            }

            group.VariationOffset = varOffset;
            group.VariationCount = n;
            _groups[slot] = group;
            _groupVolume[slot] = 1f; // reset runtime volume/mute for a (possibly reused) slot
            _groupMuted[slot] = false;

            float[] w = _groupWeights[slot];
            if (w == null || w.Length < n) { w = new float[n]; _groupWeights[slot] = w; }
            for (int i = 0; i < n; i++) w[i] = variations[i].Weight;

            SelectionState sel = _selection[slot];
            if (sel == null) { sel = new SelectionState(); _selection[slot] = sel; }
            sel.Reset();
            sel.EnsureCapacity(n);

            _groupIndex[group.Id.Value] = slot;
            _voices.EnsureGroupCapacity(_groups.Length);
            return slot;
        }

        /// <summary>
        /// Unregister a previously registered group. Frees its slot and variation range for reuse.
        /// Callers must stop the group's live voices first (indices are only safe to recycle once no
        /// voice references them).
        /// </summary>
        public bool UnregisterGroup(AudioId id)
        {
            if (id.IsNone || !_groupIndex.TryGetValue(id.Value, out int slot))
                return false;

            int n = _groups[slot].VariationCount;
            int off = _groups[slot].VariationOffset;
            if (n > 0)
            {
                if (!_freeVarRanges.TryGetValue(n, out var ranges))
                {
                    ranges = new Stack<int>();
                    _freeVarRanges[n] = ranges;
                }
                ranges.Push(off);
            }

            _groupIndex.Remove(id.Value);

            var cleared = GroupData.Default;
            cleared.Id = AudioId.None;
            cleared.VariationOffset = 0;
            cleared.VariationCount = 0;
            _groups[slot] = cleared;
            if ((uint)slot < (uint)_groupVolume.Length) { _groupVolume[slot] = 1f; _groupMuted[slot] = false; }
            _selection[slot]?.Reset();
            _freeGroupSlots.Push(slot);
            return true;
        }

        public bool IsGroupRegistered(AudioId id) => !id.IsNone && _groupIndex.ContainsKey(id.Value);

        /// <summary>Choose a variation index within group <paramref name="groupIndex"/> (local 0..count).</summary>
        public int SelectVariation(int groupIndex)
        {
            if ((uint)groupIndex >= (uint)_groups.Length)
                return -1;
            ref GroupData g = ref _groups[groupIndex];
            return VariationSelector.Select(g.PlayMode, g.VariationCount, _groupWeights[groupIndex], _selection[groupIndex], _rng);
        }

        /// <summary>The flat variation index for a (group, local variation) pair.</summary>
        public int VariationGlobalIndex(int groupIndex, int localVariation)
        {
            if ((uint)groupIndex >= (uint)_groups.Length)
                return -1;
            if (localVariation < 0 || localVariation >= _groups[groupIndex].VariationCount)
                return -1;
            return _groups[groupIndex].VariationOffset + localVariation;
        }

        public VariationData GetVariation(int globalIndex) =>
            (uint)globalIndex < (uint)_variations.Length ? _variations[globalIndex] : VariationData.Default;

        // --- Per-group runtime volume / mute ---------------------------------------------------

        public void SetGroupVolume(int groupIndex, float volume)
        {
            if ((uint)groupIndex < (uint)_groupVolume.Length) _groupVolume[groupIndex] = volume < 0f ? 0f : volume;
        }

        public float GetGroupVolume(int groupIndex) =>
            (uint)groupIndex < (uint)_groupVolume.Length ? _groupVolume[groupIndex] : 1f;

        public void SetGroupMuted(int groupIndex, bool muted)
        {
            if ((uint)groupIndex < (uint)_groupMuted.Length) _groupMuted[groupIndex] = muted;
        }

        public bool IsGroupMuted(int groupIndex) =>
            (uint)groupIndex < (uint)_groupMuted.Length && _groupMuted[groupIndex];

        /// <summary>Combined per-group gain: 0 when muted, else the group's runtime volume.</summary>
        public float GroupGain(int groupIndex) =>
            (uint)groupIndex < (uint)_groupVolume.Length ? (_groupMuted[groupIndex] ? 0f : _groupVolume[groupIndex]) : 1f;

        /// <summary>Random group-level volume in [VolumeMin, VolumeMax].</summary>
        public float RandomGroupVolume(int groupIndex)
        {
            if ((uint)groupIndex >= (uint)_groups.Length) return 1f;
            ref GroupData g = ref _groups[groupIndex];
            return _rng.NextRange(g.VolumeMin, g.VolumeMax);
        }

        /// <summary>Random group-level pitch in [PitchMin, PitchMax].</summary>
        public float RandomGroupPitch(int groupIndex)
        {
            if ((uint)groupIndex >= (uint)_groups.Length) return 1f;
            ref GroupData g = ref _groups[groupIndex];
            return _rng.NextRange(g.PitchMin, g.PitchMax);
        }

        /// <summary>
        /// Try to reserve a voice slot for a play on <paramref name="groupIndex"/>. Honours the
        /// group's voice limit + steal policy and the target bus's voice limit.
        /// </summary>
        public bool TryAcquireVoice(int groupIndex, float incomingVolume, out int slot, out int generation, out int stolenSlot)
        {
            slot = -1; generation = -1; stolenSlot = -1;
            if ((uint)groupIndex >= (uint)_groups.Length)
                return false;

            ref GroupData g = ref _groups[groupIndex];
            int bus = g.BusIndex;
            int busLimit = (uint)bus < (uint)_buses.Length ? _buses[bus].VoiceLimit : 0;

            if (!_voices.TryAcquire(groupIndex, bus, g.VoiceLimit, busLimit, g.StealPolicy, incomingVolume, out slot, out stolenSlot))
                return false;

            generation = _voices.Generation(slot);
            return true;
        }

        public void ReleaseVoice(int slot) => _voices.Release(slot);
        public bool IsVoiceValid(int slot, int generation) => _voices.IsValid(slot, generation);
        public void SetVoiceVolume(int slot, float volume) => _voices.SetVolume(slot, volume);
        public int GroupVoiceCount(int groupIndex) => _voices.GroupVoiceCount(groupIndex);
        public int BusVoiceCount(int busIndex) => _voices.BusVoiceCount(busIndex);

        // --- Bus control -----------------------------------------------------------------------

        public void FadeBusVolume(int busIndex, float target, float seconds, FadeCurve curve = FadeCurve.Linear)
        {
            if ((uint)busIndex >= (uint)_buses.Length) return;
            _busFade[busIndex].FadeTo(target, seconds, curve);
            if (seconds <= 0f)
                _busVolume[busIndex] = target;
        }

        public void SetBusVolume(int busIndex, float target) => FadeBusVolume(busIndex, target, 0f);

        public float GetBusVolume(int busIndex) =>
            (uint)busIndex < (uint)_buses.Length ? _busVolume[busIndex] : 1f;

        public void SetBusMuted(int busIndex, bool muted)
        {
            if ((uint)busIndex < (uint)_buses.Length) _busMuted[busIndex] = muted;
        }

        public bool IsBusMuted(int busIndex) =>
            (uint)busIndex < (uint)_buses.Length && _busMuted[busIndex];

        public void SetBusSoloed(int busIndex, bool soloed)
        {
            // Route through BusGraph so its cached solo count stays in sync.
            if ((uint)busIndex < (uint)_buses.Length) _busGraph.SetSoloed(busIndex, soloed);
        }

        public bool IsBusSoloed(int busIndex) =>
            (uint)busIndex < (uint)_buses.Length && _busSoloed[busIndex];

        /// <summary>Effective linear gain for a bus (master, volume, mute, solo, duck). -1 = master.</summary>
        public float BusEffectiveGain(int busIndex) => _busGraph.EffectiveGain(busIndex);

        // --- Ducking ---------------------------------------------------------------------------

        public void AddDuckTrigger(int busIndex)
        {
            if ((uint)busIndex < (uint)_buses.Length) _busDuck[busIndex].AddTrigger();
        }

        public void RemoveDuckTrigger(int busIndex)
        {
            if ((uint)busIndex < (uint)_buses.Length) _busDuck[busIndex].RemoveTrigger();
        }

        public float BusDuckGain(int busIndex) =>
            (uint)busIndex < (uint)_buses.Length ? _busDuckGain[busIndex] : 1f;

        // --- Frame tick ------------------------------------------------------------------------

        /// <summary>Advance all bus volume fades and ducking envelopes by <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            int busN = _buses.Length;
            for (int b = 0; b < busN; b++)
            {
                _busVolume[b] = _busFade[b].Tick(dt);
                _busDuckGain[b] = _busDuck[b].Tick(dt);
            }
        }

        /// <summary>Reset all runtime state (voices, selection, bus fades/duck) without re-baking.</summary>
        public void ResetRuntime()
        {
            _voices.Reset();
            for (int g = 0; g < _selection.Length; g++)
                _selection[g].Reset();
            for (int b = 0; b < _buses.Length; b++)
            {
                _busVolume[b] = _buses[b].Volume;
                _busDuckGain[b] = 1f;
                _busMuted[b] = _buses[b].Muted;
                _busSoloed[b] = _buses[b].Soloed;
                _busFade[b].SnapTo(_buses[b].Volume);
                _busDuck[b].Reset();
                _busDuck[b].Configure(_buses[b].Duck);
            }
            _busGraph.RefreshSolo();
        }
    }
}
