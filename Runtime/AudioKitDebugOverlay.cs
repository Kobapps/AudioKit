using Kobapps.AudioKit.Core;
using UnityEngine;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit
{
    /// <summary>
    /// Lightweight on-screen debug overlay: live voice counts per group and per bus, current bus
    /// volumes / effective gains, duck gains and at-limit highlighting. Add it to any scene object
    /// (or enable it from the demo) while profiling. IMGUI only, no allocations beyond the label
    /// strings GUILayout itself produces.
    /// </summary>
    [AddComponentMenu("AudioKit/AudioKit Debug Overlay")]
    public sealed class AudioKitDebugOverlay : MonoBehaviour
    {
        [Tooltip("Whether the overlay is visible on start (toggle at runtime with the key below).")]
        [SerializeField] private bool show = true;

        [Tooltip("Key that shows/hides the overlay at runtime. Set to None to disable toggling.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F9;

        [Tooltip("Top-left screen offset (pixels) of the overlay panel.")]
        [SerializeField] private Vector2 screenOffset = new Vector2(10f, 10f);

        [Tooltip("Overlay panel width in pixels.")]
        [SerializeField] private int width = 320;

        private Vector2 _scroll;
        private GUIStyle _box;

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
                show = !show;
#endif
        }

        private void OnGUI()
        {
            if (!show) return;

            var service = AK.Service;
            _box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true };

            GUILayout.BeginArea(new Rect(screenOffset.x, screenOffset.y, width, Screen.height - screenOffset.y * 2f), _box);
            GUILayout.Label("<b>AudioKit</b>  (" + toggleKey + " to toggle)");

            if (service == null || !service.IsReady)
            {
                GUILayout.Label("<i>No active AudioService.</i>");
                GUILayout.EndArea();
                return;
            }

            AudioKitCoreEngine engine = service.Engine;
            BakedAudioData data = service.Data;

            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("<b>Buses</b>");
            for (int b = 0; b < engine.BusCount; b++)
            {
                float vol = engine.GetBusVolume(b);
                float gain = engine.BusEffectiveGain(b);
                float duck = engine.BusDuckGain(b);
                int voices = engine.BusVoiceCount(b);
                string flags = (engine.IsBusMuted(b) ? " <color=#ff6666>M</color>" : "") +
                               (engine.IsBusSoloed(b) ? " <color=#ffff66>S</color>" : "");
                GUILayout.Label(
                    $"{data.BusName(b)}  vol {vol:0.00}  gain {gain:0.00}  duck {duck:0.00}  voices {voices}{flags}");
            }

            GUILayout.Space(6f);
            GUILayout.Label("<b>Groups</b>");
            for (int g = 0; g < engine.GroupCount; g++)
            {
                int count = engine.GroupVoiceCount(g);
                int limit = engine.GetGroup(g).VoiceLimit;
                bool atLimit = count >= limit;
                string c = atLimit ? "#ff6666" : "#aaffaa";
                GUILayout.Label($"{data.GroupName(g)}  <color={c}>{count}/{limit}</color>");
            }

            GUILayout.Space(6f);
            if (service is AudioService svc && svc.Music != null && svc.Music.IsPlaying)
                GUILayout.Label($"<b>Music</b>  {svc.Music.CurrentPlaylistName}  track {svc.Music.CurrentTrack}");
            else
                GUILayout.Label("<b>Music</b>  <i>idle</i>");

            GUILayout.Space(6f);
            GUILayout.Label($"Master volume: {engine.MasterVolume:0.00}");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
