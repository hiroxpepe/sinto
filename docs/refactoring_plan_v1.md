# Sinto Refactoring Plan v1

> **Status:** Pending — awaiting .NET SDK in the execution environment  
> **Standard reference:** `docs/coding-standards.md` (Stemic v2.2)  
> **Scope:** All files under `Scripts/`, `Tests~/`, `Audition~/`

---

## Environment Note

The refactoring tasks below have been **fully analyzed** but **not yet applied**.
`dotnet` SDK is not installed in the current execution environment.
Run `dotnet test "Tests~/EditModeTests/Sinto.Tests.EditMode.csproj"` before and after
applying changes to confirm no regressions.

---

## 1. Remove All `private` Keywords

> **User instruction:** Remove every `private` keyword throughout the codebase.  
> In C#, class/struct members without an access modifier default to `private`, so removal is safe.  
> **Exception:** `public static bool IsLutMode { get; private set; }` in `Calc.cs` —
> removing `private` from the setter makes it a fully public setter; this is intentional.

### Files and counts

| File | Items |
|---|---|
| `Scripts/Audio/Denormal.cs` | 3 `private const` |
| `Scripts/Audio/RingBuffer.cs` | 6 `private` fields |
| `Scripts/Synth/Calc.cs` | 3 `private const`, 2 `private static` fields, 1 `private set` |
| `Scripts/Synth/Engine.cs` | 11 `private` fields, 2 `private` methods |
| `Scripts/Synth/Voices.cs` | 18 `private` fields, 2 `private` methods |
| `Scripts/Synth/Voice.cs` | 4 `private` fields |
| `Scripts/Synth/MicroEngine.cs` | 8 `private` fields |
| `Scripts/Synth/Scaler.cs` | 1 `private static readonly` array, 5 `private const`, 6 `private` fields |
| `Scripts/Synth/Smoother.cs` | 2 `private const`, 4 `private` fields |
| `Scripts/Synth/Portamento.cs` | 3 `private` fields |
| `Scripts/Synth/Envelope.cs` | 7 `private` fields |
| `Scripts/Synth/Filter.cs` | 2 `private const`, 8 `private` fields, 2 `private` methods |
| `Scripts/Synth/Oscillator.cs` | 2 `private const`, 4 `private` fields, 1 `private static` method |
| `Scripts/Synth/Lfo.cs` | 2 `private const`, 6 `private` fields |
| `Scripts/Effects/Chorus.cs` | 6 `private` fields, 1 `private` method |
| `Scripts/Effects/Reverb.cs` | 2 `private static readonly` arrays, 8 `private` fields |
| `Scripts/Effects/Delay.cs` | 9 `private` fields |
| `Scripts/Effects/Retro.cs` | 2 `private` fields, 1 `private static` method |
| `Scripts/Preset/Loader.cs` | 1 `private static readonly` field, 1 `private static` method |
| `Scripts/Preset/Validator.cs` | 6 `private static` methods |
| `Tests~/MiniUnity/FakeTimeProvider.cs` | 1 `private` field |

**Instruction:** In every listed file, delete the word `private` (and the space after it)
from every field, constant, and method declaration. Leave access modifiers on constructors
and public/internal/protected members unchanged.

---

## 2. Rename Non-Data Public Properties to `camelCase`

> **Standard:** Non-data public properties must use `camelCase` (e.g. `isActive`, `currentBpm`).

### Renames required

