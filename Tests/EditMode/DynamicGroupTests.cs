using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class DynamicGroupTests
    {
        private static AudioKitCoreEngine MakeEngine()
        {
            var buses = new[] { new BusData { Id = AudioId.From("SFX"), Volume = 1f, VoiceLimit = 0 } };
            var variations = new[] { new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f } };
            var groups = new[]
            {
                new GroupData
                {
                    Id = AudioId.From("Static"), VariationOffset = 0, VariationCount = 1,
                    VolumeMin = 1f, VolumeMax = 1f, PitchMin = 1f, PitchMax = 1f,
                    VoiceLimit = 4, BusIndex = 0, PlayMode = PlayMode.Random, StealPolicy = VoiceStealPolicy.StealOldest,
                },
            };
            var e = new AudioKitCoreEngine(new XorShiftRandom(1));
            e.Configure(groups, buses, variations, 16);
            return e;
        }

        private static GroupData Group(string name, int voiceLimit = 4) => new GroupData
        {
            Id = AudioId.From(name), VolumeMin = 1f, VolumeMax = 1f, PitchMin = 1f, PitchMax = 1f,
            VoiceLimit = voiceLimit, BusIndex = 0, PlayMode = PlayMode.RoundRobin, StealPolicy = VoiceStealPolicy.StealOldest,
        };

        private static VariationData[] Vars(int n)
        {
            var a = new VariationData[n];
            for (int i = 0; i < n; i++) a[i] = new VariationData { Volume = 1f, Pitch = 1f, Weight = 1f };
            return a;
        }

        [Test]
        public void Register_MakesGroupPlayable()
        {
            var e = MakeEngine();
            int idx = e.RegisterGroup(Group("Dyn"), Vars(2));
            Assert.Greater(idx, 0);
            Assert.AreEqual(idx, e.GetGroupIndex(AudioId.From("Dyn")));
            Assert.IsTrue(e.IsGroupRegistered(AudioId.From("Dyn")));

            var g = e.GetGroup(idx);
            Assert.AreEqual(2, g.VariationCount);
            Assert.AreEqual(1, g.VariationOffset, "appended after the 1 static variation");

            int local = e.SelectVariation(idx);
            Assert.GreaterOrEqual(local, 0);
            Assert.Less(local, 2);

            Assert.IsTrue(e.TryAcquireVoice(idx, 1f, out int slot, out int gen, out _));
            Assert.IsTrue(e.IsVoiceValid(slot, gen));
            Assert.AreEqual(1, e.GroupVoiceCount(idx));
        }

        [Test]
        public void RegisterDuplicate_ReturnsMinusOne()
        {
            var e = MakeEngine();
            e.RegisterGroup(Group("Dyn"), Vars(1));
            Assert.AreEqual(-1, e.RegisterGroup(Group("Dyn"), Vars(1)), "duplicate id rejected");
            Assert.AreEqual(-1, e.RegisterGroup(Group("Static"), Vars(1)), "existing static id rejected");
        }

        [Test]
        public void Unregister_RemovesGroup()
        {
            var e = MakeEngine();
            e.RegisterGroup(Group("Dyn"), Vars(2));
            Assert.IsTrue(e.UnregisterGroup(AudioId.From("Dyn")));
            Assert.AreEqual(-1, e.GetGroupIndex(AudioId.From("Dyn")));
            Assert.IsFalse(e.IsGroupRegistered(AudioId.From("Dyn")));
            Assert.IsFalse(e.UnregisterGroup(AudioId.From("Dyn")), "second unregister is a no-op");
        }

        [Test]
        public void ReRegister_SameSize_ReusesSlotAndRange()
        {
            var e = MakeEngine();
            int first = e.RegisterGroup(Group("Dyn"), Vars(2));
            int groupTotal = e.GroupTotal;
            int varTotal = e.VariationTotal;

            e.UnregisterGroup(AudioId.From("Dyn"));
            int second = e.RegisterGroup(Group("Dyn2"), Vars(2));

            Assert.AreEqual(first, second, "freed group slot is reused");
            Assert.AreEqual(groupTotal, e.GroupTotal, "no group growth on reuse");
            Assert.AreEqual(varTotal, e.VariationTotal, "no variation growth on same-size reuse");
            Assert.AreEqual(1, e.GetGroup(second).VariationOffset, "freed variation range reused");
        }

        [Test]
        public void StaticGroup_StillWorks_AfterDynamicChurn()
        {
            var e = MakeEngine();
            int staticIdx = e.GetGroupIndex(AudioId.From("Static"));
            e.RegisterGroup(Group("A"), Vars(3));
            e.RegisterGroup(Group("B"), Vars(1));
            e.UnregisterGroup(AudioId.From("A"));

            Assert.AreEqual(staticIdx, e.GetGroupIndex(AudioId.From("Static")), "static index unchanged");
            Assert.IsTrue(e.TryAcquireVoice(staticIdx, 1f, out _, out _, out _));
            Assert.AreEqual(1, e.GroupVoiceCount(staticIdx));
        }
    }
}
