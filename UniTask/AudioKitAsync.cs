#if AUDIOKIT_UNITASK
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Kobapps.AudioKit.Core;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Optional UniTask convenience layer. Only compiled when UniTask is installed
    /// (AUDIOKIT_UNITASK). The base <see cref="AudioKit"/> API stays fully synchronous and
    /// allocation-free; these helpers add awaitable sugar for "play and wait", fades and handle
    /// completion. Awaiting a looping voice never returns — use these for one-shots.
    /// </summary>
    public static class AudioKitAsync
    {
        /// <summary>Await until the voice behind <paramref name="handle"/> stops (or the token cancels).</summary>
        public static async UniTask WaitForCompletion(SoundHandle handle, CancellationToken cancellationToken = default)
        {
            while (handle.IsValid && handle.IsPlaying)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        /// <summary>Play a group and await its completion.</summary>
        public static UniTask PlayAndForget(AudioId group, float volumeScale = 1f, CancellationToken cancellationToken = default)
            => WaitForCompletion(AK.Play(group, volumeScale), cancellationToken);

        public static UniTask PlayAndForget(string group, float volumeScale = 1f, CancellationToken cancellationToken = default)
            => WaitForCompletion(AK.Play(group, volumeScale), cancellationToken);

        /// <summary>Play at a world position and await completion.</summary>
        public static UniTask PlayAtAndForget(AudioId group, Vector3 position, float volumeScale = 1f, CancellationToken cancellationToken = default)
            => WaitForCompletion(AK.PlayAt(group, position, volumeScale), cancellationToken);

        /// <summary>Start a bus fade and await the fade duration.</summary>
        public static async UniTask FadeBusAsync(AudioId bus, float toVolume, float seconds, FadeCurve curve = FadeCurve.Linear, CancellationToken cancellationToken = default)
        {
            AK.FadeBus(bus, toVolume, seconds, curve);
            if (seconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellationToken);
        }

        public static UniTask FadeBusAsync(string bus, float toVolume, float seconds, FadeCurve curve = FadeCurve.Linear, CancellationToken cancellationToken = default)
            => FadeBusAsync(AudioId.From(bus), toVolume, seconds, curve, cancellationToken);
    }

    /// <summary>UniTask extensions on <see cref="SoundHandle"/>.</summary>
    public static class SoundHandleUniTaskExtensions
    {
        /// <summary>Await until this voice stops (or the token cancels).</summary>
        public static UniTask WaitForCompletionAsync(this SoundHandle handle, CancellationToken cancellationToken = default)
            => AudioKitAsync.WaitForCompletion(handle, cancellationToken);
    }
}
#endif
