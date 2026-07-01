#if AUDIOKIT_ADDRESSABLES
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Kobapps.AudioKit.AddressablesSupport
{
    /// <summary>
    /// Lazily loads any <see cref="AudioVariation.addressableKey"/>s into the baked clip table before
    /// they are needed. Dependency-free in the core package — this asmdef only compiles when
    /// Addressables is installed (AUDIOKIT_ADDRESSABLES). The core stores plain string keys, so no
    /// Addressables type leaks into the runtime assembly.
    /// </summary>
    public static class AddressablesClipProvider
    {
        /// <summary>
        /// Warm every addressable variation in <paramref name="service"/>'s database, writing the
        /// loaded clip into its slot. Invokes <paramref name="onComplete"/> once all loads settle.
        /// </summary>
        public static void Warmup(IAudioService service, Action onComplete = null)
        {
            if (service == null) { onComplete?.Invoke(); return; }
            BakedAudioData data = service.Data;
            string[] keys = data.AddressableKeys;
            if (keys == null) { onComplete?.Invoke(); return; }

            int pending = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                if (data.Clips[i] != null || string.IsNullOrEmpty(keys[i]))
                    continue;

                pending++;
                int index = i;
                AsyncOperationHandle<AudioClip> op = Addressables.LoadAssetAsync<AudioClip>(keys[index]);
                op.Completed += handle =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                        data.Clips[index] = handle.Result;
                    else
                        Debug.LogWarning($"AudioKit: failed to load addressable clip '{keys[index]}'.");

                    if (--pending == 0)
                        onComplete?.Invoke();
                };
            }

            if (pending == 0)
                onComplete?.Invoke();
        }
    }
}
#endif
