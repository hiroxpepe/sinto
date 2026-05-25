# Quyno / Sinto — Development Plan v1.0

**Date:** 2026-05-25  
**Version:** 1.4 (Gemini Round 8: CS0420 fix + Span<float> API + Time travel prevention)  
**Status:** Active  
**Based on:** quyno_sinto_proposal_v050.md  
**Language:** English (implementation reference)

---

## Repository Structure Decision

### Two separate repositories + existing portfolio

```
koleco/    — Portfolio site (existing, untouched)
sinto/     — Pure C# DSP synth engine library
quyno/     — Music sequencer + Unity integration layer
```

### Rationale

The dependency graph is strictly one-directional:

```
quyno  ──depends on──►  sinto
sinto  ──────────────►  (nothing — standalone library)
```

Sinto operates fully without Quyno:
- Standalone MIDI keyboard app (no sequencer needed)
- Per-GameObject SFX emitter in games
- Third-party sequencer integration

Keeping them separate means:
- Sinto can be published as an independent NuGet package (MIT)
- Sinto release cadence is not blocked by Quyno progress
- Breaking changes in Sinto surface immediately via quyno's failing tests
- Contributors who only need the synth engine don't pull in sequencer code

### sinto/ internal structure

```
sinto/
  src/
    Sinto.Core/          ← Pure C# .NET 8 class library (zero Unity dependency)
      Audio/
        AudioRingBuffer.cs
        SintoLUT.cs
        DenormalGuard.cs
      Synth/
        SintoEngine.cs
        Voice.cs
        VoiceManager.cs
        Oscillator.cs
        Envelope.cs
        LFO.cs
      Filter/
        MoogLadder.cs
        CEM3320.cs
        IR3109.cs
      Effects/
        BBDChorus.cs
        Freeverb.cs
        RetroFilter.cs
      Preset/
        SintoPreset.cs
        PresetLoader.cs
    Sinto.Unity/         ← Thin Unity wrapper (MonoBehaviour bridge only)
      SintoBgmPlayer.cs
      SintoSfxEmitter.cs
  tests/
    Sinto.Tests/         ← xUnit — headless, no Unity required
  benchmarks/
    Sinto.Benchmarks/    ← BenchmarkDotNet — LUT vs polynomial, etc.
```

### quyno/ internal structure

```
quyno/
  src/
    Quyno.Core/          ← Pure C# .NET 8 (references Sinto.Core via NuGet/submodule)
      Data/
        SongLoader.cs
        PatternLoader.cs
        PhraseLoader.cs
        PlayerLoader.cs
      Engine/
        Generator.cs
        Mixer.cs
        State.cs
      Theory/
        ChordTheory.cs     ← 3-chord modal theory
    Quyno.Unity/           ← Unity integration
      QuynoPlayer.cs
  tests/
    Quyno.Tests/
  data/
    samples/               ← Example JSON song data
```

---

## Phase 1 — Foundation (3 months: 2026-06 to 2026-08)

**Goal:** A silent but correctly architected audio engine that can play a single sine wave on Android arm64 without GC spikes, denormals, or threading crashes.

---

### Task 1.1 — Repository Initialization

**Deliverable:** Both repos exist with MIT license, CI skeleton, and README.

- [ ] Create `sinto` GitHub repository
- [ ] Create `quyno` GitHub repository
- [ ] Add `LICENSE` (MIT) to both repos
- [ ] Add `.gitignore` for C# / Unity
- [ ] Add `README.md` with project description and "Inspired by Yamaha QY10/20/70"
- [ ] Set up GitHub Actions: `dotnet build` + `dotnet test` on push
- [ ] Add `ARCHITECTURE.md` linking to proposal v0.5.0

**Acceptance criteria:**
- `dotnet build` passes on both repos from a fresh clone
- MIT license file is present and correct

---

### Task 1.2 — AudioRingBuffer\<ControlEvent\> (FIRST CODE TO WRITE)

**Deliverable:** Lock-free SPSC ring buffer. The foundation all threading safety rests on.

**File:** `sinto/src/Sinto.Core/Audio/AudioRingBuffer.cs`

