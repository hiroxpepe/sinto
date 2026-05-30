// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Effects;

/// <summary>Stereo delay with fractional read pointer (smooth time changes).</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Delay : IEffect {
#nullable enable
    readonly float[] _delay_buf_l;
    readonly float[] _delay_buf_r;
    int   _write_pos;
    float _read_pos_l;
    float _target_time_sec;
    readonly int _sample_rate;
    readonly int _max_samples;

    public float time      { get; set; } = 0.25f;
    public float feedback  { get; set; } = 0.3f;
    public float mix       { get; set; } = 0.3f;
    public bool  tempoSync { get; set; }
    public float bpm       { get; set; } = 120f;
    public float syncNote  { get; set; } = 0.25f;
    public bool  enabled   { get; set; }

    public Delay(int sampleRate = 44100) {
        if (sampleRate <= 0) sampleRate = 44100;
        _sample_rate = sampleRate;
        _max_samples = sampleRate * 2; // 2 second max
        _delay_buf_l = new float[_max_samples];
        _delay_buf_r = new float[_max_samples];
        _write_pos = 0;
        _read_pos_l = time * sampleRate; // used as current delay length
        _target_time_sec = time;
    }

    public void Process(Span<float> buffer, int channels) {
        if (!enabled || mix <= 0f) return;
        if (channels < 1) channels = 1;
        float fb = feedback;
        if      (fb < 0f)    fb = 0f;
        else if (fb > 0.95f) fb = 0.95f;
        // Smooth the delay time itself (not the read pointer) to avoid pitch drift.
        float target_samples = time * _sample_rate;
        if (target_samples < 1f)               target_samples = 1f;
        if (target_samples > _max_samples - 4)  target_samples = _max_samples - 4;
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            // Glide the delay length gently toward the target (click-free).
            _read_pos_l += (target_samples - _read_pos_l) * 0.0005f;
            float delay_samples = _read_pos_l;
            // Read `delay_samples` behind the write head (fractional).
            float read = _write_pos - delay_samples;
            while (read < 0) read += _max_samples;
            int idx = (int)read;
            float frac = read - idx;
            int idx2 = (idx + 1) % _max_samples;
            float delayed_l = _delay_buf_l[idx] * (1f - frac) + _delay_buf_l[idx2] * frac;
            float delayed_r = _delay_buf_r[idx] * (1f - frac) + _delay_buf_r[idx2] * frac;
            int i = f * channels;
            float in_l = buffer[i];
            float in_r = channels >= 2 ? buffer[i + 1] : in_l;
            // Ping-pong: feed the (mono) input into L only; the cross-fed
            // feedback then bounces the echo L -> R -> L across the stereo field.
            float monoIn = (in_l + in_r) * 0.5f;
            _delay_buf_l[_write_pos] = monoIn + delayed_r * fb;
            _delay_buf_r[_write_pos] = delayed_l * fb;
            _write_pos = (_write_pos + 1) % _max_samples;
            // mix
            buffer[i] = in_l * (1f - mix) + delayed_l * mix;
            if (channels >= 2)
                buffer[i + 1] = in_r * (1f - mix) + delayed_r * mix;
        }
    }

    public void Reset() {
        Array.Clear(_delay_buf_l, 0, _delay_buf_l.Length);
        Array.Clear(_delay_buf_r, 0, _delay_buf_r.Length);
        _write_pos = 0;
        _read_pos_l = time * _sample_rate;
    }

    public void SetBPM(float bpm) {
        this.bpm = bpm;
        if (tempoSync) {
            time = 60f / bpm * syncNote * 4f;
        }
    }
}
