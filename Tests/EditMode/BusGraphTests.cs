using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class BusGraphTests
    {
        private static BusGraph MakeGraph(int count, out float[] vol, out float[] duck, out bool[] muted, out bool[] soloed)
        {
            vol = new float[count];
            duck = new float[count];
            muted = new bool[count];
            soloed = new bool[count];
            for (int i = 0; i < count; i++) { vol[i] = 1f; duck[i] = 1f; }
            var g = new BusGraph();
            g.Bind(vol, duck, muted, soloed, count);
            return g;
        }

        [Test]
        public void Volume_MultipliesIntoGain()
        {
            var g = MakeGraph(2, out var vol, out _, out _, out _);
            vol[0] = 0.5f;
            Assert.That(g.EffectiveGain(0), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Master_ScalesEveryBus()
        {
            var g = MakeGraph(2, out var vol, out _, out _, out _);
            vol[0] = 0.8f;
            g.MasterVolume = 0.5f;
            Assert.That(g.EffectiveGain(0), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(g.EffectiveGain(-1), Is.EqualTo(0.5f).Within(1e-5f), "bus -1 is the implicit master");
        }

        [Test]
        public void Mute_SilencesBus()
        {
            var g = MakeGraph(2, out _, out _, out var muted, out _);
            muted[1] = true;
            Assert.AreEqual(0f, g.EffectiveGain(1));
        }

        [Test]
        public void Solo_SilencesNonSoloedBuses()
        {
            var g = MakeGraph(3, out _, out _, out _, out _);
            g.SetSoloed(1, true);
            Assert.Greater(g.EffectiveGain(1), 0f, "soloed bus stays audible");
            Assert.AreEqual(0f, g.EffectiveGain(0), "non-soloed buses are silenced while any solo is active");
            Assert.AreEqual(0f, g.EffectiveGain(2));
        }

        [Test]
        public void Duck_MultipliesGain()
        {
            var g = MakeGraph(1, out var vol, out var duck, out _, out _);
            vol[0] = 1f;
            duck[0] = 0.3f;
            Assert.That(g.EffectiveGain(0), Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void Mute_BeatsSolo()
        {
            var g = MakeGraph(2, out _, out _, out var muted, out _);
            g.SetSoloed(0, true);
            muted[0] = true; // muted soloed bus is still silent
            Assert.AreEqual(0f, g.EffectiveGain(0));
        }
    }
}
