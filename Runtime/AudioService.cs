using System;
using Kobapps.AudioKit.Core;
using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Concrete <see cref="IAudioService"/>: owns the Core engine, the AudioSource pool, the voice
    /// manager and the custom-event registry. Construct it from a baked database and a pool root,
    /// then drive it with <see cref="Tick"/> once per frame. DI containers bind this as
    /// <see cref="IAudioService"/>; the static <see cref="AudioKit"/> facade forwards to the active
    /// instance.
    /// </summary>
    public sealed class AudioService : IAudioService
    {
        private readonly AudioKitCoreEngine _engine;
        private readonly BakedAudioData _data;
        private readonly AudioSourcePool _pool;
        private readonly VoiceManager _voices;
        private readonly MusicPlayer _music;
        private readonly CustomEventRegistry _events = new CustomEventRegistry();

        public bool IsReady { get; private set; }
        public BakedAudioData Data => _data;
        public AudioKitCoreEngine Engine => _engine;

        public AudioService(BakedAudioData data, Transform poolRoot, IRandom rng = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _engine = new AudioKitCoreEngine(rng);
            _engine.Configure(data.Groups, data.Buses, data.Variations, data.VoiceCapacity);
            _pool = new AudioSourcePool(poolRoot, data.VoiceCapacity, data.PrewarmVoices);
            _voices = new VoiceManager(this, _engine, _data, _pool);
            _music = new MusicPlayer(_engine, _data, poolRoot, rng);
            IsReady = true;
        }

        /// <summary>The Phase-2 music/playlist controller.</summary>
        public MusicPlayer Music => _music;

        /// <summary>Advance bus fades, ducking, live voices and music. Call once per frame.</summary>
        public void Tick(float dt)
        {
            _engine.Tick(dt);
            _voices.Tick(dt);
            _music.Tick(dt);
        }

        public void Shutdown()
        {
            _music.Stop(0f);
            _voices.StopAll(0f);
            _pool.StopAll();
            _events.Clear();
            IsReady = false;
        }

        // --- Playback ---------------------------------------------------------------------------

        public SoundHandle Play(AudioId group, float volumeScale = 1f, float fadeInSeconds = 0f)
        {
            int g = _engine.GetGroupIndex(group);
            return g < 0 ? SoundHandle.Invalid : _voices.Play(g, volumeScale, false, false, default, null, fadeInSeconds);
        }

        public SoundHandle PlayAt(AudioId group, Vector3 position, float volumeScale = 1f, float fadeInSeconds = 0f)
        {
            int g = _engine.GetGroupIndex(group);
            return g < 0 ? SoundHandle.Invalid : _voices.Play(g, volumeScale, true, true, position, null, fadeInSeconds);
        }

        public SoundHandle PlayFollow(AudioId group, Transform follow, float volumeScale = 1f, float fadeInSeconds = 0f)
        {
            int g = _engine.GetGroupIndex(group);
            return g < 0 ? SoundHandle.Invalid : _voices.Play(g, volumeScale, true, false, default, follow, fadeInSeconds);
        }

        // --- Per-group volume / mute + diagnostics + virtualization -----------------------------

        public void SetGroupVolume(AudioId group, float volume)
        {
            int g = _engine.GetGroupIndex(group);
            if (g >= 0) _engine.SetGroupVolume(g, volume);
        }

        public float GetGroupVolume(AudioId group)
        {
            int g = _engine.GetGroupIndex(group);
            return g >= 0 ? _engine.GetGroupVolume(g) : 1f;
        }

        public void MuteGroup(AudioId group, bool muted)
        {
            int g = _engine.GetGroupIndex(group);
            if (g >= 0) _engine.SetGroupMuted(g, muted);
        }

        public bool IsGroupMuted(AudioId group)
        {
            int g = _engine.GetGroupIndex(group);
            return g >= 0 && _engine.IsGroupMuted(g);
        }

        public float VirtualizeDistance
        {
            get => _voices.VirtualizeDistance;
            set => _voices.VirtualizeDistance = value;
        }

        public int ActiveVoiceCount => _voices.ActiveVoiceCount;
        public int VoiceCapacity => _engine.VoiceCapacity;

        public void StopGroup(AudioId group, float fadeSeconds = 0f)
        {
            int g = _engine.GetGroupIndex(group);
            if (g >= 0) _voices.StopGroup(g, fadeSeconds);
        }

        public void StopAll(float fadeSeconds = 0f) => _voices.StopAll(fadeSeconds);
        public void PauseAll() => _voices.PauseAll();
        public void UnpauseAll() => _voices.UnpauseAll();

        // --- Music / playlists (Phase 2) --------------------------------------------------------

        public void PlayPlaylist(AudioId playlist) => _music.Play(playlist);
        public void StopPlaylist(float fadeSeconds = 0f) => _music.Stop(fadeSeconds);
        public void NextTrack() => _music.Next();
        public bool IsPlaylistPlaying => _music.IsPlaying;

        // --- Dynamic group registration ---------------------------------------------------------

        public GroupRegistration RegisterGroups(SoundGroupSetAsset set)
        {
            var reg = new GroupRegistration { ManageClipMemory = set != null && set.manageClipMemory };
            if (set == null || set.groups == null) return reg;

            for (int i = 0; i < set.groups.Count; i++)
            {
                var def = set.groups[i];
                if (def != null && RegisterGroupInternal(def, reg.ManageClipMemory))
                    reg.Ids.Add(AudioId.From(def.groupName));
            }
            return reg;
        }

        public void UnregisterGroups(GroupRegistration registration)
        {
            if (registration == null) return;
            for (int i = 0; i < registration.Ids.Count; i++)
                UnregisterGroupInternal(registration.Ids[i], registration.ManageClipMemory);
            registration.Ids.Clear();
        }

        public bool RegisterGroup(SoundGroupDefinition def) => RegisterGroupInternal(def, false);

        public bool UnregisterGroup(AudioId group)
        {
            if (!_engine.IsGroupRegistered(group)) return false;
            UnregisterGroupInternal(group, false);
            return true;
        }

        public bool IsGroupRegistered(AudioId group) => _engine.IsGroupRegistered(group);

        private bool RegisterGroupInternal(SoundGroupDefinition def, bool manageMemory)
        {
            if (def == null || string.IsNullOrEmpty(def.groupName))
                return false;

            int busIndex = -1;
            if (!string.IsNullOrEmpty(def.busName))
                busIndex = _engine.GetBusIndex(AudioId.From(def.busName)); // -1 (master) if unknown

            var gd = new GroupData
            {
                Id = AudioId.From(def.groupName),
                VolumeMin = def.volumeMin, VolumeMax = def.volumeMax,
                PitchMin = def.pitchMin, PitchMax = def.pitchMax,
                VoiceLimit = Mathf.Max(1, def.voiceLimit),
                RetriggerPercent = def.retriggerPercent,
                BusIndex = busIndex,
                PlayMode = def.playMode,
                StealPolicy = def.stealPolicy,
                Is3D = def.is3D,
            };

            int count = def.variations != null ? def.variations.Count : 0;
            var vars = new VariationData[count];
            for (int i = 0; i < count; i++)
            {
                var v = def.variations[i];
                vars[i] = new VariationData { Volume = v.volume, Pitch = v.pitch, Weight = v.weight };
            }

            int index = _engine.RegisterGroup(gd, vars);
            if (index < 0)
                return false; // duplicate name or empty id

            var g = _engine.GetGroup(index);
            _data.EnsureVariationCapacity(_engine.VariationTotal);
            _data.EnsureGroupCapacity(_engine.GroupTotal);

            for (int i = 0; i < count; i++)
            {
                var v = def.variations[i];
                int gi = g.VariationOffset + i;
                _data.Clips[gi] = v.clip;
                _data.AddressableKeys[gi] = v.addressableKey;
                _data.VarLoop[gi] = v.loop;
                _data.VarRandomStart[gi] = v.randomizeStartPosition;
                _data.VarOverrideRolloff[gi] = v.overrideRolloff;
                _data.VarMinDistance[gi] = v.minDistance;
                _data.VarMaxDistance[gi] = v.maxDistance;

                if (manageMemory && v.clip != null && v.clip.loadState != AudioDataLoadState.Loaded)
                    v.clip.LoadAudioData();
            }

            _data.GroupMixer[index] = def.mixerGroup;
            _data.GroupNames[index] = def.groupName;
            return true;
        }

        private void UnregisterGroupInternal(AudioId id, bool manageMemory)
        {
            int index = _engine.GetGroupIndex(id);
            if (index < 0) return;

            // Stop live voices before recycling the slot/variation indices.
            _voices.StopGroup(index, 0f);

            var g = _engine.GetGroup(index);
            for (int i = 0; i < g.VariationCount; i++)
            {
                int gi = g.VariationOffset + i;
                if ((uint)gi < (uint)_data.Clips.Length)
                {
                    if (manageMemory && _data.Clips[gi] != null)
                        _data.Clips[gi].UnloadAudioData();
                    _data.Clips[gi] = null;
                }
            }
            if ((uint)index < (uint)_data.GroupMixer.Length) _data.GroupMixer[index] = null;
            if ((uint)index < (uint)_data.GroupNames.Length) _data.GroupNames[index] = null;

            _engine.UnregisterGroup(id);
        }

        // --- Buses ------------------------------------------------------------------------------

        public void FadeBus(AudioId bus, float toVolume, float seconds, FadeCurve curve = FadeCurve.Linear)
        {
            int b = _engine.GetBusIndex(bus);
            if (b >= 0) _engine.FadeBusVolume(b, toVolume, seconds, curve);
        }

        public void SetBusVolume(AudioId bus, float volume)
        {
            int b = _engine.GetBusIndex(bus);
            if (b >= 0) _engine.SetBusVolume(b, volume);
        }

        public float GetBusVolume(AudioId bus)
        {
            int b = _engine.GetBusIndex(bus);
            return b >= 0 ? _engine.GetBusVolume(b) : 1f;
        }

        public void MuteBus(AudioId bus, bool muted)
        {
            int b = _engine.GetBusIndex(bus);
            if (b >= 0) _engine.SetBusMuted(b, muted);
        }

        public void SoloBus(AudioId bus, bool soloed)
        {
            int b = _engine.GetBusIndex(bus);
            if (b >= 0) _engine.SetBusSoloed(b, soloed);
        }

        public void DuckBus(AudioId bus)
        {
            int b = _engine.GetBusIndex(bus);
            if (b >= 0) _engine.AddDuckTrigger(b);
        }

        public void UnduckBus(AudioId bus)
        {
            int b = _engine.GetBusIndex(bus);
            if (b >= 0) _engine.RemoveDuckTrigger(b);
        }

        public float MasterVolume
        {
            get => _engine.MasterVolume;
            set => _engine.MasterVolume = value;
        }

        // --- Custom events ----------------------------------------------------------------------

        public void FireEvent(AudioId eventId) => _events.Fire(eventId);
        public void SubscribeEvent(AudioId eventId, Action handler) => _events.Subscribe(eventId, handler);
        public void UnsubscribeEvent(AudioId eventId, Action handler) => _events.Unsubscribe(eventId, handler);

        // --- Handle operations ------------------------------------------------------------------

        public bool IsVoiceValid(int slot, int generation) => _voices.IsVoiceValid(slot, generation);
        public bool IsVoicePlaying(int slot, int generation) => _voices.IsVoicePlaying(slot, generation);
        public void StopVoice(int slot, int generation, float fadeSeconds) => _voices.StopVoice(slot, generation, fadeSeconds);
        public void SetVoiceVolume(int slot, int generation, float volume) => _voices.SetVoiceVolume(slot, generation, volume);
        public void SetVoicePitch(int slot, int generation, float pitch) => _voices.SetVoicePitch(slot, generation, pitch);
        public void SetVoicePosition(int slot, int generation, Vector3 worldPosition) => _voices.SetVoicePosition(slot, generation, worldPosition);
    }
}
