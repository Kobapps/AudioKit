using Kobapps.AudioKit;
using Kobapps.AudioKit.Core;
using NUnit.Framework;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit.Tests
{
    public class MusicPlayerPlayModeTests
    {
        private GameObject _root;
        private AudioService _service;

        private static AudioClip MakeClip(float seconds = 0.5f)
        {
            int rate = 44100;
            int samples = Mathf.Max(1, (int)(rate * seconds));
            var clip = AudioClip.Create("t", samples, 1, rate, false);
            clip.SetData(new float[samples], 0);
            return clip;
        }

        private static AudioDatabaseAsset MakeMusicDatabase()
        {
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.voiceCapacity = 8;
            db.prewarmVoices = 1;
            db.buses.Add(new BusDefinition { busName = "Music", volume = 1f, isMusicBus = true });

            var pl = new PlaylistDefinition
            {
                playlistName = "Loop", mode = PlaylistMode.Sorted, crossfadeSeconds = 0f, busName = "Music",
            };
            pl.tracks.Add(new MusicTrack { name = "t0", clip = MakeClip(), volume = 1f });
            pl.tracks.Add(new MusicTrack { name = "t1", clip = MakeClip(), volume = 1f });
            db.playlists.Add(pl);
            return db;
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AudioKitMusicTestRoot");
            _service = new AudioService(MakeMusicDatabase().Bake(), _root.transform);
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
        public void PlayPlaylist_StartsPlaying_OnFirstTrack()
        {
            AK.PlayPlaylist("Loop");
            Assert.IsTrue(AK.IsPlaylistPlaying);
            Assert.AreEqual(0, _service.Music.CurrentTrack, "Sorted playlist starts on track 0");
            _service.Tick(0.02f);
            Assert.IsTrue(AK.IsPlaylistPlaying);
        }

        [Test]
        public void PlayPlaylist_UnknownName_DoesNothing()
        {
            AK.PlayPlaylist("Nope");
            Assert.IsFalse(AK.IsPlaylistPlaying);
        }

        [Test]
        public void NextTrack_AdvancesInSortedOrder()
        {
            AK.PlayPlaylist("Loop");
            Assert.AreEqual(0, _service.Music.CurrentTrack);
            AK.NextTrack();
            Assert.AreEqual(1, _service.Music.CurrentTrack, "Sorted playlist advances 0 -> 1");
            Assert.IsTrue(AK.IsPlaylistPlaying);
        }

        [Test]
        public void StopPlaylist_Immediate_Stops()
        {
            AK.PlayPlaylist("Loop");
            AK.StopPlaylist(0f);
            Assert.IsFalse(AK.IsPlaylistPlaying);
        }

        [Test]
        public void MusicRespondsToBusMuteViaEffectiveGain()
        {
            int bus = _service.Engine.GetBusIndex(AudioId.From("Music"));
            AK.PlayPlaylist("Loop");
            _service.Tick(0.02f);
            AK.MuteBus("Music", true);
            Assert.AreEqual(0f, _service.Engine.BusEffectiveGain(bus), "muting the music bus silences it");
        }
    }
}
