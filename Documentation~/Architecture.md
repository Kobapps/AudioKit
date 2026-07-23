# AudioKit — Architecture (Spine)

> BMAD artifact · Architect: Winston (BMAD) driven by Kobi · Companion to `PRD.md`

## 1. Invariants (the spine everything is built from)

1. **Core is pure.** `Kobapps.AudioKit.Core` has `noEngineReferences: true`. All decision logic
   (variation selection, voice/polyphony accounting, bus routing math, fade & duck envelopes)
   lives here as plain C# operating on **value types and indices**, never on `UnityEngine.Object`.
2. **Time and randomness are injected.** Core never reads `Time`/`UnityEngine.Random`. It takes a
   `float deltaTime` per tick and an `IRandom` (xorshift) seed. → deterministic, headless-testable.
3. **Ids are hashed.** Every group/bus/event is addressed by a 32-bit `AudioId` (FNV-1a of the name).
   Runtime lookups use `Dictionary<AudioId,int>` → index into flat arrays. No string compares on hot paths.
4. **The runtime is a thin adapter.** The Unity layer owns `AudioSource`s, the pool, and the frame
   tick; it forwards numbers into Core and applies Core's outputs (which source plays, at what
   volume/pitch, which bus gain) back onto `AudioSource`s.
5. **Handles are structs.** `SoundHandle` is a `readonly struct { int slot; int generation; }`.
   No reference to a voice escapes; stale handles are detected by generation mismatch. Zero GC.
6. **Fades are ticked, not awaited.** Bus/voice fades and ducking advance via Core envelopes ticked
   once per frame in `Update()`. No coroutine/UniTask allocation per fade. UniTask only wraps
   *optional awaitable* convenience (`PlayAndForget`, `await FadeBus(...)`).
7. **Optional deps never gate core function.** UniTask/Odin/Zenject are additive via version defines.
   Absent → the base synchronous API and IMGUI editor still work fully.

## 2. Assembly layout

```
Kobapps.AudioKit.Core          (Runtime/Core)      pure C#, noEngineReferences, no tests deps
Kobapps.AudioKit.Runtime       (Runtime)           refs Core + UnityEngine; versionDefines: UniTask
Kobapps.AudioKit.Editor        (Editor)            refs Core+Runtime; Editor only; #if ODIN_INSPECTOR
Kobapps.AudioKit.Zenject       (Runtime/Zenject)   refs Runtime; defineConstraints: AUDIOKIT_ZENJECT
Kobapps.AudioKit.Tests.EditMode (Tests/EditMode)   refs Core(+Runtime); NUnit; testAssemblies
Kobapps.AudioKit.Tests.PlayMode (Tests/PlayMode)   refs Runtime; NUnit; testAssemblies
```

Version defines (declared on the Runtime asmdef):
- `AUDIOKIT_UNITASK`  ← `com.cysharp.unitask` ≥ 2.5.0
- `AUDIOKIT_ZENJECT`  ← `com.svermeulen.extenject` ≥ 9.0.0 (constraint on the Zenject asmdef)
- `ODIN_INSPECTOR` is defined by Odin itself; Editor code keys off it directly.
- `AUDIOKIT_ADDRESSABLES` ← `com.unity.addressables` (clip loading provider).

## 3. Core domain model (headless)

- **`AudioId`** — `readonly struct` wrapping a 32-bit FNV-1a hash + (editor-only) source string.
- **`PlayMode`** — enum: Random, RandomNoImmediateRepeat, RoundRobin, Sequential, Weighted, Oldest.
- **`VoiceStealPolicy`** — enum: StealOldest, Reject, StealQuietest.
- **`VariationData`** — `struct { float volume; float pitch; float weight; … }` (clip lives in Unity layer, referenced by index).
- **`GroupData`** — `struct` of group settings (volume/pitch range, voiceLimit, retrigger%, busIndex, playMode, stealPolicy, mode2D3D).
- **`BusData`** — `struct` (volume, voiceLimit, muted, soloed, duck config).
- **`IRandom`** — deterministic RNG seam (`XorShiftRandom`).
- **`VariationSelector`** — given `GroupData`, variation weights, `SelectionState ref`, `IRandom` → returns variation index. Implements all 6 modes. **Unit-tested.**
- **`VoiceTable`** — fixed-capacity accounting of live voices with slot+generation; per-group & per-bus counts; implements stealing policies; returns a `StealDecision`. **Unit-tested.**
- **`BusGraph`** — aggregates bus volume × mute/solo × duck-gain → effective linear gain per bus. Solo-aware. **Unit-tested.**
- **`FadeEnvelope`** — `struct` { from, to, duration, elapsed, curve } → `Evaluate()`, `Tick(dt)`, `IsDone`. Linear/EaseInOut/EqualPower curves. **Unit-tested.**
- **`DuckEnvelope`** — `struct` ADHR (attack/hold/release) gain in [0,1] given active trigger count. Deterministic. **Unit-tested.**
- **`AudioKitCoreEngine`** — orchestrates the above over flat arrays; the Unity layer calls into it. Pure.

