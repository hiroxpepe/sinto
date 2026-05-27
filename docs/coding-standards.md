# Sinto — Coding Standards

> **Sinto**: Pure C# Software Synthesizer Engine for Germio  
> A real-time DSP library that runs standalone via dotnet test and integrates with Unity via asmdef.

## Build & Test

### Running Tests
```sh
dotnet restore Sinto.sln
dotnet build Sinto.sln
dotnet test "Tests~/EditModeTests/Sinto.Tests.EditMode.csproj"
```

**Single test by class:**
```sh
dotnet test "Tests~/EditModeTests/Sinto.Tests.EditMode.csproj" --filter "FullyQualifiedName~EngineTests"
```

**Exclude benchmarks:**
```sh
dotnet test "Tests~/EditModeTests/Sinto.Tests.EditMode.csproj" --filter "Category!=Benchmark"
```

Test structure:
- `.NET 8.0` target framework with `<LangVersion>12</LangVersion>`
- Tests compile shared sources from `Scripts/`
- Fake time provider in `Tests~/MiniUnity/FakeTimeProvider.cs`
- **No Unity API in tests** — Unity integration tested via asmdef in Germio

---

## High-Level Architecture

### Namespace & Directory Structure

```
Sinto.Core.Audio      ← Thread communication (RingBuffer / Event / Denormal)
  ↓ depends on nothing

Sinto.Core.Synth      ← Voice synthesis (Engine / Voice / Oscillator / Envelope / Filter / Lfo)
  ↓ depends on Audio

Sinto.Core.Effects    ← Effects processing (Chorus / Reverb / Delay / Retro / Effects)
  ↓ depends on Synth

Sinto.Core.Preset     ← Data persistence (Preset / Loader / Validator)
  ↓ depends on Synth + Effects
```

**Strict rule**: dependency flows downward only. `Audio → Synth` forbidden. `Preset → Audio` forbidden.

### Directory Layout

```
Scripts/                        ← Unity recognizes this directory
  Sinto.Core.csproj             ← for dotnet test (standalone)
  Sinto.Core.asmdef             ← for Unity (Germio integration)
  Audio/
    RingBuffer.cs               ← AudioRingBuffer<T>
    Event.cs                    ← ControlEvent + EventKind enum
    Denormal.cs                 ← DenormalGuard
  Synth/
    Enums.cs                    ← WaveType / PlayState / Interpolation / FilterKind / RetroMode / LfoWave / LfoTarget
    Calc.cs                     ← SintoMath (fast DSP math)
    Smoother.cs                 ← SmoothedParameter
    Note.cs
    OscParams.cs                ← OscillatorParams
    Oscillator.cs               ← OscillatorState
    EnvParams.cs                ← EnvelopeParams
    Envelope.cs                 ← EnvelopeState
    LfoParams.cs
    Lfo.cs                      ← LFOState
    Portamento.cs               ← PortamentoState
    Filter.cs                   ← FilterState (Moog + Roland, struct + switch)
    TrackConfig.cs              ← VoiceConfig
    Voice.cs
    Voices.cs                   ← VoiceManager
    Scaler.cs                   ← VoiceScaler
    Engine.cs                   ← SintoEngine
    MicroEngine.cs              ← SintoMicroEngine
    ITimer.cs                   ← ITimer + SystemTimer + FakeTimeProvider
  Effects/
    IEffect.cs
    MonoEffect.cs               ← MonoCompatibleEffect (abstract)
    Chorus.cs                   ← BBDChorus
    Reverb.cs                   ← FreeverbReverb
    Delay.cs                    ← StereoDelay
    Retro.cs                    ← RetroFilter
    Effects.cs                  ← EffectsChain
  Preset/
    Preset.cs                   ← SintoPreset
    OscPreset.cs                ← OscillatorPreset
    FilterPreset.cs
    EnvPreset.cs                ← EnvelopePreset
    LfoPreset.cs
    FxPreset.cs                 ← EffectsPreset
    Validator.cs                ← PresetValidator
    Loader.cs                   ← PresetLoader

Tests~/                         ← Unity ignores folders ending with ~
  EditModeTests/
    Sinto.Tests.EditMode.csproj
    Sinto.Tests.EditMode.asmdef
    *Tests.cs
  MiniUnity/
    FakeTimeProvider.cs         ← ITimer implementation for deterministic tests
```

