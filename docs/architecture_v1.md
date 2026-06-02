# Signo Audio Engine Architecture

## Overview

Signo is a multi-part VA synthesizer DSP engine designed for mobile 3D game audio (CPU priority). The architecture is inspired by the Roland VS-880EX signal flow: each part has its own channel processing, and send-return effects are shared across all parts.

---

## Core Abstraction

```
ISignal
  Process(Span<float> buffer, int channels)
  Reset()
  bool enabled
```

Everything in the audio pipeline implements `ISignal`. The three concrete roles are:

| Role | Interface | Implementations |
|---|---|---|
| Sound source | `ISynth : ISignal` | `VAEngine`, `PCMEngine`, `FMEngine` |
| Sound processor | `IEffect : ISignal` | `IInsertEffect`, `ISendEffect` |
| Signal aggregator | `Channel`, `EffectBus`, `Master` | (see below) |

---

## Signal Flow

```
┌─────────────────────────────────────────────────────┐
│  Part 1        Part 2        ...       Part 8        │
│  ISynth        ISynth                  ISynth        │
│  VAEngine      PCMEngine               FMEngine      │
│     │              │                      │          │
│  Channel 1     Channel 2           Channel 8         │
│  ChannelEq     ChannelEq           ChannelEq         │
│  Compressor    Compressor          Compressor        │
│  Waveshaper*   Waveshaper*         Waveshaper*       │
│  InsertFX*     InsertFX*           InsertFX*         │
│     │  send        │  send               │  send     │
└─────┼──────────────┼─────────────────────┼───────────┘
      └──────────────┴─────────────────────┘
                          │
                    ┌─────▼──────┐
                    │ EffectBus  │  ← shared across all parts
                    │  Chorus    │
                    │  Delay     │
                    │  Reverb    │
                    └─────┬──────┘
                          │ return
                    ┌─────▼──────┐
                    │   Master   │
                    │  MasterEq  │
                    │  Limiter   │
                    └─────┬──────┘
                          │
                        Output
```

`*` = zero or more instances, chained in series (like BOSS compact pedals)

---

## Interfaces

### ISignal (base)
```csharp
interface ISignal {
    void  Process(Span<float> buffer, int channels);
    void  Reset();
    bool  enabled { get; set; }
}
```

### ISynth : ISignal
```csharp
interface ISynth : ISignal {
    bool SendNoteOn(int midiNote, float velocity, int trackId, int priority, ushort offsetFrames);
    bool SendNoteOff(int midiNote, int trackId, ushort offsetFrames);
}
```

### IEffect : ISignal
```csharp
interface IEffect : ISignal { }

interface IInsertEffect : IEffect {
    float Send { get; set; }
}

interface ISendEffect : IEffect { }
```

---

## Sound Sources (ISynth)

| Class | Description | Status |
|---|---|---|
| `VAEngine` | Virtual analog synthesizer | ✅ implemented |
| `PCMEngine` | PCM sample playback | 🔲 future |
| `FMEngine` | FM synthesis | 🔲 future |

### Multi-part assignment

Up to 8 parts, each assigned any `ISynth` independently:

```csharp
ISynth[] parts = new ISynth[8];
parts[0] = new VAEngine();
parts[1] = new PCMEngine();
parts[2] = new FMEngine();
// ...
```

---

## SignoProvider

Wires `VAEngine → Channel → EffectBus` and exposes a clean API to the UI layer. Replaces direct FX calls on `VAEngine`.

```csharp
var provider = new SignoProvider(engine);
provider.NoteOn(60, 0.9f, 1, 1, 0);
provider.SetModFx(ChorusType.Flanger, rate, depth, feedback, send);
provider.SetReverbSend(0.7f);
provider.Process(buffer, offset, count, channels);
```

---

## Effects

### Insert Effects (IInsertEffect)

Applied in-place, in series. Any number can be chained per channel — like BOSS compact pedals connected with patch cables.

#### Dynamics : IInsertEffect (abstract)

| Class | Reference | Parameters |
|---|---|---|
| `Compressor` | BOSS CS-3 | threshold / ratio / attack / release / knee / makeup |
| `Limiter` | BOSS LM-2 | ceiling / release / lookahead |

Algorithm: MusicDSP feed-forward RMS/peak envelope detection, dB-domain gain computation, soft knee.

#### Equalizer : IInsertEffect (abstract)

RBJ Audio-EQ-Cookbook biquad IIR (Direct Form II Transpose). 5 mul + 4 add per band per sample.

| Class | Reference | Bands |
|---|---|---|
| `ChannelEq` | BOSS GE-7 | 5: LoShelf(80Hz) + Peak(250/800/2500Hz) + HiShelf(8kHz) |
| `MasterEq` | BOSS GE-10 | 7: LoShelf(60Hz) + Peak(125/250/500/1k/4kHz) + HiShelf(10kHz) |

#### Waveshaper : IInsertEffect

Single class, `WaveMode` selects character. Multiple instances can be chained.

