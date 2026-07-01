# AudioKit — Epics, Stories & Sprint Plan

> BMAD artifact · PM: John (BMAD) driven by Kobi · Companion to `PRD.md`, `Architecture.md`

## Epic E0 — Package Skeleton (M0)
- **S0.1** UPM `package.json` (`com.kobi.audiokit`), `README`, `CHANGELOG`, `LICENSE`. *(FR deliverable 1)*
- **S0.2** asmdefs: Core (noEngineReferences), Runtime (+UniTask versionDefine), Editor, Zenject (constraint), Tests.EditMode, Tests.PlayMode.
- **S0.3** UniTask dependency wired via OpenUPM; version defines validated.
- **DoD:** project compiles empty assemblies; asmdef graph correct.

## Epic E1 — Core Domain (M1)  *(FR2, FR3, FR5, FR6 logic)*
- **S1.1** `AudioId` (FNV-1a) + `IRandom`/`XorShiftRandom`.
- **S1.2** `VariationSelector` — all 6 play modes. **Tests:** determinism, weighted χ², no-immediate-repeat.
- **S1.3** `VoiceTable` — accounting + 3 steal policies. **Tests:** limit enforcement, steal selection.
- **S1.4** `BusGraph` — volume × mute/solo × duck → effective gain. **Tests:** solo precedence, mute, aggregation.
- **S1.5** `FadeEnvelope` + curves. **Tests:** endpoints, monotonicity, equal-power midpoint.
- **S1.6** `DuckEnvelope` ADHR. **Tests:** attack ramp, hold, release, multi-trigger hold.
- **S1.7** `AudioKitCoreEngine` orchestration over flat arrays.
- **DoD:** `dotnet test` green headless; zero Unity refs.

## Epic E2 — Runtime (M2)  *(FR1, FR4, FR7, FR10, FR11)*
- **S2.1** ScriptableObjects (`AudioDatabaseAsset`, `SoundGroupAsset`, `BusAsset`, `VariationAsset`) + bake to Core arrays.
- **S2.2** `AudioSourcePool` (prewarm/grow/LRU reclaim).
- **S2.3** `VoiceManager` (bind slot↔source, apply selection+bus gain+2D/3D, tick fades, recycle).
- **S2.4** `SoundHandle` struct + `AudioKitRuntime` driver (bootstrap, Update tick).
- **S2.5** `AudioKit` facade + `IAudioService`/`AudioService`; mixer routing.
- **DoD:** PlayMode smoke tests; zero-alloc Play assertion.

## Epic E3 — Events (M3)  *(FR8, FR9)*
- **S3.1** `CustomEventRegistry` zero-alloc pub/sub (hashed id + typed). **Tests:** dispatch, unsubscribe-safety.
- **S3.2** `AudioKitEventSounds` triggers + action list. **PlayMode:** lifecycle + physics wiring.

## Epic E4 — Editor (M4)  *(FR12, FR13, FR14)*
- **S4.1** `AudioKitValidator` + validation report window. *(FR13 — correctness gate, Must)*
- **S4.2** Audio Manager window (IMGUI fallback + `#if ODIN_INSPECTOR`): browse/create/edit, audition, drag-drop.
- **S4.3** Runtime debug overlay (voices per group/bus, bus volumes, limit hits).

## Epic E5 — Polish (M5)  *(FR15, FR16, deliverables 6–7)*
- **S5.1** `IClipProvider` + Addressables provider (`AUDIOKIT_ADDRESSABLES`).
- **S5.2** Zenject installer asmdef.
- **S5.3** Sample scene + demo `AudioDatabase` asset.
- **S5.4** README quick-start + Master-Audio→AudioKit concept map; CHANGELOG.

## Phase 2 — Music/Playlist
- **S6.x** `PlaylistController` over the music-bus seam (crossfade, sequencing, gapless).

---

## Sprint Plan (execution order)
| Sprint | Stories | Exit criteria |
|--------|---------|---------------|
| **Sprint 1** | E0 + E1 | Headless Core tests green; asmdefs compile. |
| **Sprint 2** | E2 | Runtime compiles in Unity; PlayMode smoke + zero-alloc Play green. |
| **Sprint 3** | E3 | Events compile; pub/sub + EventSounds wiring tests green. |
| **Sprint 4** | E4 | Validation gate usable; window opens (IMGUI); overlay shows voices. |
| **Sprint 5** | E5 | Sample scene plays; README done; package importable clean. |

## Traceability (FR → Story)
FR1→S2.1 · FR2→S1.2 · FR3→S1.3 · FR4→S2.1 · FR5→S1.4/S2.5 · FR6→S1.6 · FR7→S2.2 ·
FR8→S3.2 · FR9→S3.1 · FR10→S2.4/S2.5 · FR11→S2.5 · FR12→S4.2 · FR13→S4.1 · FR14→S4.3 ·
FR15→S5.1 · FR16→S5.2 · FR17→Phase 2.
