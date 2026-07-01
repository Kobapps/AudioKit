using Kobapps.AudioKit.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// The product of <see cref="AudioDatabaseAsset.Bake"/>: flat Core tables plus the parallel
    /// Unity-side arrays (clips, mixer groups, spatial settings) indexed identically so the voice
    /// manager can apply a chosen variation with no per-play lookups beyond an array index.
    /// </summary>
    public sealed class BakedAudioData
    {
        // Core tables (fed to AudioKitCoreEngine.Configure).
        public GroupData[] Groups;
        public BusData[] Buses;
        public VariationData[] Variations;

        // Parallel per-variation Unity data (indexed by global variation index).
        public AudioClip[] Clips;
        public string[] AddressableKeys; // optional Addressables keys (empty when not used)
        public bool[] VarLoop;
        public bool[] VarRandomStart;
        public bool[] VarOverrideRolloff;
        public float[] VarMinDistance;
        public float[] VarMaxDistance;

        // Per group / per bus Unity data.
        public AudioMixerGroup[] GroupMixer;
        public AudioMixerGroup[] BusMixer;

        // Phase-2 music playlists.
        public BakedPlaylist[] Playlists;

        // Editor/debug names.
        public string[] GroupNames;
        public string[] BusNames;

        public int VoiceCapacity;
        public int PrewarmVoices;

        public string GroupName(int index) =>
            GroupNames != null && (uint)index < (uint)GroupNames.Length && !string.IsNullOrEmpty(GroupNames[index])
                ? GroupNames[index] : "<none>";

        public string BusName(int index) =>
            (uint)index < (uint)BusNames.Length ? BusNames[index] : "<master>";

        /// <summary>Grow the per-variation arrays so index [0, count) is addressable (for dynamic groups).</summary>
        public void EnsureVariationCapacity(int count)
        {
            if (Clips != null && Clips.Length >= count) return;
            System.Array.Resize(ref Clips, count);
            System.Array.Resize(ref AddressableKeys, count);
            System.Array.Resize(ref VarLoop, count);
            System.Array.Resize(ref VarRandomStart, count);
            System.Array.Resize(ref VarOverrideRolloff, count);
            System.Array.Resize(ref VarMinDistance, count);
            System.Array.Resize(ref VarMaxDistance, count);
        }

        /// <summary>Grow the per-group arrays so index [0, count) is addressable (for dynamic groups).</summary>
        public void EnsureGroupCapacity(int count)
        {
            if (GroupMixer != null && GroupMixer.Length >= count) return;
            System.Array.Resize(ref GroupMixer, count);
            System.Array.Resize(ref GroupNames, count);
        }
    }
}
