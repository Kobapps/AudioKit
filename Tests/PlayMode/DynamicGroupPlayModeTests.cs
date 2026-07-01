using System.Collections.Generic;
using Kobapps.AudioKit;
using Kobapps.AudioKit.Core;
using NUnit.Framework;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;
using PlayMode = Kobapps.AudioKit.Core.PlayMode;

namespace Kobapps.AudioKit.Tests
{
    public class DynamicGroupPlayModeTests
    {
        private GameObject _root;
        private AudioService _service;
        private readonly List<SoundGroupSetAsset> _sets = new List<SoundGroupSetAsset>();

        private static AudioClip Clip()
        {
            var c = AudioClip.Create("t", 4410, 1, 44100, false);
            c.SetData(new float[4410], 0);
            return c;
        }

        private static AudioDatabaseAsset BaseDb()
        {
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.voiceCapacity = 8;
            db.prewarmVoices = 1;
            db.buses.Add(new BusDefinition { busName = "SFX", volume = 1f });
            return db;
        }

        private SoundGroupSetAsset MakeSet(string groupName)
        {
            var set = ScriptableObject.CreateInstance<SoundGroupSetAsset>();
            var g = new SoundGroupDefinition { groupName = groupName, busName = "SFX", playMode = PlayMode.Random, voiceLimit = 4 };
            g.variations.Add(new AudioVariation { name = "v", clip = Clip(), volume = 1f, pitch = 1f, weight = 1f });
            set.groups.Add(g);
            _sets.Add(set);
            return set;
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("DynGroupsRoot");
            _service = new AudioService(BaseDb().Bake(), _root.transform);
            AK.Service = _service;
        }

        [TearDown]
        public void TearDown()
        {
            // Release any sets still registered so the facade's static ref-count map stays clean.
            foreach (var set in _sets) { AK.UnregisterGroups(set); Object.DestroyImmediate(set); }
            _sets.Clear();
            AK.Service = null;
            _service?.Shutdown();
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void RegisterGroups_MakesGroupPlayable()
        {
            var set = MakeSet("Level_Explosion");
            Assert.IsFalse(AK.IsGroupRegistered("Level_Explosion"));

            AK.RegisterGroups(set);
            Assert.IsTrue(AK.IsGroupRegistered("Level_Explosion"));
            Assert.IsTrue(AK.Play("Level_Explosion").IsValid, "registered group is playable");
        }

        [Test]
        public void UnregisterGroups_RemovesGroup()
        {
            var set = MakeSet("Level_Coin");
            AK.RegisterGroups(set);
            AK.UnregisterGroups(set);

            Assert.IsFalse(AK.IsGroupRegistered("Level_Coin"));
            Assert.IsFalse(AK.Play("Level_Coin").IsValid, "unregistered group no longer plays");
        }

        [Test]
        public void Registration_IsRefCounted()
        {
            var set = MakeSet("Shared");
            AK.RegisterGroups(set); // two owners
            AK.RegisterGroups(set);

            AK.UnregisterGroups(set); // one leaves
            Assert.IsTrue(AK.IsGroupRegistered("Shared"), "still registered while another owner holds it");

            AK.UnregisterGroups(set); // last leaves
            Assert.IsFalse(AK.IsGroupRegistered("Shared"));
        }

        [Test]
        public void RegisterBeforeServiceReady_IsAppliedWhenReady()
        {
            var set = MakeSet("Deferred");
            AK.Service = null;              // no service yet
            AK.RegisterGroups(set);         // queued
            Assert.IsFalse(AK.IsGroupRegistered("Deferred"));

            AK.Service = _service;          // service comes up → pending flushes
            Assert.IsTrue(AK.IsGroupRegistered("Deferred"), "queued set registers when the service is set");
        }

        [Test]
        public void ScriptRegister_SingleGroup_Works()
        {
            var def = new SoundGroupDefinition { groupName = "CodeGroup", busName = "SFX", voiceLimit = 2 };
            def.variations.Add(new AudioVariation { name = "v", clip = Clip(), volume = 1f, pitch = 1f, weight = 1f });

            Assert.IsTrue(AK.RegisterGroup(def));
            Assert.IsTrue(AK.Play("CodeGroup").IsValid);
            Assert.IsTrue(AK.UnregisterGroup("CodeGroup"));
            Assert.IsFalse(AK.IsGroupRegistered("CodeGroup"));
        }

        [Test]
        public void GroupSourceComponent_RegistersOnEnable_UnregistersOnDisable()
        {
            var set = MakeSet("Prefab_Beep");
            var go = new GameObject("PrefabWithSounds");
            go.SetActive(false);
            var src = go.AddComponent<AudioKitGroupSource>();
            src.GroupSet = set;

            go.SetActive(true); // OnEnable → register
            Assert.IsTrue(AK.IsGroupRegistered("Prefab_Beep"));

            go.SetActive(false); // OnDisable → unregister
            Assert.IsFalse(AK.IsGroupRegistered("Prefab_Beep"));

            Object.DestroyImmediate(go);
        }
    }
}
