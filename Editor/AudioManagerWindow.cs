using System.Collections.Generic;
using Kobapps.AudioKit.Core;
using UnityEditor;
using UnityEngine;
// Disambiguate from UnityEngine.PlayMode.
using PlayMode = Kobapps.AudioKit.Core.PlayMode;
using AK = Kobapps.AudioKit.AudioKit;

namespace Kobapps.AudioKit.Editor
{
    /// <summary>
    /// The AudioKit authoring window: browse and edit buses, groups and variations on an
    /// <see cref="AudioDatabaseAsset"/>, audition clips in edit mode, and drag-drop AudioClips to
    /// auto-create groups. Pure IMGUI so it works with no third-party dependency (an Odin-enhanced
    /// inspector can layer on top via the ODIN_INSPECTOR define).
    /// </summary>
    public sealed class AudioManagerWindow : EditorWindow
    {
        private AudioDatabaseAsset _db;
        private Vector2 _scroll;
        private readonly HashSet<int> _groupFoldouts = new HashSet<int>();
        private readonly HashSet<int> _busFoldouts = new HashSet<int>();
        private readonly HashSet<int> _playlistFoldouts = new HashSet<int>();
        private int _tab;
        private string _search = "";

        // Cached labels + tooltips for the window's controls.
        private static class C
        {
            // Groups
            public static readonly GUIContent GroupName = new GUIContent("Name", "Unique name used to play this group: AudioKit.Play(\"name\").");
            public static readonly GUIContent Bus = new GUIContent("Bus", "Bus this group routes to. (master) = no bus.");
            public static readonly GUIContent PlayModeC = new GUIContent("Play mode", "How the next variation is chosen on each play (Random, RoundRobin, Weighted, Oldest, …).");
            public static readonly GUIContent Steal = new GUIContent("Steal policy", "At the voice limit: steal the oldest/quietest voice, or reject the new play.");
            public static readonly GUIContent GroupVoiceLimit = new GUIContent("Voice limit", "Maximum simultaneous voices for this group.");
            public static readonly GUIContent Retrigger = new GUIContent("Retrigger %", "Suppress a retrigger until a playing voice passes this % of its clip.");
            public static readonly GUIContent Is3D = new GUIContent("3D", "Play as 3D positional audio (2D if off).");
            public static readonly GUIContent MixerGroup = new GUIContent("Mixer group", "Optional AudioMixerGroup output.");
            public static readonly GUIContent VolRange = new GUIContent("Volume min/max", "Random per-play volume multiplier range (on top of the variation volume).");
            public static readonly GUIContent PitchRange = new GUIContent("Pitch min/max", "Random per-play pitch multiplier range (on top of the variation pitch).");
            // Buses
            public static readonly GUIContent BusName = new GUIContent("Name", "Unique bus name; groups and playlists route to it by this name.");
            public static readonly GUIContent BusVolume = new GUIContent("Volume", "Bus volume (0–1), smoothly faded at runtime.");
            public static readonly GUIContent BusVoiceLimit = new GUIContent("Voice limit (0=∞)", "Max simultaneous voices across all groups on this bus. 0 = unlimited.");
            public static readonly GUIContent Mute = new GUIContent("Mute", "Silence this bus.");
            public static readonly GUIContent Solo = new GUIContent("Solo", "When any bus is soloed, only soloed buses are audible.");
            public static readonly GUIContent MusicBus = new GUIContent("Music", "Marks this as the default bus for playlists / music.");
            public static readonly GUIContent Ducking = new GUIContent("Ducking", "Lower this bus while a duck trigger is active (e.g. dialogue ducks music).");
            public static readonly GUIContent DuckAmount = new GUIContent("Amount", "How far to duck: 0 = none, 1 = silence.");
            public static readonly GUIContent DuckAttack = new GUIContent("Attack", "Seconds to ramp down to the ducked level.");
            public static readonly GUIContent DuckHold = new GUIContent("Hold", "Seconds to hold the ducked level after the last trigger releases.");
            public static readonly GUIContent DuckRelease = new GUIContent("Release", "Seconds to ramp back up to full volume.");
            // Playlists
            public static readonly GUIContent PlName = new GUIContent("Name", "Unique playlist name: AudioKit.PlayPlaylist(\"name\").");
            public static readonly GUIContent PlMode = new GUIContent("Mode", "Track order: Sorted (in order), Random, or RandomNoRepeat.");
            public static readonly GUIContent Crossfade = new GUIContent("Crossfade (s)", "Seconds to crossfade between tracks. 0 = hard cut (or seamless when Gapless).");
            public static readonly GUIContent Gapless = new GUIContent("Gapless", "Sample-accurate handoff at the end of each track (best with crossfade 0).");
            // Settings
            public static readonly GUIContent VoiceCap = new GUIContent("Voice capacity (pool cap)", "Hard cap on simultaneous voices (AudioSources). Stealing kicks in at this limit.");
            public static readonly GUIContent Prewarm = new GUIContent("Prewarm voices", "Voices pre-created on load to avoid first-play hitches.");
            // Variation row + action buttons
            public static readonly GUIContent VarPitch = new GUIContent("p", "Pitch multiplier for this variation (1 = original).");
            public static readonly GUIContent VarWeight = new GUIContent("w", "Weight for the Weighted play mode (higher = chosen more often).");
            public static readonly GUIContent Preview = new GUIContent("▶", "Preview this clip.");
            public static readonly GUIContent StopPrev = new GUIContent("■", "Stop the preview.");
            public static readonly GUIContent MoveUp = new GUIContent("▲", "Move up.");
            public static readonly GUIContent MoveDown = new GUIContent("▼", "Move down.");
            public static readonly GUIContent Duplicate = new GUIContent("⧉", "Duplicate.");
            public static readonly GUIContent Remove = new GUIContent("✕", "Remove.");
        }

