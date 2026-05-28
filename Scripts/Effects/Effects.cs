// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Sinto.Core.Synth;

namespace Sinto.Core.Effects;

/// <summary>Serial effects chain: Chorus → Reverb → Delay → Retro → SoftClip.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Effects {
    public Chorus Chorus { get; }
    public Reverb Reverb { get; }
    public Delay  Delay  { get; }
    public Retro  Retro  { get; }
    public bool   monoCompatible { get; set; }

    public Effects(int sampleRate = 44100) {
        if (sampleRate <= 0) sampleRate = 44100;
        Chorus = new Chorus(sampleRate);
        Reverb = new Reverb();
        Delay  = new Delay(sampleRate);
        Retro  = new Retro();
    }

    public void Process(Span<float> buffer, int channels) {
        Chorus.monoCompatible = monoCompatible;
        Reverb.monoCompatible = monoCompatible;
        if (Chorus.enabled) Chorus.Process(buffer, channels);
        if (Reverb.enabled) Reverb.Process(buffer, channels);
        if (Delay.enabled)  Delay.Process(buffer, channels);
        if (Retro.enabled)  Retro.Process(buffer, channels);
        ApplySoftClip(buffer);
    }

    public void SetBPM(float bpm) => Delay.SetBPM(bpm);

    public void Reset() {
        Chorus.Reset();
        Reverb.Reset();
        Delay.Reset();
        Retro.Reset();
    }

    /// <summary>Apply soft clipper to prevent multi-voice clipping.</summary>
    public void ApplySoftClip(Span<float> buffer) {
        for (int i = 0; i < buffer.Length; i++) {
            buffer[i] = Calc.TanhFast(buffer[i]);
        }
    }
}
