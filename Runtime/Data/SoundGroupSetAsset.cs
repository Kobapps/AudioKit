using System.Collections.Generic;
using UnityEngine;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// A portable set of Sound Groups that can be registered into a running AudioKit at runtime and
    /// unregistered later — e.g. a level's or a prefab's own sounds. Drop it on an
    /// <see cref="AudioKitGroupSource"/> in a scene/prefab, or register it from script via
    /// <see cref="AudioKit.RegisterGroups(SoundGroupSetAsset)"/>. Groups route to buses that already
    /// exist in the main database (by name); new buses cannot be added at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "AudioKit/Sound Group Set", fileName = "SoundGroupSet")]
    public sealed class SoundGroupSetAsset : ScriptableObject
    {
        [Tooltip("Load/unload each clip's audio data alongside registration. Use for scene/prefab-scoped " +
                 "memory: set the clips' import 'Load Type' so their audio data is not preloaded, and AudioKit " +
                 "will LoadAudioData on register and UnloadAudioData on unregister.")]
        public bool manageClipMemory = false;

        [Tooltip("The groups registered into AudioKit while this set is active.")]
        public List<SoundGroupDefinition> groups = new List<SoundGroupDefinition>();
    }
}
