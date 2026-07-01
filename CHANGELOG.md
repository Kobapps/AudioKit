# Changelog

All notable changes to AudioKit are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/) and the project adheres to Semantic Versioning.

## [0.4.0] - 2026-07-01

> AAA polish pass (BMAD investigation in `docs/investigation-aaa.md`).

### Added
- **Fade-in on play:** `Play/PlayAt/PlayFollow(..., fadeInSeconds)`.
- **Per-group runtime volume & mute:** `AudioKit.SetGroupVolume`, `GetGroupVolume`, `MuteGroup`,
  `IsGroupMuted` (applied to live voices via a Core per-group gain).
- **3D voice virtualization:** `AudioKit.VirtualizeDistance` — one-shots beyond N metres from the
  listener are skipped without spending a voice.
- **Diagnostics:** `AudioKit.ActiveVoiceCount` / `VoiceCapacity`.
- **Editor UX overhaul** — Audio Manager window: search/filter, live per-group/bus voice meters +
  pool-usage bar in play mode, group/bus **reorder** (▲▼) and **duplicate** (⧉), tab count badges,
  expand/collapse-all, music-bus marker.

- **Tooltips everywhere:** `[Tooltip]` on every serialized field (variations, groups, buses, ducking,
  playlists, tracks, database, group set, event sounds/actions, debug overlay, runtime, installer) and
  `GUIContent` tooltips on every control + action button in the Audio Manager and Validation windows.

### Changed (performance)
- `VoiceManager.Tick` now iterates only **active** voices (O(1) swap-remove list) instead of scanning
  the whole pool.
- `AudioSource.volume` is written only when it changes past an epsilon (fewer native interop calls).
- `BusGraph` caches the solo state instead of rescanning all buses per gain query.

### Verified
- Headless 66/66, in Unity 6.5 **EditMode 66/66, PlayMode 24/24**.

## [0.3.0] - 2026-07-01

### Added
- **Dynamic / scene-scoped sound groups.** Groups can now be registered and unregistered at runtime:
  - `SoundGroupSetAsset` — a portable set of groups.
  - `AudioKitGroupSource` component — registers its set while enabled, unregisters on disable/destroy
    (scene/prefab-scoped lifecycle); ref-counted and safe to enable before AudioKit bootstraps.
  - Facade API: `AudioKit.RegisterGroups/UnregisterGroups(set)`, `RegisterGroup(def)`,
    `UnregisterGroup(name)`, `IsGroupRegistered(name)`.
  - Optional per-set **clip memory management** (`LoadAudioData`/`UnloadAudioData` on register/unregister).
  - Core `AudioKitCoreEngine.RegisterGroup/UnregisterGroup` with stable indices (group-slot and
    variation-range free lists so live voices are never invalidated by unrelated churn).
- Tests: 5 headless Core register/unregister tests + 6 PlayMode tests (refcount, deferred-until-ready,
  component lifecycle, script registration). **EditMode 63/63, PlayMode 20/20** green in Unity 6.5.

## [0.2.1] - 2026-07-01

### Added
- **Example showcase** (`Examples/`): an asset-backed scene + `ExampleDatabase` built from downloaded
  royalty-free audio (music: Kevin MacLeod / incompetech; SFX: Google Sound Library — both CC-BY 4.0,
  Opus→WAV transcoded for Unity). Demonstrates all six play modes, buses (volume/mute/solo), ducking,
  a crossfading `Jukebox` playlist, 3D positional audio via a moving emitter, `SoundHandle` control,
  custom events and the `AudioKitEventSounds` component. Regenerable via `AudioKit ▸ Examples ▸
  Build Showcase` (no dialogs). Verified in Play mode with no gameplay errors.
- `Examples/THIRD-PARTY-NOTICES.md` recording audio attributions (CC-BY 4.0).

### Fixed
- **Example showcase UI overlapped the debug overlay:** the showcase panel is now anchored top-right,
  and the debug overlay starts hidden in the example scene (press **F9** to toggle it).
- **Example scene was silent in Play mode:** the generated scene stored a transient database reference,
  leaving `AudioKitRuntime` with no database (service never started). The builder now reloads the
  saved asset before wiring it, and `AudioKitRuntime` logs a clear warning if it has no database.
- **Editor clip preview could get stuck playing** (especially long music tracks) if the Audio Manager
  window was closed mid-audition. Preview now stops any prior clip before playing a new one, stops on
  window close, and can always be stopped via an always-visible toolbar button or the new
  **`AudioKit ▸ Stop Preview Audio`** menu item (shortcut Ctrl/Cmd+Shift+.).

### Notes
- The AudioKit package code has no audio dependencies; example clips can be swapped for CC0/your own
  to remove the attribution requirement.

## [0.2.0] - 2026-07-01

### Added
- **Phase 2 — Music/Playlist system:** Core `PlaylistSequencer` (Sorted/Random/RandomNoRepeat, unit-tested)
  and a runtime `MusicPlayer` that crossfades between two dedicated sources on a music bus, with
  hard-cut and gapless (`PlayScheduled`) transitions and single-track looping. Authoring via a new
  **Music** tab in the Audio Manager, validation coverage, facade API (`PlayPlaylist`/`StopPlaylist`/
  `NextTrack`/`IsPlaylistPlaying`), and debug-overlay display.
- **UniTask async layer** (`Kobapps.AudioKit.UniTask`, compiled only when UniTask is present):
  `AudioKitAsync.PlayAndForget`, `FadeBusAsync`, and `SoundHandle.WaitForCompletionAsync()`.

### Verified
- In Unity 6.5 via MCP: **EditMode 58/58, PlayMode 14/14** green; all 8 assemblies compile
  (Zenject/Addressables correctly excluded when absent).

## [0.1.0] - 2026-07-01

### Added
- **Core (headless, pure C#):** `AudioId` (FNV-1a), deterministic `XorShiftRandom`, `VariationSelector`
  with all six play modes, `VoiceTable` with three steal policies + global LRU reclaim, `BusGraph`
  (volume/mute/solo/duck/master), `FadeEnvelope` (linear/ease/equal-power), `DuckEnvelope` (ADHR),
  and `AudioKitCoreEngine` orchestration. 52 headless unit tests.
- **Runtime:** `AudioDatabaseAsset` (single source of truth) + bake step, slot-indexed
  `AudioSourcePool`, `VoiceManager`, struct `SoundHandle`, static `AudioKit` facade + `IAudioService`
  / `AudioService`, AudioMixer routing, `AudioKitRuntime` driver with auto-bootstrap.
- **Events:** zero-alloc `CustomEventRegistry`, `AudioKitEventSounds` component (lifecycle, 2D/3D
  physics, particle, and custom-event triggers → action lists).
- **Editor:** Audio Manager window (IMGUI), validation gate + report window, in-editor clip preview,
  custom database inspector.
- **Optional integrations:** UniTask / Odin / Zenject / Addressables version-define seams.
- **Docs:** PRD, Architecture, Epics/Stories (BMAD artifacts), README with Master-Audio concept map.

### Known
- Addressables async clip streaming — provider seam + asmdef present; activates when Addressables is installed.
