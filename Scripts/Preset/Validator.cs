// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Preset;

/// <summary>
/// Clamp all preset parameters to valid ranges on load.
/// NEVER trust raw file data — always validate before passing to Engine.
/// Uses MathF.Min(MathF.Max()) — NOT Math.Clamp (NaN branch blocks SIMD).
/// </summary>
public static class Validator
{
    /// <summary>Validate and clamp all fields. Returns a new preset — never mutates input.</summary>
    public static Preset Validate(Preset raw)
        => throw new System.NotImplementedException();

    private static OscPreset ValidateOsc(OscPreset raw)
        => throw new System.NotImplementedException();

    private static FilterPreset ValidateFilter(FilterPreset raw)
        => throw new System.NotImplementedException();

    private static EnvPreset ValidateEnvelope(EnvPreset raw)
        => throw new System.NotImplementedException();

    private static LfoPreset ValidateLFO(LfoPreset raw)
        => throw new System.NotImplementedException();

    private static FxPreset ValidateEffects(FxPreset raw)
        => throw new System.NotImplementedException();
}
