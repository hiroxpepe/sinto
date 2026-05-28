// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

/// <summary>LFO state: Sine/Triangle/Square/S&H + tempo sync.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Lfo {
    const double TWO_PI = 6.283185307179586;
    const double INV_PI = 0.3183098861837907;

    double _phase;
    double _phase_inc;
    float  _sh_value;
    double _sh_prev_phase;
    float  _current_bpm;
    uint   _noise_seed;

    public void Initialize(in LfoParams p, int sampleRate, float bpm = 120f) {
        Calc.Initialize();
        // Start at 3π/2 (sin = -1) so LFO begins in negative half-cycle.
        // This gives clean ascending zero-crossing at quarter cycle.
        _phase = TWO_PI * 0.75;
        _current_bpm = bpm;
        _noise_seed = 0x87654321u;
        _sh_prev_phase = 0.0;
        SetBPM(bpm, p, sampleRate);
        // Initialize sh_value
        _noise_seed = _noise_seed * 1664525u + 1013904223u;
        _sh_value = (int)_noise_seed / (float)int.MaxValue;
    }

    public void SetBPM(float bpm, in LfoParams p, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        _current_bpm = bpm;
        float freq_hz;
        if (p.TempoSync) {
            // RateOrSync = note value (0.25 = 1/4 note)
            // Hz = BPM / 60 / noteValue (e.g. 120BPM, 1/4 note → 2Hz)
            float note_val = p.RateOrSync;
            if (note_val < 0.001f) note_val = 0.001f;
            freq_hz = bpm / 60f / (note_val * 4f);
        } else {
            freq_hz = p.RateOrSync;
            if (freq_hz < 0.01f) freq_hz = 0.01f;
        }
        _phase_inc = TWO_PI * freq_hz / sampleRate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(in LfoParams p) {
        float sample;
        switch (p.Wave) {
            case LfoWave.Sine:
                sample = Calc.SinFast(_phase);
                break;
            case LfoWave.Triangle:
                float t = (float)(_phase * INV_PI - 1.0);
                sample = 1f - 2f * MathF.Abs(t);
                break;
            case LfoWave.Square:
                sample = _phase < Math.PI ? 1f : -1f;
                break;
            case LfoWave.SH:
                // Trigger on phase wrap
                if (_phase < _sh_prev_phase) {
                    _noise_seed = _noise_seed * 1664525u + 1013904223u;
                    _sh_value = (int)_noise_seed / (float)int.MaxValue;
                }
                _sh_prev_phase = _phase;
                sample = _sh_value;
                break;
            default:
                sample = 0f;
                break;
        }
        _phase += _phase_inc;
        if (_phase >= TWO_PI) _phase -= TWO_PI;
        return sample;
    }
}
