using Kobapps.AudioKit;
using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class CustomEventRegistryTests
    {
        [Test]
        public void Fire_InvokesAllSubscribers()
        {
            var reg = new CustomEventRegistry();
            int a = 0, b = 0;
            reg.Subscribe(AudioId.From("Boom"), () => a++);
            reg.Subscribe(AudioId.From("Boom"), () => b++);

            reg.Fire(AudioId.From("Boom"));

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }

        [Test]
        public void Fire_OnlyInvokesMatchingId()
        {
            var reg = new CustomEventRegistry();
            int hits = 0;
            reg.Subscribe(AudioId.From("A"), () => hits++);
            reg.Fire(AudioId.From("B"));
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var reg = new CustomEventRegistry();
            int hits = 0;
            System.Action h = () => hits++;
            reg.Subscribe(AudioId.From("E"), h);
            reg.Unsubscribe(AudioId.From("E"), h);
            reg.Fire(AudioId.From("E"));
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Subscribe_NoneId_IsIgnored()
        {
            var reg = new CustomEventRegistry();
            int hits = 0;
            reg.Subscribe(AudioId.None, () => hits++);
            reg.Fire(AudioId.None);
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Fire_IsSafe_WhenHandlerUnsubscribesAnother()
        {
            var reg = new CustomEventRegistry();
            var id = AudioId.From("Chain");
            int hits = 0;
            System.Action second = () => hits++;
            System.Action first = () => reg.Unsubscribe(id, second);
            reg.Subscribe(id, first);
            reg.Subscribe(id, second);

            Assert.DoesNotThrow(() => reg.Fire(id));
        }

        [Test]
        public void GrowsBeyondInitialCapacity()
        {
            var reg = new CustomEventRegistry();
            var id = AudioId.From("Many");
            int hits = 0;
            for (int i = 0; i < 50; i++)
                reg.Subscribe(id, () => hits++);
            reg.Fire(id);
            Assert.AreEqual(50, hits);
        }
    }
}
