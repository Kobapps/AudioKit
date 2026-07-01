namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// Headless per-variation parameters. The actual <c>AudioClip</c>/<c>AssetReference</c> lives in
    /// the Unity layer and is addressed by the variation's index within its group.
    /// </summary>
    public struct VariationData
    {
        public float Volume;   // linear multiplier (0..1+)
        public float Pitch;    // multiplier (1 = unchanged)
        public float Weight;   // relative weight for Weighted play mode (>= 0)

        public static VariationData Default => new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f };
    }

    /// <summary>
    /// Headless settings for a Sound Group. References to variations and bus are by index into the
    /// engine's flat arrays. No Unity types — fully value-based.
    /// </summary>
    public struct GroupData
    {
        public AudioId Id;

        // Variation slice within the engine's shared variation array.
        public int VariationOffset;
        public int VariationCount;

        // Randomized per-play ranges (group level, multiplied onto the chosen variation).
        public float VolumeMin;
        public float VolumeMax;
        public float PitchMin;
        public float PitchMax;

        public int VoiceLimit;          // max simultaneous voices in this group (>=1)
        public float RetriggerPercent;  // 0..100; below this % of clip elapsed, retrigger is suppressed
        public int BusIndex;            // -1 for the implicit master bus

        public PlayMode PlayMode;
        public VoiceStealPolicy StealPolicy;
        public bool Is3D;

        public static GroupData Default => new GroupData
        {
            Id = AudioId.None,
            VariationOffset = 0,
            VariationCount = 0,
            VolumeMin = 1f,
            VolumeMax = 1f,
            PitchMin = 1f,
            PitchMax = 1f,
            VoiceLimit = 8,
            RetriggerPercent = 0f,
            BusIndex = -1,
            PlayMode = PlayMode.Random,
            StealPolicy = VoiceStealPolicy.StealOldest,
            Is3D = false,
        };
    }

    /// <summary>Ducking envelope configuration for a bus (deterministic ADHR).</summary>
    public struct DuckConfig
    {
        public bool Enabled;
        public float Amount;   // 0..1, how far to duck (gain floor = 1 - Amount)
        public float Attack;   // seconds to reach the floor
        public float Hold;     // seconds to hold the floor after the last trigger releases
        public float Release;  // seconds to return to unity gain

        public static DuckConfig Default => new DuckConfig
        {
            Enabled = false,
            Amount = 0.7f,
            Attack = 0.1f,
            Hold = 0.2f,
            Release = 0.4f,
        };
    }

    /// <summary>Headless settings for a Bus.</summary>
    public struct BusData
    {
        public AudioId Id;
        public float Volume;     // target linear volume (0..1+); smoothed by a FadeEnvelope at runtime
        public int VoiceLimit;   // max simultaneous voices across all member groups (0 = unlimited)
        public bool Muted;
        public bool Soloed;
        public bool IsMusicBus;  // Phase-2 seam: playlist/crossfade routes here
        public DuckConfig Duck;

        public static BusData Default => new BusData
        {
            Id = AudioId.None,
            Volume = 1f,
            VoiceLimit = 0,
            Muted = false,
            Soloed = false,
            IsMusicBus = false,
            Duck = DuckConfig.Default,
        };
    }
}
