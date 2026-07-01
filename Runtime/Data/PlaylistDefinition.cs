using System;
using System.Collections.Generic;
using Kobapps.AudioKit.Core;
using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>One song in a playlist.</summary>
    [Serializable]
    public class MusicTrack
    {
        [Tooltip("Display name for this track (editor only).")]
        public string name = "Track";

        [Tooltip("The music AudioClip. Leave empty to load via the Addressables key instead.")]
        public AudioClip clip;

        [Tooltip("Optional Addressables key used when 'clip' is empty and Addressables is installed.")]
        public string addressableKey = "";

        [Tooltip("Per-track volume multiplier (0–1).")]
        [Range(0f, 1f)] public float volume = 1f;
    }

    /// <summary>A sequence of music tracks played on a bus with optional crossfade / gapless.</summary>
    [Serializable]
    public class PlaylistDefinition
    {
        [Tooltip("Unique playlist name used to start it: AudioKit.PlayPlaylist(\"name\").")]
        public string playlistName = "NewPlaylist";

        [Tooltip("Track order: Sorted (in order), Random, or RandomNoRepeat.")]
        public PlaylistMode mode = PlaylistMode.Sorted;

        [Tooltip("Seconds to crossfade between tracks. 0 = hard cut (or seamless when Gapless).")]
        [Min(0f)] public float crossfadeSeconds = 2f;

        [Tooltip("Schedule the next track sample-accurately at the end of the current one (best with crossfade 0).")]
        public bool gapless = false;

        [Tooltip("Bus to play on. Empty = the first bus flagged as a music bus, else master.")]
        public string busName = "";

        [Tooltip("The tracks in this playlist, in order.")]
        public List<MusicTrack> tracks = new List<MusicTrack>();
    }

    /// <summary>Runtime-baked playlist: flat clip/volume arrays + resolved bus index.</summary>
    public sealed class BakedPlaylist
    {
        public AudioId Id;
        public string Name;
        public PlaylistMode Mode;
        public float CrossfadeSeconds;
        public bool Gapless;
        public int BusIndex; // -1 = master
        public AudioClip[] Clips;
        public string[] AddressableKeys;
        public float[] Volumes;

        public int TrackCount => Clips != null ? Clips.Length : 0;
    }
}
