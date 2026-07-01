using System.Collections.Generic;
using Kobapps.AudioKit.Core;
using Kobapps.AudioKit.Examples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PlayMode = Kobapps.AudioKit.Core.PlayMode;

namespace Kobapps.AudioKit.Examples.Editor
{
    /// <summary>
    /// Regenerates the AudioKit example assets from the downloaded royalty-free clips: an
    /// <see cref="AudioDatabaseAsset"/> exercising every play mode + buses + a playlist, and a scene
    /// wired with the runtime, debug overlay, the <see cref="ExampleShowcase"/> GUI and an
    /// <see cref="AudioKitEventSounds"/> component. No blocking dialogs, so it is safe to run headless.
    /// </summary>
    public static class ExampleBuilder
    {
        private const string Root = "Assets/AudioKit/Examples";
        private const string AudioSfx = Root + "/Audio/SFX";
        private const string AudioMusic = Root + "/Audio/Music";
        private const string DbPath = Root + "/ExampleDatabase.asset";
        private const string ScenePath = Root + "/ExampleShowcase.unity";

        [MenuItem("AudioKit/Examples/Build Showcase")]
        public static void BuildAll()
        {
            BuildDatabase();
            // Reload from disk so the scene stores a persisted asset reference (not a transient one).
            var db = AssetDatabase.LoadAssetAtPath<AudioDatabaseAsset>(DbPath);
            BuildScene(db);
            Debug.Log("AudioKit: example showcase built → " + ScenePath);
        }

        private static AudioClip Sfx(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioSfx}/{file}");
        private static AudioClip Music(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioMusic}/{file}");

        public static AudioDatabaseAsset BuildDatabase()
        {
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.voiceCapacity = 32;
            db.prewarmVoices = 6;

            db.buses.Add(new BusDefinition
            {
                busName = "Music", volume = 0.8f, isMusicBus = true,
                duck = new DuckSettings { enabled = true, amount = 0.7f, attack = 0.15f, hold = 0.25f, release = 0.6f },
            });
            db.buses.Add(new BusDefinition { busName = "SFX", volume = 1f });
            db.buses.Add(new BusDefinition { busName = "UI", volume = 1f });
            db.buses.Add(new BusDefinition { busName = "Ambience", volume = 0.7f });

            // Impact — Random, 3D, volume + pitch randomization.
            db.groups.Add(Group("Impact", "SFX", PlayMode.Random, new[] { V("crash", Sfx("impact_crash.wav")) },
                is3D: true, voiceLimit: 5, volMin: 0.8f, volMax: 1f, pitchMin: 0.9f, pitchMax: 1.1f));

            // Laser — RoundRobin, SoundHandle control demo.
            db.groups.Add(Group("Laser", "SFX", PlayMode.RoundRobin, new[] { V("laser", Sfx("laser.wav")) },
                voiceLimit: 3, steal: VoiceStealPolicy.StealOldest));

            // UI_Click — RandomNoImmediateRepeat across two variations.
            db.groups.Add(Group("UI_Click", "UI", PlayMode.RandomNoImmediateRepeat,
                new[] { V("pop", Sfx("ui_pop.wav")), V("wood", Sfx("ui_wood.wav")) }, voiceLimit: 4));

            // UI_Positive — Weighted (boing favored 3:1).
            db.groups.Add(Group("UI_Positive", "UI", PlayMode.Weighted,
                new[] { V("boing", Sfx("ui_boing.wav"), weight: 3f), V("whistle", Sfx("ui_slide_whistle.wav"), weight: 1f) },
                voiceLimit: 3));

            // Beep — Sequential.
            db.groups.Add(Group("Beep", "UI", PlayMode.Sequential, new[] { V("beep", Sfx("beep.wav")) }, voiceLimit: 4));

            // Footstep — Oldest (LRU), 3D, pitch randomization; reuses the wood tick.
            db.groups.Add(Group("Footstep", "SFX", PlayMode.Oldest, new[] { V("step", Sfx("ui_wood.wav")) },
                is3D: true, voiceLimit: 4, pitchMin: 0.9f, pitchMax: 1.15f));

            // Ambience — 3D looping bed followed by the moving emitter.
            var ambience = Group("Ambience", "Ambience", PlayMode.Sequential, new[] { V("coffee", Sfx("ambience_coffee.wav")) },
                is3D: true, voiceLimit: 1);
            ambience.variations[0].loop = true;
            db.groups.Add(ambience);

            // Jukebox playlist — Sorted with a 3s crossfade on the Music bus.
            var jukebox = new PlaylistDefinition
            {
                playlistName = "Jukebox", mode = PlaylistMode.Sorted, crossfadeSeconds = 3f, busName = "Music",
            };
            jukebox.tracks.Add(new MusicTrack { name = "Carefree", clip = Music("carefree.mp3"), volume = 0.9f });
            jukebox.tracks.Add(new MusicTrack { name = "Fluffing a Duck", clip = Music("fluffing_a_duck.mp3"), volume = 0.9f });
            jukebox.tracks.Add(new MusicTrack { name = "Sneaky Snitch", clip = Music("sneaky_snitch.mp3"), volume = 0.9f });
            db.playlists.Add(jukebox);

            AssetDatabase.CreateAsset(db, DbPath);
            AssetDatabase.SaveAssets();
            return db;
        }

