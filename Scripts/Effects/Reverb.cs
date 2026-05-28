// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Effects;

/// <summary>Freeverb-style reverb. 4 comb filters + 2 allpass per channel.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Reverb : MonoEffect {
    static readonly int[] COMB_TUNINGS = { 1116, 1188, 1277, 1356 };
    static readonly int[] AP_TUNINGS = { 556, 441 };

    readonly float[][] _comb_l;
    readonly float[][] _comb_r;
    readonly float[][] _ap_l;
    readonly float[][] _ap_r;
    readonly int[] _comb_pos;
    readonly int[] _ap_pos;
    readonly float[] _comb_filterstore_l;
    readonly float[] _comb_filterstore_r;

    public float roomSize { get; set; } = 0.5f;
    public float damping  { get; set; } = 0.5f;
    public float mix      { get; set; } = 0.3f;

    public Reverb() {
        _comb_l = new float[4][];
        _comb_r = new float[4][];
        for (int i = 0; i < 4; i++) {
            _comb_l[i] = new float[COMB_TUNINGS[i]];
            _comb_r[i] = new float[COMB_TUNINGS[i] + 23];
        }
        _ap_l = new float[2][];
        _ap_r = new float[2][];
        for (int i = 0; i < 2; i++) {
            _ap_l[i] = new float[AP_TUNINGS[i]];
            _ap_r[i] = new float[AP_TUNINGS[i] + 23];
        }
        _comb_pos = new int[4];
        _ap_pos = new int[2];
        _comb_filterstore_l = new float[4];
        _comb_filterstore_r = new float[4];
    }

    public override void Process(Span<float> buffer, int channels) {
        if (!enabled || mix <= 0f) return;
        if (channels < 1) channels = 1;
        int frames = buffer.Length / channels;
        float feedback = 0.7f + roomSize * 0.28f;
        float damp = damping * 0.4f;
        float inv_damp = 1f - damp;
        for (int f = 0; f < frames; f++) {
            int i = f * channels;
            float in_l = buffer[i];
            float in_r = channels >= 2 ? buffer[i + 1] : in_l;
            float input = (in_l + in_r) * 0.015f;
            // Sum comb filters
            float out_l = 0f, out_r = 0f;
            for (int c = 0; c < 4; c++) {
                int pos = _comb_pos[c];
                if (pos >= _comb_l[c].Length) pos = 0;
                float sample_l = _comb_l[c][pos];
                _comb_filterstore_l[c] = sample_l * inv_damp + _comb_filterstore_l[c] * damp;
                _comb_l[c][pos] = input + _comb_filterstore_l[c] * feedback;
                out_l += sample_l;
                if (pos >= _comb_r[c].Length) pos = 0;
                float sample_r = _comb_r[c][pos];
                _comb_filterstore_r[c] = sample_r * inv_damp + _comb_filterstore_r[c] * damp;
                _comb_r[c][pos] = input + _comb_filterstore_r[c] * feedback;
                out_r += sample_r;
                _comb_pos[c]++;
                if (_comb_pos[c] >= _comb_l[c].Length) _comb_pos[c] = 0;
            }
            // Allpass
            for (int a = 0; a < 2; a++) {
                int pos = _ap_pos[a];
                if (pos >= _ap_l[a].Length) pos = 0;
                float buf_l = _ap_l[a][pos];
                _ap_l[a][pos] = out_l + buf_l * 0.5f;
                out_l = -out_l + buf_l;
                if (pos >= _ap_r[a].Length) pos = 0;
                float buf_r = _ap_r[a][pos];
                _ap_r[a][pos] = out_r + buf_r * 0.5f;
                out_r = -out_r + buf_r;
                _ap_pos[a]++;
                if (_ap_pos[a] >= _ap_l[a].Length) _ap_pos[a] = 0;
            }
            buffer[i] = in_l * (1f - mix) + out_l * mix;
            if (channels >= 2) buffer[i + 1] = in_r * (1f - mix) + out_r * mix;
        }
        ApplyMonoCompatibility(buffer, channels);
    }

    public override void Reset() {
        for (int i = 0; i < 4; i++) {
            Array.Clear(_comb_l[i], 0, _comb_l[i].Length);
            Array.Clear(_comb_r[i], 0, _comb_r[i].Length);
            _comb_filterstore_l[i] = 0f;
            _comb_filterstore_r[i] = 0f;
            _comb_pos[i] = 0;
        }
        for (int i = 0; i < 2; i++) {
            Array.Clear(_ap_l[i], 0, _ap_l[i].Length);
            Array.Clear(_ap_r[i], 0, _ap_r[i].Length);
            _ap_pos[i] = 0;
        }
    }
}
