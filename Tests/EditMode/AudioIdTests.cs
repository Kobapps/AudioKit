using Kobapps.AudioKit.Core;
using NUnit.Framework;

namespace Kobapps.AudioKit.Tests
{
    public class AudioIdTests
    {
        [Test]
        public void From_SameString_ProducesEqualIds()
        {
            Assert.AreEqual(AudioId.From("Explosion"), AudioId.From("Explosion"));
            Assert.IsTrue(AudioId.From("Explosion") == AudioId.From("Explosion"));
        }

        [Test]
        public void From_DifferentStrings_ProduceDifferentIds()
        {
            Assert.AreNotEqual(AudioId.From("Explosion"), AudioId.From("Footstep"));
            Assert.IsTrue(AudioId.From("Explosion") != AudioId.From("explosion")); // case sensitive
        }

        [Test]
        public void From_NullOrEmpty_IsNone()
        {
            Assert.IsTrue(AudioId.From(null).IsNone);
            Assert.IsTrue(AudioId.From("").IsNone);
            Assert.AreEqual(AudioId.None, AudioId.From(""));
        }

        [Test]
        public void None_HasZeroValue_AndNoNameCollidesWithIt()
        {
            Assert.AreEqual(0u, AudioId.None.Value);
            // FNV-1a of a real name must never be zero (the factory guards against it).
            Assert.AreNotEqual(0u, AudioId.From("anything").Value);
        }

        [Test]
        public void GetHashCode_IsStableAndUsableAsDictionaryKey()
        {
            var dict = new System.Collections.Generic.Dictionary<AudioId, int>
            {
                [AudioId.From("A")] = 1,
                [AudioId.From("B")] = 2,
            };
            Assert.AreEqual(1, dict[AudioId.From("A")]);
            Assert.AreEqual(2, dict[AudioId.From("B")]);
        }
    }
}
