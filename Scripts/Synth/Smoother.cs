// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;
using Signo.Core.Audio;

namespace Signo.Core.Synth;

/// <summary>One-pole lowpass parameter smoother. Denormal-safe.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Smoother {
    const float SLEEP_THRESHOLD = 1e-5f;
    const float TWO_PI = 6.28318530717958647692f;

    float _current;
    float _target;
    float _coeff;
    long  _sample_index;

    public float current => _current;
    public float target  => _target;

    public Smoother(float initialValue, float smoothingHz = 20f, int sampleRate = 44100) {
        if (sampleRate <= 0)
            throw new ArgumentException("sampleRate must be > 0", nameof(sampleRate));
        if (smoothingHz <= 0f)
            throw new ArgumentOutOfRangeException(nameof(smoothingHz),
                "smoothingHz must be > 0");
        _current = initialValue;
        _target  = initialValue;
        _coeff   = 1f - MathF.Exp(-TWO_PI * smoothingHz / sampleRate);
        _sample_index = 0L;
    }

    public void SetTarget(float target) => _target = target;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick() {
        float diff = _target - _current;
        float abs = diff < 0f ? -diff : diff;
        if (abs < SLEEP_THRESHOLD) {
            _current = _target;
            return _current;
        }
        _current += diff * _coeff;
        _current = Denormal.Protect(_current, _sample_index++);
        return _current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SnapToTarget() => _current = _target;

    public void SetSampleRate(int sampleRate, float smoothingHz = 20f) {
        if (sampleRate <= 0)
            throw new ArgumentException("sampleRate must be > 0", nameof(sampleRate));
        _coeff = 1f - MathF.Exp(-TWO_PI * smoothingHz / sampleRate);
    }
}
