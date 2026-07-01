using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// A fixed-capacity, slot-indexed pool of <see cref="AudioSource"/> components parented under a
    /// single hidden root. The voice slot index returned by the Core <c>VoiceTable</c> maps 1:1 to a
    /// pooled source, so there is no separate free list here — the Core allocator owns reclamation
    /// and this pool only lazily materializes sources up to the hard cap.
    /// </summary>
    public sealed class AudioSourcePool
    {
        private readonly Transform _root;
        private readonly AudioSource[] _sources;
        private int _created;

        public int Capacity => _sources.Length;
        public int Created => _created;

        public AudioSourcePool(Transform root, int capacity, int prewarm)
        {
            _root = root;
            if (capacity < 1) capacity = 1;
            _sources = new AudioSource[capacity];
            if (prewarm < 0) prewarm = 0;
            if (prewarm > capacity) prewarm = capacity;
            for (int i = 0; i < prewarm; i++)
                Create(i);
        }

        /// <summary>Get the pooled source for a voice slot, creating it on first use.</summary>
        public AudioSource Get(int slot)
        {
            if ((uint)slot >= (uint)_sources.Length)
                return null;
            return _sources[slot] != null ? _sources[slot] : Create(slot);
        }

        private AudioSource Create(int slot)
        {
            var go = new GameObject("AudioKitVoice_" + slot);
            go.transform.SetParent(_root, false);
            go.hideFlags = HideFlags.DontSave;
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            _sources[slot] = src;
            if (slot >= _created) _created = slot + 1;
            return src;
        }

        /// <summary>Stop and reset every materialized source (used on teardown/scene reset).</summary>
        public void StopAll()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                var s = _sources[i];
                if (s != null)
                {
                    s.Stop();
                    s.clip = null;
                }
            }
        }
    }
}
