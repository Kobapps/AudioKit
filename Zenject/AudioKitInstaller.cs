#if AUDIOKIT_ZENJECT
using UnityEngine;
using Zenject;

namespace Kobapps.AudioKit.Zenject
{
    /// <summary>
    /// Optional Zenject installer. Bakes a database into an <see cref="AudioService"/>, publishes it
    /// to the static <see cref="AudioKit"/> facade, and binds <see cref="IAudioService"/> for
    /// constructor injection. Only compiled when Extenject is present (AUDIOKIT_ZENJECT).
    ///
    /// Add it as a MonoInstaller on a GameObjectContext / SceneContext and assign the database.
    /// </summary>
    [AddComponentMenu("AudioKit/AudioKit Installer (Zenject)")]
    public sealed class AudioKitInstaller : MonoInstaller
    {
        [Tooltip("The audio database baked into the AudioService and bound as IAudioService.")]
        [SerializeField] private AudioDatabaseAsset database;

        public override void InstallBindings()
        {
            var runtime = AudioKitRuntime.Create(database);
            Container.Bind<IAudioService>().FromInstance(runtime.Service).AsSingle();
            Container.Bind<AudioKitRuntime>().FromInstance(runtime).AsSingle();
        }
    }
}
#endif