```csharp
// Target API — with False Sharing prevention
// IMPORTANT: [LayoutKind.Explicit] cannot be applied to generic classes in .NET.
// Doing so causes TypeLoadException at startup (GC cannot track T[] with Explicit layout).
// Solution: LayoutKind.Sequential with manual long-field padding (8 bytes each).
// 7 longs = 56 bytes. Together with the int (4 bytes) = 60 bytes, next field on new line.

[StructLayout(LayoutKind.Sequential)]
public sealed class AudioRingBuffer<T> where T : struct {
    // Cache line 0: shared read-only data (buffer ref + mask, never written after ctor)
    readonly T[]  _buffer;
    readonly int  _mask;

    // Padding: push _head to the next cache line (offset ~64)
    long _pad1, _pad2, _pad3, _pad4, _pad5, _pad6, _pad7; // 7 × 8 = 56 bytes

    // Cache line 1: _head — written ONLY by audio thread
    int _head;

    // Padding: push _tail to the next cache line
    long _pad8, _pad9, _pad10, _pad11, _pad12, _pad13, _pad14; // 7 × 8 = 56 bytes

    // Cache line 2: _tail — written ONLY by main thread
    int _tail;

    // Guard: capacity MUST be a power of 2
    // Bitmask optimization (n & mask) is only valid when capacity = 2^k exactly.
    // e.g. capacity=1000 → mask=999 → 1000 & 999 = 960 ≠ 0 → buffer wraps incorrectly.
    public AudioRingBuffer(int capacityPow2 = 1024) {
        if (capacityPow2 <= 0 || (capacityPow2 & (capacityPow2 - 1)) != 0)
            throw new ArgumentException(
                $"Capacity must be a positive power of 2 (e.g. 512, 1024, 2048). Got: {capacityPow2}");
        _buffer = new T[capacityPow2];
        _mask   = capacityPow2 - 1;
    }
    // TryEnqueue / TryDequeue unchanged — Volatile.Read/Write as before
}

public enum ControlEventKind : byte {
    NoteOn, NoteOff, Pause, Resume, SetVoiceLimit,
    SwapPreset  // LoadPreset removed — use double-buffered Interlocked.Exchange instead
}
public readonly struct ControlEvent {
    public readonly ControlEventKind Kind;
    // OffsetFrames: sample position within the current audio buffer at which to fire.
    // Without this, ALL events fire at buffer[0] regardless of when they were queued,
    // causing up to 46ms jitter (2048 samples @ 44100Hz) — rhythm becomes unrecognizable.
    // Quyno tick engine calculates this offset when it detects a note trigger:
    //   offsetFrames = (int)((tickTimeInSeconds - bufferStartTime) * sampleRate)
    public readonly ushort OffsetFrames; // 0 = fire at buffer start; max = buffer length - 1
    public readonly int    IntParam;     // MidiNote, VoiceLimit
    public readonly float  FloatParam;  // Velocity
    public readonly int    TrackId;
    public readonly int    Priority;    // Voice Stealing priority
}

// Sub-buffering render loop — the ONLY correct way to achieve sample-accurate scheduling
// Uses Span<float> for zero-cost compatibility with both Unity and Oboe native callbacks.
//
// void RenderBuffer(Span<float> buffer) {
//     int pos = 0;
//     while (_eventQueue.TryDequeue(out var ev)) {
//         // TIME TRAVEL PREVENTION:
//         // Events are expected in ascending OffsetFrames order, but if the main thread
//         // sends an event with OffsetFrames < pos (out-of-order or same-frame burst),
//         // offset - pos would be NEGATIVE → negative-length render → index crash.
//         // Math.Max clamps to current position; Math.Min clamps to buffer end.
//         // Note: Math.Max/Min on int — no NaN issue, no branch misprediction concern.
//         int offset = Math.Max(ev.OffsetFrames, pos);  // prevent time travel (pos ≤ offset)
//         offset     = Math.Min(offset, buffer.Length - 1); // prevent buffer overrun
//
//         if (offset > pos)
//             _synth.RenderSamples(buffer.Slice(pos, offset - pos)); // render up to event
//         ApplyEvent(ev);                                              // fire at exact sample
//         pos = offset;
//     }
//     if (pos < buffer.Length)
//         _synth.RenderSamples(buffer.Slice(pos));                    // render remainder
// }
```

- [ ] Implement `AudioRingBuffer<T>` with `LayoutKind.Sequential` + manual `long` padding fields
- [ ] **Do NOT use `LayoutKind.Explicit` on generic class** — causes `TypeLoadException` at startup
  - Generic class + Explicit layout: GC cannot track `T[]` reference → runtime crash
  - Fix: 7 × `long` padding fields between `_mask` and `_head`, and between `_head` and `_tail`
- [ ] **Power-of-2 guard in constructor**: throw `ArgumentException` if `capacityPow2` is not a power of 2
  - Bitmask `(n & mask)` is only correct when capacity = 2^k; e.g. `1000 & 999 = 960 ≠ 0`
- [ ] **False Sharing prevention**: `_head` and `_tail` separated by >= 56 bytes of padding
- [ ] `TryEnqueue(in T item)` — main thread only, uses `Volatile.Write`
- [ ] `TryDequeue(out T item)` — audio thread only, uses `Volatile.Read`
- [ ] Verify: no `new`, no `lock`, no `Monitor` inside either method
- [ ] Add `IsFull` and `Count` properties (approximate, for diagnostics only)
- [ ] Write unit test: enqueue 1023 items, verify 1024th is rejected (full)
- [ ] Write unit test: enqueue/dequeue 10,000 items in sequence, verify FIFO order
- [ ] Write concurrency test: producer thread + consumer thread, 1,000,000 ops, zero loss
- [ ] `ControlEvent` includes `ushort OffsetFrames` — verified field exists
- [ ] Sub-buffering render loop implemented: events fire at `buffer[OffsetFrames]`, not always `buffer[0]`
- [ ] Write jitter test: enqueue NoteOn with OffsetFrames=512 into 1024-sample buffer
      → verify `NoteOn` fires at sample 512, not sample 0
- [ ] **Time travel prevention test**: enqueue OffsetFrames=500 then OffsetFrames=100
      → second event is clamped to pos=500 (no negative-length render, no crash)