        [MenuItem("Tools/AudioKit/Audio Manager")]
        public static void Open()
        {
            var w = GetWindow<AudioManagerWindow>("AudioKit");
            w.minSize = new Vector2(460, 400);
            w.Show();
        }

        public static void Open(AudioDatabaseAsset db)
        {
            Open();
            GetWindow<AudioManagerWindow>()._db = db;
        }

        private void OnEnable()
        {
            if (_db == null)
                _db = Selection.activeObject as AudioDatabaseAsset;
        }

        // Never leave a preview auditioning after the window closes.
        private void OnDisable() => AudioKitEditorPreview.StopAll();

        private void OnGUI()
        {
            DrawToolbar();

            if (_db == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Assign or create an Audio Database to begin.", MessageType.Info);
                if (GUILayout.Button("Create New Audio Database…"))
                    CreateDatabase();
                return;
            }

            HandleClipDragAndDrop();

            _tab = GUILayout.Toolbar(_tab, new[]
            {
                $"Groups ({_db.groups.Count})", $"Buses ({_db.buses.Count})",
                $"Music ({_db.playlists.Count})", "Settings"
            });
            DrawStatusRow();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_tab)
            {
                case 0: DrawGroups(); break;
                case 1: DrawBuses(); break;
                case 2: DrawPlaylists(); break;
                default: DrawSettings(); break;
            }
            EditorGUILayout.EndScrollView();

            if (Application.isPlaying) Repaint(); // live meters
        }

