using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Scene/prefab-scoped sound groups. Point this at a <see cref="SoundGroupSetAsset"/> and its
    /// groups are registered into the running AudioKit while this object is enabled, then unregistered
    /// (and optionally their clip audio data unloaded) when it is disabled or destroyed. Put it on a
    /// level root, a spawned prefab, or a UI screen so those objects bring their own sounds and clean
    /// up after themselves. Registration is ref-counted, so several sources may share a set, and it is
    /// safe to enable before AudioKit has bootstrapped (the set is registered as soon as it is ready).
    /// </summary>
    [AddComponentMenu("AudioKit/AudioKit Group Source")]
    public sealed class AudioKitGroupSource : MonoBehaviour
    {
        [Tooltip("The sound groups this object owns while enabled.")]
        [SerializeField] private SoundGroupSetAsset groupSet;

        [Tooltip("Register automatically on enable / unregister on disable. Turn off to drive it from code.")]
        [SerializeField] private bool registerOnEnable = true;

        private bool _registered;

        public SoundGroupSetAsset GroupSet
        {
            get => groupSet;
            set
            {
                if (groupSet == value) return;
                bool wasRegistered = _registered;
                if (wasRegistered) Unregister();
                groupSet = value;
                if (wasRegistered && isActiveAndEnabled) Register();
            }
        }

        private void OnEnable()
        {
            if (registerOnEnable) Register();
        }

        private void OnDisable() => Unregister();

        /// <summary>Register this source's group set (idempotent).</summary>
        public void Register()
        {
            if (_registered || groupSet == null) return;
            AK.RegisterGroups(groupSet);
            _registered = true;
        }

        /// <summary>Unregister this source's group set (idempotent).</summary>
        public void Unregister()
        {
            if (!_registered || groupSet == null) return;
            AK.UnregisterGroups(groupSet);
            _registered = false;
        }
    }
}
