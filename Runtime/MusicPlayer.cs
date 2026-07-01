using System.Collections.Generic;
using Kobapps.AudioKit.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Phase-2 music/playlist controller. Plays a <see cref="BakedPlaylist"/> across two dedicated
    /// AudioSources on a music bus, crossfading between tracks (or hard-cutting / gapless-scheduling
    /// when the crossfade is zero). Track order comes from the pure <see cref="PlaylistSequencer"/>;
    /// per-frame volume is <c>trackVolume × fade × busGain</c>, so bus volume, mute/solo and ducking
    /// all apply to music exactly like sound groups. Allocation-free on the tick path.
    /// </summary>
    public sealed class MusicPlayer
    {
        private readonly AudioKitCoreEngine _engine;
        private readonly BakedAudioData _data;
        private readonly IRandom _rng;
        private readonly Dictionary<uint, int> _playlistIndex = new Dictionary<uint, int>(16);

        private readonly AudioSource[] _src = new AudioSource[2];
        private FadeEnvelope[] _fade = new FadeEnvelope[2];
        private readonly float[] _trackVol = new float[2];
        private readonly bool[] _used = new bool[2];

        private int _active;
        private BakedPlaylist _current;
        private readonly PlaylistSequencer _seq = new PlaylistSequencer();
        private bool _playing;
        private int _busIndex = -1;
        private int _currentTrack = -1;

        // Gapless scheduling state.
        private bool _nextScheduled;
        private int _scheduledSrc = -1;
        private int _scheduledTrack = -1;

        public bool IsPlaying => _playing;
        public int CurrentTrack => _currentTrack;
        public string CurrentPlaylistName => _current != null ? _current.Name : null;

        public MusicPlayer(AudioKitCoreEngine engine, BakedAudioData data, Transform root, IRandom rng)
        {
            _engine = engine;
            _data = data;
            _rng = rng ?? new XorShiftRandom();

            if (data.Playlists != null)
                for (int i = 0; i < data.Playlists.Length; i++)
                    _playlistIndex[data.Playlists[i].Id.Value] = i;

            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("AudioKitMusic_" + (i == 0 ? "A" : "B"));
                go.transform.SetParent(root, false);
                go.hideFlags = HideFlags.DontSave;
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _src[i] = s;
            }
        }

        public int GetPlaylistIndex(AudioId id) => _playlistIndex.TryGetValue(id.Value, out int i) ? i : -1;

        public void Play(AudioId playlist)
        {
            int idx = GetPlaylistIndex(playlist);
            if (idx >= 0) Play(idx);
        }

        public void Play(int playlistIndex)
        {
            if (_data.Playlists == null || (uint)playlistIndex >= (uint)_data.Playlists.Length)
                return;

            StopImmediate();

            _current = _data.Playlists[playlistIndex];
            _busIndex = _current.BusIndex;
            _seq.Reset();

            int track = _seq.Next(_current.TrackCount, _current.Mode, _rng);
            if (track < 0)
            {
                _playing = false;
                return;
            }

            _active = 0;
            _currentTrack = track;
            StartTrack(0, track, _current.CrossfadeSeconds);
            _playing = _used[0];
        }

        /// <summary>Advance to the next track (crossfade / hard cut per the playlist config).</summary>
        public void Next()
        {
            if (!_playing || _current == null) return;
            AdvanceTrack();
        }

        public void Stop(float fadeSeconds = 0f)
        {
            if (!_playing) return;
            if (fadeSeconds <= 0f)
            {
                StopImmediate();
                return;
            }
            for (int i = 0; i < 2; i++)
                if (_used[i]) _fade[i].FadeTo(0f, fadeSeconds, FadeCurve.EqualPower);
            _stopping = true;
        }

        private bool _stopping;

        public void Tick(float dt)
        {
            if (!_playing) return;

            float busGain = _engine.BusEffectiveGain(_busIndex);

            for (int i = 0; i < 2; i++)
            {
                if (!_used[i]) continue;
                float f = _fade[i].Tick(dt);
                _src[i].volume = _trackVol[i] * f * busGain;

                // A faded-out, non-active source (outgoing crossfade, or a stop) is retired.
                if (_fade[i].IsDone && (i != _active || _stopping) && f <= 0.0001f)
                {
                    _src[i].Stop();
                    _src[i].clip = null;
                    _used[i] = false;
                }
            }

            if (_stopping)
            {
                if (!_used[0] && !_used[1])
                {
                    _stopping = false;
                    _playing = false;
                }
                return;
            }

            HandleAdvance();
        }

        private void HandleAdvance()
        {
            var a = _src[_active];
            if (!_used[_active] || a.clip == null || a.loop) return;

            float length = a.clip.length;
            if (length <= 0f) return;
            float remaining = length - a.time;

            if (_current.Gapless)
            {
                // Sample-accurate handoff via PlayScheduled.
                if (!_nextScheduled && a.isPlaying && remaining <= 1.0f && a.time > 0.01f)
                {
                    int track = _seq.Next(_current.TrackCount, _current.Mode, _rng);
                    if (track >= 0)
                    {
                        int inc = 1 - _active;
                        PrepareSource(inc, track);
                        _fade[inc].SnapTo(1f);
                        _src[inc].PlayScheduled(AudioSettings.dspTime + remaining);
                        _used[inc] = true;
                        _scheduledSrc = inc;
                        _scheduledTrack = track;
                        _nextScheduled = true;
                    }
                }
                if (_nextScheduled && !a.isPlaying)
                {
                    a.Stop();
                    a.clip = null;
                    _used[_active] = false;
                    _active = _scheduledSrc;
                    _currentTrack = _scheduledTrack;
                    _nextScheduled = false;
                }
                return;
            }

            // Crossfade / hard-cut: trigger a transition when the tail is within the crossfade window.
            float lead = Mathf.Min(Mathf.Max(_current.CrossfadeSeconds, 0.05f), length * 0.5f);
            if (a.isPlaying && a.time > 0.01f && remaining <= lead)
                AdvanceTrack();
        }

        private void AdvanceTrack()
        {
            int track = _seq.Next(_current.TrackCount, _current.Mode, _rng);
            if (track < 0) return;

            float xf = _current.CrossfadeSeconds;
            int inc = 1 - _active;

            // Fade the outgoing track down; it retires in Tick when the fade completes.
            _fade[_active].FadeTo(0f, xf, FadeCurve.EqualPower);

            _currentTrack = track;
            StartTrack(inc, track, xf);
            _active = inc;

            if (xf <= 0f)
            {
                // Hard cut: retire the previous source now.
                int prev = 1 - _active;
                _src[prev].Stop();
                _src[prev].clip = null;
                _used[prev] = false;
            }
        }

        private bool StartTrack(int srcIndex, int track, float fadeIn)
        {
            if (!PrepareSource(srcIndex, track))
                return false;

            if (fadeIn <= 0f)
                _fade[srcIndex].SnapTo(1f);
            else
                _fade[srcIndex].StartFrom(0f, 1f, fadeIn, FadeCurve.EqualPower);

            _src[srcIndex].volume = 0f;
            _src[srcIndex].Play();
            _used[srcIndex] = true;
            return true;
        }

        private bool PrepareSource(int srcIndex, int track)
        {
            AudioClip clip = (uint)track < (uint)_current.Clips.Length ? _current.Clips[track] : null;
            if (clip == null)
                return false;

            var s = _src[srcIndex];
            s.clip = clip;
            s.loop = _current.TrackCount == 1; // single-track playlists loop seamlessly
            s.outputAudioMixerGroup = ResolveMixer(_busIndex);
            _trackVol[srcIndex] = (uint)track < (uint)_current.Volumes.Length ? _current.Volumes[track] : 1f;
            return true;
        }

        private AudioMixerGroup ResolveMixer(int busIndex) =>
            (uint)busIndex < (uint)_data.BusMixer.Length ? _data.BusMixer[busIndex] : null;

        private void StopImmediate()
        {
            for (int i = 0; i < 2; i++)
            {
                _src[i].Stop();
                _src[i].clip = null;
                _used[i] = false;
                _fade[i].SnapTo(0f);
            }
            _stopping = false;
            _nextScheduled = false;
            _playing = false;
            _currentTrack = -1;
        }
    }
}
