// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Sinto.Core.Synth;

namespace Sinto.Core.Effects;

/// <summary>Retro filter: N64 / PS1 lo-fi degradation via Sample &amp; Hold.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Retro : IEffect {
#nullable enable
    public RetroMode mode    { get; set; } = RetroMode.Clean;
    public bool      enabled { get; set; }

    float _hold_l, _hold_r;
    int _frame_counter;

    public void Process(Span<float> buffer, int channels) {
        if (!enabled || mode == RetroMode.Clean) return;
        if (channels < 1) channels = 1;
        int hold_frames = mode switch {
            RetroMode.N64 => 2,   // 44100 → 22050
            RetroMode.PS1 => 4,   // 44100 → 11025
            _ => 1,
        };
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            int i = f * channels;
            if (_frame_counter == 0) {
                // Sample
                _hold_l = AdpcmWaveshape(buffer[i]);
                if (channels >= 2) _hold_r = AdpcmWaveshape(buffer[i + 1]);
            }
            // Hold for hold_frames samples
            buffer[i] = _hold_l;
            if (channels >= 2) buffer[i + 1] = _hold_r;
            _frame_counter++;
            if (_frame_counter >= hold_frames) _frame_counter = 0;
        }
    }

    public void Reset() {
        _hold_l = 0f;
        _hold_r = 0f;
        _frame_counter = 0;
    }

    static float AdpcmWaveshape(float x)
        => MathF.Round(x * 16f) / 16f * 0.7f + x * 0.3f;
}