---

## Class Name Reference

### Audio (`Sinto.Core.Audio`)

| Class | Kind | Description |
|---|---|---|
| `RingBuffer<T>` | class | SPSC lock-free ring buffer |
| `Event` | struct | Control event (NoteOn / Pause / SetBPM …) |
| `EventKind` | enum | Event type discriminator |
| `Denormal` | static class | IIR subnormal protection |

### Synth (`Sinto.Core.Synth`)

| Class | Kind | Description |
|---|---|---|
| `Engine` | sealed class | Main synthesizer engine |
| `MicroEngine` | sealed class | Lightweight SFX-only engine |
| `Calc` | static class | Fast DSP math (SinFast / TanhFast / PitchRatioFast) |
| `Smoother` | struct | One-pole lowpass parameter smoothing |
| `Note` | readonly struct | MIDI note + velocity + track |
| `OscParams` | readonly struct | Oscillator parameters |
| `Oscillator` | struct | Oscillator state (phase accumulator) |
| `EnvParams` | readonly struct | Envelope parameters (ADSR) |
| `Envelope` | struct | Envelope state machine |
| `LfoParams` | readonly struct | LFO parameters |
| `Lfo` | struct | LFO state |
| `Portamento` | struct | Portamento glide state |
| `Filter` | struct | Filter state (Moog + Roland, struct + switch) |
| `Voice` | struct | Single synthesizer voice |
| `TrackConfig` | readonly struct | Per-track voice allocation config |
| `Voices` | sealed class | Voice manager (polyphony) |
| `Scaler` | sealed class | Dynamic voice scaling |
| `ITimer` | interface | Time abstraction for testability |
| `SystemTimer` | sealed class | Production ITimer (Stopwatch) |
| `WaveType` | enum | Oscillator waveform |
| `PlayState` | enum | Voice play state |
| `Interpolation` | enum | Sample interpolation mode |
| `FilterKind` | enum | Filter algorithm (Roland / Moog) |
| `RetroMode` | enum | Retro degradation mode |
| `LfoWave` | enum | LFO waveform |
| `LfoTarget` | flags enum | LFO modulation destination |

### Effects (`Sinto.Core.Effects`)

| Class | Kind | Description |
|---|---|---|
| `IEffect` | interface | Effect processor contract |
| `MonoEffect` | abstract class | Mono-compatible effect base |
| `Chorus` | sealed class | BBD chorus |
| `Reverb` | sealed class | Freeverb reverb |
| `Delay` | sealed class | Stereo delay |
| `Retro` | sealed class | Retro filter (N64 / PS1) |
| `Effects` | sealed class | Serial effects chain |

### Preset (`Sinto.Core.Preset`)

| Class | Kind | Description |
|---|---|---|
| `Preset` | sealed class | Full synthesizer preset |
| `OscPreset` | sealed class | Oscillator preset data |
| `FilterPreset` | sealed class | Filter preset data |
| `EnvPreset` | sealed class | Envelope preset data |
| `LfoPreset` | sealed class | LFO preset data |
| `FxPreset` | sealed class | Effects preset data |
| `Validator` | static class | Preset parameter clamping |
| `Loader` | static class | .sinto JSON file loading |

---

## Naming Conventions (Stemic v2.2)

| Category | Rule | Example |
|---|---|---|
| **Class names** | Single word, no project prefix | `Engine` (not `SintoEngine`) |
| **Data class properties** | `snake_case` (matches JSON keys) | `amp_envelope`, `lfo1` |
| **Non-data public properties** | `camelCase` | `isActive`, `currentBpm` |
| **Private fields** | `_snake_case` | `_sample_rate`, `_is_paused` |
| **Local variables** | `snake_case` | `raw_freq`, `buf_size` |
| **Constants** | `ALL_CAPS` | `SAMPLE_RATE`, `MAX_VOICES` |
| **Method calls** | Always use named parameters | `SetParams(cutoff: 0.5f, resonance: 0.3f)` |