| File | Old name → New name |
|---|---|
| `Scripts/Synth/Engine.cs` | `ActiveVoices` → `activeVoices` |
| | `CurrentMaxVoices` → `currentMaxVoices` |
| | `IsPaused` → `isPaused` |
| | `CurrentBpm` → `currentBpm` |
| | `DspTimeSamples` → `dspTimeSamples` |
| `Scripts/Synth/Voices.cs` | `MaxVoices` → `maxVoices` |
| | `ActiveVoices` → `activeVoices` |
| `Scripts/Synth/Scaler.cs` | `CurrentMaxVoices` → `currentMaxVoices` |
| | `CurrentTierIndex` → `currentTierIndex` |
| `Scripts/Synth/MicroEngine.cs` | `IsActive` → `isActive` |
| `Scripts/Synth/Envelope.cs` | `Level` → `level` |
| | `Phase` → `phase` |
| | `IsDone` → `isDone` |
| `Scripts/Synth/Portamento.cs` | `CurrentFrequency` → `currentFrequency` |
| | `IsGliding` → `isGliding` |
| `Scripts/Synth/Smoother.cs` | `Current` → `current` |
| | `Target` → `target` |
| `Scripts/Synth/Voice.cs` | `CurrentAmplitude` → `currentAmplitude` |
| `Scripts/Synth/Calc.cs` | `IsLutMode` → `isLutMode` |
| `Scripts/Synth/ITimer.cs` | `Frequency` → `frequency` (interface + `SystemTimer`) |
| `Scripts/Effects/IEffect.cs` | `Enabled` → `enabled` |
| `Scripts/Effects/MonoEffect.cs` | `MonoCompatible` → `monoCompatible` |
| | `Enabled` → `enabled` |
| `Scripts/Effects/Effects.cs` | `MonoCompatible` → `monoCompatible` |
| `Scripts/Effects/Chorus.cs` | `Mode` → `mode` |
| | `Rate` → `rate` |
| | `Depth` → `depth` |
| | `Mix` → `mix` |
| `Scripts/Effects/Reverb.cs` | `RoomSize` → `roomSize` |
| | `Damping` → `damping` |
| | `Mix` → `mix` |
| `Scripts/Effects/Delay.cs` | `Time` → `time` |
| | `Feedback` → `feedback` |
| | `Mix` → `mix` |
| | `TempoSync` → `tempoSync` |
| | `Bpm` → `bpm` |
| | `SyncNote` → `syncNote` |
| | `Enabled` → `enabled` |
| `Scripts/Effects/Retro.cs` | `Mode` → `mode` |
| | `Enabled` → `enabled` |

### Callers that must be updated after the renames above

| File | References to update |
|---|---|
| `Tests~/EditModeTests/EngineTests.cs` | `e.IsPaused` → `e.isPaused`, `e.ActiveVoices` → `e.activeVoices`, `e.CurrentBpm` → `e.currentBpm`, `e.DspTimeSamples` → `e.dspTimeSamples` |
| `Tests~/EditModeTests/VoicesTests.cs` | `vm.ActiveVoices` → `vm.activeVoices`, `vm.MaxVoices` → `vm.maxVoices` |
| `Tests~/EditModeTests/ScalerTests.cs` | `vs.CurrentTierIndex` → `vs.currentTierIndex`, `vs.CurrentMaxVoices` → `vs.currentMaxVoices` |
| `Tests~/EditModeTests/EnvelopeTests.cs` | `e.Phase` → `e.phase`, `e.Level` → `e.level`, `e.IsDone` → `e.isDone` |
| `Tests~/EditModeTests/PortamentoTests.cs` | `p.IsGliding` → `p.isGliding`, `p.CurrentFrequency` → `p.currentFrequency` |
| `Tests~/EditModeTests/SmootherTests.cs` | `sp.Current` → `sp.current`, `sp.Target` → `sp.target` |
| `Tests~/EditModeTests/VoiceTests.cs` | `v.CurrentAmplitude` → `v.currentAmplitude`, `v.SmoothedCutoff.Current` → `v.SmoothedCutoff.current`, `v.SmoothedCutoff.Target` → `v.SmoothedCutoff.target`, `v.Portamento.IsGliding` → `v.Portamento.isGliding` |
| `Tests~/EditModeTests/EffectsTests.cs` | `c.Mix` → `c.mix`, `c.Enabled` → `c.enabled`, `r.Mix` → `r.mix`, `r.RoomSize` → `r.roomSize`, `r.Damping` → `r.damping`, `r.Enabled` → `r.enabled`, `ec.Retro.Mode` → `ec.Retro.mode`, `ec.Retro.Enabled` → `ec.Retro.enabled` |
| `Tests~/EditModeTests/DelayTests.cs` | `d.Time` → `d.time`, `d.Feedback` → `d.feedback`, `d.Mix` → `d.mix`, `d.Enabled` → `d.enabled` |
| `Tests~/EditModeTests/MicroEngineTests.cs` | `m.IsActive` → `m.isActive` |
| `Tests~/MiniUnity/FakeTimeProvider.cs` | `Frequency` → `frequency` |
| `Scripts/Synth/Scaler.cs` (internal) | `_time.Frequency` → `_time.frequency` |
| `Tests~/EditModeTests/VoicesTests.cs` | `vm.SmoothedCutoff.Current` → `vm.SmoothedCutoff.current` (line 267 comment; actual usage needs check) |
| `Scripts/Effects/Effects.cs` (internal) | `Chorus.Enabled` → `Chorus.enabled`, `Reverb.Enabled` → `Reverb.enabled`, `Delay.Enabled` → `Delay.enabled`, `Retro.Enabled` → `Retro.enabled`, `Chorus.MonoCompatible` → `Chorus.monoCompatible`, `Reverb.MonoCompatible` → `Reverb.monoCompatible` |
| `Scripts/Synth/Voice.cs` (internal) | `AmpEnvelope.IsDone` → `AmpEnvelope.isDone`, `AmpEnvelope.Tick()` return used with `isDone` |
| `Scripts/Preset/Validator.cs` | `Delay.SetBPM` uses `Bpm`/`TempoSync`/`SyncNote` (internal to Delay.cs — update there) |
| `Audition~/MainWindow.xaml.cs` | No direct property references to the renamed props (uses method calls only) |

