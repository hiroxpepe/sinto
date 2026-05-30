// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Synth;

/// <summary>Portamento glide state. Linear frequency interpolation.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Portamento {
#nullable enable
    float _current_freq;
    float _target_freq;
    float _rate;

    public float currentFrequency => _current_freq;
    public bool  isGliding => _current_freq != _target_freq;

    public void SetTarget(float targetFreqHz, float timeSeconds, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        if (targetFreqHz < 0.001f) targetFreqHz = 0.001f;
        _target_freq = targetFreqHz;
        if (timeSeconds <= 0f) {
            _current_freq = targetFreqHz;
            _rate = 0f;
        } else {
            float diff = targetFreqHz - _current_freq;
            _rate = diff / (timeSeconds * sampleRate);
        }
    }

    public void SnapToTarget() => _current_freq = _target_freq;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick() {
        if (_current_freq == _target_freq) return _current_freq;
        _current_freq += _rate;
        // Check if reached target (handle both directions)
        if ((_rate > 0f && _current_freq >= _target_freq) ||
            (_rate < 0f && _current_freq <= _target_freq)) {
            _current_freq = _target_freq;
        }
        if (_current_freq < 0.001f) _current_freq = 0.001f;
        return _current_freq;
    }
}
