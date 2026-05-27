// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Preset;

/// <summary>Clamp all preset parameters to valid ranges on load.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public static class Validator {
#nullable enable
    public static Preset Validate(Preset raw) {
        if (raw == null) return Preset.Default;
        return new Preset {
            Name           = raw.Name ?? "Unnamed",
            Version        = raw.Version ?? "1.0",
            Osc1           = ValidateOsc(raw.Osc1),
            Osc2           = ValidateOsc(raw.Osc2),
            Filter         = ValidateFilter(raw.Filter),
            AmpEnvelope    = ValidateEnvelope(raw.AmpEnvelope),
            FilterEnvelope = ValidateEnvelope(raw.FilterEnvelope),
            PitchEnvelope  = ValidateEnvelope(raw.PitchEnvelope),
            Lfo1           = ValidateLFO(raw.Lfo1),
            Lfo2           = ValidateLFO(raw.Lfo2),
            PortamentoTime = Clamp(raw.PortamentoTime, 0f, 5f),
            Effects        = ValidateEffects(raw.Effects),
            RetroMode      = raw.RetroMode,
        };
    }

    private static OscPreset ValidateOsc(OscPreset? raw) {
        if (raw == null) return OscPreset.Default;
        return new OscPreset {
            Wave        = raw.Wave,
            Interp      = raw.Interp,
            DetuneCents = Clamp(raw.DetuneCents, -100f, 100f),
            PulseWidth  = Clamp(raw.PulseWidth, 0.01f, 0.99f),
            Level       = Clamp(raw.Level, 0f, 1f),
        };
    }

    private static FilterPreset ValidateFilter(FilterPreset? raw) {
        if (raw == null) return FilterPreset.Default;
        return new FilterPreset {
            Mode      = raw.Mode,
            Cutoff    = Clamp(raw.Cutoff, 0.001f, 0.999f),
            Resonance = Clamp(raw.Resonance, 0f, 1f),
            EnvAmt    = Clamp(raw.EnvAmt, -1f, 1f),
            KeyFollow = Clamp(raw.KeyFollow, 0f, 1f),
        };
    }

    private static EnvPreset ValidateEnvelope(EnvPreset? raw) {
        if (raw == null) return EnvPreset.Default;
        return new EnvPreset {
            Attack  = Clamp(raw.Attack,  0.001f, 10f),
            Decay   = Clamp(raw.Decay,   0.001f, 10f),
            Sustain = Clamp(raw.Sustain, 0f, 1f),
            Release = Clamp(raw.Release, 0.001f, 20f),
        };
    }

    private static LfoPreset ValidateLFO(LfoPreset? raw) {
        if (raw == null) return LfoPreset.Default;
        return new LfoPreset {
            Wave         = raw.Wave,
            RateOrSync   = Clamp(raw.RateOrSync, 0.001f, 20f),
            Depth        = Clamp(raw.Depth, 0f, 1f),
            TempoSync    = raw.TempoSync,
            Destinations = raw.Destinations,
        };
    }

    private static FxPreset ValidateEffects(FxPreset? raw) {
        if (raw == null) return FxPreset.Default;
        return new FxPreset {
            ChorusMode     = raw.ChorusMode,
            ChorusRate     = Clamp(raw.ChorusRate, 0f, 20f),
            ChorusDepth    = Clamp(raw.ChorusDepth, 0f, 1f),
            ChorusMix      = Clamp(raw.ChorusMix, 0f, 1f),
            ReverbRoomSize = Clamp(raw.ReverbRoomSize, 0f, 1f),
            ReverbDamping  = Clamp(raw.ReverbDamping, 0f, 1f),
            ReverbMix      = Clamp(raw.ReverbMix, 0f, 1f),
            DelayTime      = Clamp(raw.DelayTime, 0.001f, 2f),
            DelayFeedback  = Clamp(raw.DelayFeedback, 0f, 0.95f),
            DelayMix       = Clamp(raw.DelayMix, 0f, 1f),
            DelayTempoSync = raw.DelayTempoSync,
            RetroMode      = raw.RetroMode,
        };
    }

    // Custom clamp — Math.Clamp blocks SIMD due to NaN branch
    private static float Clamp(float v, float min, float max) {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
