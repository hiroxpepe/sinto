// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

/// <summary>Fast DSP math: SinFast / TanhFast / PitchRatioFast.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public static class Calc {
#nullable enable
    public const int LUT_SIZE = 4096;
    private const float TWO_PI = 6.28318530717958647692f;
    private const float INV_TWO_PI = 0.15915494309189534f;
    private const float LN2_DIV_12 = 0.05776226504666215f; // ln(2)/12

    private static readonly float[] _sin_lut    = new float[LUT_SIZE];
    private static bool _initialized;

    public static bool IsLutMode { get; private set; } = true;

    /// <summary>Initialize sine LUT. Safe to call multiple times.</summary>
    public static void Initialize() {
        if (_initialized) return;
        for (int i = 0; i < LUT_SIZE; i++) {
            _sin_lut[i] = MathF.Sin((float)i / LUT_SIZE * TWO_PI);
        }
        _initialized = true;
    }

    /// <summary>Fast sin using LUT + linear interpolation. Phase wraps automatically.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinFast(double phase) {
        if (!_initialized) Initialize();
        // Normalize phase to [0, 1)
        double normalized = phase * INV_TWO_PI;
        normalized -= Math.Floor(normalized);
        float idx_f = (float)(normalized * LUT_SIZE);
        int idx = (int)idx_f;
        float frac = idx_f - idx;
        int idx_next = (idx + 1) & (LUT_SIZE - 1);
        return _sin_lut[idx] + (_sin_lut[idx_next] - _sin_lut[idx]) * frac;
    }

    /// <summary>Fast tanh approximation. Smooth saturation: ℝ → (-1, 1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TanhFast(float x) {
        // Padé approximation: tanh(x) ≈ x(27 + x²) / (27 + 9x²)
        // Clamp input to prevent overflow on extreme values
        if (x >  5f) return  1f;
        if (x < -5f) return -1f;
        float x2 = x * x;
        return x * (27f + x2) / (27f + 9f * x2);
    }

    /// <summary>Fast pitch ratio: 2^(semitones/12). Continuous, safe for extreme values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float PitchRatioFast(float semitones) {
        // Clamp extreme values to prevent overflow (still produces finite result)
        if (semitones >  120f) semitones =  120f;
        if (semitones < -120f) semitones = -120f;
        // Use exp(semitones * ln(2)/12) — accurate, fast on modern CPUs
        return MathF.Exp(semitones * LN2_DIV_12);
    }
}
