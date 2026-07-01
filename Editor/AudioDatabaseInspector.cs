using UnityEditor;
using UnityEngine;

namespace Kobapps.AudioKit.Editor
{
    /// <summary>
    /// Adds quick actions on top of the default <see cref="AudioDatabaseAsset"/> inspector: open the
    /// authoring window and run the validation gate. The default property drawer below still shows
    /// the raw lists for power users.
    /// </summary>
    [CustomEditor(typeof(AudioDatabaseAsset))]
    public sealed class AudioDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var db = (AudioDatabaseAsset)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open in Audio Manager", GUILayout.Height(26)))
                    AudioManagerWindow.Open(db);
                if (GUILayout.Button("Validate", GUILayout.Height(26), GUILayout.Width(90)))
                    AudioKitValidationWindow.Open(db);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Buses: {db.buses.Count}    Groups: {db.groups.Count}", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            DrawDefaultInspector();
        }
    }
}
