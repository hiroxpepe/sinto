// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Signo.Core.Signal;
using Signo.Core.Synth;

namespace Signo.Core.Effects;

/// <summary>BBD-style chorus with modulated delay lines.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Chorus : MonoEffect, ISendEffect {
    readonly float[] _delay_l;
    readonly float[] _delay_r;
    int   _write_pos;
    double _lfo_phase;
    readonly int _sample_rate;
    readonly int _max_delay_samples;

    public int   mode  { get; set; } = 1;
    public float rate  { get; set; } = 0.5f;
    public float depth { get; set; } = 0.4f;
    public float mix   { get; set; } = 0.5f;

    public Chorus(int sampleRate = 44100) {
        if (sampleRate <= 0) sampleRate = 44100;
        _sample_rate = sampleRate;
        _max_delay_samples = sampleRate / 20; // 50ms max
        _delay_l = new float[_max_delay_samples];
        _delay_r = new float[_max_delay_samples];
        _write_pos = 0;
        _lfo_phase = 0.0;
    }

    public override void Process(Span<float> buffer, int channels) {
        if (!enabled || mix <= 0f) return;
        if (channels < 1) channels = 1;
        int frames = buffer.Length / channels;
        double lfo_inc = 2.0 * Math.PI * rate / _sample_rate;
        // Base delay = 15ms
        float base_delay = _sample_rate * 0.015f;
        float depth_samples = base_delay * depth;
        for (int f = 0; f < frames; f++) {
            float lfo_l = (float)Math.Sin(_lfo_phase);
            // Right LFO offset by ~150 deg for a wide stereo image.
            float lfo_r = (float)Math.Sin(_lfo_phase + 2.6);
            _lfo_phase += lfo_inc;
            if (_lfo_phase >= 2.0 * Math.PI) _lfo_phase -= 2.0 * Math.PI;
            float delay_l_samples = base_delay + lfo_l * depth_samples;
            float delay_r_samples = base_delay + lfo_r * depth_samples;
            // Read with fractional delay (linear interpolation)
            float read_l = ReadDelay(_delay_l, delay_l_samples);
            float read_r = ReadDelay(_delay_r, delay_r_samples);
            // Get input
            int i = f * channels;
            float dry_l = buffer[i];
            float dry_r = channels >= 2 ? buffer[i + 1] : dry_l;
            // Write dry to delay
            _delay_l[_write_pos] = dry_l;
            _delay_r[_write_pos] = dry_r;
            _write_pos = (_write_pos + 1) % _max_delay_samples;
            // mix
            buffer[i] = dry_l * (1f - mix) + read_l * mix;
            if (channels >= 2)
                buffer[i + 1] = dry_r * (1f - mix) + read_r * mix;
        }
        ApplyMonoCompatibility(buffer, channels);
    }

    float ReadDelay(float[] buf, float samples) {
        if (samples < 1f) samples = 1f;
        if (samples > _max_delay_samples - 1) samples = _max_delay_samples - 1;
        int idx = (int)samples;
        float frac = samples - idx;
        int read_pos = (_write_pos - idx + _max_delay_samples) % _max_delay_samples;
        int read_pos2 = (read_pos - 1 + _max_delay_samples) % _max_delay_samples;
        return buf[read_pos] * (1f - frac) + buf[read_pos2] * frac;
    }

    public override void Reset() {
        Array.Clear(_delay_l, 0, _delay_l.Length);
        Array.Clear(_delay_r, 0, _delay_r.Length);
        _write_pos = 0;
        _lfo_phase = 0.0;
    }
}