| WaveMode | Character | Algorithm |
|---|---|---|
| `Tube` | Warm, even harmonics | Asymmetric tanh |
| `Tape` | Smooth compression | Symmetric tanh + cubic |
| `Transistor` | Punchy, FET-style | Hard-knee asymmetric |
| `Overdrive` | Natural breakup | Soft clip |
| `Distortion` | Aggressive | Hard clip |
| `Fuzz` | Square-wave | Full rectification |
| `None` | Transparent | Bypass |

Parameters: `drive` (0–1) / `mix` (dry–wet) / `outputGain`

#### Existing Insert Effects

| Class | Reference |
|---|---|
| `Flanger` | BOSS BF-3 (with Gate mode) |
| `Phaser` | BOSS PH-3 |
| `Tremolo` | BOSS TR-2 |
| `Vibrato` | BOSS VB-2 |
| `AutoWah` | BOSS AW-3 (SVF) |

### Send-Return Effects (ISendEffect)

Shared across all parts via `EffectBus`. CPU-efficient: one Reverb for all 8 parts.

| Class | Reference |
|---|---|
| `Chorus` | BOSS CE-5 |
| `Delay` | BOSS DD-8 (with BPM sync) |
| `Reverb` | BOSS RV-6 |

---

## Channel

Per-part signal processing. Owns insert FX chain and send amounts to EffectBus.

```
Channel
  ChannelEq      (Equalizer)       ← future
  Compressor     (Dynamics)        ← future
  Waveshaper[]   (0–N instances)   ← future
  InsertFX       (Flanger / Phaser / Tremolo / Vibrato / AutoWah)
  ReverbSend / DelaySend / ChorusSend → EffectBus
```

---

## EffectBus

Shared send-return bus. All parts send to one `EffectBus`. Return is mixed back into the output.

```
EffectBus
  Chorus   (ISendEffect)
  Delay    (ISendEffect, BPM sync)
  Reverb   (ISendEffect)
```

---

## Master

Final stage. One per engine. Placeholder for MasterEq and Limiter.

```
Master
  MasterEq   (Equalizer, 7-band)  ← future
  Limiter    (Dynamics, brick-wall) ← future
```

---

## Design Principles

1. **CPU priority** — designed for mobile 3D game audio. Every algorithm chosen for minimum CPU: DPW-2 oscillators, RBJ biquad EQ, RMS compressor, tanh saturation via `TanhFast` LUT.

2. **BOSS compact pedal philosophy** — any `IInsertEffect` can be chained in any order, any number of times. Adding a new effect type requires no changes to existing code.

3. **VS-880EX signal flow** — send-return effects (`EffectBus`) are shared across all parts. This is the key to CPU efficiency in a multi-part engine.

4. **TDD-driven** — all DSP logic is unit-tested. No waveform, filter, or effect is shipped without passing tests. Target: zero regressions.

5. **MIT license** — designed for Unity/Godot direct integration as a C# game audio engine. No FMOD/Wwise dependency.

6. **Waveform quality** — DPW-2 SAW, 2nd-order polyBLEP SQR, DC-blocked TRI, SinFast LUT (65536 entries). Band-limited synthesis competitive with VA synths of the JP-8000 era.

7. **Clean separation** — `VAEngine` owns only voice synthesis. All FX are managed by `Channel`, `EffectBus`, and `Master`. `SignoProvider` wires them together.

---

## File Structure

```
Scripts/
  Signal/
    ISignal.cs          ← base interface
    ISynth.cs           ← sound source interface
    Channel.cs          ← per-part insert FX chain + send amounts
    EffectBus.cs        ← shared send-return (Chorus/Delay/Reverb)
    Master.cs           ← final stage placeholder
    SignoProvider.cs    ← wires VAEngine → Channel → EffectBus
  Effects/
    IEffect.cs          ← IEffect / IInsertEffect / ISendEffect
    Chain.cs            ← ordered insert FX chain
    MonoEffect.cs       ← base for stereo effects
    Dynamics.cs         ← Compressor + Limiter (future)
    Equalizer.cs        ← ChannelEq + MasterEq (future)
    Waveshaper.cs       ← multi-mode waveshaper (future)
    Flanger.cs
    Phaser.cs
    Tremolo.cs
    Vibrato.cs
    AutoWah.cs
    Chorus.cs
    Delay.cs
    Reverb.cs
  Synth/
    VAEngine.cs         ← voice synthesis only (no FX)
  Audio/
    ScopeRenderer.cs
    OscilloscopeDefaults.cs
    RingBuffer.cs
```

---

## Phase Status

| Phase | Task | Status |
|---|---|---|
| 1 | Rename `IAudioEffect` → `IEffect`, `InsertFxChain` → `Chain`, `Engine` → `VAEngine` | ✅ done |
| 2 | Add `ISignal`, `ISynth`, `IEffect` interfaces | ✅ done |
| 3 | Introduce `Channel`, `EffectBus`, `Master`; decouple FX from `VAEngine` | ✅ done |
| 3.5 | Add `SignoProvider`; wire UI through provider | ✅ done |
| 4 | Add `Dynamics` (Compressor/Limiter), `Equalizer` (ChannelEq/MasterEq), `Waveshaper` | 🔲 next |
| 5 | `PCMEngine`, `FMEngine` stubs | 🔲 future |

Each phase: TDD red → green → commit. No phase breaks existing tests.