---

## 3. Rename Data Class Properties to `snake_case`

> **Standard:** Data class properties (those that map to JSON keys) must use `snake_case`.

### `Scripts/Preset/Preset.cs`

| Old | New |
|---|---|
| `Name` | `name` |
| `Version` | `version` |
| `Osc1` | `osc1` |
| `Osc2` | `osc2` |
| `Filter` | `filter` |
| `AmpEnvelope` | `amp_envelope` |
| `FilterEnvelope` | `filter_envelope` |
| `PitchEnvelope` | `pitch_envelope` |
| `Lfo1` | `lfo1` |
| `Lfo2` | `lfo2` |
| `PortamentoTime` | `portamento_time` |
| `Effects` | `effects` |
| `RetroMode` | `retro_mode` |

### `Scripts/Preset/OscPreset.cs`

| Old | New |
|---|---|
| `Wave` | `wave` |
| `Interp` | `interp` |
| `DetuneCents` | `detune_cents` |
| `PulseWidth` | `pulse_width` |
| `Level` | `level` |

### `Scripts/Preset/FilterPreset.cs`

| Old | New |
|---|---|
| `Mode` | `mode` |
| `Cutoff` | `cutoff` |
| `Resonance` | `resonance` |
| `EnvAmt` | `env_amt` |
| `KeyFollow` | `key_follow` |

### `Scripts/Preset/EnvPreset.cs`

| Old | New |
|---|---|
| `Attack` | `attack` |
| `Decay` | `decay` |
| `Sustain` | `sustain` |
| `Release` | `release` |

### `Scripts/Preset/LfoPreset.cs`

| Old | New |
|---|---|
| `Wave` | `wave` |
| `RateOrSync` | `rate_or_sync` |
| `Depth` | `depth` |
| `TempoSync` | `tempo_sync` |
| `Destinations` | `destinations` |

### `Scripts/Preset/FxPreset.cs`

| Old | New |
|---|---|
| `ChorusMode` | `chorus_mode` |
| `ChorusRate` | `chorus_rate` |
| `ChorusDepth` | `chorus_depth` |
| `ChorusMix` | `chorus_mix` |
| `ReverbRoomSize` | `reverb_room_size` |
| `ReverbDamping` | `reverb_damping` |
| `ReverbMix` | `reverb_mix` |
| `DelayTime` | `delay_time` |
| `DelayFeedback` | `delay_feedback` |
| `DelayMix` | `delay_mix` |
| `DelayTempoSync` | `delay_tempo_sync` |
| `RetroMode` | `retro_mode` |

### Callers that must be updated after the Preset renames

| File | Notes |
|---|---|
| `Scripts/Preset/Validator.cs` | All property accesses in `ValidateOsc`, `ValidateFilter`, `ValidateEnvelope`, `ValidateLFO`, `ValidateEffects`, and the `Validate` top-level method |
| `Tests~/EditModeTests/PresetTests.cs` | `OscPreset.Default.Level` → `.level`, `FilterPreset.Default.Mode` → `.mode`, `LfoPreset.Default.Depth` → `.depth`, `LfoPreset.Default.TempoSync` → `.tempo_sync` |
| `Tests~/EditModeTests/OscParamsTests.cs` | `p.Level` — this is `OscParams.Level` (a params struct, not a preset), **not affected** |
| `Tests~/EditModeTests/LFOParamsTests.cs` | `p.Depth`, `p.TempoSync` — these are `LfoParams` fields, **not affected** |
| `Scripts/Synth/Voice.cs` | Uses `Osc1Params.DetuneCents`, `Osc1Params.PulseWidth`, etc. — these are `OscParams` (params struct), **not affected** |

> **Note:** `OscParams`, `EnvParams`, `LfoParams` are separate value-type structs in `Scripts/Synth/`.
> They are **not** preset classes and are **not** renamed here.
> Only the classes in `Scripts/Preset/` are data classes subject to `snake_case` renaming.

