using System;
using System.Collections.Generic;
using Kobapps.AudioKit.Core;
using UnityEngine;
using UnityEngine.Audio;
// Disambiguate from UnityEngine.PlayMode.
using PlayMode = Kobapps.AudioKit.Core.PlayMode;

namespace Kobapps.AudioKit
{
    /// <summary>One playable clip + its per-variation parameters inside a Sound Group.</summary>
    [Serializable]
    public class AudioVariation
    {
        [Tooltip("Display name for this variation (editor only).")]
        public string name = "Variation";

        [Tooltip("The AudioClip to play. Leave empty to load via the Addressables key instead.")]
        public AudioClip clip;

        [Tooltip("Optional Addressables key. When 'clip' is null and Addressables is installed, the " +
                 "AddressablesClipProvider loads this key into the clip slot before first play.")]
        public string addressableKey = "";

        [Tooltip("Per-variation volume multiplier (0–1).")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Per-variation pitch multiplier (1 = original, 2 = an octave up). Negative plays reversed.")]
        [Range(-3f, 3f)] public float pitch = 1f;

        [Tooltip("Relative likelihood under the Weighted play mode. Higher = chosen more often. Ignored by other modes.")]
        [Min(0f)] public float weight = 1f;

        [Tooltip("Loop this clip until it is explicitly stopped.")]
        public bool loop;

        [Tooltip("Begin playback at a random position within the clip.")]
        public bool randomizeStartPosition;

        [Header("3D rolloff override (optional)")]
        [Tooltip("Override the group's 3D distance rolloff for just this variation.")]
        public bool overrideRolloff;

        [Tooltip("3D: distance (metres) within which the sound plays at full volume.")]
        [Min(0f)] public float minDistance = 1f;

        [Tooltip("3D: distance (metres) beyond which the sound is fully attenuated.")]
        [Min(0f)] public float maxDistance = 500f;
    }

    /// <summary>A named group of variations with selection, voice-limit and routing settings.</summary>
    [Serializable]
    public class SoundGroupDefinition
    {
        [Tooltip("Unique name used to trigger this group: AudioKit.Play(\"name\").")]
        public string groupName = "NewGroup";

        [Tooltip("The clips this group chooses between each time it is played.")]
        public List<AudioVariation> variations = new List<AudioVariation>();

        [Header("Per-play randomization")]
        [Tooltip("Lower bound of the random per-play volume multiplier (applied on top of the variation volume).")]
        public float volumeMin = 1f;
        [Tooltip("Upper bound of the random per-play volume multiplier.")]
        public float volumeMax = 1f;
        [Tooltip("Lower bound of the random per-play pitch multiplier (applied on top of the variation pitch).")]
        public float pitchMin = 1f;
        [Tooltip("Upper bound of the random per-play pitch multiplier.")]
        public float pitchMax = 1f;

        [Header("Polyphony")]
        [Tooltip("Maximum simultaneous voices for this group. At the limit, the steal policy decides what happens.")]
        [Min(1)] public int voiceLimit = 8;

        [Tooltip("Suppress a retrigger until a playing voice has passed this percentage of its clip (0 = always allow).")]
        [Range(0f, 100f)] public float retriggerPercent = 0f;

        [Tooltip("What happens at the voice limit: steal the oldest/quietest voice, or reject the new play.")]
        public VoiceStealPolicy stealPolicy = VoiceStealPolicy.StealOldest;

        [Header("Selection & routing")]
        [Tooltip("How the next variation is chosen on each play (Random, RoundRobin, Weighted, Oldest, …).")]
        public PlayMode playMode = PlayMode.Random;

        [Tooltip("Name of the bus this group routes to (must exist in this database). Empty = master.")]
        public string busName = "";

        [Tooltip("Play as 3D positional (spatialized) audio. Leave off for 2D UI/ambient sounds.")]
        public bool is3D = false;

        [Tooltip("Optional AudioMixerGroup this group outputs to (overrides the bus's mixer group).")]
        public AudioMixerGroup mixerGroup;
    }

    /// <summary>Mirror of <see cref="DuckConfig"/> for inspector authoring.</summary>
    [Serializable]
    public class DuckSettings
    {
        [Tooltip("Enable ducking on this bus — lower it while a duck trigger is active (e.g. dialogue ducks music).")]
        public bool enabled;

