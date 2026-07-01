using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class CoreEngineTests
    {
        private static AudioKitCoreEngine MakeEngine(out AudioId groupId, out AudioId busId)
        {
            groupId = AudioId.From("Explosion");
            busId = AudioId.From("SFX");

            var buses = new[]
            {
                new BusData
                {
                    Id = busId, Volume = 1f, VoiceLimit = 0, Muted = false, Soloed = false,
                    Duck = new DuckConfig { Enabled = true, Amount = 0.8f, Attack = 0.1f, Hold = 0.1f, Release = 0.1f },
                },
            };

            var variations = new[]
            {
                new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f },
                new VariationData { Volume = 0.8f, Pitch = 1.1f, Weight = 2f },
                new VariationData { Volume = 0.9f, Pitch = 0.9f, Weight = 1f },
            };

            var groups = new[]
            {
                new GroupData
                {
                    Id = groupId, VariationOffset = 0, VariationCount = 3,
                    VolumeMin = 1f, VolumeMax = 1f, PitchMin = 1f, PitchMax = 1f,
                    VoiceLimit = 2, RetriggerPercent = 0f, BusIndex = 0,
                    PlayMode = PlayMode.RoundRobin, StealPolicy = VoiceStealPolicy.StealOldest, Is3D = false,
                },
            };

            var engine = new AudioKitCoreEngine(new XorShiftRandom(1234));
            engine.Configure(groups, buses, variations, voiceCapacity: 16);
            return engine;
        }

        [Test]
        public void Configure_BuildsLookups()
        {
            var engine = MakeEngine(out var groupId, out var busId);
            Assert.AreEqual(1, engine.GroupCount);
            Assert.AreEqual(1, engine.BusCount);
            Assert.AreEqual(0, engine.GetGroupIndex(groupId));
            Assert.AreEqual(0, engine.GetBusIndex(busId));
            Assert.AreEqual(-1, engine.GetGroupIndex(AudioId.From("Nope")));
        }

        [Test]
        public void SelectVariation_ReturnsInRange()
        {
            var engine = MakeEngine(out _, out _);
            for (int i = 0; i < 100; i++)
            {
                int v = engine.SelectVariation(0);
                Assert.GreaterOrEqual(v, 0);
                Assert.Less(v, 3);
            }
        }

        [Test]
        public void AcquireVoice_HonoursGroupLimit_WithStealing()
        {
            var engine = MakeEngine(out _, out _);
            Assert.IsTrue(engine.TryAcquireVoice(0, 1f, out int s1, out int g1, out _));
            Assert.IsTrue(engine.TryAcquireVoice(0, 1f, out int s2, out int g2, out _));
            Assert.AreEqual(2, engine.GroupVoiceCount(0));

            // Third play steals the oldest (group limit is 2).
            Assert.IsTrue(engine.TryAcquireVoice(0, 1f, out int s3, out int g3, out int stolen));
            Assert.AreEqual(2, engine.GroupVoiceCount(0));
            Assert.AreEqual(s1, stolen);
            Assert.IsFalse(engine.IsVoiceValid(s1, g1), "stolen voice handle invalidated");
            Assert.IsTrue(engine.IsVoiceValid(s3, g3));
        }

        [Test]
        public void FadeBusVolume_ProgressesOverTicks()
        {
            var engine = MakeEngine(out _, out _);
            engine.FadeBusVolume(0, 0f, 1f, FadeCurve.Linear);
            engine.Tick(0.5f);
            Assert.That(engine.GetBusVolume(0), Is.EqualTo(0.5f).Within(1e-3f));
            engine.Tick(0.5f);
            Assert.That(engine.GetBusVolume(0), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Ducking_ReducesBusGain_ThenRecovers()
        {
            var engine = MakeEngine(out _, out _);
            Assert.That(engine.BusEffectiveGain(0), Is.EqualTo(1f).Within(1e-4f));

            engine.AddDuckTrigger(0);
            for (int i = 0; i < 20; i++) engine.Tick(0.02f); // run past attack
            Assert.That(engine.BusEffectiveGain(0), Is.EqualTo(0.2f).Within(1e-2f), "duck floor = 1 - amount");

            engine.RemoveDuckTrigger(0);
            for (int i = 0; i < 40; i++) engine.Tick(0.02f); // hold + release
            Assert.That(engine.BusEffectiveGain(0), Is.EqualTo(1f).Within(1e-2f));
        }

        [Test]
        public void MuteAndSolo_AffectEffectiveGain()
        {
            var engine = MakeEngine(out _, out _);
            engine.SetBusMuted(0, true);
            Assert.AreEqual(0f, engine.BusEffectiveGain(0));
            engine.SetBusMuted(0, false);
            Assert.Greater(engine.BusEffectiveGain(0), 0f);
        }
    }
}
