using System.Collections;
using Kobapps.AudioKit;
using Kobapps.AudioKit.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints; // brings the AllocatingGCMemory() extension into scope
using AK = Kobapps.AudioKit.AudioKit;
// Disambiguate from UnityEngine.PlayMode.
using PlayMode = Kobapps.AudioKit.Core.PlayMode;

namespace Kobapps.AudioKit.Tests
{
    public class RuntimePlayModeTests
    {
        private GameObject _root;
        private AudioService _service;

        private static AudioClip MakeClip(float seconds = 0.15f)
        {
            int rate = 44100;
            int samples = Mathf.Max(1, (int)(rate * seconds));
            var clip = AudioClip.Create("test", samples, 1, rate, false);
            var data = new float[samples];
            for (int i = 0; i < samples; i++) data[i] = 0f; // silent; we test the state machine, not audio
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioDatabaseAsset MakeDatabase(int groupVoiceLimit = 3, int capacity = 8, int prewarm = 2)
        {
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.voiceCapacity = capacity;
            db.prewarmVoices = prewarm;
            db.buses.Add(new BusDefinition { busName = "SFX", volume = 1f });
            var grp = new SoundGroupDefinition
            {
                groupName = "Beep",
                busName = "SFX",
                voiceLimit = groupVoiceLimit,
                playMode = PlayMode.Random,
                stealPolicy = VoiceStealPolicy.StealOldest,
            };
            grp.variations.Add(new AudioVariation { name = "v0", clip = MakeClip(), volume = 1f, pitch = 1f, weight = 1f });
            db.groups.Add(grp);
            return db;
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AudioKitTestRoot");
            _service = new AudioService(MakeDatabase().Bake(), _root.transform);
            AK.Service = _service;
        }

        [TearDown]
        public void TearDown()
        {
            AK.Service = null;
            _service?.Shutdown();
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void Play_ReturnsValidPlayingHandle()
        {
            var h = AK.Play("Beep");
            Assert.IsTrue(h.IsValid, "handle should be valid right after Play");
            Assert.IsTrue(h.IsPlaying, "voice should be playing right after Play");
        }

        [Test]
        public void Play_UnknownGroup_ReturnsInvalidHandle()
        {
            var h = AK.Play("DoesNotExist");
            Assert.IsFalse(h.IsValid);
        }

        [Test]
        public void Stop_InvalidatesHandle()
        {
            var h = AK.Play("Beep");
            h.Stop(0f);
            Assert.IsFalse(h.IsValid, "handle should be invalid after immediate stop");
        }

        [Test]
        public void GroupVoiceLimit_IsEnforced_WithStealing()
        {
            for (int i = 0; i < 6; i++) AK.Play("Beep"); // limit is 3
            Assert.AreEqual(3, _service.Engine.GroupVoiceCount(0), "voice count must not exceed the group limit");
        }

        [Test]
        public void StealOldest_InvalidatesTheOldestHandle()
        {
            var first = AK.Play("Beep");
            AK.Play("Beep");
            AK.Play("Beep"); // at limit (3)
            Assert.IsTrue(first.IsValid);

            AK.Play("Beep"); // steals oldest → first
            Assert.IsFalse(first.IsValid, "oldest voice should have been stolen");
        }

        [UnityTest]
        public IEnumerator Voice_RecyclesAfterClipFinishes()
        {
            var h = AK.Play("Beep"); // ~0.15s clip
            Assert.IsTrue(h.IsValid);

            float timeout = 2f;
            while (h.IsValid && timeout > 0f)
            {
                _service.Tick(Time.unscaledDeltaTime);
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(h.IsValid, "voice should auto-recycle once the clip finishes");
            Assert.AreEqual(0, _service.Engine.GroupVoiceCount(0));
        }

        [Test]
        public void FadeBus_DrivesBusVolumeOverTicks()
        {
            AK.FadeBus("SFX", 0f, 1f, FadeCurve.Linear);
            _service.Tick(0.5f);
            Assert.That(AK.GetBusVolume("SFX"), NUnit.Framework.Is.EqualTo(0.5f).Within(0.05f));
        }

        [Test]
        public void MuteBus_SilencesEffectiveGain()
        {
            AK.MuteBus("SFX", true);
            int busIndex = _service.Engine.GetBusIndex(AudioId.From("SFX"));
            Assert.AreEqual(0f, _service.Engine.BusEffectiveGain(busIndex));
        }

        [Test]
        public void Play_HotPath_DoesNotAllocate()
        {
            var id = AudioId.From("Beep");

            // Warm up: materialize the pooled sources this group will reuse, then clear.
            for (int i = 0; i < 3; i++) _service.Play(id);
            _service.StopAll(0f);

            // Uses Unity's GC constraint (UnityEngine.TestTools.Constraints.Is), fully qualified so
            // it doesn't clash with NUnit's Is used elsewhere in this fixture.
            Assert.That(() =>
            {
                var h = _service.Play(id);
                h.Stop(0f);
            }, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(), "Play → Stop on a warmed pool must not allocate GC memory");
        }
    }
}