- [ ] Buffer overrun test: enqueue OffsetFrames=9999 into 1024-sample buffer
      → clamped to 1023, no IndexOutOfRangeException

**Acceptance criteria:**
- All tests pass
- No allocations inside `TryEnqueue` / `TryDequeue` (verified with `dotnet-trace` or BenchmarkDotNet `MemoryDiagnoser`)
- `ConcurrentQueue<T>` appears nowhere in the codebase
- `[LayoutKind.Explicit]` does NOT appear on any generic type (TypeLoadException prevention)
- `new AudioRingBuffer<ControlEvent>(1000)` throws `ArgumentException` (power-of-2 guard)
- `new AudioRingBuffer<ControlEvent>(512)` constructs without exception
- Concurrency test: 1,000,000 enqueue/dequeue ops across two threads → zero data loss, zero exceptions

---

### Task 1.3 — Sin / Tanh Benchmark (LUT vs Polynomial)

**Deliverable:** A measured decision on which Sin/Tanh implementation to use for the rest of the project.

**File:** `sinto/benchmarks/Sinto.Benchmarks/SinBenchmark.cs`

- [ ] Implement `SinLUT` — `float[4096]`, precomputed at static init
- [ ] Implement `SinPoly` — parabolic approximation (Myklebust method, register-only)
- [ ] Implement `TanhLUT` — `float[4096]`, range -3 to +3
- [ ] Implement `TanhPoly` — rational approximation `x*(27+x*x)/(27+9*x*x)`
- [ ] Run BenchmarkDotNet on **Android arm64 device** (not PC, not emulator)
- [ ] Measure: throughput (ns/op), L1/L2 cache miss rate if available
- [ ] Simulate 32-voice scenario: 32 parallel random-phase calls in one benchmark loop
- [ ] Document result in `sinto/benchmarks/RESULTS.md`
- [ ] Adopt the faster implementation as `SintoMath.SinFast()` and `SintoMath.TanhFast()`

**Acceptance criteria:**
- Decision is documented with actual device numbers
- `SintoMath.SinFast()` and `SintoMath.TanhFast()` are the only calls used in hot paths
- `Math.Sin` and `Math.Pow` appear nowhere in `Sinto.Core/Synth/` or `Sinto.Core/Filter/`

---

### Task 1.4 — DenormalGuard

**Deliverable:** A compile-time-zero-cost mechanism to inject DC offset into all IIR feedback loops.

**File:** `sinto/src/Sinto.Core/Audio/DenormalGuard.cs`

```csharp
// CRITICAL: DenormalGuard must NOT use static state.
// Unity's FMOD calls OnAudioFilterRead from multiple worker threads simultaneously
// when multiple AudioSources (BGM + SFX emitters) are active.
// A static _sign field causes a data race → sign becomes indeterminate → filter diverges.
// DSP iron rule: processing state is NEVER static.
//
// Solution: derive the alternating offset from the sample index (no stored state needed).
// Each filter/voice tracks its own sample counter; parity gives sign for free.

public static class DenormalGuard {
    private const float _magnitude = 1e-15f;

    // Pass the current sample index (per-voice or per-filter counter).
    // Parity of sampleIndex gives alternating sign — zero additional state.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Protect(float x, long sampleIndex)
        => x + ((sampleIndex & 1L) == 0L ? _magnitude : -_magnitude);
}
```

Each `Voice` struct and each filter state struct maintains its own `long _sampleCount`  
incremented once per sample. This eliminates shared state entirely.

> **Why sample index parity instead of stored sign?**  
> Stored `_sign` requires either a static field (data race across threads) or an  
> instance field per filter (adds 4 bytes per state struct). Sample index is already  
> tracked per-voice for the phase accumulator — reusing its parity costs nothing.

- [ ] Implement `DenormalGuard.Protect(float x, long sampleIndex)` — **no static state**
  - Sign derived from `sampleIndex & 1L` — parity of per-voice sample counter
  - **`static` fields are FORBIDDEN in DenormalGuard** — Unity FMOD runs multiple threads
- [ ] Annotate with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- [ ] Write unit test: `Protect(0.0f, 0)` and `Protect(0.0f, 1)` return opposite signs
- [ ] Write unit test: sum of `Protect(0.0f, i)` for i=0..999 is within `[-1e-12, +1e-12]`
- [ ] Write thread-safety test: 8 threads call `Protect()` concurrently → no data race (verified with Helgrind / .NET concurrency analyzer)
- [ ] Confirm `_sampleCount` is a per-voice / per-filter field, never `static`
- [ ] Audit checklist: every IIR state assignment (`s1 +=`, `s2 +=`, delay buffer write) ends with `DenormalGuard.Protect()`
- [ ] Create PR checklist item: "Does this IIR loop call DenormalGuard.Protect()?"

**Acceptance criteria:**
- `DenormalGuard.Protect()` is called on every feedback state variable in Filter/ and Effects/
- No raw IIR state update exists without it
- DC cancellation test passes: sum of 1000 `Protect(0.0f)` calls is within `[-1e-12, +1e-12]`

---

### Task 1.5 — Sinto Core: 5 Waveforms + ADSR

**Deliverable:** A single voice that can play all 5 waveforms with ADSR envelope, GC-zero, denormal-safe.

