using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kobapps.AudioKit.Editor
{
    /// <summary>One-click validation report for an <see cref="AudioDatabaseAsset"/>.</summary>
    public sealed class AudioKitValidationWindow : EditorWindow
    {
        private AudioDatabaseAsset _db;
        private float _maxClipSeconds = AudioKitValidator.DefaultMaxClipSeconds;
        private List<ValidationIssue> _issues;
        private Vector2 _scroll;

        public static void Open(AudioDatabaseAsset db)
        {
            var w = GetWindow<AudioKitValidationWindow>(true, "AudioKit Validation");
            w._db = db;
            w.minSize = new Vector2(420, 300);
            w.Run();
            w.Show();
        }

        [MenuItem("Window/AudioKit/Validate Database")]
        private static void OpenFromMenu() => Open(Selection.activeObject as AudioDatabaseAsset);

        private void Run()
        {
            _issues = AudioKitValidator.Validate(_db, _maxClipSeconds);
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _db = (AudioDatabaseAsset)EditorGUILayout.ObjectField(
                    new GUIContent("Database", "The Audio Database to validate."), _db, typeof(AudioDatabaseAsset), false);
                if (GUILayout.Button(new GUIContent("Validate", "Re-run all checks."), GUILayout.Width(80)))
                    Run();
            }
            _maxClipSeconds = EditorGUILayout.Slider(
                new GUIContent("Max clip seconds", "Clips longer than this are flagged as oversized (consider streaming / a music bus)."),
                _maxClipSeconds, 1f, 120f);

            if (_issues == null)
            {
                EditorGUILayout.HelpBox("Assign a database and press Validate.", MessageType.Info);
                return;
            }

            AudioKitValidator.CountBySeverity(_issues, out int errors, out int warnings, out int infos);
            EditorGUILayout.Space();
            if (errors == 0 && warnings == 0)
                EditorGUILayout.HelpBox("All checks passed. ✔", MessageType.Info);
            else
                EditorGUILayout.HelpBox($"{errors} error(s), {warnings} warning(s), {infos} info.", errors > 0 ? MessageType.Error : MessageType.Warning);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _issues.Count; i++)
            {
                var issue = _issues[i];
                var type = issue.Severity == ValidationSeverity.Error ? MessageType.Error
                    : issue.Severity == ValidationSeverity.Warning ? MessageType.Warning
                    : MessageType.None;
                EditorGUILayout.HelpBox(issue.Message, type);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