        private void DrawStatusRow()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("🔍", GUILayout.Width(20));
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(190));
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22))) { _search = ""; GUI.FocusControl(null); }

                GUILayout.FlexibleSpace();

                bool live = Application.isPlaying && AK.Service != null && AK.Service.IsReady;
                if (live)
                {
                    int active = AK.ActiveVoiceCount, cap = AK.VoiceCapacity;
                    float t = cap > 0 ? (float)active / cap : 0f;
                    DrawMeter($"Voices {active}/{cap}", t, 170f,
                        active >= cap ? new Color(0.85f, 0.4f, 0.4f) : new Color(0.35f, 0.75f, 0.5f));
                }
                else
                {
                    if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(58))) SetAllFoldouts(true);
                    if (GUILayout.Button("Collapse", EditorStyles.toolbarButton, GUILayout.Width(64))) SetAllFoldouts(false);
                }
            }
        }

        private static void DrawMeter(string label, float t, float width, Color color)
        {
            Rect r = GUILayoutUtility.GetRect(width, 16f, GUILayout.Width(width));
            r.y += 1f; r.height -= 2f;
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.30f));
            var fill = new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height);
            EditorGUI.DrawRect(fill, color);
            var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Label(r, label, style);
        }

        private void SetAllFoldouts(bool open)
        {
            _groupFoldouts.Clear(); _busFoldouts.Clear(); _playlistFoldouts.Clear();
            if (!open) return;
            for (int i = 0; i < _db.groups.Count; i++) _groupFoldouts.Add(i);
            for (int i = 0; i < _db.buses.Count; i++) _busFoldouts.Add(i);
            for (int i = 0; i < _db.playlists.Count; i++) _playlistFoldouts.Add(i);
        }

        private bool Match(string name) =>
            string.IsNullOrEmpty(_search) ||
            (name != null && name.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) >= 0);

        private int LiveVoiceCount(string groupName)
        {
            if (!Application.isPlaying || AK.Service == null || !AK.Service.IsReady) return -1;
            var engine = AK.Service.Engine;
            int idx = engine.GetGroupIndex(Core.AudioId.From(groupName));
            return idx >= 0 ? engine.GroupVoiceCount(idx) : 0;
        }

        private int LiveBusVoiceCount(string busName)
        {
            if (!Application.isPlaying || AK.Service == null || !AK.Service.IsReady) return -1;
            var engine = AK.Service.Engine;
            int idx = engine.GetBusIndex(Core.AudioId.From(busName));
            return idx >= 0 ? engine.BusVoiceCount(idx) : 0;
        }

        private static void Swap<T>(List<T> list, int a, int b)
        {
            (list[a], list[b]) = (list[b], list[a]);
        }

        private static T Clone<T>(T obj) => JsonUtility.FromJson<T>(JsonUtility.ToJson(obj));

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var db = (AudioDatabaseAsset)EditorGUILayout.ObjectField(_db, typeof(AudioDatabaseAsset), false, GUILayout.Width(220));
                if (EditorGUI.EndChangeCheck()) _db = db;

                if (GUILayout.Button(new GUIContent("New", "Create a new Audio Database asset."), EditorStyles.toolbarButton, GUILayout.Width(40)))
                    CreateDatabase();

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!AudioKitEditorPreview.IsSupported))
                    if (GUILayout.Button(new GUIContent("■ Stop Preview", "Stop any editor clip preview (also AudioKit ▸ Stop Preview Audio)."), EditorStyles.toolbarButton, GUILayout.Width(100)))
                        AudioKitEditorPreview.StopAll();

                using (new EditorGUI.DisabledScope(_db == null))
                {
                    if (GUILayout.Button(new GUIContent("Validate", "Run the validation gate on this database."), EditorStyles.toolbarButton, GUILayout.Width(64)))
                        AudioKitValidationWindow.Open(_db);
                    if (GUILayout.Button(new GUIContent("Save", "Save the database asset to disk."), EditorStyles.toolbarButton, GUILayout.Width(48)))
                    {
                        EditorUtility.SetDirty(_db);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        // --- Groups -----------------------------------------------------------------------------

        private void DrawGroups()
        {
            string[] busNames = BusNameOptions();

            for (int g = 0; g < _db.groups.Count; g++)
            {
                var grp = _db.groups[g];
                if (!Match(grp.groupName)) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool open = _groupFoldouts.Contains(g);
                        bool now = EditorGUILayout.Foldout(open, $"{grp.groupName}  ({grp.variations.Count} var)", true);
                        ToggleFoldout(_groupFoldouts, g, now);

                        int lv = LiveVoiceCount(grp.groupName);
                        if (lv >= 0)
                        {
                            var style = new GUIStyle(EditorStyles.miniBoldLabel);
                            style.normal.textColor = lv > 0 ? new Color(0.5f, 1f, 0.6f) : new Color(0.55f, 0.55f, 0.55f);
                            GUILayout.Label("♪ " + lv, style, GUILayout.Width(40));
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(C.MoveUp, EditorStyles.miniButtonLeft, GUILayout.Width(22)) && g > 0)
                        { Record("Reorder Group"); Swap(_db.groups, g, g - 1); EditorUtility.SetDirty(_db); break; }
                        if (GUILayout.Button(C.MoveDown, EditorStyles.miniButtonMid, GUILayout.Width(22)) && g < _db.groups.Count - 1)
                        { Record("Reorder Group"); Swap(_db.groups, g, g + 1); EditorUtility.SetDirty(_db); break; }
                        if (GUILayout.Button(C.Duplicate, EditorStyles.miniButtonMid, GUILayout.Width(24)))
                        { Record("Duplicate Group"); var c = Clone(grp); c.groupName += " (copy)"; _db.groups.Insert(g + 1, c); EditorUtility.SetDirty(_db); break; }
                        if (GUILayout.Button(C.Remove, EditorStyles.miniButtonRight, GUILayout.Width(22)))
                        {
                            Record("Remove Group");
                            _db.groups.RemoveAt(g);
                            EditorUtility.SetDirty(_db);
                            break;
                        }
                    }

                    if (!_groupFoldouts.Contains(g)) continue;

                    EditorGUI.BeginChangeCheck();
                    grp.groupName = EditorGUILayout.TextField(C.GroupName, grp.groupName);

                    int busIdx = Mathf.Max(0, System.Array.IndexOf(busNames, string.IsNullOrEmpty(grp.busName) ? "(master)" : grp.busName));
                    int newBus = EditorGUILayout.Popup(C.Bus, busIdx, busNames);
                    grp.busName = newBus == 0 ? "" : busNames[newBus];

                    grp.playMode = (PlayMode)EditorGUILayout.EnumPopup(C.PlayModeC, grp.playMode);
                    grp.stealPolicy = (VoiceStealPolicy)EditorGUILayout.EnumPopup(C.Steal, grp.stealPolicy);
                    grp.voiceLimit = Mathf.Max(1, EditorGUILayout.IntField(C.GroupVoiceLimit, grp.voiceLimit));
                    grp.retriggerPercent = EditorGUILayout.Slider(C.Retrigger, grp.retriggerPercent, 0f, 100f);
                    grp.is3D = EditorGUILayout.Toggle(C.Is3D, grp.is3D);
                    grp.mixerGroup = (UnityEngine.Audio.AudioMixerGroup)EditorGUILayout.ObjectField(C.MixerGroup, grp.mixerGroup, typeof(UnityEngine.Audio.AudioMixerGroup), false);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(C.VolRange);
                        grp.volumeMin = EditorGUILayout.FloatField(grp.volumeMin, GUILayout.Width(60));
                        grp.volumeMax = EditorGUILayout.FloatField(grp.volumeMax, GUILayout.Width(60));
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel(C.PitchRange);
                        grp.pitchMin = EditorGUILayout.FloatField(grp.pitchMin, GUILayout.Width(60));
                        grp.pitchMax = EditorGUILayout.FloatField(grp.pitchMax, GUILayout.Width(60));
                    }
                    if (EditorGUI.EndChangeCheck())
                        EditorUtility.SetDirty(_db);

                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Variations", EditorStyles.boldLabel);
                    DrawVariations(grp);

                    if (GUILayout.Button("+ Add Variation"))
                    {
                        Record("Add Variation");
                        grp.variations.Add(new AudioVariation { name = "Variation " + grp.variations.Count });
                        EditorUtility.SetDirty(_db);
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Add Group"))
            {
                Record("Add Group");
                _db.groups.Add(new SoundGroupDefinition { groupName = "Group " + _db.groups.Count });
                EditorUtility.SetDirty(_db);
            }
            EditorGUILayout.HelpBox("Tip: drag AudioClips into this window to auto-create a group.", MessageType.None);
        }

        private void DrawVariations(SoundGroupDefinition grp)
        {
            for (int v = 0; v < grp.variations.Count; v++)
            {
                var variation = grp.variations[v];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    variation.clip = (AudioClip)EditorGUILayout.ObjectField(variation.clip, typeof(AudioClip), false, GUILayout.MinWidth(120));
                    variation.volume = EditorGUILayout.Slider(variation.volume, 0f, 1f, GUILayout.MinWidth(80));
                    GUILayout.Label(C.VarPitch, GUILayout.Width(12));
                    variation.pitch = EditorGUILayout.FloatField(variation.pitch, GUILayout.Width(40));
                    GUILayout.Label(C.VarWeight, GUILayout.Width(14));
                    variation.weight = EditorGUILayout.FloatField(variation.weight, GUILayout.Width(36));
                    if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_db);

                    using (new EditorGUI.DisabledScope(variation.clip == null || !AudioKitEditorPreview.IsSupported))
                    {
                        if (GUILayout.Button(C.Preview, GUILayout.Width(24)))
                            AudioKitEditorPreview.Play(variation.clip);
                    }
                    if (GUILayout.Button(C.StopPrev, GUILayout.Width(24)))
                        AudioKitEditorPreview.StopAll();
                    if (GUILayout.Button(C.Remove, GUILayout.Width(22)))
                    {
                        Record("Remove Variation");
                        grp.variations.RemoveAt(v);
                        EditorUtility.SetDirty(_db);
                        break;
                    }
                }
            }
        }

        // --- Buses ------------------------------------------------------------------------------

        private void DrawBuses()
        {
            for (int b = 0; b < _db.buses.Count; b++)
            {
                var bus = _db.buses[b];
                if (!Match(bus.busName)) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool open = _busFoldouts.Contains(b);
                        string title = bus.isMusicBus ? bus.busName + "  ♫" : bus.busName;
                        bool now = EditorGUILayout.Foldout(open, title, true);
                        ToggleFoldout(_busFoldouts, b, now);

                        int lv = LiveBusVoiceCount(bus.busName);
                        if (lv >= 0)
                        {
                            var style = new GUIStyle(EditorStyles.miniBoldLabel);
                            style.normal.textColor = lv > 0 ? new Color(0.5f, 1f, 0.6f) : new Color(0.55f, 0.55f, 0.55f);
                            GUILayout.Label("♪ " + lv, style, GUILayout.Width(40));
                        }

                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(C.MoveUp, EditorStyles.miniButtonLeft, GUILayout.Width(22)) && b > 0)
                        { Record("Reorder Bus"); Swap(_db.buses, b, b - 1); EditorUtility.SetDirty(_db); break; }
                        if (GUILayout.Button(C.MoveDown, EditorStyles.miniButtonMid, GUILayout.Width(22)) && b < _db.buses.Count - 1)
                        { Record("Reorder Bus"); Swap(_db.buses, b, b + 1); EditorUtility.SetDirty(_db); break; }
                        if (GUILayout.Button(C.Duplicate, EditorStyles.miniButtonMid, GUILayout.Width(24)))
                        { Record("Duplicate Bus"); var c = Clone(bus); c.busName += " (copy)"; _db.buses.Insert(b + 1, c); EditorUtility.SetDirty(_db); break; }
                        if (GUILayout.Button(C.Remove, EditorStyles.miniButtonRight, GUILayout.Width(22)))
                        {
                            Record("Remove Bus");
                            _db.buses.RemoveAt(b);
                            EditorUtility.SetDirty(_db);
                            break;
                        }
                    }

                    if (!_busFoldouts.Contains(b)) continue;

                    EditorGUI.BeginChangeCheck();
                    bus.busName = EditorGUILayout.TextField(C.BusName, bus.busName);
                    bus.volume = EditorGUILayout.Slider(C.BusVolume, bus.volume, 0f, 1f);
                    bus.voiceLimit = Mathf.Max(0, EditorGUILayout.IntField(C.BusVoiceLimit, bus.voiceLimit));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bus.mute = EditorGUILayout.ToggleLeft(C.Mute, bus.mute, GUILayout.Width(70));
                        bus.solo = EditorGUILayout.ToggleLeft(C.Solo, bus.solo, GUILayout.Width(70));
                        bus.isMusicBus = EditorGUILayout.ToggleLeft(C.MusicBus, bus.isMusicBus, GUILayout.Width(70));
                    }
                    bus.mixerGroup = (UnityEngine.Audio.AudioMixerGroup)EditorGUILayout.ObjectField(C.MixerGroup, bus.mixerGroup, typeof(UnityEngine.Audio.AudioMixerGroup), false);

                    bus.duck.enabled = EditorGUILayout.ToggleLeft(C.Ducking, bus.duck.enabled);
                    if (bus.duck.enabled)
                    {
                        EditorGUI.indentLevel++;
                        bus.duck.amount = EditorGUILayout.Slider(C.DuckAmount, bus.duck.amount, 0f, 1f);
                        bus.duck.attack = EditorGUILayout.FloatField(C.DuckAttack, bus.duck.attack);
                        bus.duck.hold = EditorGUILayout.FloatField(C.DuckHold, bus.duck.hold);
                        bus.duck.release = EditorGUILayout.FloatField(C.DuckRelease, bus.duck.release);
                        EditorGUI.indentLevel--;
                    }
                    if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_db);
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Add Bus"))
            {
                Record("Add Bus");
                _db.buses.Add(new BusDefinition { busName = "Bus " + _db.buses.Count });
                EditorUtility.SetDirty(_db);
            }
        }

        private void DrawPlaylists()
        {
            string[] busNames = BusNameOptions();

            for (int p = 0; p < _db.playlists.Count; p++)
            {
                var pl = _db.playlists[p];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool now = EditorGUILayout.Foldout(_playlistFoldouts.Contains(p), $"{pl.playlistName}  ({pl.tracks.Count} tracks)", true);
                        ToggleFoldout(_playlistFoldouts, p, now);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(C.Remove, GUILayout.Width(22)))
                        {
                            Record("Remove Playlist");
                            _db.playlists.RemoveAt(p);
                            EditorUtility.SetDirty(_db);
                            break;
                        }
                    }

                    if (!_playlistFoldouts.Contains(p)) continue;

                    EditorGUI.BeginChangeCheck();
                    pl.playlistName = EditorGUILayout.TextField(C.PlName, pl.playlistName);
                    pl.mode = (PlaylistMode)EditorGUILayout.EnumPopup(C.PlMode, pl.mode);
                    pl.crossfadeSeconds = Mathf.Max(0f, EditorGUILayout.FloatField(C.Crossfade, pl.crossfadeSeconds));
                    pl.gapless = EditorGUILayout.Toggle(C.Gapless, pl.gapless);

                    int busIdx = Mathf.Max(0, System.Array.IndexOf(busNames, string.IsNullOrEmpty(pl.busName) ? "(master)" : pl.busName));
                    int newBus = EditorGUILayout.Popup(C.Bus, busIdx, busNames);
                    pl.busName = newBus == 0 ? "" : busNames[newBus];
                    if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_db);

                    EditorGUILayout.LabelField("Tracks", EditorStyles.boldLabel);
                    for (int t = 0; t < pl.tracks.Count; t++)
                    {
                        var track = pl.tracks[t];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUI.BeginChangeCheck();
                            track.clip = (AudioClip)EditorGUILayout.ObjectField(track.clip, typeof(AudioClip), false, GUILayout.MinWidth(120));
                            track.volume = EditorGUILayout.Slider(track.volume, 0f, 1f, GUILayout.MinWidth(80));
                            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_db);

                            using (new EditorGUI.DisabledScope(track.clip == null || !AudioKitEditorPreview.IsSupported))
                                if (GUILayout.Button(C.Preview, GUILayout.Width(24))) AudioKitEditorPreview.Play(track.clip);
                            if (GUILayout.Button(C.StopPrev, GUILayout.Width(24))) AudioKitEditorPreview.StopAll();
                            if (GUILayout.Button(C.Remove, GUILayout.Width(22)))
                            {
                                Record("Remove Track");
                                pl.tracks.RemoveAt(t);
                                EditorUtility.SetDirty(_db);
                                break;
                            }
                        }
                    }

                    if (GUILayout.Button("+ Add Track"))
                    {
                        Record("Add Track");
                        pl.tracks.Add(new MusicTrack { name = "Track " + pl.tracks.Count });
                        EditorUtility.SetDirty(_db);
                    }
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("+ Add Playlist"))
            {
                Record("Add Playlist");
                _db.playlists.Add(new PlaylistDefinition { playlistName = "Playlist " + _db.playlists.Count });
                EditorUtility.SetDirty(_db);
            }
        }

        private void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            _db.voiceCapacity = Mathf.Max(1, EditorGUILayout.IntField(C.VoiceCap, _db.voiceCapacity));
            _db.prewarmVoices = Mathf.Clamp(EditorGUILayout.IntField(C.Prewarm, _db.prewarmVoices), 0, _db.voiceCapacity);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_db);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Place a database named 'AudioKitDatabase' in a Resources folder to auto-bootstrap at runtime, " +
                "or add an AudioKitRuntime component and assign this database.", MessageType.Info);
        }

        // --- Helpers ----------------------------------------------------------------------------

        private string[] BusNameOptions()
        {
            var list = new List<string> { "(master)" };
            for (int b = 0; b < _db.buses.Count; b++)
                list.Add(_db.buses[b].busName);
            return list.ToArray();
        }

        private void ToggleFoldout(HashSet<int> set, int index, bool open)
        {
            if (open) set.Add(index); else set.Remove(index);
        }

        private void Record(string label) => Undo.RecordObject(_db, label);

        private void HandleClipDragAndDrop()
        {
            var evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            bool hasClip = false;
            foreach (var obj in DragAndDrop.objectReferences)
                if (obj is AudioClip) { hasClip = true; break; }
            if (!hasClip) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Record("Add Group from Clips");
                var grp = new SoundGroupDefinition { groupName = "Group " + _db.groups.Count };
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is AudioClip clip)
                    {
                        if (grp.variations.Count == 0) grp.groupName = clip.name;
                        grp.variations.Add(new AudioVariation { name = clip.name, clip = clip });
                    }
                }
                _db.groups.Add(grp);
                EditorUtility.SetDirty(_db);
            }
            evt.Use();
        }

        private void CreateDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Audio Database", "AudioDatabase", "asset",
                "Choose where to save the AudioKit database.");
            if (string.IsNullOrEmpty(path)) return;

            var db = CreateInstance<AudioDatabaseAsset>();
            db.buses.Add(new BusDefinition { busName = "Music", isMusicBus = true });
            db.buses.Add(new BusDefinition { busName = "SFX" });
            AssetDatabase.CreateAsset(db, path);
            AssetDatabase.SaveAssets();
            _db = db;
            Selection.activeObject = db;
        }
    }
}