        [Tooltip("How far to duck: 0 = none, 1 = full silence. The ducked floor gain is (1 − amount).")]
        [Range(0f, 1f)] public float amount = 0.7f;

        [Tooltip("Seconds to ramp down to the ducked level when a trigger becomes active.")]
        [Min(0f)] public float attack = 0.1f;

        [Tooltip("Seconds to hold the ducked level after the last trigger releases.")]
        [Min(0f)] public float hold = 0.2f;

        [Tooltip("Seconds to ramp back up to full volume after the hold.")]
        [Min(0f)] public float release = 0.4f;

        public DuckConfig ToConfig() => new DuckConfig
        {
            Enabled = enabled, Amount = amount, Attack = attack, Hold = hold, Release = release,
        };
    }

    /// <summary>A logical mixing/routing bus above groups.</summary>
    [Serializable]
    public class BusDefinition
    {
        [Tooltip("Unique bus name. Groups and playlists route to it by this name.")]
        public string busName = "NewBus";

        [Tooltip("Bus volume (0–1), smoothly faded at runtime.")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Maximum simultaneous voices across all groups on this bus. 0 = unlimited.")]
        [Min(0)] public int voiceLimit = 0; // 0 = unlimited

        [Tooltip("Silence this bus.")]
        public bool mute;

        [Tooltip("Solo: when any bus is soloed, only soloed buses are audible.")]
        public bool solo;

        [Tooltip("Marks this as the default bus for playlists / music.")]
        public bool isMusicBus;

        [Tooltip("Ducking envelope applied to this bus.")]
        public DuckSettings duck = new DuckSettings();

        [Tooltip("Optional AudioMixerGroup this bus outputs to.")]
        public AudioMixerGroup mixerGroup;
    }

    /// <summary>
    /// The single design-time source of truth: all buses and sound groups for a project. At runtime
    /// it is <see cref="Bake"/>d once into flat Core arrays + parallel Unity-side arrays (clips,
    /// mixer groups, spatial settings) that the voice manager applies.
    /// </summary>
    [CreateAssetMenu(menuName = "AudioKit/Audio Database", fileName = "AudioDatabase")]
    public class AudioDatabaseAsset : ScriptableObject
    {
        [Header("Pool")]
        [Tooltip("Hard cap on simultaneous voices (AudioSources). Voice stealing kicks in at this limit.")]
        [Min(1)] public int voiceCapacity = 32;
        [Tooltip("Voices pre-warmed on load to avoid first-play hitches.")]
        [Min(0)] public int prewarmVoices = 8;

        [Tooltip("All routing/mixing buses in this project.")]
        public List<BusDefinition> buses = new List<BusDefinition>();

        [Tooltip("All sound groups in this project.")]
        public List<SoundGroupDefinition> groups = new List<SoundGroupDefinition>();

        [Tooltip("Music playlists (crossfade / sequencing).")]
        public List<PlaylistDefinition> playlists = new List<PlaylistDefinition>();