**Files:** `Oscillator.cs`, `Envelope.cs`, `Voice.cs`, `SintoEngine.cs`

- [ ] Implement `Oscillator` as `readonly struct` (stack-only, no heap)
  - [ ] Sine (via `SintoMath.SinFast`)
  - [ ] Sawtooth (phase accumulator, direct)
  - [ ] Triangle (phase accumulator, abs + scale)
  - [ ] Square (phase accumulator, sign)
  - [ ] Noise (LCG PRNG, register-only)
  - [ ] `InterpMode` enum: `Linear` | `NearestNeighbor`
  - [ ] `NearestNeighbor` mode: no interpolation on phase readout (N64 aliasing)
- [ ] Implement `Envelope` as `readonly struct`
  - [ ] ADSR state machine: Idle → Attack → Decay → Sustain → Release → Done
  - [ ] `Evaluate(double noteTime, double noteOffTime)` — pure function, no side effects
- [ ] Implement `Voice` struct: OSC × 2 + AmpEnv + FilterEnv + state
- [ ] Implement `SintoEngine` (class, owns voice array): `NoteOn`, `NoteOff`, `RenderSamples`
- [ ] **`RenderSamples` signature: `void RenderSamples(Span<float> buffer)`** — NOT `float[]`
  - `float[]` API forces copy or Marshal when bridging Oboe C++ raw pointer → catastrophic overhead
  - `Span<float>` is zero-cost from BOTH call sites:
    - Unity `OnAudioFilterRead(float[] data)` → `data.AsSpan()` (zero allocation)
    - Oboe C++ callback `(float* ptr, int frames)` → `new Span<float>(ptr, frames * channels)` (zero allocation)
  - Same single API serves both Unity and standalone — no platform-specific overloads needed
- [ ] Verify `Note` is `readonly struct` (zero heap allocation per note event)
- [ ] Verify `RenderSamples(Span<float>)` allocates zero bytes (BenchmarkDotNet MemoryDiagnoser)

**Acceptance criteria:**
- `dotnet-trace` shows zero Gen0 GC collections during 60-second continuous playback test
- Single voice renders 44100 samples in < 1ms on target Android device
- `DenormalGuard.Protect()` called on all feedback paths
- `RenderSamples` takes `Span<float>` — verified `float[]` overload does NOT exist
- `data.AsSpan()` compiles without allocation from Unity `OnAudioFilterRead`

---

### Task 1.6 — Pause / Resume Control Events

**Deliverable:** Audio thread correctly silences output when Pause event is received, resumes without tick accumulation.

- [ ] Add `ControlEventKind.Pause` and `ControlEventKind.Resume` to enum
- [ ] Audio thread: on Pause → set `_paused = true`, `Array.Clear(buffer)`, skip `RenderSamples`
- [ ] Audio thread: on Resume → set `_paused = false`, resume tick from current DSP time
- [ ] Main thread helper: `void SendPause()` and `void SendResume()` — enqueue via ring buffer
- [ ] Write test: send Pause → verify output buffer is all zeros
- [ ] Write test: send Pause → Resume → verify tick counter has NOT advanced during pause
- [ ] Unity integration: `SintoBgmPlayer.OnApplicationPause(bool paused)` calls `SendPause/Resume`

**Acceptance criteria:**
- No tick accumulation after resume (no "fast-forward" glitch)
- No `lock` used anywhere in pause/resume path

---

### Task 1.7 — arm64-v8a + Oboe Integration

**Deliverable:** A single sine wave plays on a physical Android arm64 device via Oboe with < 20ms latency.

- [ ] Add `liboboe.so` (arm64-v8a + armeabi-v7a) to Unity project
- [ ] Implement `OboeOutputCallback` in C# interop or Unity's built-in audio path
- [ ] Verify build succeeds with `arm64-v8a` as the only ABI target
- [ ] Measure round-trip latency on 3 different Android devices
- [ ] Add buffer size auto-adjustment fallback (double buffer size on underrun detection)
- [ ] Document per-device latency results in `quyno/docs/DEVICE_COMPATIBILITY.md`

**Acceptance criteria:**
- App installs and runs on Android 8+ arm64 device
- No FluidSynth `.so` files present in build output
- Single sine wave plays without artifacts for 5 minutes continuous

---

### Task 1.8 — GC Zero Audit

**Deliverable:** A `GC_ZERO_AUDIT.md` checklist and passing automated test that proves zero allocations in the audio render path.

- [ ] Run `dotnet-trace` with GC events on audio render loop for 60 seconds
- [ ] Verify zero Gen0 collections during playback
- [ ] Replace any remaining LINQ in Generator hot path with `for` loops
- [ ] Replace any remaining `List<T>` construction with `Clear()` + reuse
- [ ] Confirm `MemoryStream` instances are reused (not `new`-ed per tick)
- [ ] Confirm `AllSpan` result is cached with dirty flag
- [ ] Set `GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency` on audio thread start
- [ ] Document: add `// GC_ZERO: no allocation below this line` comment at audio callback entry
- [ ] Write automated allocation test using BenchmarkDotNet `MemoryDiagnoser`: `AllocatedBytes == 0`