## 4. Runtime layer (Unity adapter)

- **ScriptableObjects** (`AudioDatabaseAsset`, `SoundGroupAsset`, `BusAsset`, `VariationAsset`) are the
  design-time source of truth. On load, the runtime **bakes** them into flat Core arrays + `AudioId` maps.
- **`AudioSourcePool`** — pre-warms `cap` pooled `AudioSource` components on a hidden root; grows on demand
  to a hard cap; LRU reclaim via the Core `VoiceTable` steal decision.
- **`VoiceManager`** — binds a Core voice slot to a pooled `AudioSource`; applies selected variation +
  bus gain + 2D/3D; advances fades each tick; recycles on completion.
- **`AudioKitRuntime`** (MonoBehaviour, `[DefaultExecutionOrder]`, `DontDestroyOnLoad`) — owns the engine,
  pool, voice manager; ticks fades/ducking in `Update`/`LateUpdate`. Auto-bootstraps via `RuntimeInitializeOnLoad`.
- **`SoundHandle`** — struct control surface; methods delegate to `VoiceManager` by slot+generation.
- **`AudioKit`** (static facade) + **`IAudioService`/`AudioService`** (instance, DI-friendly). Static delegates to the active service.
- **Clip loading**: `IClipProvider` with `DirectClipProvider` (AudioClip refs) and, when
  `AUDIOKIT_ADDRESSABLES`, `AddressablesClipProvider` (AssetReference, async warm).

## 5. Events layer
- **`CustomEventRegistry`** — `Dictionary<AudioId, InvocationList>` with array-backed subscriber lists;
  zero-alloc `Fire(AudioId)`; supports typed payload via `CustomEvent<T>`.
- **`AudioKitEventSounds`** (MonoBehaviour) — serialized list of `(Trigger, ActionList)`. Triggers:
  lifecycle (Awake/Start/OnEnable/OnDisable/OnDestroy), physics (collision/trigger enter+exit 2D & 3D,
  particle collision), custom-event subscription. Actions: PlayGroup, StopGroup, FadeBus, Pause/Unpause,
  SetBusVolume, FireCustomEvent.

## 6. Music / playlists (Phase 2 — implemented in 0.2.0)
- Core `PlaylistSequencer` (Sorted/Random/RandomNoRepeat) picks track order deterministically.
- Runtime `MusicPlayer` crossfades between two dedicated AudioSources on a music bus using two
  `FadeEnvelope`s (equal-power); zero crossfade = hard cut; `gapless` uses `PlayScheduled` for a
  sample-accurate handoff. `BusData.IsMusicBus` selects the default bus. Per-frame volume is
  `trackVolume × fade × busGain`, so bus volume/mute/solo/duck apply to music like any group.

## 7. Test strategy
- **Headless** (`dotnet test`, scratchpad csproj over the same Core sources): selection per mode, weighted
  distribution (chi-square tolerance), no-immediate-repeat guarantee, voice limiting + 3 steal policies,
  ducking ADHR envelope, bus aggregation w/ solo & mute. *Single source of truth — same files Unity compiles.*
- **EditMode** (Unity NUnit): same Core suite runs in-editor + SO baking round-trip.
- **PlayMode**: pool grow/reclaim, play→complete handle lifecycle, fade correctness over frames,
  EventSounds trigger wiring, **zero-alloc `Play` assertion**.
