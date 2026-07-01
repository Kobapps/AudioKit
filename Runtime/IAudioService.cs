using Kobapps.AudioKit.Core;
using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// The injectable AudioKit surface. The static <see cref="AudioKit"/> facade forwards to the
    /// active instance; DI containers can resolve this directly. All ids are hashed
    /// <see cref="AudioId"/>s — string overloads on the facade hash once and call through.
    /// </summary>
    public interface IAudioService
    {
        bool IsReady { get; }

        // --- Playback ---------------------------------------------------------------------------
        SoundHandle Play(AudioId group, float volumeScale = 1f, float fadeInSeconds = 0f);
        SoundHandle PlayAt(AudioId group, Vector3 position, float volumeScale = 1f, float fadeInSeconds = 0f);
        SoundHandle PlayFollow(AudioId group, Transform follow, float volumeScale = 1f, float fadeInSeconds = 0f);

        void StopGroup(AudioId group, float fadeSeconds = 0f);
        void StopAll(float fadeSeconds = 0f);
        void PauseAll();
        void UnpauseAll();

        // --- Music / playlists (Phase 2) --------------------------------------------------------
        void PlayPlaylist(AudioId playlist);
        void StopPlaylist(float fadeSeconds = 0f);
        void NextTrack();
        bool IsPlaylistPlaying { get; }

        // --- Per-group volume / mute + diagnostics + virtualization -----------------------------
        void SetGroupVolume(AudioId group, float volume);
        float GetGroupVolume(AudioId group);
        void MuteGroup(AudioId group, bool muted);
        bool IsGroupMuted(AudioId group);
        float VirtualizeDistance { get; set; }
        int ActiveVoiceCount { get; }
        int VoiceCapacity { get; }

        // --- Dynamic groups ---------------------------------------------------------------------
        GroupRegistration RegisterGroups(SoundGroupSetAsset set);
        void UnregisterGroups(GroupRegistration registration);
        bool RegisterGroup(SoundGroupDefinition def);
        bool UnregisterGroup(AudioId group);
        bool IsGroupRegistered(AudioId group);

        // --- Buses ------------------------------------------------------------------------------
        void FadeBus(AudioId bus, float toVolume, float seconds, FadeCurve curve = FadeCurve.Linear);
        void SetBusVolume(AudioId bus, float volume);
        float GetBusVolume(AudioId bus);
        void MuteBus(AudioId bus, bool muted);
        void SoloBus(AudioId bus, bool soloed);
        void DuckBus(AudioId bus);
        void UnduckBus(AudioId bus);

        float MasterVolume { get; set; }

        // --- Custom events ----------------------------------------------------------------------
        void FireEvent(AudioId eventId);
        void SubscribeEvent(AudioId eventId, System.Action handler);
        void UnsubscribeEvent(AudioId eventId, System.Action handler);

        // --- Handle operations (called by SoundHandle) ------------------------------------------
        bool IsVoiceValid(int slot, int generation);
        bool IsVoicePlaying(int slot, int generation);
        void StopVoice(int slot, int generation, float fadeSeconds);
        void SetVoiceVolume(int slot, int generation, float volume);
        void SetVoicePitch(int slot, int generation, float pitch);
        void SetVoicePosition(int slot, int generation, Vector3 worldPosition);

        // --- Introspection (editor overlay / debugging) -----------------------------------------
        BakedAudioData Data { get; }
        AudioKitCoreEngine Engine { get; }
    }
}
