namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// Deterministic attack/hold/release ducking envelope for a single bus. While one or more duck
    /// triggers are active the gain ramps down to the floor (1 - amount) over the attack time and
    /// stays there. When the last trigger releases the floor is held for <c>Hold</c> seconds, then
    /// the gain ramps back to 1 over the release time. Pure value type, ticked once per frame.
    /// </summary>
    public struct DuckEnvelope
    {
        public enum Phase : byte { Idle, Attack, Sustain, Hold, Release }

        private DuckConfig _config;
        private int _activeTriggers;
        private float _gain;       // current multiplier in [floor, 1]
        private float _holdTimer;  // counts down during Hold
        private Phase _phase;

        public float Gain => _gain;
        public Phase CurrentPhase => _phase;
        public int ActiveTriggers => _activeTriggers;

        public void Configure(in DuckConfig config)
        {
            _config = config;
            if (_gain == 0f && _phase == Phase.Idle)
                _gain = 1f;
        }

        public void Reset()
        {
            _activeTriggers = 0;
            _gain = 1f;
            _holdTimer = 0f;
            _phase = Phase.Idle;
        }

        private float Floor
        {
            get
            {
                float f = 1f - _config.Amount;
                if (f < 0f) f = 0f;
                if (f > 1f) f = 1f;
                return f;
            }
        }

        /// <summary>Register a new duck trigger (e.g. a dialogue line started).</summary>
        public void AddTrigger()
        {
            _activeTriggers++;
            if (_config.Enabled)
                _phase = Phase.Attack;
        }

        /// <summary>Release one duck trigger. When the count hits zero the hold/release begins.</summary>
        public void RemoveTrigger()
        {
            if (_activeTriggers > 0)
                _activeTriggers--;
            if (_activeTriggers == 0 && _config.Enabled)
            {
                _phase = Phase.Hold;
                _holdTimer = _config.Hold;
            }
        }

        /// <summary>Advance the envelope by <paramref name="dt"/> seconds. Returns the current gain.</summary>
        public float Tick(float dt)
        {
            if (!_config.Enabled)
            {
                _gain = 1f;
                _phase = Phase.Idle;
                return _gain;
            }

            float floor = Floor;

            switch (_phase)
            {
                case Phase.Attack:
                {
                    float rate = _config.Attack > 0f ? (1f - floor) / _config.Attack : float.MaxValue;
                    _gain -= rate * dt;
                    if (_gain <= floor)
                    {
                        _gain = floor;
                        _phase = Phase.Sustain;
                    }
                    break;
                }
                case Phase.Sustain:
                    _gain = floor;
                    break;

                case Phase.Hold:
                    _gain = floor;
                    _holdTimer -= dt;
                    if (_holdTimer <= 0f)
                        _phase = Phase.Release;
                    break;

                case Phase.Release:
                {
                    float rate = _config.Release > 0f ? (1f - floor) / _config.Release : float.MaxValue;
                    _gain += rate * dt;
                    if (_gain >= 1f)
                    {
                        _gain = 1f;
                        _phase = Phase.Idle;
                    }
                    break;
                }
                case Phase.Idle:
                default:
                    _gain = 1f;
                    break;
            }

            return _gain;
        }
    }
}
