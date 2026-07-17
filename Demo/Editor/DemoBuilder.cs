using Kobapps.AudioKit.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kobapps.AudioKit.DemoEditor
{
    /// <summary>
    /// Generates the AudioKit demo assets reliably from the editor (rather than shipping a binary
    /// scene/asset): a sample scene wired with the self-contained <see cref="AudioKitDemo"/> and the
    /// debug overlay, plus an optional starter database with Music/SFX buses.
    /// </summary>
    public static class DemoBuilder
    {
        private const string DemoFolder = "Assets/AudioKit/Demo";

        [MenuItem("Tools/AudioKit/Demo/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("AudioKitDemo");
            go.AddComponent<AudioKitDemo>();
            go.AddComponent<AudioKitDebugOverlay>();

            EnsureFolder();
            string path = DemoFolder + "/DemoScene.unity";
            EditorSceneManager.SaveScene(scene, path);
            EditorUtility.DisplayDialog("AudioKit",
                "Demo scene created at " + path + ".\nPress Play and use the on-screen panel (or Space/C/M/D keys).", "OK");
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
        }

        [MenuItem("Tools/AudioKit/Demo/Create Starter Database")]
        public static void CreateStarterDatabase()
        {
            EnsureFolder();
            var db = ScriptableObject.CreateInstance<AudioDatabaseAsset>();
            db.buses.Add(new BusDefinition { busName = "Music", isMusicBus = true });
            db.buses.Add(new BusDefinition { busName = "SFX" });
            db.voiceCapacity = 32;
            db.prewarmVoices = 8;

            string path = AssetDatabase.GenerateUniqueAssetPath(DemoFolder + "/AudioDatabase.asset");
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(DemoFolder))
                AssetDatabase.CreateFolder("Assets/AudioKit", "Demo");
        }
    }
}