**Acceptance criteria:**
- `AllocatedBytes = 0 B` in BenchmarkDotNet output for `RenderSamples(buffer)`
- No `new` keyword inside any method in the audio render call chain

---

### Task 1.9 — Dynamic Voice Scaling Scaffold

**Deliverable:** The infrastructure for dynamic voice scaling exists, even if max voices is still fixed at 32.

- [ ] Implement `VoiceScaler` class with `int[] TIERS = { 32, 24, 16 }`
- [ ] Implement processing time measurement via `Stopwatch.GetTimestamp()` per callback
- [ ] Implement tier-down logic: usage > 70% of buffer duration → lower tier
- [ ] Implement tier-up logic: usage < 40% for 5+ seconds → raise tier
- [ ] Expose `CurrentMaxVoices` property (read by main thread for UI/debug)
- [ ] Wire to `SintoEngine.SetMaxVoices(int n)`
- [ ] Implement `_cooldownRemaining` counter — skip tier evaluation while > 0
- [ ] Set `_cooldownRemaining = COOLDOWN_CALLBACKS` (default 64) after every tier change
- [ ] Write test: simulate 80% CPU load for 10 callbacks → verify tier decreases exactly once
- [ ] Write test: simulate 80% CPU load continuously → verify tier does NOT decrease twice within COOLDOWN_CALLBACKS callbacks (no hunting)
- [ ] Write test: simulate 30% CPU load for 300 callbacks → verify tier increases
- [ ] Write test: simulate 90% load → tier down → 90% load immediately after → tier stays (cooldown active)

**Acceptance criteria:**
- `CurrentMaxVoices` changes dynamically under simulated load
- No allocation inside `VoiceScaler.Update()` (called per audio callback)

---

## Phase 2 — Synth Feature Expansion (2 months: 2026-09 to 2026-10)

**Goal:** A fully playable 24-32 voice polyphonic synthesizer with professional-quality filters, MIDI keyboard input, and zero click noise on voice stealing.

---

### Task 2.1 — Moog Ladder Filter (Huovilainen Model)

**File:** `sinto/src/Sinto.Core/Filter/MoogLadder.cs`

- [ ] Implement Huovilainen improved Moog ladder (4-pole LPF)
- [ ] Clamp `resonance` to `[0, 3.99]` using `MathF.Min(MathF.Max(r, 0f), 3.99f)`
- [ ] Clamp `cutoff` to `[0.001, 0.999]` using `MathF.Min(MathF.Max(c, 0.001f), 0.999f)`
- [ ] **`Math.Clamp` is FORBIDDEN in hot paths** — contains NaN branch that blocks SIMD vectorization
  - `Math.Clamp(x, min, max)` → internal NaN check → conditional branch → branch mispredict stall
  - `MathF.Min(MathF.Max(x, min), max)` → maps to ARM `fmin`/`fmax` hardware instruction (no branch)
- [ ] Use `SintoMath.SinFast` for frequency coefficient
- [ ] Use `SintoMath.TanhFast` for saturation (non-linear character)
- [ ] Apply `DenormalGuard.Protect()` to all 4 state variables after each update
- [ ] Write test: resonance = 3.99, cutoff = 0.999, white noise input → output stays finite (no NaN/Inf)
- [ ] Write test: 5 minutes silent input → output stays near zero (no denormal blowup)
- [ ] Benchmark: single filter pass on 1024 samples < 0.1ms on Android arm64

**Acceptance criteria:**
- Output is always finite (`!float.IsNaN(output) && !float.IsInfinity(output)`)
- No `Math.Sin` or `Math.Pow` calls (LUT/poly only)
- `DenormalGuard.Protect()` called on every state variable

---

### Task 2.2 — LFO × 2 + PolyMod

**File:** `sinto/src/Sinto.Core/Synth/LFO.cs`

- [ ] Implement LFO as `readonly struct`
  - [ ] Waveforms: Sine, Triangle, Square, S&H (sample and hold)
  - [ ] Rate: free-running Hz or tempo-synced (beats)
  - [ ] Targets: Pitch, Cutoff, Amplitude, PWM
- [ ] Implement PolyMod routing (Prophet-5 style)
  - [ ] OSC-B output → OSC-A pitch (poly mod source A)
  - [ ] Filter Envelope → OSC-A pitch (poly mod source B)
  - [ ] OSC-B output → Filter cutoff
  - [ ] Analog drift: per-voice pitch offset via seeded LCG noise
- [ ] Write test: LFO at 1Hz, 44100 sample rate → output completes exactly 1 cycle in 44100 samples

---

### Task 2.3 — 24-32 Voice Polyphony + Priority Voice Stealing + Quick Release

**File:** `sinto/src/Sinto.Core/Synth/VoiceManager.cs`

- [ ] Implement `VoiceConfig` struct: `ReservedVoices`, `Priority`, `Protected`
- [ ] Define 8-track configs (Drum: Protected=true, Bass: Priority=8, etc.)
- [ ] Voice allocation: fill reserved slots first, then steal from lowest-priority releasing voices
- [ ] `StealVoice(int idx)`:
  - [ ] If `CurrentAmplitude < 0.001f` → immediate free
  - [ ] Else → set `QuickRelease` state, fade to zero over 5ms (220 samples @ 44100Hz)
