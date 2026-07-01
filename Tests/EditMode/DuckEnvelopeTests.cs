using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class DuckEnvelopeTests
    {
        private static DuckEnvelope Make(float amount, float attack, float hold, float release)
        {
            var d = new DuckEnvelope();
            d.Reset();
            d.Configure(new DuckConfig
            {
                Enabled = true,
                Amount = amount,
                Attack = attack,
                Hold = hold,
                Release = release,
            });
            return d;
        }

        [Test]
        public void Disabled_GainStaysAtOne()
        {
            var d = new DuckEnvelope();
            d.Reset();
            d.Configure(DuckConfig.Default); // Enabled = false
            d.AddTrigger();
            d.Tick(1f);
            Assert.That(d.Gain, Is.EqualTo(1f).Within(1e-6f));
        }

        [Test]
        public void Attack_RampsDownToFloor()
        {
            var d = Make(amount: 0.5f, attack: 0.1f, hold: 0f, release: 0.1f);
            d.AddTrigger();

            d.Tick(0.05f); // halfway through attack
            Assert.That(d.Gain, Is.LessThan(1f));
            Assert.That(d.Gain, Is.GreaterThan(0.5f));

            d.Tick(0.05f); // reach floor
            Assert.That(d.Gain, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.AreEqual(DuckEnvelope.Phase.Sustain, d.CurrentPhase);
        }

        [Test]
        public void Sustain_HoldsFloorWhileTriggerActive()
        {
            var d = Make(0.6f, 0.05f, 0.2f, 0.2f);
            d.AddTrigger();
            d.Tick(0.05f); // reach floor 0.4
            d.Tick(1.0f);  // long time, still active
            Assert.That(d.Gain, Is.EqualTo(0.4f).Within(1e-4f));
        }

        [Test]
        public void Release_ReturnsToUnityAfterHold()
        {
            var d = Make(0.5f, 0.05f, 0.1f, 0.1f);
            d.AddTrigger();
            d.Tick(0.05f); // floor 0.5
            d.RemoveTrigger();

            d.Tick(0.1f); // consume hold
            Assert.AreEqual(DuckEnvelope.Phase.Release, d.CurrentPhase);

            d.Tick(0.1f); // consume release
            Assert.That(d.Gain, Is.EqualTo(1f).Within(1e-4f));
            Assert.AreEqual(DuckEnvelope.Phase.Idle, d.CurrentPhase);
        }

        [Test]
        public void MultipleTriggers_HoldFloorUntilLastReleased()
        {
            var d = Make(0.5f, 0.02f, 0.05f, 0.05f);
            d.AddTrigger();
            d.AddTrigger();
            d.Tick(0.02f); // floor
            d.RemoveTrigger(); // one still active
            d.Tick(0.5f);
            Assert.That(d.Gain, Is.EqualTo(0.5f).Within(1e-4f), "floor held while any trigger active");

            d.RemoveTrigger(); // now zero
            d.Tick(0.05f); // hold
            d.Tick(0.05f); // release
            Assert.That(d.Gain, Is.EqualTo(1f).Within(1e-4f));
        }
    }
}
