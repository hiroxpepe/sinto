// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Preset;

/// <summary>
/// Clamp all preset parameters to valid ranges on load.
/// NEVER trust raw file data — always validate before passing to SintoEngine.
/// Uses MathF.Min(MathF.Max()) — NOT Math.Clamp (NaN branch blocks SIMD).
/// </summary>
public static class PresetValidator
{
    /// <summary>Validate and clamp all fields. Returns a new preset — never mutates input.</summary>
    public static SintoPreset Validate(SintoPreset raw)
        => throw new System.NotImplementedException();

    private static OscillatorPreset ValidateOsc(OscillatorPreset raw)
        => throw new System.NotImplementedException();

    private static FilterPreset ValidateFilter(FilterPreset raw)
        => throw new System.NotImplementedException();

    private static EnvelopePreset ValidateEnvelope(EnvelopePreset raw)
        => throw new System.NotImplementedException();

    private static LFOPreset ValidateLFO(LFOPreset raw)
        => throw new System.NotImplementedException();

    private static EffectsPreset ValidateEffects(EffectsPreset raw)
        => throw new System.NotImplementedException();
}
