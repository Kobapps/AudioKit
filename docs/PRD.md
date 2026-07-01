# AudioKit — Product Requirements Document

> BMAD artifact · Module: `bmm` · Author: Kobi · Status: Approved for build
> Package: `com.kobi.audiokit` · Root namespace: `Kobapps.AudioKit` · Unity 6.5 (6000.5)

## 1. Vision

AudioKit is a Unity audio-management package that gives teams the proven mental model of
Dark Tonic's *Master Audio* — **Sound Groups, Buses, Variations, Event Sounds, Custom Events** —
in a clean-room, allocation-conscious, async-first implementation built for mobile (IL2CPP).

This is a **reimplementation of concepts**, not a port. No Master Audio source is referenced or
reproduced. The patterns and workflow are implemented from first principles.

## 2. Goals & Non-Goals

### Goals
- Concept parity with Master Audio core: groups, variations, buses, ducking, event sounds, custom events.
- **Zero GC allocation on the `Play` hot path.** Pooled voices, hashed-id lookups, struct handles.
- **Pure, headless, unit-testable Core** with zero `UnityEngine` dependency.
- IL2CPP-safe runtime (no reflection in playback paths).
- Graceful optionality: works with **UniTask / Odin / Zenject** absent *or* present (version defines).
- A new user can create a group, drop clips, and `AudioKit.Play("Name")` in under 2 minutes.

### Non-Goals (this release)
- Music/Playlist controller ships in **Phase 2** (Core seams designed now, not built).
- DSP/effment graph authoring beyond `AudioMixer` routing.
- Networked/replicated audio.

## 3. Personas
- **Gameplay engineer** — calls `AudioKit.Play(...)` from code; wants a tiny, allocation-free API and a struct handle to control a voice.
- **Technical sound designer** — builds the `AudioDatabase` (groups/buses/variations) in the editor window, auditions clips, runs the validation gate.
- **Mobile tech lead** — cares about GC, voice caps, IL2CPP safety, and that the package degrades gracefully when optional deps are missing.

## 4. Functional Requirements (FR)

| ID | Requirement | Priority |
|----|-------------|----------|
| FR1 | **Sound Groups**: named group of N variations; group-level volume range, pitch range, voice limit, retrigger %, bus assignment, AudioMixerGroup output, default 2D/3D mode. | M |
| FR2 | **Play modes** for variation selection: `Random`, `RandomNoImmediateRepeat`, `RoundRobin`, `Sequential`, `Weighted`, `Oldest`. Selection logic in Core, unit-tested. | M |
| FR3 | **Voice stealing** per group when at limit: `StealOldest`, `Reject`, `StealQuietest`. | M |
| FR4 | **Variations**: per-variation clip (`AudioClip` or `AssetReference`), volume, pitch, weight, randomized start/loop, optional 3D rolloff override. | M |
| FR5 | **Buses**: volume (smooth fade), voice limit across member groups, mute/solo, ducking config. Fades allocation-free. | M |
| FR6 | **Ducking**: trigger groups duck a bus; configurable amount/attack/hold/release; deterministic Core envelope. | M |
| FR7 | **AudioSource pooling**: pre-warmed pool grown to a cap; LRU/oldest reclaim; live-voice tracking per group & per bus. | M |
| FR8 | **Event Sounds component**: fire actions on lifecycle, physics (2D/3D collision/trigger enter+exit, particle collision), and custom events. | M |
| FR9 | **Custom Event system**: zero-alloc pub/sub by hashed id or typed key. | M |
| FR10 | **Runtime API facade**: static convenience layer + `IAudioService` for DI. `Play/PlayAt/PlayFollow/PlayAndForget/StopGroup/FadeBus/MuteBus/FireEvent`; struct `SoundHandle` with `SetVolume/SetPitch/Stop(fade)/IsPlaying`. | M |
| FR11 | **AudioMixer integration**: groups/buses route to `AudioMixerGroup`; optional exposed-parameter volume control. | S |
| FR12 | **Editor — Audio Manager window**: browse/create/edit groups/buses/variations; audition in-edit; drag-drop clips to auto-create groups. Odin when present, IMGUI fallback. | S |
| FR13 | **Editor — Validation gate**: missing clips, duplicate group names, orphaned bus refs, empty groups, oversized clips → one-click report. | M |
| FR14 | **Editor — Debug overlay**: live active voices per group/bus, current bus volumes, voice-limit hits. | S |
| FR15 | **Addressables/AssetReference** lazy clip loading. | S |
| FR16 | **Zenject installer** (optional asmdef, version-defined). | C |
| FR17 | **Music/Playlist controller** (crossfade, sequencing, gapless, per-song volume, bus sync). | **Phase 2** |

Priority: M=Must, S=Should, C=Could.

## 5. Non-Functional Requirements (NFR)
- **NFR1 — Zero GC**: `Play` and per-frame fade/duck tick allocate nothing (verified by `Assert.That(() => …, Is.Not.AllocatingGCMemory())` in PlayMode and Profiler spot-checks).
- **NFR2 — Determinism**: Core selection/voice/duck math is deterministic given an injected RNG and clock; no hidden `UnityEngine.Random`/`Time` in Core.
- **NFR3 — IL2CPP-safe**: no runtime reflection on playback paths; reflection/Odin confined to Editor.
- **NFR4 — Decoupled Core**: `Kobapps.AudioKit.Core` references no Unity assemblies; compiles & tests under plain .NET.
- **NFR5 — Optional deps**: UniTask/Odin/Zenject each guarded by a version define; package compiles in all 8 presence combinations.
- **NFR6 — Cross-platform**: mobile-first (iOS/Android, IL2CPP) but runs on all platforms.

## 6. Definition of Done
- Core compiles and all EditMode tests pass **without Unity** (headless `dotnet test`) and **in** Unity.
- Zero GC alloc in `Play` hot path (asserted).
- Compiles with UniTask/Odin/Zenject absent and present.
- Quick-start: create group → drop clips → `AudioKit.Play("Name")` in < 2 minutes.
- README with quick-start + Master-Audio → AudioKit concept map; sample scene + demo `AudioDatabase`.

## 7. Risks & Mitigations
| Risk | Mitigation |
|------|-----------|
| Hidden allocations from `AudioSource` APIs / boxing | Pool sources, cache `Transform`, avoid LINQ/closures, struct handles, pre-sized arrays. |
| Optional-dependency combinatorial breakage | Version-defined asmdefs + CI matrix conceptually; guard every optional symbol. |
| Core/Unity coupling creep | Core asmdef has `noEngineReferences: true`; enforced by build. |
| AudioMixer exposed-param naming drift | Validation gate flags unmapped params; routing optional. |
