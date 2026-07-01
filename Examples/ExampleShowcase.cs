using Kobapps.AudioKit.Core;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit.Examples
{
    /// <summary>
    /// On-screen showcase for the AudioKit example scene. Drives SFX (variations, weighted, 3D),
    /// the music playlist (crossfade), per-bus volume/mute/solo, ducking and custom events, and
    /// spawns a moving 3D emitter so spatialization is audible. Uses only the public
    /// <see cref="AudioKit"/> facade — read it top to bottom as usage documentation.
    /// </summary>
    [AddComponentMenu("AudioKit/Example Showcase")]
    public sealed class ExampleShowcase : MonoBehaviour
    {
        private static readonly string[] Buses = { "Music", "SFX", "UI", "Ambience" };

        private readonly bool[] _muted = new bool[4];
        private readonly bool[] _soloed = new bool[4];
        private bool _ducking;

        private Transform _emitter;
        private float _orbitAngle;
        private float _stepTimer;

        private void Start()
        {
            AK.SubscribeEvent("Celebrate", OnCelebrate);

            // A moving 3D emitter with a following looping ambience.
            var go = new GameObject("MovingEmitter");
            go.transform.SetParent(transform, false);
            _emitter = go.transform;
            AK.PlayFollow("Ambience", _emitter, 0.7f);
        }

        private void OnDestroy() => AK.UnsubscribeEvent("Celebrate", OnCelebrate);

        private void OnCelebrate()
        {
            for (int i = 0; i < 3; i++) AK.Play("UI_Positive");
        }

        private void Update()
        {
            // Orbit the emitter around the listener so 3D panning is obvious.
            _orbitAngle += Time.deltaTime * 60f;
            float r = 6f;
            _emitter.position = new Vector3(Mathf.Cos(_orbitAngle * Mathf.Deg2Rad) * r, 0f,
                                            Mathf.Sin(_orbitAngle * Mathf.Deg2Rad) * r);

            // Footsteps from the moving emitter.
            _stepTimer += Time.deltaTime;
            if (_stepTimer >= 0.55f)
            {
                _stepTimer = 0f;
                AK.PlayAt("Footstep", _emitter.position, 0.9f);
            }
        }

        private void OnGUI()
        {
            // Anchored top-right so it doesn't overlap the AudioKitDebugOverlay (top-left).
            const float panelW = 300f;
            GUILayout.BeginArea(new Rect(Screen.width - panelW - 12, 12, panelW, Screen.height - 24), GUI.skin.box);
            GUILayout.Label("<b>AudioKit — Example Showcase</b>", Rich());

            GUILayout.Label("<b>SFX</b>", Rich());
            if (GUILayout.Button("Impact (3D, random pos + pitch)"))
                AK.PlayAt("Impact", Random.insideUnitSphere * 8f);
            if (GUILayout.Button("Laser (SoundHandle: stop after 0.4s)"))
            {
                var h = AK.Play("Laser");
                _pendingStop = h; _pendingStopAt = Time.time + 0.4f;
            }
            if (GUILayout.Button("UI Click (2 vars, no-repeat)")) AK.Play("UI_Click");
            if (GUILayout.Button("UI Positive (weighted)")) AK.Play("UI_Positive");
            if (GUILayout.Button("Beep (sequential)")) AK.Play("Beep");

            GUILayout.Space(6);
            GUILayout.Label("<b>Music (playlist crossfade)</b>", Rich());
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(AK.IsPlaylistPlaying ? "Stop" : "Play Jukebox")) ToggleMusic();
                GUI.enabled = AK.IsPlaylistPlaying;
                if (GUILayout.Button("Next")) AK.NextTrack();
                GUI.enabled = true;
            }

            GUILayout.Space(6);
            GUILayout.Label("Hold to duck music:", Rich());
            HandleDuck(GUILayout.RepeatButton("DUCK"));

            GUILayout.Space(6);
            GUILayout.Label("<b>Buses</b>", Rich());
            for (int b = 0; b < Buses.Length; b++)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(Buses[b], GUILayout.Width(70));
                    float v = AK.GetBusVolume(Buses[b]);
                    float nv = GUILayout.HorizontalSlider(v, 0f, 1f, GUILayout.Width(90));
                    if (!Mathf.Approximately(nv, v)) AK.SetBusVolume(Buses[b], nv);

                    bool m = GUILayout.Toggle(_muted[b], "M", GUILayout.Width(28));
                    if (m != _muted[b]) { _muted[b] = m; AK.MuteBus(Buses[b], m); }
                    bool s = GUILayout.Toggle(_soloed[b], "S", GUILayout.Width(28));
                    if (s != _soloed[b]) { _soloed[b] = s; AK.SoloBus(Buses[b], s); }
                }
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Fire 'Celebrate' custom event")) AK.FireEvent("Celebrate");
            AK.MasterVolume = GUILayout.HorizontalSlider(AK.MasterVolume, 0f, 1f);
            GUILayout.Label($"Master {AK.MasterVolume:0.00}   ·   F9 = debug overlay", Rich());
            GUILayout.EndArea();
        }

        private SoundHandle _pendingStop;
        private float _pendingStopAt = -1f;

        private void LateUpdate()
        {
            if (_pendingStopAt > 0f && Time.time >= _pendingStopAt)
            {
                _pendingStop.Stop(0.15f);
                _pendingStopAt = -1f;
            }
        }

        private void ToggleMusic()
        {
            if (AK.IsPlaylistPlaying) AK.StopPlaylist(2f);
            else AK.PlayPlaylist("Jukebox");
        }

        private void HandleDuck(bool held)
        {
            if (held && !_ducking) { AK.DuckBus("Music"); _ducking = true; }
            else if (!held && _ducking) { AK.UnduckBus("Music"); _ducking = false; }
        }

        private static GUIStyle _rich;
        private static GUIStyle Rich() => _rich ??= new GUIStyle(GUI.skin.label) { richText = true };
    }
}