        private static AudioVariation V(string name, AudioClip clip, float weight = 1f) =>
            new AudioVariation { name = name, clip = clip, volume = 1f, pitch = 1f, weight = weight };

        private static SoundGroupDefinition Group(
            string name, string bus, PlayMode mode, AudioVariation[] variations,
            bool is3D = false, int voiceLimit = 8,
            float volMin = 1f, float volMax = 1f, float pitchMin = 1f, float pitchMax = 1f,
            VoiceStealPolicy steal = VoiceStealPolicy.StealOldest)
        {
            return new SoundGroupDefinition
            {
                groupName = name, busName = bus, playMode = mode, is3D = is3D, voiceLimit = voiceLimit,
                stealPolicy = steal, volumeMin = volMin, volumeMax = volMax, pitchMin = pitchMin, pitchMax = pitchMax,
                variations = new List<AudioVariation>(variations),
            };
        }

        public static void BuildScene(AudioDatabaseAsset db)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Runtime + overlay, bound to the example database.
            var akGo = new GameObject("AudioKit");
            var runtime = akGo.AddComponent<AudioKitRuntime>();
            var so = new SerializedObject(runtime);
            var prop = so.FindProperty("database");
            if (prop != null) { prop.objectReferenceValue = db; so.ApplyModifiedPropertiesWithoutUndo(); }
            var overlay = akGo.AddComponent<AudioKitDebugOverlay>();
            // Hidden by default so it doesn't overlap the showcase panel; press F9 in play mode to reveal.
            var oso = new SerializedObject(overlay);
            var showProp = oso.FindProperty("show");
            if (showProp != null) { showProp.boolValue = false; oso.ApplyModifiedPropertiesWithoutUndo(); }

            // Showcase GUI.
            new GameObject("Showcase").AddComponent<ExampleShowcase>();

            // Event Sounds demo: a cube that beeps on enable and plays a positive sting on 'Celebrate'.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "EventSoundsDemo";
            cube.transform.position = new Vector3(0f, 1f, 0f);
            var evt = cube.AddComponent<AudioKitEventSounds>();
            evt.Entries.Add(new AudioTriggerEntry
            {
                trigger = AudioEventTrigger.OnEnable,
                actions = new List<AudioAction> { new AudioAction { kind = AudioActionKind.PlayGroup, target = "Beep" } },
            });
            evt.Entries.Add(new AudioTriggerEntry
            {
                trigger = AudioEventTrigger.CustomEvent,
                customEventName = "Celebrate",
                actions = new List<AudioAction> { new AudioAction { kind = AudioActionKind.PlayGroup, target = "UI_Positive" } },
            });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
