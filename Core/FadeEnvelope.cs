using System;

namespace Kobapps.AudioKit.Core
{
    /// <summary>
    /// A value-type fade between two scalar values over a duration. Ticked once per frame; never
    /// allocates and never awaits. Used for bus volume changes, voice fade-in/out and the building
    /// block of Phase-2 crossfades.
    /// </summary>
    public struct FadeEnvelope
    {
        private float _from;
        private float _to;
        private float _duration;
        private float _elapsed;
        private FadeCurve _curve;
        private bool _active;

        /// <summary>Current interpolated value. Valid before, during and after the fade.</summary>
        public float Value
        {
            get
            {
                if (_duration <= 0f)
                    return _to;
                float t = _elapsed >= _duration ? 1f : _elapsed / _duration;
                return Lerp(_from, _to, Evaluate(_curve, t));
            }
        }

        /// <summary>The value this fade is heading toward.</summary>
        public float Target => _to;

        /// <summary>True while the fade is still progressing.</summary>
        public bool IsActive => _active;

        /// <summary>True once the fade has reached its target (or was instant).</summary>
        public bool IsDone => !_active;

        /// <summary>Begin a fade from the current value to <paramref name="to"/>.</summary>
        public void StartFrom(float from, float to, float duration, FadeCurve curve = FadeCurve.Linear)
        {
            _from = from;
            _to = to;
            _curve = curve;
            _elapsed = 0f;
            if (duration <= 0f)
            {
                _duration = 0f;
                _active = false;
            }
            else
            {
                _duration = duration;
                _active = true;
            }
        }

        /// <summary>Begin a fade from the current interpolated value to <paramref name="to"/>.</summary>
        public void FadeTo(float to, float duration, FadeCurve curve = FadeCurve.Linear)
        {
            StartFrom(Value, to, duration, curve);
        }

        /// <summary>Snap immediately to <paramref name="value"/> with no active fade.</summary>
        public void SnapTo(float value)
        {
            _from = value;
            _to = value;
            _duration = 0f;
            _elapsed = 0f;
            _active = false;
        }

        /// <summary>Advance the fade by <paramref name="dt"/> seconds. Returns the new value.</summary>
        public float Tick(float dt)
        {
            if (!_active)
                return _to;
            _elapsed += dt;
            if (_elapsed >= _duration)
            {
                _elapsed = _duration;
                _active = false;
                _from = _to;
                return _to;
            }
            return Value;
        }

        public static float Evaluate(FadeCurve curve, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            switch (curve)
            {
                case FadeCurve.EaseInOut:
                    return t * t * (3f - 2f * t); // smoothstep
                case FadeCurve.EqualPower:
                    // sin(t * pi/2) gives an equal-power ramp from 0→1.
                    return (float)Math.Sin(t * (Math.PI * 0.5));
                case FadeCurve.Linear:
                default:
                    return t;
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
