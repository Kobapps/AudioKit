using Kobapps.AudioKit.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Binds Core voice slots to pooled <see cref="AudioSource"/>s. Owns the per-voice playback
    /// state and, each frame, applies <c>baseVolume × userScale × busGain × fade</c> to every live
    /// source, follows transforms, and recycles finished or faded-out voices. Allocation free on the
    /// play and tick paths.
    /// </summary>
    public sealed class VoiceManager
    {
        private readonly IAudioService _service;
        private readonly AudioKitCoreEngine _engine;
        private readonly BakedAudioData _data;
        private readonly AudioSourcePool _pool;

        private readonly float[] _baseVolume;
        private readonly float[] _userScale;
        private readonly int[] _bus;
        private readonly int[] _group;
        private readonly bool[] _loop;
        private readonly bool[] _stopping;
        private readonly bool[] _skipFinishCheck;
        private readonly Transform[] _follow;
        private FadeEnvelope[] _fade;
        private readonly float[] _lastAppliedVolume; // last value written to AudioSource.volume (dirty-check)

        // Compact list of active voice slots so the per-frame tick iterates only live voices.
        private readonly int[] _activeSlots;
        private readonly int[] _slotPos; // slot -> index in _activeSlots, or -1 if inactive
        private int _activeCount;

        private const float VolumeEpsilon = 0.0005f;

        /// <summary>Number of currently live voices (for diagnostics / editor meters).</summary>
        public int ActiveVoiceCount => _activeCount;

        public VoiceManager(IAudioService service, AudioKitCoreEngine engine, BakedAudioData data, AudioSourcePool pool)
        {
            _service = service;
            _engine = engine;
            _data = data;
            _pool = pool;

            int cap = engine.VoiceCapacity;
            _baseVolume = new float[cap];
            _userScale = new float[cap];
            _bus = new int[cap];
            _group = new int[cap];
            _loop = new bool[cap];
            _stopping = new bool[cap];
            _skipFinishCheck = new bool[cap];
            _follow = new Transform[cap];
            _fade = new FadeEnvelope[cap];
            _lastAppliedVolume = new float[cap];
            _activeSlots = new int[cap];
            _slotPos = new int[cap];
            for (int i = 0; i < cap; i++) { _slotPos[i] = -1; _lastAppliedVolume[i] = -1f; }
        }

        private void AddActive(int slot)
        {
            if (_slotPos[slot] >= 0) return; // already active (e.g. a stolen/repurposed slot)
            _slotPos[slot] = _activeCount;
            _activeSlots[_activeCount++] = slot;
        }

        private void RemoveActive(int slot)
        {
            int pos = _slotPos[slot];
            if (pos < 0) return;
            int last = --_activeCount;
            int lastSlot = _activeSlots[last];
            _activeSlots[pos] = lastSlot;
            _slotPos[lastSlot] = pos;
            _slotPos[slot] = -1;
        }

        /// <summary>Cull distance for 3D one-shots: beyond this from the listener, a play is skipped
        /// (no voice spent). 0 disables. Set via the facade / database.</summary>
        public float VirtualizeDistance;

        private AudioListener _listener;

        private Transform ListenerTransform
        {
            get
            {
                if (_listener == null) _listener = Object.FindObjectOfType<AudioListener>();
                return _listener != null ? _listener.transform : null;
            }
        }

        public SoundHandle Play(int groupIndex, float volumeScale, bool forceSpatial, bool hasPosition, Vector3 position, Transform follow, float fadeInSeconds = 0f)
        {
            // Bound against the engine's group table (grows with dynamically registered groups),
            // not the static baked _data.Groups.
            if ((uint)groupIndex >= (uint)_engine.GroupCount)
                return SoundHandle.Invalid;

            // Distance virtualization: skip inaudible far 3D one-shots before spending a voice.
            if (VirtualizeDistance > 0f && (follow != null || hasPosition))
            {
                Vector3 target = follow != null ? follow.position : position;
                Transform lt = ListenerTransform;
                if (lt != null && (target - lt.position).sqrMagnitude > VirtualizeDistance * VirtualizeDistance)
                    return SoundHandle.Invalid;
            }

            int local = _engine.SelectVariation(groupIndex);
            if (local < 0)
                return SoundHandle.Invalid;

            int global = _engine.VariationGlobalIndex(groupIndex, local);
            if (global < 0)
                return SoundHandle.Invalid;

            AudioClip clip = _data.Clips[global];
            if (clip == null)
                return SoundHandle.Invalid; // nothing to play; validation gate flags this at design time

            GroupData g = _engine.GetGroup(groupIndex);
            VariationData v = _engine.GetVariation(global);

            float groupVol = _engine.RandomGroupVolume(groupIndex);
            float groupPitch = _engine.RandomGroupPitch(groupIndex);
            float baseVolume = v.Volume * groupVol;

            if (!_engine.TryAcquireVoice(groupIndex, baseVolume * volumeScale, out int slot, out int generation, out int stolen))
                return SoundHandle.Invalid;

            AudioSource src = _pool.Get(slot);
            if (src == null)
            {
                _engine.ReleaseVoice(slot);
                return SoundHandle.Invalid;
            }

            // If we reclaimed a live voice, make sure it is silenced before reconfiguring.
            if (stolen >= 0)
                src.Stop();

            bool spatial = g.Is3D || forceSpatial || hasPosition || follow != null;

            src.clip = clip;
            src.pitch = v.Pitch * groupPitch;
            src.loop = _data.VarLoop[global];
            src.spatialBlend = spatial ? 1f : 0f;
            src.outputAudioMixerGroup = ResolveMixer(groupIndex, g.BusIndex);

            if (_data.VarOverrideRolloff[global])
            {
                src.minDistance = _data.VarMinDistance[global];
                src.maxDistance = _data.VarMaxDistance[global];
            }

            Transform st = src.transform;
            if (follow != null)
                st.position = follow.position;
            else if (hasPosition)
                st.position = position;

            // Per-voice state.
            _baseVolume[slot] = baseVolume;
            _userScale[slot] = volumeScale;
            _bus[slot] = g.BusIndex;
            _group[slot] = groupIndex;
            _loop[slot] = src.loop;
            _stopping[slot] = false;
            _skipFinishCheck[slot] = true;
            _follow[slot] = follow;

            if (fadeInSeconds > 0f) _fade[slot].StartFrom(0f, 1f, fadeInSeconds, FadeCurve.EqualPower);
            else _fade[slot].SnapTo(1f);

            float busGain = _engine.BusEffectiveGain(g.BusIndex);
            float startVolume = baseVolume * volumeScale * _engine.GroupGain(groupIndex) * busGain * _fade[slot].Value;
            src.volume = startVolume;
            _lastAppliedVolume[slot] = startVolume;

            if (_data.VarRandomStart[global] && clip.length > 0f)
                src.time = Random.Range(0f, clip.length * 0.9f);

            // A stolen slot is already tracked as active; a fresh slot needs adding.
            if (stolen < 0) AddActive(slot);

            src.Play();
            return new SoundHandle(_service, slot, generation);
        }

        private AudioMixerGroup ResolveMixer(int groupIndex, int busIndex)
        {
            var gm = (uint)groupIndex < (uint)_data.GroupMixer.Length ? _data.GroupMixer[groupIndex] : null;
            if (gm != null) return gm;
            return (uint)busIndex < (uint)_data.BusMixer.Length ? _data.BusMixer[busIndex] : null;
        }

        /// <summary>Advance every live voice. Call once per frame after <c>engine.Tick</c>. Iterates
        /// only the active-voice list, and writes <c>AudioSource.volume</c> only when it actually moves.</summary>
        public void Tick(float dt)
        {
            int i = 0;
            while (i < _activeCount)
            {
                int slot = _activeSlots[i];
                if (TickVoice(slot, dt))
                    continue; // slot released → swapped a new slot into position i; re-process it
                i++;
            }
        }

        /// <summary>Advance one voice. Returns true if it was released (and removed from the active list).</summary>
        private bool TickVoice(int slot, float dt)
        {
            AudioSource src = _pool.Get(slot);
            if (src == null) { Release(slot, null); return true; }

            // Follow target lost → stop.
            Transform follow = _follow[slot];
            if (follow != null)
                src.transform.position = follow.position;
            else if (ReferenceEquals(follow, null) == false)
            {
                // follow was set but Unity-destroyed (== null but not ReferenceEquals null)
                Release(slot, src);
                return true;
            }

            float fade = _fade[slot].Tick(dt);
            float preBus = _baseVolume[slot] * _userScale[slot] * _engine.GroupGain(_group[slot]);
            float vol = preBus * _engine.BusEffectiveGain(_bus[slot]) * fade;
            if (vol < 0f) vol = 0f;
            if (vol < _lastAppliedVolume[slot] - VolumeEpsilon || vol > _lastAppliedVolume[slot] + VolumeEpsilon)
            {
                src.volume = vol;
                _lastAppliedVolume[slot] = vol;
            }
            _engine.SetVoiceVolume(slot, preBus * fade); // for steal-quietest accounting

            if (_stopping[slot])
            {
                if (_fade[slot].IsDone) { Release(slot, src); return true; }
                return false;
            }

            if (_skipFinishCheck[slot])
            {
                _skipFinishCheck[slot] = false;
                return false;
            }

            if (!_loop[slot] && !src.isPlaying) { Release(slot, src); return true; }
            return false;
        }

        private void Release(int slot, AudioSource src)
        {
            if (src != null)
            {
                src.Stop();
                src.clip = null;
                src.outputAudioMixerGroup = null;
            }
            _follow[slot] = null;
            _stopping[slot] = false;
            _lastAppliedVolume[slot] = -1f;
            RemoveActive(slot);
            _engine.ReleaseVoice(slot);
        }

        // --- Group / global control -------------------------------------------------------------

        public void StopGroup(int groupIndex, float fadeSeconds)
        {
            int cap = _engine.VoiceCapacity;
            for (int slot = 0; slot < cap; slot++)
            {
                if (_engine.Voices.IsActive(slot) && _group[slot] == groupIndex)
                    StopSlot(slot, fadeSeconds);
            }
        }

        public void StopAll(float fadeSeconds)
        {
            int cap = _engine.VoiceCapacity;
            for (int slot = 0; slot < cap; slot++)
            {
                if (_engine.Voices.IsActive(slot))
                    StopSlot(slot, fadeSeconds);
            }
        }

        private void StopSlot(int slot, float fadeSeconds)
        {
            AudioSource src = _pool.Get(slot);
            if (src == null)
            {
                _engine.ReleaseVoice(slot);
                return;
            }
            if (fadeSeconds <= 0f)
            {
                Release(slot, src);
            }
            else
            {
                _fade[slot].FadeTo(0f, fadeSeconds, FadeCurve.EqualPower);
                _stopping[slot] = true;
            }
        }

        public void PauseAll()
        {
            int cap = _engine.VoiceCapacity;
            for (int slot = 0; slot < cap; slot++)
            {
                if (_engine.Voices.IsActive(slot))
                {
                    var src = _pool.Get(slot);
                    if (src != null) src.Pause();
                }
            }
        }

        public void UnpauseAll()
        {
            int cap = _engine.VoiceCapacity;
            for (int slot = 0; slot < cap; slot++)
            {
                if (_engine.Voices.IsActive(slot))
                {
                    var src = _pool.Get(slot);
                    if (src != null) src.UnPause();
                }
            }
        }

        // --- Handle operations ------------------------------------------------------------------

        public bool IsVoiceValid(int slot, int generation) => _engine.IsVoiceValid(slot, generation);

        public bool IsVoicePlaying(int slot, int generation)
        {
            if (!_engine.IsVoiceValid(slot, generation)) return false;
            var src = _pool.Get(slot);
            return src != null && src.isPlaying;
        }

        public void StopVoice(int slot, int generation, float fadeSeconds)
        {
            if (_engine.IsVoiceValid(slot, generation))
                StopSlot(slot, fadeSeconds);
        }

        public void SetVoiceVolume(int slot, int generation, float volume)
        {
            if (_engine.IsVoiceValid(slot, generation))
                _userScale[slot] = volume < 0f ? 0f : volume;
        }

        public void SetVoicePitch(int slot, int generation, float pitch)
        {
            if (!_engine.IsVoiceValid(slot, generation)) return;
            var src = _pool.Get(slot);
            if (src != null) src.pitch = pitch;
        }

        public void SetVoicePosition(int slot, int generation, Vector3 worldPosition)
        {
            if (!_engine.IsVoiceValid(slot, generation)) return;
            if (_follow[slot] != null) return; // following voices ignore manual positioning
            var src = _pool.Get(slot);
            if (src != null) src.transform.position = worldPosition;
        }
    }
}