- [ ] Verify: drum track voices are never stolen during 8-track polyphonic stress test
- [ ] Verify: no click noise audible when stealing under max-polyphony load (subjective listening test)
- [ ] Write test: 40 simultaneous NoteOn events → max active voices never exceeds `MaxVoices`
- [ ] Write test: steal a voice with amplitude 0.8 → amplitude reaches 0.0 within 220 samples

---

### Task 2.4 — MIDI Keyboard + ASIO (Standalone)

**Target:** Standalone .NET 8 Native AOT app (no Unity)

- [ ] Add `Sanford.Multimedia.Midi` reference to `Sinto.Core`
- [ ] Implement MIDI input listener: NoteOn, NoteOff, PitchBend (CC2), Modulation (CC1), Sustain (CC64)
- [ ] Implement ASIO output via `NAudio.Asio` (Windows)
- [ ] Build standalone `.exe` targeting `win-x64` with **Self-Contained Single File** (NOT Native AOT)
  - Native AOT does not support COM Interop required by NAudio.Asio and Sanford.Multimedia.Midi
  - Use: `dotnet publish -r win-x64 --self-contained true /p:PublishSingleFile=true`
  - CoreCLR performance is sufficient for audio; Native AOT offers no meaningful gain here
- [ ] Measure round-trip MIDI-to-audio latency on Windows: target < 5ms with ASIO
- [ ] Set audio thread priority on standalone app startup:
  ```csharp
  // Must be called from inside the audio thread itself, immediately on thread start
  Thread.CurrentThread.Priority = ThreadPriority.Highest;
  // Note: Unity sets this automatically for OnAudioFilterRead.
  // Standalone threads default to ThreadPriority.Normal — OS will preempt them
  // during mouse movement, Windows Update, etc., causing buffer underruns.
  ```
- [ ] Verify on Windows: start audio thread, run CPU-heavy background task → no underrun
- [ ] Verify on Android (Oboe): AudioStreamBuilder sets `PerformanceMode::LowLatency` + `SharingMode::Exclusive`
- [ ] Implement Oboe output path for Android standalone (no Unity dependency)
  - Oboe C++ callback receives `float* audioData, int32_t numFrames`
  - Bridge to C#: `var span = new Span<float>(audioData, numFrames * numChannels);`
  - Pass directly to `_synth.RenderSamples(span)` — zero copy, zero GC allocation
- [ ] Write `Sinto.Standalone/Program.cs` — entry point, initializes MIDI + audio, runs until Ctrl+C

**Acceptance criteria:**
- ASIO latency < 5ms measured with loopback test
- Oboe standalone APK builds for arm64-v8a without Unity
- Windows build uses Self-Contained Single File (not Native AOT) — `NAudio.Asio` loads without COM Interop errors
- `Native AOT` does not appear in `win-x64` publish profile
- Audio thread priority is `ThreadPriority.Highest` — verified via `Thread.CurrentThread.Priority` log on startup
- No buffer underrun during 60-second stress test with background CPU load on Windows

---

## Phase 3 — P5 / JP-8 Emulation + Retro Texture (2 months: 2026-11 to 2026-12)

**Goal:** Prophet-5 and Jupiter-8 character sounds, and the genuine N64/PS1 sonic texture.

---

### Task 3.1 — CEM3320 Filter + PolyMod (Prophet-5)

**File:** `sinto/src/Sinto.Core/Filter/CEM3320.cs`

- [ ] Implement CEM3320 filter model (2-pole state-variable, Prophet-5 character)
- [ ] Clamp all parameters
- [ ] Apply `DenormalGuard.Protect()` to all state vars
- [ ] Wire PolyMod routing from Task 2.2: OSC-B → cutoff, FilterEnv → pitch
- [ ] Create preset `p5_brass_lead.sinto` — verify against reference recordings (subjective)
- [ ] Create preset `p5_poly_string.sinto`

---

### Task 3.2 — IR3109 Filter + BBD Chorus (Jupiter-8)

**File:** `sinto/src/Sinto.Core/Filter/IR3109.cs`  
**File:** `sinto/src/Sinto.Core/Effects/BBDChorus.cs`

- [ ] Implement IR3109 LPF/HPF state-variable filter
- [ ] Apply `DenormalGuard.Protect()` on all state vars
- [ ] Implement BBD Chorus:
  - [ ] Static circular buffer allocation: `static readonly float[] _delayBuf = new float[44100]`
  - [ ] Two LFO rates (slow + fast) for JP-8 double-chorus character
  - [ ] Apply `DenormalGuard.Protect()` on delay buffer writes
  - [ ] Verify: no allocation per sample (BenchmarkDotNet)
- [ ] Create preset `jp8_pad.sinto`
- [ ] Create preset `jp8_strings.sinto`

---

### Task 3.3 — N64 Nearest-Neighbor Interpolation Mode

- [ ] Add `InterpMode.NearestNeighbor` to `Oscillator`
- [ ] `ReadNN(float[] table, double phase)` — truncate phase index, no interpolation
- [ ] Verify: non-integer pitch shifts produce inharmonic aliasing (the N64 "metallic" character)
- [ ] Write listening test: C4 shifted +7 cents → aliasing artifacts audible in spectrum
- [ ] Create preset `n64_square_lead.sinto` with `interpMode: NearestNeighbor`

