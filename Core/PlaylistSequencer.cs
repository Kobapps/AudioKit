namespace Kobapps.AudioKit.Core
{
    /// <summary>Order in which a playlist advances through its tracks.</summary>
    public enum PlaylistMode
    {
        /// <summary>Play tracks in order, wrapping at the end.</summary>
        Sorted = 0,

        /// <summary>Pick a uniformly random track each time (may repeat).</summary>
        Random = 1,

        /// <summary>Random, but never the track that just played.</summary>
        RandomNoRepeat = 2,
    }

    /// <summary>
    /// Pure track-advance logic for a playlist. Deterministic given the injected <see cref="IRandom"/>
    /// and its own cursor state. No Unity types, allocation free — unit-tested headless. The runtime
    /// music player owns one of these per active playlist.
    /// </summary>
    public sealed class PlaylistSequencer
    {
        private int _cursor = -1;
        private int _last = -1;

        public int LastIndex => _last;

        public void Reset()
        {
            _cursor = -1;
            _last = -1;
        }

        /// <summary>Next track index in [0, count), or -1 when the playlist is empty.</summary>
        public int Next(int count, PlaylistMode mode, IRandom rng)
        {
            if (count <= 0)
                return -1;
            if (count == 1)
            {
                _cursor = 0;
                _last = 0;
                return 0;
            }

            int idx;
            switch (mode)
            {
                case PlaylistMode.Sorted:
                    _cursor = (_cursor + 1) % count;
                    idx = _cursor;
                    break;

                case PlaylistMode.RandomNoRepeat:
                {
                    int pick = rng.NextInt(count - 1);
                    if (_last >= 0 && pick >= _last)
                        pick++;
                    idx = pick;
                    break;
                }

                case PlaylistMode.Random:
                default:
                    idx = rng.NextInt(count);
                    break;
            }

            _cursor = idx;
            _last = idx;
            return idx;
        }

        /// <summary>Peek/force the cursor so the next Sorted call continues from <paramref name="index"/>.</summary>
        public void SetCursor(int index)
        {
            _cursor = index;
            _last = index;
        }
    }
}
