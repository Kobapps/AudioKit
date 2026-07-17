using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Kobapps.AudioKit.Editor
{
    /// <summary>
    /// Auditions <see cref="AudioClip"/>s inside the editor (edit mode) by reflecting into the
    /// internal <c>UnityEditor.AudioUtil</c> preview API. Method signatures have drifted across Unity
    /// versions, so each entry point is resolved defensively and degrades to a no-op if unavailable.
    /// Only one preview plays at a time, and <see cref="StopAll"/> is exposed both as a toolbar button
    /// and the <c>AudioKit ▸ Stop Preview Audio</c> menu item so a stuck preview can always be stopped.
    /// </summary>
    public static class AudioKitEditorPreview
    {
        private static MethodInfo _play;
        private static MethodInfo _stopAll;
        private static MethodInfo _isPlaying;
        private static bool _resolved;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (type == null) return;

            const BindingFlags F = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // Newer: PlayPreviewClip(AudioClip, int startSample, bool loop). Older: PlayClip(AudioClip).
            _play = type.GetMethod("PlayPreviewClip", F, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    ?? type.GetMethod("PlayClip", F, null, new[] { typeof(AudioClip) }, null);

            _stopAll = type.GetMethod("StopAllPreviewClips", F, null, Type.EmptyTypes, null)
                       ?? type.GetMethod("StopAllClips", F, null, Type.EmptyTypes, null);

            _isPlaying = type.GetMethod("IsPreviewClipPlaying", F, null, Type.EmptyTypes, null)
                         ?? type.GetMethod("IsClipPlaying", F, null, Type.EmptyTypes, null);
        }

        public static bool IsSupported
        {
            get { Resolve(); return _play != null; }
        }

        /// <summary>True while a preview clip is auditioning (best-effort; false if unknown).</summary>
        public static bool IsPlaying
        {
            get
            {
                Resolve();
                try { return _isPlaying != null && (bool)_isPlaying.Invoke(null, null); }
                catch { return false; }
            }
        }

        /// <summary>Audition a clip. Stops any current preview first so only one ever plays.</summary>
        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            Resolve();
            if (_play == null) return;

            StopAll(); // never stack previews — the previous stuck-forever behaviour came from this
            try
            {
                if (_play.GetParameters().Length == 3)
                    _play.Invoke(null, new object[] { clip, 0, false }); // loop = false
                else
                    _play.Invoke(null, new object[] { clip });
            }
            catch (Exception e)
            {
                Debug.LogWarning("AudioKit: clip preview failed — " + e.Message);
            }
        }

        /// <summary>Stop every editor preview clip. Always safe to call.</summary>
        [MenuItem("Tools/AudioKit/Stop Preview Audio %#.")] // Ctrl/Cmd + Shift + .
        public static void StopAll()
        {
            Resolve();
            try { _stopAll?.Invoke(null, null); }
            catch { /* ignore */ }
        }
    }
}
