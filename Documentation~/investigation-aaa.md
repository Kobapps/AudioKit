# AudioKit — AAA Investigation & Improvement Plan

> BMAD investigation · Investigator driven by Kobi · 2026-07-01 · Baseline v0.3.0

## Method
Reviewed the shipped package (Core + Runtime + Editor + Examples), the test suites, and profiled the
hot paths by inspection. Graded against AAA Unity audio middleware (Master Audio, FMOD/Wwise
integrations). Findings below are ranked by impact/effort.

## Strengths (keep)
- Pure headless Core, deterministic, 63 unit tests. Zero-alloc `Play`. Clean asmdef split with
  optional deps. Solid feature parity (groups, buses, ducking, playlists, events, dynamic groups).

## Findings

### Performance
| # | Finding | Impact | Fix |
|---|---------|--------|-----|
| P1 | `VoiceManager.Tick` scans **all** voice slots every frame, active or not. | Med | Iterate an active-slot list (O(1) swap-remove on acquire/release). |
| P2 | `AudioSource.volume` is written every frame for every voice — a native interop call even when unchanged. | Med | Dirty-check: only write when it moves past an epsilon. |
| P3 | `BusGraph.AnySoloed()` rescans all buses on **every** `EffectiveGain` call (per voice, per frame). | Low-Med | Cache `_anySolo`; recompute only when a solo flag changes. |
| P4 | 3D one-shots always allocate a voice even when the emitter is far beyond audible range. | Med | Optional distance **virtualization**: skip the play (return invalid) when beyond a cull radius from the listener. |

### Features (AAA gaps)
| # | Gap | Value | Plan |
|---|-----|-------|------|
| F1 | No **fade-in on play** (only fade-out on stop). | High | `Play(..., fadeInSeconds)`; drives the existing per-voice fade envelope up from 0. |
| F2 | No **per-group runtime volume / mute** (only bus + master). | High | `SetGroupVolume`, `MuteGroup` in Core (per-group gain) applied in the voice volume product. |
| F3 | `PauseAll` has no fade. | Low | `PauseAll(fade)` / `ResumeAll(fade)` via bus/voice fades. |
| F4 | No live **diagnostics API** for tooling (active voice list, pool usage). | Med | `AudioService.Diagnostics` snapshot used by the editor meters. |

### Editor UX (top-tier gaps)
| # | Gap | Plan |
|---|-----|------|
| E1 | No search/filter across groups/buses. | Toolbar search field filtering the lists. |
| E2 | No live feedback in play mode (which groups are playing, voice usage). | Per-group **live voice meters** + pool usage bar while playing. |
| E3 | No reorder / duplicate of groups/buses. | ▲▼ reorder + duplicate buttons. |
| E4 | Flat, unstyled IMGUI. | Master-detail-ish grouping, colored bus headers, count badges, collapse/expand-all, sticky toolbar. |

## Plan (this pass)
1. **Perf:** P3 (solo cache) → P1 (active-voice iteration) → P2 (volume dirty-check) → P4 (virtualization, opt-in).
2. **Features:** F1 fade-in, F2 group volume/mute, F3 pause-with-fade, F4 diagnostics.
3. **Editor:** E1 search, E2 live meters + pool bar, E3 reorder/duplicate, E4 styling.
4. Every step: keep headless Core green, re-run EditMode/PlayMode in Unity, screenshot the editor.

## Non-goals (out of scope this pass)
Occlusion/low-pass DSP, AudioMixer snapshot transitions, full UI-Toolkit rewrite (kept IMGUI for
verifiability), waveform rendering.
