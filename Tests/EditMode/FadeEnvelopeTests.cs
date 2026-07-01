using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class FadeEnvelopeTests
    {
        [Test]
        public void InstantFade_IsDoneImmediately()
        {
            var f = new FadeEnvelope();
            f.SnapTo(0f);
            f.FadeTo(1f, 0f);
            Assert.IsTrue(f.IsDone);
            Assert.That(f.Value, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Linear_ReachesTargetOverDuration()
        {
            var f = new FadeEnvelope();
            f.SnapTo(0f);
            f.FadeTo(1f, 1f, FadeCurve.Linear);

            f.Tick(0.5f);
            Assert.That(f.Value, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.IsTrue(f.IsActive);

            f.Tick(0.5f);
            Assert.That(f.Value, Is.EqualTo(1f).Within(1e-5f));
            Assert.IsTrue(f.IsDone);
        }

        [Test]
        public void Tick_OvershootClampsToTarget()
        {
            var f = new FadeEnvelope();
            f.SnapTo(0f);
            f.FadeTo(2f, 1f, FadeCurve.Linear);
            f.Tick(5f);
            Assert.That(f.Value, Is.EqualTo(2f).Within(1e-6f));
            Assert.IsTrue(f.IsDone);
        }

        [Test]
        public void EqualPower_MidpointIsAboutRoot2Over2()
        {
            // sin(0.5 * pi/2) = sin(pi/4) ≈ 0.7071
            Assert.That(FadeEnvelope.Evaluate(FadeCurve.EqualPower, 0.5f), Is.EqualTo(0.70710678f).Within(1e-4f));
        }

        [Test]
        public void EaseInOut_IsMonotonicAndBounded()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                float v = FadeEnvelope.Evaluate(FadeCurve.EaseInOut, t);
                Assert.GreaterOrEqual(v, 0f);
                Assert.LessOrEqual(v, 1f);
                Assert.GreaterOrEqual(v, prev, "ease-in-out must be monotonic non-decreasing");
                prev = v;
            }
            Assert.That(FadeEnvelope.Evaluate(FadeCurve.EaseInOut, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void FadeTo_StartsFromCurrentValue()
        {
            var f = new FadeEnvelope();
            f.SnapTo(0f);
            f.FadeTo(1f, 1f, FadeCurve.Linear);
            f.Tick(0.5f); // value ~0.5
            f.FadeTo(0f, 1f, FadeCurve.Linear); // reverse from 0.5
            Assert.That(f.Value, Is.EqualTo(0.5f).Within(1e-4f));
            f.Tick(0.5f);
            Assert.That(f.Value, Is.EqualTo(0.25f).Within(1e-3f));
        }
    }
}