**JSON serialization**: Property names are JSON keys directly. **No `[JsonProperty]` attributes.**

---

## Code Style

- **XML doc comments** on all public classes and methods (`///`)
- **Author tag**: Always `<author>h.adachi (STUDIO MeowToon)</author>`
- **`#nullable enable`** at the top of each file
- **No blank lines inside method bodies** — compact, readable
- **Early returns** for validation — no deeply nested blocks
- **No LINQ in hot paths** — use explicit loops
- **No `Math.Clamp`** — use `MathF.Min(MathF.Max(...))` (NaN branch blocks SIMD)
- **No `ConcurrentQueue<T>`** — internal GC allocation on audio thread
- **No `static` DSP buffers** — data race between Engine and MicroEngine instances
- **No `lock` on audio thread** — use SPSC ring buffer + `Interlocked` only

---

## Test File Organization

```
Tests~/EditModeTests/
  Audio/
    RingBufferTests.cs
    EventTests.cs
    DenormalTests.cs
  Synth/
    CalcTests.cs
    SmootherTests.cs
    NoteTests.cs
    OscParamsTests.cs
    OscillatorTests.cs
    EnvParamsTests.cs
    EnvelopeTests.cs
    LfoParamsTests.cs
    LfoTests.cs
    PortamentoTests.cs
    FilterTests.cs
    VoiceTests.cs
    TrackConfigTests.cs
    VoicesTests.cs
    ScalerTests.cs
    EngineTests.cs
    MicroEngineTests.cs
  Effects/
    ChorusTests.cs
    ReverbTests.cs
    DelayTests.cs
    RetroTests.cs
    EffectsTests.cs
  Preset/
    PresetTests.cs
    ValidatorTests.cs
    LoaderTests.cs
```

**Test method naming**: `ClassName_Feature_ExpectedBehavior`
Example: `Engine_Pause_ZeroesBuffer()`

**Assertion pattern**: NUnit fluent API
```csharp
Assert.That(value, Is.EqualTo(expected).Within(0.001f));
Assert.That(result, Is.Not.Null);
```

---

## Critical DSP Rules

- **`ref var v = ref _voices[i]`** — always use ref for struct array mutation
- **`Span<float>` for all audio buffers** — zero-copy from Unity `float[]` and Oboe `float*`
- **`buffer.Clear()`** not `Array.Clear(buffer)` — Span has native Clear()
- **Denormal protection** — `Denormal.Protect()` in all IIR feedback loops
- **SnapToTarget on NoteOn** — prevents "pyun" transient artifact on stolen voices
- **Zero-attack bypass** — `if (attack <= 0f) { level = 1f; phase = Decay; }` not clamp
- **Moog resonance ×4** — user [0,1] → internal [0, 3.99] before clamp
- **Sample & Hold downsampling** — never shrink buffer; hold each sample N times

---

## Assembly & Namespace

**Scripts assembly** (`Scripts/Sinto.Core.asmdef`):
- Platform: all (Unity + standalone)
- Allow unsafe: true
- Namespaces: `Sinto.Core.*`

**Test project** (`Tests~/EditModeTests/Sinto.Tests.EditMode.csproj`):
- Shares source compilation of `Scripts/**/*.cs`
- Namespace: `Sinto.Tests.*`
- Never imports compiled assembly — uses shared source

---

## References

- **docs/project_proposal_v1.md**: Vision and business model
- **docs/development_plan_v1.md**: Roadmap and task breakdown
- **docs/synthesizer_spec_v1.md**: Full synthesizer specification
- **docs/class_and_method_design_v1.md**: Class and method design (pre-refactor reference)
- **Germio repo**: Unity game project; references Sinto via asmdef

---

*© STUDIO MeowToon — MIT License*
