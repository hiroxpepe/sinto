// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Effects;

/// <summary>Stereo delay with fractional read pointer (smooth time changes).</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Delay : IEffect {
#nullable enable
    private readonly float[] _delay_buf_l;
    private readonly float[] _delay_buf_r;
    private int   _write_pos;
    private float _read_pos_l;
    private float _read_pos_r;
    private float _target_time_sec;
    private readonly int _sample_rate;
    private readonly int _max_samples;

    public float Time      { get; set; } = 0.25f;
    public float Feedback  { get; set; } = 0.3f;
    public float Mix       { get; set; } = 0.3f;
    public bool  TempoSync { get; set; }
    public float Bpm       { get; set; } = 120f;
    public float SyncNote  { get; set; } = 0.25f;
    public bool  Enabled   { get; set; }

    public Delay(int sampleRate = 44100) {
        if (sampleRate <= 0) sampleRate = 44100;
        _sample_rate = sampleRate;
        _max_samples = sampleRate * 2; // 2 second max
        _delay_buf_l = new float[_max_samples];
        _delay_buf_r = new float[_max_samples];
        _write_pos = 0;
        _read_pos_l = 0f;
        _read_pos_r = 0f;
        _target_time_sec = Time;
    }

    public void Process(Span<float> buffer, int channels) {
        if (!Enabled || Mix <= 0f) return;
        if (channels < 1) channels = 1;
        // Clamp feedback to prevent runaway
        float fb = Feedback;
        if      (fb < 0f)    fb = 0f;
        else if (fb > 0.95f) fb = 0.95f;
        // Target sample delay (fractional)
        float target_samples = Time * _sample_rate;
        if (target_samples < 1f)              target_samples = 1f;
        if (target_samples > _max_samples - 4) target_samples = _max_samples - 4;
        // Smooth read pointer towards target (fractional delay — prevents click)
        const float SMOOTH = 0.001f;
        float target_read_l = (_write_pos - target_samples + _max_samples) % _max_samples;
        float target_read_r = target_read_l;
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            // Move read pointers towards target
            float diff_l = target_read_l - _read_pos_l;
            if (diff_l >  _max_samples * 0.5f) diff_l -= _max_samples;
            if (diff_l < -_max_samples * 0.5f) diff_l += _max_samples;
            _read_pos_l += diff_l * SMOOTH + 1f;
            if (_read_pos_l < 0)            _read_pos_l += _max_samples;
            if (_read_pos_l >= _max_samples) _read_pos_l -= _max_samples;
            _read_pos_r += diff_l * SMOOTH + 1f;
            if (_read_pos_r < 0)            _read_pos_r += _max_samples;
            if (_read_pos_r >= _max_samples) _read_pos_r -= _max_samples;
            // Linear interpolation read
            int idx_l = (int)_read_pos_l;
            float frac_l = _read_pos_l - idx_l;
            int idx_l2 = (idx_l + 1) % _max_samples;
            float delayed_l = _delay_buf_l[idx_l] * (1f - frac_l) + _delay_buf_l[idx_l2] * frac_l;
            int idx_r = (int)_read_pos_r;
            float frac_r = _read_pos_r - idx_r;
            int idx_r2 = (idx_r + 1) % _max_samples;
            float delayed_r = _delay_buf_r[idx_r] * (1f - frac_r) + _delay_buf_r[idx_r2] * frac_r;
            int i = f * channels;
            float in_l = buffer[i];
            float in_r = channels >= 2 ? buffer[i + 1] : in_l;
            // Write to delay (input + feedback)
            _delay_buf_l[_write_pos] = in_l + delayed_l * fb;
            _delay_buf_r[_write_pos] = in_r + delayed_r * fb;
            _write_pos = (_write_pos + 1) % _max_samples;
            // Mix
            buffer[i] = in_l * (1f - Mix) + delayed_l * Mix;
            if (channels >= 2)
                buffer[i + 1] = in_r * (1f - Mix) + delayed_r * Mix;
        }
    }

    public void Reset() {
        Array.Clear(_delay_buf_l, 0, _delay_buf_l.Length);
        Array.Clear(_delay_buf_r, 0, _delay_buf_r.Length);
        _write_pos = 0;
        _read_pos_l = 0f;
        _read_pos_r = 0f;
    }

    public void SetBPM(float bpm) {
        Bpm = bpm;
        if (TempoSync) {
            Time = 60f / bpm * SyncNote * 4f;
        }
    }
}
