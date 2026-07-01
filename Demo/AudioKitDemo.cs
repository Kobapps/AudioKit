using Kobapps.AudioKit.Core;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;
// Disambiguate from UnityEngine.PlayMode.
using PlayMode = Kobapps.AudioKit.Core.PlayMode;

namespace Kobapps.AudioKit.Demo
{
    /// <summary>
    /// A zero-asset, self-contained demo. It builds an <see cref="AudioDatabaseAsset"/> in code from
    /// procedurally generated tones, boots an <see cref="AudioKitRuntime"/>, and exposes an on-screen
    /// panel to exercise groups, buses, ducking and custom events. Drop it on an empty GameObject and
    /// press Play.
    /// </summary>
    [AddComponentMenu("AudioKit/AudioKit Demo")]
    public sealed class AudioKitDemo : MonoBehaviour
    {
        private AudioDatabaseAsset _db;
        private bool _musicPlaying;

        private void Awake()
        {
            if (AK.Service == null)
            {
                _db = BuildDatabase();
                AudioKitRuntime.Create(_db);
            }

            AK.SubscribeEvent("Celebrate", OnCelebrate);
        }

        private void OnDestroy() => AK.UnsubscribeEvent("Celebrate", OnCelebrate);

        private void OnCelebrate()
        {
            // A custom event that fans out into several blips.
            for (int i = 0; i < 4; i++)
                AK.Play("Blip");
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space)) AK.Play("Blip");
            if (Input.GetKeyDown(KeyCode.C)) AK.Play("Coin");
            if (Input.GetKeyDown(KeyCode.M)) ToggleMusic();
            if (Input.GetKeyDown(KeyCode.D)) AK.FireEvent("Celebrate");
#endif
        }

        private void ToggleMusic()
        {
            if (!_musicPlaying)
            {
                AK.SetBusVolume("Music", 1f);
                AK.Play("Music");
                _musicPlaying = true;
            }
            else
            {
                AK.FadeBus("Music", 0f, 1f, FadeCurve.EqualPower);
                AK.StopGroup("Music", 1f);
                _musicPlaying = false;
            }
        }

        private void OnGUI()
        {
            const int w = 250;
            GUILayout.BeginArea(new Rect(Screen.width - w - 12, 12, w, 320), GUI.skin.box);
            GUILayout.Label("<b>AudioKit Demo</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label("Keys: Space=Blip  C=Coin  M=Music  D=Event");

            if (GUILayout.Button("Play Blip (RoundRobin)")) AK.Play("Blip");
            if (GUILayout.Button("Play Coin (Weighted)")) AK.Play("Coin");
            if (GUILayout.Button(_musicPlaying ? "Fade Music Out" : "Start Music")) ToggleMusic();

            GUILayout.Space(6);
            GUILayout.Label("Duck music while held:");
            // DuckBus on mouse down, Unduck on up via repaint-safe polling.
            bool duck = GUILayout.RepeatButton("Hold to Duck Music");
            HandleDuck(duck);

            GUILayout.Space(6);
            GUILayout.Label("Playlist (crossfade):");
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(AK.IsPlaylistPlaying ? "Stop" : "Play")) TogglePlaylist();
                bool wasEnabled = GUI.enabled;
                GUI.enabled = AK.IsPlaylistPlaying;
                if (GUILayout.Button("Next")) AK.NextTrack();
                GUI.enabled = wasEnabled;
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Fire 'Celebrate' event")) AK.FireEvent("Celebrate");
            AK.MasterVolume = GUILayout.HorizontalSlider(AK.MasterVolume, 0f, 1f);
            GUILayout.Label($"Master volume: {AK.MasterVolume:0.00}");
            GUILayout.EndArea();
        }

        private void TogglePlaylist()
        {
            if (AK.IsPlaylistPlaying) AK.StopPlaylist(1f);
            else AK.PlayPlaylist("Ambient");
        }

        private bool _ducking;
        private void HandleDuck(bool held)
        {
            if (held && !_ducking) { AK.DuckBus("Music"); _ducking = true; }
            else if (!held && _ducking) { AK.UnduckBus("Music"); _ducking = false; }
        }

        // --- Database construction --------------------------------------------------------------

        private static AudioDatabaseAsset BuildDatabase()
        {
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.name = "DemoDatabase";
            db.voiceCapacity = 24;
            db.prewarmVoices = 6;

            db.buses.Add(new BusDefinition
            {
                busName = "Music",
                volume = 0.8f,
                isMusicBus = true,
                duck = new DuckSettings { enabled = true, amount = 0.75f, attack = 0.12f, hold = 0.15f, release = 0.5f },
            });
            db.buses.Add(new BusDefinition { busName = "SFX", volume = 1f });

            // Blip: round-robin across a little arpeggio.
            var blip = new SoundGroupDefinition
            {
                groupName = "Blip", busName = "SFX", playMode = PlayMode.RoundRobin,
                voiceLimit = 6, pitchMin = 0.98f, pitchMax = 1.02f,
            };
            blip.variations.Add(Tone("blipA", 660f, 0.12f));
            blip.variations.Add(Tone("blipB", 880f, 0.12f));
            blip.variations.Add(Tone("blipC", 990f, 0.12f));
            db.groups.Add(blip);

            // Coin: weighted toward the brighter tone.
            var coin = new SoundGroupDefinition
            {
                groupName = "Coin", busName = "SFX", playMode = PlayMode.Weighted, voiceLimit = 4,
            };
            coin.variations.Add(Tone("coinLow", 1175f, 0.1f, weight: 1f));
            coin.variations.Add(Tone("coinHigh", 1568f, 0.14f, weight: 3f));
            db.groups.Add(coin);

            // Music: a longer looping pad (single-shot group demo).
            var music = new SoundGroupDefinition
            {
                groupName = "Music", busName = "Music", playMode = PlayMode.Sequential, voiceLimit = 1, is3D = false,
            };
            music.variations.Add(Tone("pad", 220f, 2.0f, loop: true, volume: 0.7f));
            db.groups.Add(music);

            // Phase-2 playlist: two ambient tracks that crossfade in order.
            var playlist = new PlaylistDefinition
            {
                playlistName = "Ambient", mode = PlaylistMode.Sorted, crossfadeSeconds = 1.5f, busName = "Music",
            };
            playlist.tracks.Add(new MusicTrack { name = "amb1", clip = MakeTone("amb1", 174f, 3f, true), volume = 0.55f });
            playlist.tracks.Add(new MusicTrack { name = "amb2", clip = MakeTone("amb2", 233f, 3f, true), volume = 0.55f });
            db.playlists.Add(playlist);

            return db;
        }

        private static AudioVariation Tone(string name, float freq, float seconds, float volume = 1f, float weight = 1f, bool loop = false)
        {
            return new AudioVariation
            {
                name = name,
                clip = MakeTone(name, freq, seconds, loop),
                volume = volume,
                pitch = 1f,
                weight = weight,
                loop = loop,
            };
        }

        private static AudioClip MakeTone(string name, float freq, float seconds, bool loopSeamless)
        {
            const int rate = 44100;
            int n = Mathf.Max(1, (int)(rate * seconds));
            var data = new float[n];
            float attack = rate * 0.005f;
            float release = rate * 0.02f;
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Sin(2f * Mathf.PI * freq * i / rate);
                float env = 1f;
                if (i < attack) env = i / attack;
                else if (!loopSeamless && i > n - release) env = (n - i) / release;
                data[i] = s * env * 0.5f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
