using Kobapps.AudioKit.Core;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// The runtime host. Bakes an <see cref="AudioDatabaseAsset"/> into an <see cref="AudioService"/>,
    /// parents the pooled voices under itself, ticks the service each frame and publishes it to the
    /// static <see cref="AudioKit"/> facade. Survives scene loads.
    ///
    /// Three ways to start it:
    /// <list type="bullet">
    /// <item>Drop a database named <c>AudioKitDatabase</c> in a <c>Resources</c> folder — it auto-boots.</item>
    /// <item>Add this component to a scene object and assign the database in the inspector.</item>
    /// <item>Call <see cref="Create"/> from code or a DI installer.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-5000)]
    [AddComponentMenu("AudioKit/AudioKit Runtime")]
    public sealed class AudioKitRuntime : MonoBehaviour
    {
        private const string DefaultResourcePath = "AudioKitDatabase";

        [Tooltip("The audio database to bake on startup. If null, tries Resources/AudioKitDatabase.")]
        [SerializeField] private AudioDatabaseAsset database;

        [Tooltip("Use unscaled time so fades/ducking keep running while the game is paused (timeScale 0).")]
        [SerializeField] private bool useUnscaledTime = true;

        [Tooltip("Optional fixed RNG seed for reproducible variation selection. 0 = nondeterministic.")]
        [SerializeField] private uint randomSeed = 0;

        public static AudioKitRuntime Instance { get; private set; }

        public AudioService Service { get; private set; }
        public AudioDatabaseAsset Database => database;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            transform.SetParent(null, false);
            DontDestroyOnLoad(gameObject);

            if (Service == null && database != null)
                InitializeFrom(database);
        }

        private void Start()
        {
            // If nothing bootstrapped us (no inspector database and no Resources/AudioKitDatabase),
            // AudioKit.Play() would be silently ignored — make that obvious instead.
            if (Service == null)
                Debug.LogWarning("AudioKitRuntime has no database assigned (and no Resources/AudioKitDatabase " +
                                 "was found). AudioKit playback will be silent — assign a database on this component.", this);
        }

        private void InitializeFrom(AudioDatabaseAsset db)
        {
            database = db;
            BakedAudioData baked = db.Bake();
            IRandom rng = randomSeed != 0 ? new XorShiftRandom(randomSeed) : new XorShiftRandom();
            Service = new AudioService(baked, transform, rng);
            AK.Service = Service;
        }

        private void Update()
        {
            if (Service == null) return;
            Service.Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Service?.Shutdown();
            if (AK.Service == Service)
                AK.Service = null;
            Instance = null;
        }

        /// <summary>Create and bootstrap a runtime from a database (for code / DI startup).</summary>
        public static AudioKitRuntime Create(AudioDatabaseAsset db)
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("AudioKit");
            var runtime = go.AddComponent<AudioKitRuntime>();
            runtime.InitializeFrom(db);
            // Awake will have run during AddComponent and set Instance + DontDestroyOnLoad.
            return runtime;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (AudioKit.Service != null)
                return;
            var db = Resources.Load<AudioDatabaseAsset>(DefaultResourcePath);
            if (db != null)
                Create(db);
        }
    }
}
