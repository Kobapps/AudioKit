using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class VoiceTableTests
    {
        [Test]
        public void Acquire_AssignsValidSlot_AndTracksCounts()
        {
            var t = new VoiceTable();
            t.Init(capacity: 8, groupCount: 2, busCount: 2);

            Assert.IsTrue(t.TryAcquire(0, 0, 4, 0, VoiceStealPolicy.StealOldest, 1f, out int slot, out int stolen));
            Assert.GreaterOrEqual(slot, 0);
            Assert.AreEqual(-1, stolen, "fresh acquire should not steal");
            Assert.AreEqual(1, t.GroupVoiceCount(0));
            Assert.AreEqual(1, t.BusVoiceCount(0));
            Assert.IsTrue(t.IsValid(slot, t.Generation(slot)));
        }

        [Test]
        public void Release_FreesSlot_BumpsGeneration_InvalidatesHandle()
        {
            var t = new VoiceTable();
            t.Init(8, 2, 2);
            t.TryAcquire(0, 0, 4, 0, VoiceStealPolicy.StealOldest, 1f, out int slot, out _);
            int gen = t.Generation(slot);

            t.Release(slot);

            Assert.IsFalse(t.IsValid(slot, gen), "handle must be invalid after release");
            Assert.AreEqual(0, t.GroupVoiceCount(0));
            Assert.AreEqual(0, t.BusVoiceCount(0));
        }

        [Test]
        public void GroupLimit_Reject_RefusesWhenFull()
        {
            var t = new VoiceTable();
            t.Init(8, 1, 1);
            t.TryAcquire(0, 0, 2, 0, VoiceStealPolicy.Reject, 1f, out _, out _);
            t.TryAcquire(0, 0, 2, 0, VoiceStealPolicy.Reject, 1f, out _, out _);

            bool ok = t.TryAcquire(0, 0, 2, 0, VoiceStealPolicy.Reject, 1f, out int slot, out _);
            Assert.IsFalse(ok, "Reject policy must refuse beyond the group limit");
            Assert.AreEqual(-1, slot);
            Assert.AreEqual(2, t.GroupVoiceCount(0));
        }

        [Test]
        public void GroupLimit_StealOldest_ReusesOldestSlot()
        {
            var t = new VoiceTable();
            t.Init(8, 1, 1);
            t.TryAcquire(0, 0, 2, 0, VoiceStealPolicy.StealOldest, 1f, out int first, out _);
            int firstGen = t.Generation(first);
            t.TryAcquire(0, 0, 2, 0, VoiceStealPolicy.StealOldest, 1f, out int second, out _);

            bool ok = t.TryAcquire(0, 0, 2, 0, VoiceStealPolicy.StealOldest, 1f, out int slot, out int stolen);
            Assert.IsTrue(ok);
            Assert.AreEqual(first, slot, "should reclaim the oldest slot");
            Assert.AreEqual(first, stolen);
            Assert.IsFalse(t.IsValid(first, firstGen), "old voice's handle is now invalid");
            Assert.AreEqual(2, t.GroupVoiceCount(0), "count stays at the limit");
            Assert.AreNotEqual(slot, second);
        }

        [Test]
        public void GroupLimit_StealQuietest_PicksLowestVolume()
        {
            var t = new VoiceTable();
            t.Init(8, 1, 1);
            t.TryAcquire(0, 0, 3, 0, VoiceStealPolicy.StealQuietest, 0.9f, out int loud, out _);
            t.TryAcquire(0, 0, 3, 0, VoiceStealPolicy.StealQuietest, 0.2f, out int quiet, out _);
            t.TryAcquire(0, 0, 3, 0, VoiceStealPolicy.StealQuietest, 0.5f, out int mid, out _);

            bool ok = t.TryAcquire(0, 0, 3, 0, VoiceStealPolicy.StealQuietest, 1f, out int slot, out int stolen);
            Assert.IsTrue(ok);
            Assert.AreEqual(quiet, slot, "quietest voice should be reclaimed");
            Assert.AreEqual(quiet, stolen);
            Assert.AreNotEqual(loud, slot);
            Assert.AreNotEqual(mid, slot);
        }

        [Test]
        public void BusLimit_StealsAcrossGroupsInBus()
        {
            var t = new VoiceTable();
            t.Init(8, 2, 1);
            // Two groups share bus 0 with a bus voice limit of 2.
            t.TryAcquire(0, 0, 8, 2, VoiceStealPolicy.StealOldest, 1f, out int a, out _);
            t.TryAcquire(1, 0, 8, 2, VoiceStealPolicy.StealOldest, 1f, out int b, out _);
            Assert.AreEqual(2, t.BusVoiceCount(0));

            bool ok = t.TryAcquire(1, 0, 8, 2, VoiceStealPolicy.StealOldest, 1f, out int slot, out int stolen);
            Assert.IsTrue(ok);
            Assert.AreEqual(a, slot, "oldest voice in the bus (from the other group) is reclaimed");
            Assert.AreEqual(2, t.BusVoiceCount(0), "bus stays at its limit");
        }

        [Test]
        public void PoolExhausted_UnderLimits_ReclaimsGlobalOldest()
        {
            var t = new VoiceTable();
            t.Init(capacity: 2, groupCount: 1, busCount: 1);
            // Group/bus limits are generous; the hard pool cap (2) forces an LRU reclaim.
            t.TryAcquire(0, 0, 100, 0, VoiceStealPolicy.StealOldest, 1f, out int first, out _);
            t.TryAcquire(0, 0, 100, 0, VoiceStealPolicy.StealOldest, 1f, out int second, out _);

            bool ok = t.TryAcquire(0, 0, 100, 0, VoiceStealPolicy.StealOldest, 1f, out int slot, out int stolen);
            Assert.IsTrue(ok);
            Assert.AreEqual(first, slot, "global-oldest voice reclaimed when the pool is exhausted");
            Assert.AreEqual(first, stolen);
            Assert.LessOrEqual(t.GroupVoiceCount(0), 2);
        }

        [Test]
        public void Reset_ClearsAllCounts_AndInvalidatesHandles()
        {
            var t = new VoiceTable();
            t.Init(4, 1, 1);
            t.TryAcquire(0, 0, 4, 0, VoiceStealPolicy.StealOldest, 1f, out int slot, out _);
            int gen = t.Generation(slot);

            t.Reset();

            Assert.AreEqual(0, t.GroupVoiceCount(0));
            Assert.IsFalse(t.IsValid(slot, gen));
        }
    }
}
