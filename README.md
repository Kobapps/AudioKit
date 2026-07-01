# AudioKit

**Allocation-conscious, async-first audio management for Unity 6.5+.**
A clean-room conceptual replacement for Dark Tonic's *Master Audio*: **Sound Groups, Buses,
Variations, Event Sounds**, a zero-alloc **Custom Event** system, a **music/playlist** controller,
and **runtime/scene-scoped dynamic groups** — with a pure-C# core, IL2CPP-safe playback, and zero GC
on the play hot path.

> Package: `com.kobapps.audiokit` · Namespace: `Kobapps.AudioKit` · License: MIT · Unity **6000.5+**

---

## Installation

### Option A — Package Manager (git URL)
1. In Unity: **Window ▸ Package Manager**.
2. Click **+ ▸ Add package from git URL…**
3. Enter:
   ```
   https://github.com/Kobapps/AudioKit.git
   ```

### Option B — `manifest.json`
Add to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.kobapps.audiokit": "https://github.com/Kobapps/AudioKit.git"
  }
}
```
To pin a version, append `#v0.4.0` (or any tag/commit) to the URL.

### Optional dependency — UniTask (recommended)
AudioKit's core API is fully synchronous; UniTask only adds awaitable helpers (`AudioKitAsync`). To
enable them, install **UniTask** via OpenUPM by adding this scoped registry to `Packages/manifest.json`:
```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp"]
    }
  ],
  "dependencies": {
    "com.cysharp.unitask": "2.5.11"
  }
}
```
> Odin, Zenject/Extenject and Addressables are **also optional** — AudioKit detects each via version
> defines and works with them absent or present.

### Requirements
- Unity **6000.5** (Unity 6.5) or newer.
- No mandatory third-party dependencies.

---

## Quick start (under 2 minutes)

1. **Create a database:** `Window ▸ AudioKit ▸ Audio Manager ▸ New`, or right-click
   `Create ▸ AudioKit ▸ Audio Database`.
2. **Add a bus** (e.g. `SFX`) in the *Buses* tab.
3. **Drag AudioClips into the window** — a Sound Group is auto-created; set its **Bus** to `SFX`.
4. **Bootstrap the runtime** — either put the database in a `Resources` folder named
   **`AudioKitDatabase`** (auto-boots), or add an **`AudioKit Runtime`** component and assign it.
5. **Play from code:**

```csharp
using Kobapps.AudioKit;

SoundHandle h = AudioKit.Play("Explosion");           // 2D one-shot
AudioKit.PlayAt("Explosion", transform.position);      // positional 3D
AudioKit.PlayFollow("Footstep", transform);            // follows a transform
AudioKit.Play("Music", fadeInSeconds: 1f);             // fade in

AudioKit.FadeBus("Music", to: 0f, seconds: 1.5f);      // smooth, allocation-free
AudioKit.MuteBus("SFX", true);
AudioKit.SetGroupVolume("Footsteps", 0.6f);
AudioKit.PlayPlaylist("Jukebox");                      // crossfading music
AudioKit.FireEvent("BossDefeated");

h.SetVolume(0.5f);
h.Stop(fadeSeconds: 0.25f);
```

---

## Features

- **Sound Groups** with 6 play modes (Random, RandomNoImmediateRepeat, RoundRobin, Sequential,
  Weighted, Oldest), per-play volume/pitch randomization, voice limits + 3 steal policies.
- **Buses** — volume (smooth-faded), voice limits, mute/solo, **ADHR ducking**, AudioMixer routing.
- **Music / Playlists** — crossfade, sequencing, gapless, per-track volume.
- **Event Sounds** component — lifecycle / 2D+3D physics / particle / custom-event triggers → actions.
- **Custom Events** — zero-alloc pub/sub by hashed name.
- **Scene/prefab & dynamic groups** — register/unregister sound sets at runtime (`AudioKitGroupSource`
  + `AudioKit.RegisterGroups`), with optional clip load/unload.
- **Struct `SoundHandle`** for GC-free voice control; **fade-in on play**; **per-group volume/mute**;
  **3D voice virtualization**; live **diagnostics**.
- **Editor** — Audio Manager window with search, live voice meters, reorder/duplicate, validation gate,
  in-editor clip preview, and a runtime debug overlay.
- **Pure C# Core** (no UnityEngine) — headless-unit-tested; **zero GC on `Play`**; IL2CPP-safe.

Design docs live in **[docs/](docs/)** — [PRD](docs/PRD.md), [Architecture](docs/Architecture.md),
[Epics & Stories](docs/Epics-and-Stories.md).

---

## Master Audio → AudioKit concept map

| Master Audio | AudioKit |
|--------------|----------|
| Master Audio prefab (singleton) | `AudioKitRuntime` (auto-boots from `Resources/AudioKitDatabase`) |
| Sound Group / Variation | `SoundGroupDefinition` / `AudioVariation` in the `AudioDatabaseAsset` |
| Bus | `BusDefinition` |
| `PlaySound3DAtTransform` / `AtVector3` / 2D | `AudioKit.PlayFollow` / `PlayAt` / `Play` |
| `StopAllOfSound` / `FadeBusToVolume` / `MuteBus` | `AudioKit.StopGroup` / `FadeBus` / `MuteBus` |
| Bus ducking | `BusDefinition.duck` (ADHR) + `AudioKit.DuckBus/UnduckBus` |
| Event Sounds / Custom Events | `AudioKitEventSounds` / `AudioKit.FireEvent` |
| Playlist Controller | `AudioKit.PlayPlaylist` (crossfade / sequencing / gapless) |

---

## Samples

Two ready-to-run examples ship with the package (`Examples/` and `Demo/`):
- **`AudioKit ▸ Examples ▸ Build Showcase`** — generates `Examples/ExampleShowcase.unity` + a demo
  database using real royalty-free audio (music + SFX). Press Play; **F9** toggles the debug overlay.
- **`AudioKit ▸ Demo ▸ Create Demo Scene`** — a zero-asset procedural demo.

---

## Testing
- **Headless Core** compiles under plain .NET; the EditMode tests double as a `dotnet test` suite.
- **EditMode / PlayMode** suites run in the Unity Test Runner (add AudioKit as a *testable* in
  `manifest.json` to compile them). Current: EditMode 66/66, PlayMode 24/24.

---

## License
AudioKit is **MIT** (see [LICENSE.md](LICENSE.md)). The example scene uses royalty-free audio under
**CC-BY 4.0** (Kevin MacLeod / incompetech; Google Sound Library) — attributions in
[`Examples/THIRD-PARTY-NOTICES.md`](Examples/THIRD-PARTY-NOTICES.md). The package **code has no audio
dependencies**; swap the example clips for your own or CC0 assets to drop the attribution requirement.
