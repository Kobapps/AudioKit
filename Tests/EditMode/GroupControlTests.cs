using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class GroupControlTests
    {
        private static AudioKitCoreEngine MakeEngine()
        {
            var buses = new[] { new BusData { Id = AudioId.From("SFX"), Volume = 1f } };
            var variations = new[] { new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f } };
            var groups = new[]
            {
                new GroupData { Id = AudioId.From("G"), VariationOffset = 0, VariationCount = 1, VoiceLimit = 4, BusIndex = 0 },
            };
            var e = new AudioKitCoreEngine(new XorShiftRandom(1));
            e.Configure(groups, buses, variations, 8);
            return e;
        }

        [Test]
        public void GroupVolume_DefaultsToOne_AndScalesGain()
        {
            var e = MakeEngine();
            Assert.That(e.GetGroupVolume(0), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(e.GroupGain(0), Is.EqualTo(1f).Within(1e-5f));

            e.SetGroupVolume(0, 0.5f);
            Assert.That(e.GetGroupVolume(0), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(e.GroupGain(0), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void GroupMute_ZeroesGain_ButPreservesVolume()
        {
            var e = MakeEngine();
            e.SetGroupVolume(0, 0.7f);
            e.SetGroupMuted(0, true);
            Assert.IsTrue(e.IsGroupMuted(0));
            Assert.AreEqual(0f, e.GroupGain(0), "muted group has zero gain");

            e.SetGroupMuted(0, false);
            Assert.That(e.GroupGain(0), Is.EqualTo(0.7f).Within(1e-5f), "volume restored on unmute");
        }

        [Test]
        public void DynamicGroup_VolumeMute_ResetOnRegister()
        {
            var e = MakeEngine();
            int idx = e.RegisterGroup(
                new GroupData { Id = AudioId.From("Dyn"), VoiceLimit = 2, BusIndex = 0 },
                new[] { new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f } });

            Assert.That(e.GetGroupVolume(idx), Is.EqualTo(1f).Within(1e-5f), "fresh dynamic group starts at full volume");
            Assert.IsFalse(e.IsGroupMuted(idx));

            e.SetGroupMuted(idx, true);
            e.UnregisterGroup(AudioId.From("Dyn"));
            int idx2 = e.RegisterGroup(
                new GroupData { Id = AudioId.From("Dyn2"), VoiceLimit = 2, BusIndex = 0 },
                new[] { new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f } });
            Assert.AreEqual(idx, idx2, "reused slot");
            Assert.IsFalse(e.IsGroupMuted(idx2), "reused slot's mute state was reset");
            Assert.That(e.GroupGain(idx2), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}
