# Quyno / Sinto — Project Proposal

**Version:** 1.0 (translated from v0.5.0)  
**Date:** 2026-05-25  
**Status:** Confirmed — Implementation Ready  
**Review:** Gemini Rounds 1–4 fully applied · MIT License confirmed

---

## Table of Contents

1. [Background and History](#1-background-and-history)
2. [Problem Definition](#2-problem-definition)
3. [Solution and Concept](#3-solution-and-concept)
4. [System Overview](#4-system-overview)
5. [Quyno — Procedural Music Sequencer](#5-quyno--procedural-music-sequencer)
6. [Sinto — Software Synthesizer Engine](#6-sinto--software-synthesizer-engine)
7. [Real-Device Survival Strategy](#7-real-device-survival-strategy)
8. [Explicit Out-of-Scope Definitions](#8-explicit-out-of-scope-definitions)
9. [Business Model](#9-business-model)
10. [Technology Stack](#10-technology-stack)
11. [Development Roadmap](#11-development-roadmap)
12. [Risks and Mitigations](#12-risks-and-mitigations)

---

## 1. Background and History

### 1.1 Origin: An Homage to the Yamaha QY Series

In the 1990s, Yamaha introduced the QY10, QY20, and QY70 — pocket-sized music sequencers that ran on batteries, fit in a jacket pocket, and allowed anyone to compose pattern-based music without formal music theory knowledge. Artists including Thom Yorke, Björk, PJ Harvey, and Tricky were among their devoted users.

**Quyno** is named as the spiritual successor to this QY lineage.

```
QY10 (1990) → QY20 (1992) → QY70 (1997)
                    ↓
              Quyno (2026–)
```

"No music theory required. Compose anywhere. Accessible to everyone." — this philosophy is reinterpreted for the Pure C# / mobile era.

### 1.2 MeowziQ Development and Lessons Learned

STUDIO MeowToon has developed MeowziQ (→ rebranded as Quyno) for over five years. Its core innovations include a proprietary "3-chord" modal theory (extracting three major triads from each of the seven church modes) and a text-based song description format.

### 1.3 Alignment with the N64 / PS1 Aesthetic

The target mobile 3D game's visual and audio aesthetic has been confirmed as **N64 / PS1 (fifth-generation consoles)**. This choice is not a constraint — it is a performance advantage. The absence of PolyBLEP (anti-aliasing) and the correct use of 11–22kHz sample rates reduce CPU load by 1/2 to 1/4. The QY series and these consoles are products of the same era — the alignment is natural.

### 1.4 Naming Decisions

| Repository | Meaning | Rationale |
|---|---|---|
| `Koleco` | Portfolio site (existing) | Esperanto |
| `Quyno` | Sequencer | QY10/20/70 tribute + Esperanto noun suffix -o. Pronounced "kyoo-no" |
| `Sinto` | Synth engine | Root of Esperanto `sintezi` (to synthesize) + -o |

All three names verified clean across music, game, and software domains. npm and PyPI confirmed available.

---

## 2. Problem Definition

```mermaid
graph TD
    A[Traditional game music implementation] --> B[Fixed OGG/WAV file playback]
    B --> C1[Large files: several MB per BGM track]
    B --> C2[No variation: identical music every playthrough]
    B --> C3[No differentiation: indistinguishable from other titles]
    B --> C4[No timbre change: world aesthetic cannot be reskinned]
    B --> C5[Hard to monetize: low unit price for music DLC]
```

---

## 3. Solution and Concept

### 3.1 Core Concept

> **Separate "song structure" from "timbre" and sell each as an independent product.**  
> Quyno defines *what to play*. Sinto defines *how it sounds*.

### 3.2 Replacing FluidSynth

```mermaid
graph LR
    subgraph Before
        B1[Quyno Core] --> B2[FluidSynth .so\n~30MB · no arm64]
    end
    subgraph After
        A1[Quyno Core] --> A2[Sinto Pure C#\nzero external dependencies]
    end
```

Migrating to Pure C# resolves arm64-v8a, iOS, WebGL, copy protection, and MIT licensing in a single move.

### 3.3 License: MIT Confirmed

The original MeowziQ used GNU GPL v2. The GPL's copyleft provision requires any game that incorporates it to publish its full source code — directly contradicting commercial game and DLC sales. **MIT** removes all restrictions on commercial use.

---

## 4. System Overview

```mermaid
graph TB
    subgraph DataLayer[Data Layer]
        D1[pattern.json / song.json\nphrase.json / player.json]
        D5[.sinto presets\nobfuscated key protection]
    end

    subgraph MainThread[Main Thread]
        MT1[Game logic / JSON loader]
        MT2[Control event dispatch\nNoteOn / Pause / VoiceLimit]
    end

    subgraph AudioThread[Audio Thread — fully isolated]
        AT1[Quyno Tick Engine\nsample-accurate]
        AT2[Custom SPSC ring buffer\nread side]
        AT3[Sinto synth engine\ndynamic 16–32 voices]
        AT4[Denormal protection\nDC offset injection]
    end

    subgraph Output[Output]
        O1[BGM: stereo\nUnity OnAudioFilterRead]
        O2[SFX: mono\nvia Unity AudioSource\n→ Unity handles 3D positioning]
        O3[Standalone\nOboe / ASIO direct]
    end

    DataLayer --> MT1 --> MT2
    MT2 -.->|zero-allocation transfer| AT2
    AT2 --> AT1 --> AT3 --> AT4
    AT4 --> Output
```

### 4.1 Thread Isolation: The Most Critical Design Principle

Full respect for the asynchrony between the audio thread and the main thread.

**Three absolute rules:**

1. Never use `lock` on the audio thread
2. All shared data access via **custom fixed-length ring buffer + `Interlocked`** only (`ConcurrentQueue<T>` is forbidden)
3. Quyno's tick calculation executes inside the audio thread's sampling loop (sample-accurate)

#### Why `ConcurrentQueue<T>` is Forbidden

`ConcurrentQueue<T>` is internally implemented as a linked list of segments. When a segment is exhausted, it calls `new` internally, allocating on the GC heap. Since GC allocations on the audio thread are strictly forbidden, the standard library cannot be trusted here.

#### Custom Fixed-Length Ring Buffer

```csharp
// SPSC lock-free ring buffer
// Transfers arbitrary events from main thread → audio thread
public enum ControlEventKind : byte {
    NoteOn, NoteOff, Pause, Resume, SetVoiceLimit, SwapPreset, SetBPM
}
public readonly struct ControlEvent {
    public readonly ControlEventKind Kind;
    public readonly ushort OffsetFrames; // sample-accurate position in buffer
    public readonly int    IntParam;
    public readonly float  FloatParam;
    public readonly int    TrackId;
    public readonly int    Priority;
}
```

### 4.2 Latency Requirements by Use Case

| Use case | Path | Expected latency |
|---|---|---|
| In-game BGM | Unity OnAudioFilterRead | 20–80ms |
| In-game SFX (3D positioning handled by Unity) | Unity AudioSource + Sinto mono output | 20–80ms |
| Standalone instrument (Android) | Oboe direct | 8–12ms |
| Standalone instrument (Windows) | ASIO direct | 2–5ms |

---

## 5. Quyno — Procedural Music Sequencer

### 5.1 Proprietary "3-Chord" Modal Theory

```mermaid
graph LR
    Ly[Lydian\nC, D, G] --> Io[Ionian\nC, F, G]
    Io --> Mi[Mixolydian\nC, F, Bb]
    Mi --> Do[Dorian\nCm, F, Bb]
    Do --> Ae[Aeolian\nCm, Ab, Bb]
    Ae --> Ph[Phrygian\nCm, Ab, Bbm]
    Ph --> Lo[Locrian\nCm-5, Ab, Bbm]
```

### 5.2 Song Data Structure

```mermaid
erDiagram
    Song ||--o{ Section : contains
    Section ||--o{ Pattern : references
    Pattern ||--o{ Meas : contains
    Meas ||--o{ Span : contains
    Player ||--|| Song : has
    Player ||--o{ Phrase : has
    Phrase ||--o{ Note : generates
    Phrase }o--|| Pattern : matches_name
```

### 5.3 GC-Zero + Fast Math: 7 Principles

| Priority | Principle | Effect |
|---|---|---|
| Highest | `Note` → `readonly struct` | Eliminates the root cause of Gen0 GC |
| Highest | Remove Generator LINQ · reuse static buffers | 3 → 0 allocations per tick |
| Highest | Fast Sin/Tanh (decided by device benchmark) | Optimizes 705,600 calls/sec |
| High | Track LINQ → `for` / `RemoveAll` | Eliminates temporary List creation |
| High | `AllSpan` cache (dirty flag) | Zeroes List creation in build loop |
| Medium | Reuse `MemoryStream` | Eliminates double allocation on load |
| Auxiliary | `GCLatencyMode.SustainedLowLatency` | Blocks GC full collection during playback |

#### Fast Sin/Tanh: LUT vs Polynomial Approximation — Decided by Device Benchmark

| Method | Advantage | Disadvantage |
|---|---|---|
| LUT (`float[4096]`) | Single memory lookup · minimal error | Cache miss risk |
| Polynomial (parabolic · 5th-order Taylor) | Register-only · no cache | Lower precision (acceptable for audio) |

**Phase 1 task:** Implement both → benchmark with Unity Profiler + Android GPU Inspector → adopt the faster one.

---

## 6. Sinto — Software Synthesizer Engine

### 6.1 Synth Architecture

```mermaid
graph TB
    subgraph VM[Voice Manager — dynamic 16–32 voices]
        VS[Priority Voice Stealing\n+ Quick Release\n+ Dynamic Voice Scaling]
    end
    subgraph Voice[Voice × dynamic]
        O1[OSC × 2\nSine/Saw/Tri/Square/Noise\nphase accumulator mode select]
        F1[FilterState\nMoog or Roland IR3109\n+ parameter clamp\n+ denormal protection]
        E1[Amp + Filter + Pitch ADSR × 3]
        M1[LFO × 2 + SmoothedParameter]
    end
    subgraph FX[Effects — serial chain]
        FX1[BBD Chorus\ninstance buffer · denormal protected]
        FX2[Freeverb Reverb]
        FX3[Stereo Delay\ntempo sync]
        FX4[RetroFilter\nbit crush + NN interpolation + ADPCM shaper]
    end
    VM --> Voice --> FX --> Output
```

### 6.2 Voice Stealing: Quick Release Prevents Click Noise

Forcing a waveform to stop at a non-zero amplitude produces a "click" artifact. Any stolen voice must fade out over 5ms (Quick Release) before being freed.

```csharp
void StealVoice(int voiceIdx) {
    var v = _voices[voiceIdx];
    if (MathF.Abs(v.CurrentAmplitude) < 0.001f) {
        v.State = VoiceState.Free;
    } else {
        v.State = VoiceState.QuickRelease;
        v.QuickReleaseSamples = (int)(0.005f * SAMPLE_RATE); // 220 samples
    }
}
```

### 6.3 Filter: Numerical Stability + Denormal Protection

```csharp
// FilterState struct — Moog and Roland algorithms in one struct
// switch(FilterMode) is JIT-inlined — no virtual dispatch, no boxing
float Process(float input, long sampleIndex) => _mode switch {
    FilterMode.Moog   => ProcessMoog(input, sampleIndex),
    FilterMode.Roland => ProcessRoland(input, sampleIndex),
    _                 => input
};

// Moog ladder — resonance clamped [0, 3.99], DenormalGuard on all states
// MathF.Min(MathF.Max()) — NOT Math.Clamp (NaN branch blocks SIMD)
float ProcessMoog(float input, long sampleIndex) {
    resonance = MathF.Min(MathF.Max(resonance * 4f, 0f), 3.99f);
    // ... Huovilainen model with DenormalGuard.Protect(s, sampleIndex)
}
```

### 6.4 Parameter Smoothing: No Zipper Noise

All real-time parameters use one-pole lowpass smoothing (~8ms) to prevent zipper noise during MIDI CC sweeps and LFO modulation. On NoteOn, all smoothers must snap instantly to their target values — otherwise the previous voice's parameter state causes a "pyun" transient artifact.

```csharp
// On NoteOn — always snap first
smoother.SnapToTarget();   // current = target (instantaneous)

// During playback — smooth interpolation
smoother.Tick();           // current += (target - current) × coeff
```

### 6.5 RetroFilter: Authentic N64 / PS1 Texture

```mermaid
graph LR
    A[44100Hz source] --> B[Bit crush]
    B --> C{RetroMode}
    C -->|N64| D[Nearest-Neighbor interpolation\ninharmonic aliasing]
    C -->|PS1| E[ADPCM waveshaper\nBRR distortion simulation]
    D --> F[22050Hz downsample]
    E --> G[11025Hz downsample]
```

```csharp
// N64: no interpolation — truncated phase index
float ReadNN(float[] table, double phase)
    => table[(int)(phase * table.Length) % table.Length];

// PS1: ADPCM BRR distortion simulation
float AdpcmShape(float x)
    => MathF.Round(x * 16f) / 16f * 0.7f + x * 0.3f;
```

### 6.6 Preset Format (.sinto)

```json
{
  "name": "N64 Square Lead",
  "osc1": { "wave": "Square", "interpMode": "NearestNeighbor", "pw": 0.5 },
  "filter": { "type": "Moog", "cutoff": 0.8, "resonance": 0.2 },
  "ampEnv": { "attack": 0.01, "decay": 0.1, "sustain": 0.8, "release": 0.2 },
  "retro": { "mode": "N64", "sampleRate": 22050, "bitDepth": 16 }
}
```

---

## 7. Real-Device Survival Strategy

Three traps that only surface when running on physical mobile hardware. All must be addressed before writing the first line of code.

### 7.1 Denormal Bomb

**Problem:** When audio fades to silence, IIR filter feedback state approaches subnormal floats (e.g. `1e-30`). ARM CPUs process subnormal values via microcode emulation — **CPU load spikes 10–100×**. The game stutters the moment the music stops.

**Solution:** Inject an alternating DC offset (`1e-15f`, alternating sign via sample index parity) into every IIR feedback loop. Cost: one addition per sample per state variable.

```csharp
public static float Protect(float x, long sampleIndex)
    => x + ((sampleIndex & 1L) == 0L ? 1e-15f : -1e-15f);
```

### 7.2 Unity Pause (`Time.timeScale = 0`)

**Problem:** `OnAudioFilterRead` continues to be called by the OS audio hardware even when Unity is paused. The main thread's "game time" and the audio thread's "DSP time" diverge completely. When the game resumes, the sequencer attempts to catch up all accumulated ticks at high speed.

**Solution:** Send `Pause` / `Resume` control events through the ring buffer. The audio thread zeroes its output buffer and freezes tick advancement while paused.

```mermaid
graph LR
    M[Main thread\nTime.timeScale = 0] --> RB[Ring buffer\nPause event]
    RB -.-> A[Audio thread]
    A --> S{State}
    S -->|Pause received| Z[Freeze tick\nzero output]
    S -->|Resume received| R[Resume tick\nno accumulation]
```

### 7.3 Thermal Throttling — Dynamic Voice Scaling

**Problem:** In hot weather, smartphones reduce CPU clock speed (thermal throttling). Fixed 32 voices work fine in a cool room but cause buffer underruns after 10 minutes of gameplay.

**Solution:** Dynamically scale the maximum voice count across three tiers (32 → 24 → 16) based on measured callback processing time. A cooldown of 64 callbacks (~300ms) prevents hunting after a tier change — Quick Release needs time to complete before load actually drops.

```mermaid
graph TD
    A[Audio callback start] --> B[Record timestamp]
    B --> C[Render samples]
    C --> D[Measure elapsed]
    D --> E{usage > 70%?}
    E -->|YES + cooldown=0| F[Tier down\n32→24→16\nreset cooldown=64]
    E -->|NO + usage < 40%| G{headroom\n5+ seconds?}
    G -->|YES| H[Tier up\n16→24→32\nreset cooldown=64]
    G -->|NO| I[Hold]
    F --> J[Continue]
    H --> J
```

---

## 8. Explicit Out-of-Scope Definitions

### 8.1 3D Spatial Audio is Out of Scope for Sinto

**Decision:** Sinto does not implement 3D spatial positioning.

**Rationale:**

- Sinto is a synthesizer engine, not a spatial audio engine. Clean separation of concerns is required.
- Unity's `AudioSource` / `AudioListener` already handles distance attenuation, panning, and Doppler natively. Reimplementing this would be technical debt.
- Instantiating a full SintoEngine per enemy GameObject would instantly exhaust CPU.

**Sinto output specification:**

| Use | Output | 3D positioning |
|---|---|---|
| BGM | Stereo (Quyno-driven) | N/A |
| SFX | **Mono 1-voice** via `SintoMicroEngine` | **Unity AudioSource** |

### 8.2 Other Out-of-Scope Items

| Feature | Reason |
|---|---|
| PolyBLEP (anti-aliasing) | Aliasing is the aesthetic in N64/PS1 mode |
| Multi-output (per-track routing) | Unity AudioMixer handles this |
| VST / AU plugin (current) | Phase 5+. `RenderSamples(Span<float>)` guarantees future compatibility |
| Dynamic transposition | Documented as TODO constraint |

---

## 9. Business Model

### 9.1 Revenue Structure

```mermaid
graph TB
    subgraph ProductA[In-game audio system]
        A1[Quyno BGM + Sinto SFX]
        A2[Basic preset pack included]
    end
    subgraph ProductB[Sinto preset pack DLC]
        B1[N64/PS1 retro pack]
        B2[Moog character — bass/lead]
        B3[Roland character — pads/strings]
        B4[Custom pack — world-specific]
    end
    subgraph ProductC[Standalone instrument app]
        C1[MIDI keyboard performance]
        C2[Windows / Android]
    end
    subgraph ProductD[MIT open source]
        D1[GitHub MIT]
        D2[Community · commercial use free]
    end
```

### 9.2 Copy Protection: Speed Bump Strategy

C# / IL2CPP has a fundamental limitation: AES keys hardcoded in the binary are extractable from `global-metadata.dat` in minutes with a hex editor. Memory dumps expose decrypted parameters at runtime.

**Goal:** Deter casual extraction, not defeat determined adversaries.  
**Strategy:** Price and convenience — make buying easier than reverse engineering.  
**Implementation budget:** 2 weeks maximum.

---

## 10. Technology Stack

```mermaid
graph TB
    subgraph Core
        C1[C# .NET 8 / Unity 2022 LTS]
        C2[.NET 8 Self-Contained Single File\nstandalone only — NOT Native AOT\nNAudio.Asio requires COM Interop]
        C3[MIT License]
    end
    subgraph Platform
        P1[Android arm64-v8a + armeabi-v7a]
        P2[Windows x64]
        P3[iOS / WebGL future]
    end
    subgraph AudioOutput
        A1[Unity OnAudioFilterRead]
        A2[Oboe direct]
        A3[NAudio.Asio]
    end
    subgraph Threading
        T1[Custom SPSC ring buffer\nLayoutKind.Sequential + long padding\nNO LayoutKind.Explicit on generic]
        T2[Interlocked / Volatile only]
        T3[Tick runs inside audio thread]
    end
    subgraph Survival
        S1[Denormal: alternating DC offset injection]
        S2[Pause: control event via ring buffer]
        S3[Thermal: Dynamic Voice Scaling]
    end
    subgraph Removed
        R1[FluidSynth ❌]
        R2[SF2 format ❌]
        R3[GPL v2 ❌]
        R4[ConcurrentQueue ❌\ninternal GC allocation]
        R5[3D spatial audio ❌\nUnity handles it]
        R6[IFilter interface ❌\nboxing + virtual dispatch]
        R7[static DSP buffers ❌\ndata race across engine instances]
    end
```

### 10.1 Design Principles Summary

**GC-Zero 7 Principles:**

1. `Note` → `readonly struct`
2. Remove Generator LINQ · reuse static buffers
3. Fast Sin/Tanh (LUT vs polynomial — decided by device benchmark)
4. Track LINQ → `for` / `RemoveAll`
5. `AllSpan` cache (dirty flag)
6. Reuse `MemoryStream` (`Position = 0`)
7. `GCLatencyMode.SustainedLowLatency`

**Thread Safety 3 Principles:**

1. No `lock` on audio thread
2. Custom SPSC ring buffer (no `ConcurrentQueue<T>`)
3. Tick calculation inside audio thread (sample-accurate)

**Voice Stealing 3 Principles:**

1. Prioritize release-phase voices for stealing
2. Drum tracks are Protected — never stolen by other tracks
3. Always apply Quick Release (5–10ms) on steal

**Real-Device Survival 3 Principles:**

1. **Denormal protection** — alternating DC offset injection in all IIR feedback loops
2. **Pause / Resume control events** — sent via ring buffer from main thread
3. **Dynamic Voice Scaling** — auto-switch between 32 ↔ 24 ↔ 16 voices under thermal load

---

## 11. Development Roadmap

```mermaid
gantt
    title Quyno / Sinto Development Roadmap
    dateFormat  YYYY-MM
    axisFormat  %Y-%m

    section Phase 1 — Foundation (3 months)
    MIT rebrand · remove FluidSynth           :p1a, 2026-06, 1w
    Custom SPSC ring buffer + Pause support   :p1b, 2026-06, 2w
    LUT vs polynomial benchmark on device     :p1c, 2026-06, 3w
    Sinto core + denormal protection          :p1d, 2026-07, 4w
    arm64-v8a + Oboe integration              :p1e, 2026-08, 2w
    GC-zero 7 principles + Dynamic Voice Scaling :p1f, 2026-08, 2w

    section Phase 2 — Synth Expansion (2 months)
    Moog ladder (Huovilainen + clamp + DC)    :p2a, 2026-09, 3w
    LFO × 2 + PolyMod                         :p2b, 2026-09, 2w
    Dynamic 16–32 voices + priority stealing\n+ Quick Release :p2c, 2026-10, 3w
    MIDI keyboard + ASIO support              :p2d, 2026-10, 2w

    section Phase 3 — Retro Texture (2 months)
    IR3109 filter + BBD chorus                :p3a, 2026-11, 3w
    N64 Nearest-Neighbor interpolation mode   :p3b, 2026-12, 2w
    PS1 ADPCM waveshaper                      :p3c, 2026-12, 2w

    section Phase 4 — Commercialization (2.5 months)
    .sinto format finalization                :p4a, 2027-01, 1w
    Copy protection (speed bump)              :p4b, 2027-01, 2w
    N64/PS1 preset pack                       :p4c, 2027-01, 3w
    Moog/Roland preset packs                  :p4d, 2027-02, 4w
    Standalone instrument app                 :p4e, 2027-03, 3w
    Thermal stress test + game integration    :p4f, 2027-03, 2w
```

---

## 12. Risks and Mitigations

Gemini Rounds 1–4 fully applied. Final version.

| Risk | Severity | Mitigation |
|---|---|---|
| **Multi-thread race condition / crash** | **Critical** | **Custom SPSC ring buffer (Phase 1 first task)** |
| **Filter divergence (loud explosion)** | **Critical** | **Huovilainen + clamp + TanhFast** |
| **Denormal CPU spike** | **Critical** | **Alternating DC offset injection in all IIR loops** |
| **GPL v2 vs commercial conflict** | **Critical** | **Changed to MIT** |
| Voice Stealing click noise | High | Quick Release (5–10ms) — mandatory |
| Cache miss CPU spike | High | LUT vs polynomial decided by device profile |
| 16-voice shortage | High | Dynamic 16–32 voices + priority stealing + drum protection |
| Thermal throttling underrun | High | Dynamic Voice Scaling (32→24→16 auto-switch) |
| Unity pause vs DSP time divergence | High | Pause / Resume control events |
| Unity / Oboe latency confusion | High | Explicit per-use-case path documentation |
| AES-256 overconfidence | Medium | Speed bump framing — 2-week implementation cap |
| Oboe device-specific bugs | Medium | Auto buffer size adjustment + fallback |
| Preset production cost underestimate | Medium | Pilot production for data collection |
| "Quyno" read as "Kuino" (悔い = regret) in Japanese | Low | Brand guide specifies pronunciation "Kyoo-no" |

---

## Summary

```mermaid
graph LR
    subgraph Confirmed Design
        D1[MIT License]
        D2[Custom SPSC ring buffer]
        D3[GC-zero 7 principles]
        D4[Huovilainen + clamp]
        D5[Quick Release stealing]
        D6[N64 NN + PS1 ADPCM]
        D7[3D audio → Unity]
        D8[Denormal DC injection]
        D9[Pause control event]
        D10[Dynamic Voice Scaling]
    end
    subgraph Value
        V1[Authentic N64/PS1 aesthetic]
        V2[Single codebase · all platforms]
        V3[Preset DLC revenue model]
        V4[Stable real-device performance]
    end
    D1 & D2 & D3 --> V2
    D4 & D5 & D6 --> V1
    D7 --> V3
    D8 & D9 & D10 --> V4
```

### Three Repositories

```
Koleco  ─────────────────────────────  Portfolio site
                                              │
Quyno  ──── defines song structure ───┐       │
                                      ├── Game ┤
Sinto  ──── defines timbre ────────────┘       │
              │                                │
              └──── Standalone instrument app ─┘
                         │
                         └── MIDI keyboard performance
```

### Phase 1 — First Three Tasks

```
1. Initialize Quyno / Sinto repositories with MIT license
2. Implement AudioRingBuffer<ControlEvent> (including Pause / Resume)
3. Implement both LUT and polynomial Sin/Tanh and benchmark on Android device
```

**Design phase complete. Write code.**

---

*© STUDIO MeowToon — MIT License*  
*project_proposal_v1.md — translated from quyno_sinto_proposal_v050.md*  
*Reviewed by Gemini (Rounds 1–4)*
