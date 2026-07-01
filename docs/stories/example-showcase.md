# Story: AudioKit Example Showcase

> BMAD dev story · Epic E5 (Polish) follow-up · Dev agent: Amelia (BMAD) driven by Kobi
> Status: In progress → implement

## Story
**As a** developer evaluating AudioKit,
**I want** a ready-to-play example scene that uses real royalty-free audio,
**so that** I can hear and read how every feature (SFX, music, buses, ducking, 3D, events) is wired.

## Context
The package ships with a procedural demo (`AudioKitDemo`). This story adds a richer, asset-backed
**Example Showcase** using downloaded CC-BY audio so the features are demonstrated with real music
and SFX, and so the authored `AudioDatabase` doubles as documentation.

## Assets (downloaded, royalty-free)
- **Music** (Kevin MacLeod, incompetech, CC-BY 4.0): `carefree`, `fluffing_a_duck`, `sneaky_snitch`.
- **SFX** (Google Sound Library, CC-BY 4.0; transcoded Opus→WAV for Unity): `ui_pop`, `ui_boing`,
  `ui_slide_whistle`, `ui_wood`, `impact_crash`, `laser`, `beep`, `ambience_coffee`.
- Attribution recorded in `Examples/THIRD-PARTY-NOTICES.md`.

## Acceptance criteria
1. An `ExampleDatabase.asset` exists with buses **Music / SFX / UI / Ambience**, groups covering
   **all six play modes**, per-play volume/pitch randomization, a 3D group, and a **Jukebox** playlist.
2. An `ExampleShowcase.unity` scene contains an `AudioKitRuntime` (bound to the database), the debug
   overlay, an `ExampleShowcase` GUI driver, and an `AudioKitEventSounds` component wired to a custom event.
3. The showcase GUI demonstrates: SFX (variations, weighted, no-repeat, 3D), music playlist
   (play/stop/next crossfade), per-bus volume + mute + solo, ducking (music ducks under a press),
   and firing a custom event.
4. A moving 3D emitter plays a following ambience so spatialization is audible.
5. Builds via an editor menu (`AudioKit ▸ Examples ▸ Build Showcase`) with no blocking dialogs so it
   can be regenerated/CI-driven; entering Play mode raises no errors.

## Feature → demonstration map
| Feature | Where |
|--------|-------|
| Play modes (Random/NoRepeat/RoundRobin/Sequential/Weighted/Oldest) | groups in ExampleDatabase |
| Variations + weight | `UI_Click`, `UI_Positive` |
| 3D + follow + PlayAt | `Impact`, `Footstep`, moving ambience emitter |
| Buses volume/mute/solo | showcase bus panel |
| Ducking (ADHR) | Music bus + hold-to-duck |
| Music playlist crossfade + sequencing | `Jukebox` |
| Custom events + Event Sounds component | `Celebrate` + `AudioKitEventSounds` on a cube |
| SoundHandle control | Laser (stop/fade), pitch |

## Tasks
- [x] Download + transcode audio; record attribution.
- [ ] `ExampleShowcase` runtime GUI driver (Examples asmdef).
- [ ] `ExampleBuilder` editor: build database asset + scene, wire components (no dialogs).
- [ ] Build via MCP, Play-mode smoke test.
- [ ] README "Examples" section.
