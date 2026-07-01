using System;
using System.Collections.Generic;
using Kobapps.AudioKit.Core;
using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Static convenience facade over the active <see cref="IAudioService"/>. String overloads hash
    /// the name once via <see cref="AudioId.From"/> and call through; prefer the <see cref="AudioId"/>
    /// overloads on hot paths and cache ids with <see cref="Id"/>. When no service is active every
    /// call is a safe no-op returning <see cref="SoundHandle.Invalid"/>.
    /// </summary>
    public static class AudioKit
    {
        private static IAudioService _service;

        /// <summary>The active service. Set by <see cref="AudioKitRuntime"/> bootstrap or a DI installer.</summary>
        public static IAudioService Service
        {
            get => _service;
            set
            {
                _service = value;
                if (_service != null) FlushPendingRegistrations();
                else MarkAllRegistrationsPending();
            }
        }

        public static bool IsReady => Service != null && Service.IsReady;

        /// <summary>Cache a hashed id once and reuse it to avoid re-hashing strings.</summary>
        public static AudioId Id(string name) => AudioId.From(name);

        // --- Playback ---------------------------------------------------------------------------

        public static SoundHandle Play(AudioId group, float volumeScale = 1f, float fadeInSeconds = 0f)
        {
            var s = Service;
            return s != null ? s.Play(group, volumeScale, fadeInSeconds) : SoundHandle.Invalid;
        }

        public static SoundHandle Play(string group, float volumeScale = 1f, float fadeInSeconds = 0f) =>
            Play(AudioId.From(group), volumeScale, fadeInSeconds);

        public static SoundHandle PlayAt(AudioId group, Vector3 position, float volumeScale = 1f, float fadeInSeconds = 0f)
        {
            var s = Service;
            return s != null ? s.PlayAt(group, position, volumeScale, fadeInSeconds) : SoundHandle.Invalid;
        }

        public static SoundHandle PlayAt(string group, Vector3 position, float volumeScale = 1f, float fadeInSeconds = 0f) =>
            PlayAt(AudioId.From(group), position, volumeScale, fadeInSeconds);

        public static SoundHandle PlayFollow(AudioId group, Transform follow, float volumeScale = 1f, float fadeInSeconds = 0f)
        {
            var s = Service;
            return s != null ? s.PlayFollow(group, follow, volumeScale, fadeInSeconds) : SoundHandle.Invalid;
        }

        public static SoundHandle PlayFollow(string group, Transform follow, float volumeScale = 1f, float fadeInSeconds = 0f) =>
            PlayFollow(AudioId.From(group), follow, volumeScale, fadeInSeconds);

        // --- Per-group volume / mute + diagnostics ----------------------------------------------

        public static void SetGroupVolume(AudioId group, float volume) => Service?.SetGroupVolume(group, volume);
        public static void SetGroupVolume(string group, float volume) => Service?.SetGroupVolume(AudioId.From(group), volume);

        public static float GetGroupVolume(AudioId group) => Service != null ? Service.GetGroupVolume(group) : 1f;
        public static float GetGroupVolume(string group) => GetGroupVolume(AudioId.From(group));

        public static void MuteGroup(AudioId group, bool muted) => Service?.MuteGroup(group, muted);
        public static void MuteGroup(string group, bool muted) => Service?.MuteGroup(AudioId.From(group), muted);

        public static bool IsGroupMuted(AudioId group) => Service != null && Service.IsGroupMuted(group);
        public static bool IsGroupMuted(string group) => IsGroupMuted(AudioId.From(group));

        /// <summary>3D one-shots beyond this distance from the listener are skipped (no voice). 0 = off.</summary>
        public static float VirtualizeDistance
        {
            get => Service != null ? Service.VirtualizeDistance : 0f;
            set { if (Service != null) Service.VirtualizeDistance = value; }
        }

        /// <summary>Number of currently live voices.</summary>
        public static int ActiveVoiceCount => Service != null ? Service.ActiveVoiceCount : 0;
        /// <summary>Hard voice cap (pool size).</summary>
        public static int VoiceCapacity => Service != null ? Service.VoiceCapacity : 0;

        public static void StopGroup(AudioId group, float fadeSeconds = 0f) => Service?.StopGroup(group, fadeSeconds);
        public static void StopGroup(string group, float fadeSeconds = 0f) => Service?.StopGroup(AudioId.From(group), fadeSeconds);

        public static void StopAll(float fadeSeconds = 0f) => Service?.StopAll(fadeSeconds);
        public static void PauseAll() => Service?.PauseAll();
        public static void UnpauseAll() => Service?.UnpauseAll();

        // --- Music / playlists (Phase 2) --------------------------------------------------------

        public static void PlayPlaylist(AudioId playlist) => Service?.PlayPlaylist(playlist);
        public static void PlayPlaylist(string playlist) => Service?.PlayPlaylist(AudioId.From(playlist));
        public static void StopPlaylist(float fadeSeconds = 0f) => Service?.StopPlaylist(fadeSeconds);
        public static void NextTrack() => Service?.NextTrack();
        public static bool IsPlaylistPlaying => Service != null && Service.IsPlaylistPlaying;

        // --- Buses ------------------------------------------------------------------------------

        public static void FadeBus(AudioId bus, float to, float seconds, FadeCurve curve = FadeCurve.Linear) =>
            Service?.FadeBus(bus, to, seconds, curve);

        public static void FadeBus(string bus, float to, float seconds, FadeCurve curve = FadeCurve.Linear) =>
            Service?.FadeBus(AudioId.From(bus), to, seconds, curve);

        public static void SetBusVolume(AudioId bus, float volume) => Service?.SetBusVolume(bus, volume);
        public static void SetBusVolume(string bus, float volume) => Service?.SetBusVolume(AudioId.From(bus), volume);

        public static float GetBusVolume(AudioId bus) => Service != null ? Service.GetBusVolume(bus) : 1f;
        public static float GetBusVolume(string bus) => GetBusVolume(AudioId.From(bus));

        public static void MuteBus(AudioId bus, bool muted) => Service?.MuteBus(bus, muted);
        public static void MuteBus(string bus, bool muted) => Service?.MuteBus(AudioId.From(bus), muted);

        public static void SoloBus(AudioId bus, bool soloed) => Service?.SoloBus(bus, soloed);
        public static void SoloBus(string bus, bool soloed) => Service?.SoloBus(AudioId.From(bus), soloed);

        public static void DuckBus(AudioId bus) => Service?.DuckBus(bus);
        public static void DuckBus(string bus) => Service?.DuckBus(AudioId.From(bus));

        public static void UnduckBus(AudioId bus) => Service?.UnduckBus(bus);
        public static void UnduckBus(string bus) => Service?.UnduckBus(AudioId.From(bus));

        public static float MasterVolume
        {
            get => Service != null ? Service.MasterVolume : 1f;
            set { if (Service != null) Service.MasterVolume = value; }
        }

        // --- Custom events ----------------------------------------------------------------------

        public static void FireEvent(AudioId eventId) => Service?.FireEvent(eventId);
        public static void FireEvent(string eventId) => Service?.FireEvent(AudioId.From(eventId));

        public static void SubscribeEvent(AudioId eventId, Action handler) => Service?.SubscribeEvent(eventId, handler);
        public static void SubscribeEvent(string eventId, Action handler) => Service?.SubscribeEvent(AudioId.From(eventId), handler);

        public static void UnsubscribeEvent(AudioId eventId, Action handler) => Service?.UnsubscribeEvent(eventId, handler);
        public static void UnsubscribeEvent(string eventId, Action handler) => Service?.UnsubscribeEvent(AudioId.From(eventId), handler);

        // --- Dynamic group registration ---------------------------------------------------------
        //
        // Registrations are ref-counted per SoundGroupSetAsset so multiple components can share a set.
        // If the service isn't up yet (e.g. a group source enables before bootstrap), the set is queued
        // and registered when the service becomes active; on service teardown everything is re-queued.

        private sealed class RegEntry { public GroupRegistration Handle; public int RefCount; public bool Pending; }
        private static readonly Dictionary<SoundGroupSetAsset, RegEntry> _regs = new Dictionary<SoundGroupSetAsset, RegEntry>();

        /// <summary>Register a set of groups (ref-counted). Safe to call before the service is ready.</summary>
        public static void RegisterGroups(SoundGroupSetAsset set)
        {
            if (set == null) return;
            if (_regs.TryGetValue(set, out var e)) { e.RefCount++; return; }

            e = new RegEntry { RefCount = 1 };
            _regs[set] = e;

            var s = Service;
            if (s != null) { e.Handle = s.RegisterGroups(set); e.Pending = false; }
            else e.Pending = true;
        }

        /// <summary>Release a set registered with <see cref="RegisterGroups"/>; removed once its last user unregisters.</summary>
        public static void UnregisterGroups(SoundGroupSetAsset set)
        {
            if (set == null || !_regs.TryGetValue(set, out var e)) return;
            if (--e.RefCount > 0) return;

            _regs.Remove(set);
            var s = Service;
            if (!e.Pending && e.Handle != null) s?.UnregisterGroups(e.Handle);
        }

        /// <summary>Register a single group built in code. Requires an active service.</summary>
        public static bool RegisterGroup(SoundGroupDefinition def)
        {
            var s = Service;
            return s != null && s.RegisterGroup(def);
        }

        public static bool UnregisterGroup(AudioId group)
        {
            var s = Service;
            return s != null && s.UnregisterGroup(group);
        }

        public static bool UnregisterGroup(string group) => UnregisterGroup(AudioId.From(group));

        public static bool IsGroupRegistered(AudioId group)
        {
            var s = Service;
            return s != null && s.IsGroupRegistered(group);
        }

        public static bool IsGroupRegistered(string group) => IsGroupRegistered(AudioId.From(group));

        private static void FlushPendingRegistrations()
        {
            var s = _service;
            if (s == null) return;
            foreach (var kv in _regs)
            {
                if (kv.Value.Pending)
                {
                    kv.Value.Handle = s.RegisterGroups(kv.Key);
                    kv.Value.Pending = false;
                }
            }
        }

        private static void MarkAllRegistrationsPending()
        {
            foreach (var kv in _regs)
            {
                kv.Value.Handle = null;
                kv.Value.Pending = true;
            }
        }
    }
}
