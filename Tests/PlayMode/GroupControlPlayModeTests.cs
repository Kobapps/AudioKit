using Kobapps.AudioKit;
using Kobapps.AudioKit.Core;
using NUnit.Framework;
using UnityEngine;
using System.Linq;
using AK = Kobapps.AudioKit.AudioKit;
using PlayMode = Kobapps.AudioKit.Core.PlayMode;

namespace Kobapps.AudioKit.Tests
{
    public class GroupControlPlayModeTests
    {
        private GameObject _root;
        private AudioService _service;

        private static AudioClip Clip(string name)
        {
            var c = AudioClip.Create(name, 44100, 1, 44100, false); // 1s
            var data = new float[44100];
            for (int i = 0; i < data.Length; i++) data[i] = 0.5f;
            c.SetData(data, 0);
            return c;
        }

        private static AudioDatabaseAsset Db()
        {
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.voiceCapacity = 8;
            db.prewarmVoices = 1;
            db.buses.Add(new BusDefinition { busName = "SFX", volume = 1f });

            var beep = new SoundGroupDefinition { groupName = "Beep", busName = "SFX", playMode = PlayMode.Random, voiceLimit = 4 };
            beep.variations.Add(new AudioVariation { name = "beep", clip = Clip("beep"), volume = 1f, pitch = 1f, weight = 1f });
            db.groups.Add(beep);

            var boom = new SoundGroupDefinition { groupName = "Boom", busName = "SFX", playMode = PlayMode.Random, voiceLimit = 4, is3D = true };
            boom.variations.Add(new AudioVariation { name = "boom", clip = Clip("boom"), volume = 1f, pitch = 1f, weight = 1f });
            db.groups.Add(boom);
            return db;
        }

        private static AudioSource VoicePlaying(string clipName)
        {
            return Resources.FindObjectsOfTypeAll<AudioSource>().FirstOrDefault(s =>
                s.gameObject.name.Contains("AudioKitVoice") && s.isPlaying && s.clip != null && s.clip.name == clipName);
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("GroupCtrlRoot");
            _root.AddComponent<AudioListener>(); // at origin, for virtualization
            _service = new AudioService(Db().Bake(), _root.transform);
            AK.Service = _service;
        }

        [TearDown]
        public void TearDown()
        {
            AK.VirtualizeDistance = 0f;
            AK.Service = null;
            _service?.Shutdown();
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void FadeIn_StartsQuiet_ThenRises()
        {
            var h = AK.Play("Beep", 1f, fadeInSeconds: 1f);
            Assert.IsTrue(h.IsValid);
            var src = VoicePlaying("beep");
            Assert.IsNotNull(src);
            Assert.Less(src.volume, 0.15f, "fade-in starts near silent");

            _service.Tick(0.5f);
            Assert.Greater(src.volume, 0.4f, "volume rises over the fade");
        }

        [Test]
        public void MuteGroup_SilencesLiveVoices()
        {
            AK.Play("Beep");
            _service.Tick(0.02f);
            var src = VoicePlaying("beep");
            Assert.IsNotNull(src);
            Assert.Greater(src.volume, 0.5f, "audible before mute");

            AK.MuteGroup("Beep", true);
            _service.Tick(0.02f);
            Assert.Less(src.volume, 0.01f, "muted group silences its live voice");

            AK.MuteGroup("Beep", false);
            _service.Tick(0.02f);
            Assert.Greater(src.volume, 0.5f, "unmuting restores volume");
        }

        [Test]
        public void GroupVolume_ScalesLiveVoices()
        {
            AK.Play("Beep");
            _service.Tick(0.02f);
            var src = VoicePlaying("beep");
            AK.SetGroupVolume("Beep", 0.25f);
            _service.Tick(0.02f);
            Assert.That(src.volume, Is.EqualTo(0.25f).Within(0.05f));
        }

        [Test]
        public void Virtualization_SkipsFarPlays()
        {
            AK.VirtualizeDistance = 5f;
            Assert.IsFalse(AK.PlayAt("Boom", new Vector3(100f, 0f, 0f)).IsValid, "far play is culled");
            Assert.IsTrue(AK.PlayAt("Boom", new Vector3(1f, 0f, 0f)).IsValid, "near play proceeds");
        }
    }
}