---

### Task 3.4 — PS1 ADPCM Waveshaper

**File:** `sinto/src/Sinto.Core/Effects/RetroFilter.cs`

- [ ] Implement `AdpcmWaveshape(float x)`: BRR quantization simulation
  - `MathF.Round(x * 16f) / 16f * 0.7f + x * 0.3f`
- [ ] Implement full RetroFilter pipeline:
  - [ ] Bit crush → quantize to N bits
  - [ ] Waveshape (PS1 mode only)
  - [ ] Nearest-Neighbor downsample to target rate
- [ ] `RetroMode` enum: `N64` (22050Hz/16bit), `PS1` (11025Hz/8bit), `Clean`
- [ ] Apply after final mix, before output
- [ ] Write test: Clean mode → output equals input (passthrough)
- [ ] Write test: PS1 mode → output sample count = `input * (11025 / 44100)`
- [ ] Create `n64_retro_kit.sinto` and `ps1_retro_kit.sinto` drum presets

---

## Phase 4 — Commercialization (2.5 months: 2027-01 to 2027-03)

**Goal:** Shippable product: preset packs, standalone app, game integration, thermal stress-tested.

---

### Task 4.1 — .sinto Preset Format Finalization

- [ ] Define final JSON schema for `.sinto` (all fields from v0.5.0 proposal)
- [ ] Implement `SintoPreset` deserialization (System.Text.Json, no allocation on hot path)
- [ ] Implement preset validation: use `MathF.Min(MathF.Max(...))` clamps on load, never trust user data
- [ ] Implement obfuscated key protection (speed bump, 2 weeks maximum)
  - [ ] XOR obfuscation + runtime key derivation (not hardcoded string)
  - [ ] Memory-only decryption: no plaintext written to filesystem
- [ ] Write test: corrupted preset file → graceful fallback to default values, no crash
- [ ] Write test: all shipped presets pass validation

#### Preset Hot-Swap: Double-Buffered `Interlocked.Exchange` Design

**Why NOT `ControlEvent` per parameter:**  
A preset with 50 parameters = 50 `ControlEvent` entries queued simultaneously.  
Combined with NoteOn bursts, the 1024-slot ring buffer fills instantly → events dropped → wrong sound.

**Correct approach: reference swap, not parameter flood.**

```csharp
// SintoEngine holds two preset slots; main thread writes to pending,
// audio thread swaps atomically on SwapPreset ControlEvent.
//
// CS0420 PREVENTION:
// Do NOT declare these fields as `volatile`.
// Passing a `volatile` field to `ref` parameter (Volatile.Read / Interlocked.Exchange)
// causes compiler warning CS0420: "reference to volatile field will not be treated as volatile."
// The `volatile` semantics are lost when passed by ref — exactly the opposite of intended.
// Correct pattern: plain fields + ALL accesses via explicit Volatile.* / Interlocked.* APIs.

public sealed class SintoEngine {
    SintoPreset _activePreset;   // plain field — accessed only via Interlocked.Exchange
    SintoPreset _pendingPreset;  // plain field — accessed only via Volatile.Read/Write
    // ^^^ NO `volatile` modifier. Volatile.Read/Write provide the necessary memory barriers.

    // Main thread: write pending preset, then enqueue ONE swap signal
    public void RequestPresetSwap(SintoPreset newPreset) {
        Volatile.Write(ref _pendingPreset, newPreset);   // full memory barrier
        _eventQueue.TryEnqueue(new ControlEvent {
            Kind = ControlEventKind.SwapPreset,
            OffsetFrames = 0
        });
    }

    // Audio thread: on SwapPreset event
    case ControlEventKind.SwapPreset:
        var pending = Volatile.Read(ref _pendingPreset); // full memory barrier
        if (pending is not null) {
            Interlocked.Exchange(ref _activePreset, pending); // atomic swap
            Volatile.Write(ref _pendingPreset, null);
        }
        break;
}
```

- [ ] Implement `_pendingPreset` volatile field in `SintoEngine`
- [ ] Implement `RequestPresetSwap()` — enqueues ONE `SwapPreset` event (not 50 parameter events)
- [ ] Audio thread: on `SwapPreset` event → `Interlocked.Exchange(ref _activePreset, pending)`
- [ ] **CS0420 prevention**: `_pendingPreset` and `_activePreset` are plain fields (NO `volatile` modifier)
  - `volatile` + `ref` parameter = CS0420 warning = volatile semantics silently lost
  - All accesses MUST go through `Volatile.Read` / `Volatile.Write` / `Interlocked.*`
- [ ] Write test: swap preset while 32 voices are active → no ring buffer overflow
- [ ] Write test: swap preset 100 times in rapid succession → no event dropped, final preset is correct
- [ ] Confirm `ControlEventKind.LoadPreset` does NOT exist — only `SwapPreset`
- [ ] Grep check: `volatile` keyword does NOT appear on any field accessed via `ref` parameter

---

### Task 4.2 — N64 / PS1 Preset Pack

