// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Signo.Core.Signal;

namespace Signo.Core.Effects;

/// <summary>
/// Freeverb (Jezar Wakefield) reverb: 8 comb filters + 4 allpass per channel,
/// with a stereo spread on the right channel for a wide, natural tail.
/// </summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Reverb : MonoEffect, ISendEffect {
    // Jezar's original tunings (samples @ 44.1 kHz).
    static readonly int[] COMB = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    static readonly int[] ALLPASS = { 556, 441, 341, 225 };
    const int STEREO_SPREAD = 23;
    const float FIXED_GAIN = 0.015f;
    const float SCALE_DAMP = 0.4f;
    const float SCALE_ROOM = 0.28f;
    const float OFFSET_ROOM = 0.7f;

    readonly Comb[] _comb_l = new Comb[8];
    readonly Comb[] _comb_r = new Comb[8];
    readonly Allpass[] _ap_l = new Allpass[4];
    readonly Allpass[] _ap_r = new Allpass[4];

    float _room = 0.5f;
    float _damp = 0.5f;

    public float roomSize { get => _room; set { _room = value; UpdateComb(); } }
    public float damping  { get => _damp; set { _damp = value; UpdateComb(); } }
    public float mix      { get; set; } = 0.3f;
    public float width    { get; set; } = 1f;

    public Reverb() {
        for (int i = 0; i < 8; i++) {
            _comb_l[i] = new Comb(COMB[i]);
            _comb_r[i] = new Comb(COMB[i] + STEREO_SPREAD);
        }
        for (int i = 0; i < 4; i++) {
            _ap_l[i] = new Allpass(ALLPASS[i]);
            _ap_r[i] = new Allpass(ALLPASS[i] + STEREO_SPREAD);
            _ap_l[i].feedback = 0.5f;
            _ap_r[i].feedback = 0.5f;
        }
        UpdateComb();
    }

    void UpdateComb() {
        float fb = _room * SCALE_ROOM + OFFSET_ROOM;
        float damp = _damp * SCALE_DAMP;
        for (int i = 0; i < 8; i++) {
            _comb_l[i].feedback = fb; _comb_l[i].damp = damp;
            _comb_r[i].feedback = fb; _comb_r[i].damp = damp;
        }
    }

    public override void Process(Span<float> buffer, int channels) {
        if (!enabled || mix <= 0f) return;
        if (channels < 1) channels = 1;
        int frames = buffer.Length / channels;
        float wet1 = width * 0.5f + 0.5f;
        float wet2 = (1f - width) * 0.5f;
        for (int f = 0; f < frames; f++) {
            int i = f * channels;
            float in_l = buffer[i];
            float in_r = channels >= 2 ? buffer[i + 1] : in_l;
            float input = (in_l + in_r) * FIXED_GAIN;

            float out_l = 0f, out_r = 0f;
            for (int c = 0; c < 8; c++) {
                out_l += _comb_l[c].Process(input);
                out_r += _comb_r[c].Process(input);
            }
            for (int a = 0; a < 4; a++) {
                out_l = _ap_l[a].Process(out_l);
                out_r = _ap_r[a].Process(out_r);
            }
            float wide_l = out_l * wet1 + out_r * wet2;
            float wide_r = out_r * wet1 + out_l * wet2;
            buffer[i] = in_l * (1f - mix) + wide_l * mix;
            if (channels >= 2) buffer[i + 1] = in_r * (1f - mix) + wide_r * mix;
        }
        ApplyMonoCompatibility(buffer, channels);
    }

    public override void Reset() {
        for (int i = 0; i < 8; i++) { _comb_l[i].Reset(); _comb_r[i].Reset(); }
        for (int i = 0; i < 4; i++) { _ap_l[i].Reset(); _ap_r[i].Reset(); }
    }

    sealed class Comb {
        readonly float[] _buf;
        int _pos;
        float _store;
        public float feedback;
        public float damp;
        public Comb(int size) { _buf = new float[size]; }
        public float Process(float input) {
            float output = _buf[_pos];
            _store = output * (1f - damp) + _store * damp;
            _buf[_pos] = input + _store * feedback;
            if (++_pos >= _buf.Length) _pos = 0;
            return output;
        }
        public void Reset() { Array.Clear(_buf, 0, _buf.Length); _store = 0f; _pos = 0; }
    }

    sealed class Allpass {
        readonly float[] _buf;
        int _pos;
        public float feedback;
        public Allpass(int size) { _buf = new float[size]; }
        public float Process(float input) {
            float bufout = _buf[_pos];
            float output = -input + bufout;
            _buf[_pos] = input + bufout * feedback;
            if (++_pos >= _buf.Length) _pos = 0;
            return output;
        }
        public void Reset() { Array.Clear(_buf, 0, _buf.Length); _pos = 0; }
    }
}
