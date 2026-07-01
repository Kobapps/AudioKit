namespace Kobapps.AudioKit.Core
{
    /// <summary>How a Sound Group picks which variation to play on each trigger.</summary>
    public enum PlayMode
    {
        /// <summary>Uniformly random; may repeat the same variation back to back.</summary>
        Random = 0,

        /// <summary>Uniformly random but never the variation that played last.</summary>
        RandomNoImmediateRepeat = 1,

        /// <summary>Cycles through every variation; starting phase is randomized once.</summary>
        RoundRobin = 2,

        /// <summary>Cycles 0,1,2,…,n-1 deterministically from the first play.</summary>
        Sequential = 3,

        /// <summary>Random, biased by each variation's weight.</summary>
        Weighted = 4,

        /// <summary>Least-recently-used: the variation idle longest is chosen next.</summary>
        Oldest = 5,
    }

    /// <summary>What a group does when asked to play while already at its voice limit.</summary>
    public enum VoiceStealPolicy
    {
        /// <summary>Stop the oldest live voice in the group and reuse it.</summary>
        StealOldest = 0,

        /// <summary>Refuse the new play; return an invalid handle.</summary>
        Reject = 1,

        /// <summary>Stop the quietest live voice in the group and reuse it.</summary>
        StealQuietest = 2,
    }

    /// <summary>Interpolation shape used by fades.</summary>
    public enum FadeCurve
    {
        /// <summary>Constant-rate linear interpolation.</summary>
        Linear = 0,

        /// <summary>Smoothstep ease in/out.</summary>
        EaseInOut = 1,

        /// <summary>Equal-power (sin/cos) — keeps perceived loudness constant across a crossfade.</summary>
        EqualPower = 2,
    }
}
