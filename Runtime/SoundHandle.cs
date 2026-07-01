using Kobapps.AudioKit.Core;
using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// A lightweight, GC-free control surface for a single playing voice. Holds only a service
    /// reference plus a (slot, generation) pair; a stale handle (whose voice was stopped or stolen)
    /// is detected by generation mismatch, so all operations are safe no-ops on a dead voice.
    /// </summary>
    public readonly struct SoundHandle
    {
        private readonly IAudioService _service;
        internal readonly int Slot;
        internal readonly int Generation;

        internal SoundHandle(IAudioService service, int slot, int generation)
        {
            _service = service;
            Slot = slot;
            Generation = generation;
        }

        /// <summary>An invalid handle that controls nothing.</summary>
        public static SoundHandle Invalid => default;

        /// <summary>True while the underlying voice still exists (not stopped, finished or stolen).</summary>
        public bool IsValid => _service != null && _service.IsVoiceValid(Slot, Generation);

        /// <summary>True while the voice is audibly playing.</summary>
        public bool IsPlaying => _service != null && _service.IsVoicePlaying(Slot, Generation);

        /// <summary>Stop the voice, optionally fading out over <paramref name="fadeSeconds"/>.</summary>
        public void Stop(float fadeSeconds = 0f) => _service?.StopVoice(Slot, Generation, fadeSeconds);

        /// <summary>Set the voice's per-call volume scale (0..1+).</summary>
        public void SetVolume(float volume) => _service?.SetVoiceVolume(Slot, Generation, volume);

        /// <summary>Set the voice's pitch multiplier.</summary>
        public void SetPitch(float pitch) => _service?.SetVoicePitch(Slot, Generation, pitch);

        /// <summary>Move a one-shot voice to a world position (no-op for following/2D voices).</summary>
        public void SetPosition(Vector3 worldPosition) => _service?.SetVoicePosition(Slot, Generation, worldPosition);
    }
}