---

## 4. Remove Duplicate `#nullable enable` Directives

> **Standard:** `#nullable enable` belongs at the top of the file (file-scoped).
> A second `#nullable enable` inside the class/struct body is redundant and must be removed.

### Files with duplicate directive (remove the one inside the type body)

`Scripts/Audio/Denormal.cs`,
`Scripts/Synth/Engine.cs`,
`Scripts/Synth/Voices.cs`,
`Scripts/Synth/Calc.cs`,
`Scripts/Synth/Oscillator.cs`,
`Scripts/Synth/Envelope.cs`,
`Scripts/Synth/Filter.cs`,
`Scripts/Synth/Lfo.cs`,
`Scripts/Synth/MicroEngine.cs`,
`Scripts/Synth/Scaler.cs`,
`Scripts/Synth/Smoother.cs`,
`Scripts/Preset/Validator.cs`,
`Scripts/Preset/Loader.cs`,
`Scripts/Effects/Effects.cs`,
`Scripts/Effects/Chorus.cs`,
`Scripts/Effects/Reverb.cs`,
`Scripts/Effects/MonoEffect.cs`

**Total: 17 files**

---

## 5. Add Missing Copyright Headers, XML Doc Comments, and Author Tags

> **Standard:** Every public class/struct/interface requires:
> 1. Copyright header at top of file
> 2. `/// <summary>...</summary>` on the type declaration
> 3. `/// <author>h.adachi (STUDIO MeowToon)</author>` after the summary

### Files missing one or more of the above

| File | Missing |
|---|---|
| `Scripts/Preset/OscPreset.cs` | Copyright header, `/// <summary>`, author tag |
| `Scripts/Preset/FilterPreset.cs` | Copyright header, `/// <summary>`, author tag |
| `Scripts/Preset/EnvPreset.cs` | Copyright header, `/// <summary>`, author tag |
| `Scripts/Preset/LfoPreset.cs` | Copyright header, `/// <summary>`, author tag |
| `Scripts/Preset/FxPreset.cs` | Copyright header, `/// <summary>`, author tag |
| `Scripts/Audio/RingBuffer.cs` | `/// <summary>`, author tag |
| `Scripts/Audio/Event.cs` | `/// <summary>` on enum and struct, author tag |
| `Scripts/Synth/TrackConfig.cs` | `/// <summary>`, author tag |
| `Scripts/Synth/Enums.cs` | `/// <summary>` on all enums, author tag |
| `Scripts/Synth/ITimer.cs` | Author tag on `ITimer` and `SystemTimer` |
| `Scripts/Effects/IEffect.cs` | `/// <summary>`, author tag |

### Copyright header template (copy from any existing file)

```
// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
```

### XML doc template

```csharp
/// <summary>One-line description.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Foo { ... }
```

---

## 6. Remove Blank Lines Inside Method Bodies

> **Standard:** No blank lines inside method bodies.

### Violations found

| File | Method | Blank lines inside body |
|---|---|---|
| `Scripts/Synth/Voice.cs` | `Tick(...)` | 8 blank lines (before `// ──` section comments on lines 103, 109, 113, 123, 133, 145, 164 area) |

**Instruction:** Delete all blank lines within the `Tick` method body in `Voice.cs`.
The `// ──` section separator comments may remain; only the blank lines before them are removed.

---

## 7. Summary Table

| Category | Files affected | Items |
|---|---|---|
| Remove `private` | 21 | ~140 occurrences |
| Rename to `camelCase` | 18 + 14 caller files | ~60 renames |
| Rename to `snake_case` (Preset) | 6 + 3 caller files | ~45 renames |
| Remove duplicate `#nullable enable` | 17 | 17 occurrences |
| Add copyright / doc / author | 11 | ~30 additions |
| Remove blank lines in method bodies | 1 | 8 blank lines |

---

## 8. Recommended Execution Order

1. **`private` removal** — purely additive deletion, lowest risk, no callers affected.
2. **`#nullable enable` deduplication** — mechanical removal, no logic change.
3. **camelCase property renames** — rename definition first, then update all callers in the same pass. Run tests after this step.
4. **snake_case preset property renames** — rename definition first, then `Validator.cs`, then test files. Run tests after this step.
5. **Missing docs/headers** — cosmetic, no compile impact.
6. **Blank line removal in `Voice.Tick`** — cosmetic, no logic change.

---

## 9. Test Command

```
dotnet test "Tests~/EditModeTests/Sinto.Tests.EditMode.csproj"
```

Run before and after all changes. All tests must pass in both runs.