        /// <summary>Bake this database into runtime data. Allocates; call once on load.</summary>
        public BakedAudioData Bake()
        {
            int busN = buses.Count;
            int groupN = groups.Count;

            var busData = new BusData[busN];
            var busMixer = new AudioMixerGroup[busN];
            var busNames = new string[busN];
            var busIndexByName = new Dictionary<string, int>(busN);

            for (int b = 0; b < busN; b++)
            {
                var def = buses[b];
                busNames[b] = def.busName;
                if (!string.IsNullOrEmpty(def.busName))
                    busIndexByName[def.busName] = b;
                busMixer[b] = def.mixerGroup;
                busData[b] = new BusData
                {
                    Id = AudioId.From(def.busName),
                    Volume = def.volume,
                    VoiceLimit = def.voiceLimit,
                    Muted = def.mute,
                    Soloed = def.solo,
                    IsMusicBus = def.isMusicBus,
                    Duck = def.duck != null ? def.duck.ToConfig() : DuckConfig.Default,
                };
            }

            // Count variations for the flat array.
            int totalVars = 0;
            for (int g = 0; g < groupN; g++)
                totalVars += groups[g].variations != null ? groups[g].variations.Count : 0;

            var variations = new VariationData[totalVars];
            var clips = new AudioClip[totalVars];
            var addressableKeys = new string[totalVars];
            var varLoop = new bool[totalVars];
            var varRandomStart = new bool[totalVars];
            var varOverrideRolloff = new bool[totalVars];
            var varMinDist = new float[totalVars];
            var varMaxDist = new float[totalVars];

            var groupData = new GroupData[groupN];
            var groupMixer = new AudioMixerGroup[groupN];
            var groupNames = new string[groupN];

            int cursor = 0;
            for (int g = 0; g < groupN; g++)
            {
                var def = groups[g];
                groupNames[g] = def.groupName;
                groupMixer[g] = def.mixerGroup;

                int offset = cursor;
                int count = def.variations != null ? def.variations.Count : 0;
                for (int i = 0; i < count; i++)
                {
                    var v = def.variations[i];
                    variations[cursor] = new VariationData
                    {
                        Volume = v.volume,
                        Pitch = v.pitch,
                        Weight = v.weight,
                    };
                    clips[cursor] = v.clip;
                    addressableKeys[cursor] = v.addressableKey;
                    varLoop[cursor] = v.loop;
                    varRandomStart[cursor] = v.randomizeStartPosition;
                    varOverrideRolloff[cursor] = v.overrideRolloff;
                    varMinDist[cursor] = v.minDistance;
                    varMaxDist[cursor] = v.maxDistance;
                    cursor++;
                }

                int busIndex = -1;
                if (!string.IsNullOrEmpty(def.busName))
                    busIndexByName.TryGetValue(def.busName, out busIndex);

                groupData[g] = new GroupData
                {
                    Id = AudioId.From(def.groupName),
                    VariationOffset = offset,
                    VariationCount = count,
                    VolumeMin = def.volumeMin,
                    VolumeMax = def.volumeMax,
                    PitchMin = def.pitchMin,
                    PitchMax = def.pitchMax,
                    VoiceLimit = Mathf.Max(1, def.voiceLimit),
                    RetriggerPercent = def.retriggerPercent,
                    BusIndex = busIndex,
                    PlayMode = def.playMode,
                    StealPolicy = def.stealPolicy,
                    Is3D = def.is3D,
                };
            }

            // Playlists (Phase 2 music system).
            int defaultMusicBus = -1;
            for (int b = 0; b < busN; b++)
                if (buses[b].isMusicBus) { defaultMusicBus = b; break; }

            var bakedPlaylists = new BakedPlaylist[playlists.Count];
            for (int p = 0; p < playlists.Count; p++)
            {
                var pl = playlists[p];
                int trackN = pl.tracks != null ? pl.tracks.Count : 0;
                var pClips = new AudioClip[trackN];
                var pKeys = new string[trackN];
                var pVols = new float[trackN];
                for (int t = 0; t < trackN; t++)
                {
                    var track = pl.tracks[t];
                    pClips[t] = track.clip;
                    pKeys[t] = track.addressableKey;
                    pVols[t] = track.volume;
                }

                int plBus = defaultMusicBus;
                if (!string.IsNullOrEmpty(pl.busName) && busIndexByName.TryGetValue(pl.busName, out int resolved))
                    plBus = resolved;

                bakedPlaylists[p] = new BakedPlaylist
                {
                    Id = AudioId.From(pl.playlistName),
                    Name = pl.playlistName,
                    Mode = pl.mode,
                    CrossfadeSeconds = pl.crossfadeSeconds,
                    Gapless = pl.gapless,
                    BusIndex = plBus,
                    Clips = pClips,
                    AddressableKeys = pKeys,
                    Volumes = pVols,
                };
            }

            return new BakedAudioData
            {
                Groups = groupData,
                Playlists = bakedPlaylists,
                Buses = busData,
                Variations = variations,
                Clips = clips,
                AddressableKeys = addressableKeys,
                VarLoop = varLoop,
                VarRandomStart = varRandomStart,
                VarOverrideRolloff = varOverrideRolloff,
                VarMinDistance = varMinDist,
                VarMaxDistance = varMaxDist,
                GroupMixer = groupMixer,
                BusMixer = busMixer,
                GroupNames = groupNames,
                BusNames = busNames,
                VoiceCapacity = Mathf.Max(1, voiceCapacity),
                PrewarmVoices = Mathf.Clamp(prewarmVoices, 0, Mathf.Max(1, voiceCapacity)),
            };
        }
    }
}