- [ ] Square Lead (N64, NearestNeighbor)
- [ ] Sawtooth Bass (N64)
- [ ] Triangle Pad (N64)
- [ ] Noise Sweep (N64)
- [ ] ADPCM Kick (PS1)
- [ ] ADPCM Snare (PS1)
- [ ] ADPCM HiHat (PS1)
- [ ] ADPCM Bass (PS1)
- [ ] Listen test on target game scene: all presets sound intentionally retro, not accidentally broken
- [ ] Pack as `sinto-pack-retro-v1.0.zip`

---

### Task 4.3 — Prophet-5 / Jupiter-8 Preset Pack

- [ ] P5: Brass Lead, Poly String, Metallic Bell, PWM Pad
- [ ] JP-8: String Pad, Lush Pad, Slow Brass, Choir
- [ ] Listen test: compare against reference hardware recordings (YouTube archival material)
- [ ] Pack as `sinto-pack-vintage-v1.0.zip`

---

### Task 4.4 — Standalone Sound App (Windows + Android)

- [ ] Windows: ASIO output + MIDI keyboard input, preset browser, 5-octave virtual keyboard UI
- [ ] Android: Oboe output + MIDI USB keyboard input via USB MIDI API
- [ ] Preset browser: load `.sinto` files from filesystem
- [ ] Basic UI: oscilloscope visualizer, ADSR display, retro mode toggle
- [ ] Build: `win-x64` installer + Android APK (arm64-v8a)

---

### Task 4.5 — Game Integration + Thermal Stress Test

- [ ] Integrate Quyno + Sinto into target game scene
- [ ] Verify `SintoBgmPlayer` and `SintoSfxEmitter` work in Unity build
- [ ] Run thermal stress test protocol:
  - [ ] Device: mid-range Android (not flagship)
  - [ ] Duration: 30 minutes continuous 3D gameplay
  - [ ] Ambient temperature: warm room (25°C+)
  - [ ] Monitor: Dynamic Voice Scaling tier changes via on-screen debug overlay
  - [ ] Pass criteria: zero buffer underruns, Dynamic Voice Scaling fires at least once
- [ ] Document results in `quyno/docs/THERMAL_TEST_RESULTS.md`
- [ ] Final listening test: game runs with N64/PS1 preset pack, BGM + SFX, 5 minutes

---

## Cross-Cutting Checklist (applies to every PR)

- [ ] No `new` inside audio callback chain
- [ ] No `Math.Sin` or `Math.Pow` inside `Sinto.Core/Synth/` or `Sinto.Core/Filter/`
- [ ] No `lock` inside audio thread
- [ ] No `ConcurrentQueue<T>` anywhere in codebase
- [ ] Every IIR state update calls `DenormalGuard.Protect()`
- [ ] Every new filter/effect has a "5 minutes of silence" test (denormal regression)
- [ ] Every Voice Stealing path uses Quick Release (no hard cut)
- [ ] `[LayoutKind.Explicit]` is NOT applied to any generic type (TypeLoadException)
- [ ] Every ring buffer constructor call uses a power-of-2 argument (bitmask guard)
- [ ] `DenormalGuard.Protect(x, sampleIndex)` — no `static` state (thread-safe)
- [ ] `Math.Clamp` does NOT appear in any hot-path audio code (use `MathF.Min(MathF.Max(...))`)
- [ ] After any Dynamic Voice Scaling tier change, `_cooldownRemaining` is set (no hunting)
- [ ] Windows standalone uses Self-Contained Single File, NOT Native AOT
- [ ] Standalone audio thread sets `Thread.CurrentThread.Priority = ThreadPriority.Highest` on start
- [ ] `ControlEvent.OffsetFrames` is set correctly by Quyno tick engine (never always 0)
- [ ] Sub-buffering render loop splits buffer at each event's `OffsetFrames`
- [ ] Sub-buffering uses `Math.Max(ev.OffsetFrames, pos)` to prevent time travel (negative render length)
- [ ] `ControlEventKind.LoadPreset` does NOT exist — preset changes use `Interlocked.Exchange`
- [ ] `RenderSamples` takes `Span<float>`, NOT `float[]` — verified in all call sites
- [ ] `volatile` keyword does NOT appear on fields passed to `ref` parameters (CS0420 prevention)

---

## Definition of Done — Phase 1 Exit Criteria

Before Phase 2 begins, ALL of the following must be true:

1. `AudioRingBuffer<ControlEvent>` — zero allocations confirmed by BenchmarkDotNet
2. Sin/Tanh implementation — decision documented with real Android benchmark numbers
3. `DenormalGuard.Protect()` — present on every IIR state variable in codebase
4. Single voice renders on Android arm64 without GC collection for 60 seconds
5. Pause/Resume — no tick accumulation after resume (automated test)
6. Dynamic Voice Scaling scaffold — tier changes under simulated load (automated test)
7. Oboe integration — sine wave plays on physical device for 5 minutes without artifact
8. All GC Zero audit items checked off
9. `dotnet test` passes 100% on CI

---

*© STUDIO MeowToon — MIT License*  
*develop_plan_v1.4.md — Gemini Round 8: CS0420 (volatile+ref) + Span<float> API + Time travel prevention*
*Gemini rounds 1-8: all defects resolved. Implementation phase begins.*
