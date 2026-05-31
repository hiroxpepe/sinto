// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// Gate mode consistency: Flanger Gate1 and Phaser Gate (Square LFO) must behave identically.
/// Gate OFF = complete silence (buffer[i] = 0f). Phaser Gate is the reference.
/// </summary>
[TestFixture]
public class GateModeTests
{
    const int SR = 44100;

    [Test]
    public void Phaser_Gate_OffHalf_IsCompletesilence()
    {
        // Reference behaviour: gate OFF = 0f.
        var fx = new Phaser(SR);
        fx.SetParams(2.0f, 0.9f, 0.5f, 1f);
        fx.SetLfoWaveform(LfoWaveform.Square);
        fx.Reset();
        int half = (int)(SR / 2.0f / 2f);
        var skip = MakeSine(half);
        fx.Process(skip.AsSpan(), 2); // advance to OFF half
        var buf = MakeSine(half);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.LessThan(0.05f),
            "Phaser Gate OFF must be near-silent (reference).");
    }

    [Test]
    public void Phaser_Gate_OnHalf_HasOutput()
    {
        var fx = new Phaser(SR);
        fx.SetParams(2.0f, 0.9f, 0.5f, 1f);
        fx.SetLfoWaveform(LfoWaveform.Square);
        fx.Reset();
        int half = (int)(SR / 2.0f / 2f);
        var buf = MakeSine(half);
        fx.Process(buf.AsSpan(), 2); // ON half
        Assert.That(Peak(buf), Is.GreaterThan(0.1f),
            "Phaser Gate ON must have output.");
    }

    [Test]
    public void Flanger_Gate1_OffHalf_IsCompleteSilence()
    {
        // Flanger Gate1 must match Phaser Gate: OFF = 0f.
        // Use full silence as input so any non-zero output must come from the effect.
        var fx = new Flanger(SR);
        fx.SetParams(2.0f, 0.9f, 0.5f, 1f);
        fx.SetStepMode(FlangerStepMode.Gate1);
        fx.Reset(); // phase=0 = gate ON
        int half = (int)(SR / 2.0f / 2f);
        // Render ON half with silence input
        var silOn  = new float[half * 2];
        var silOff = new float[half * 2];
        fx.Process(silOn.AsSpan(),  2); // phase 0→0.5 = gate ON
        fx.Process(silOff.AsSpan(), 2); // phase 0.5→1 = gate OFF
        // OFF half with silence input: output must be 0
        Assert.That(Peak(silOff), Is.LessThan(0.001f),
            "Flanger Gate1 OFF with silence input must be zero.");
    }

    [Test]
    public void Flanger_Gate1_OnHalf_HasOutput()
    {
        var fx = new Flanger(SR);
        fx.SetParams(2.0f, 0.9f, 0.3f, 1f);
        fx.SetStepMode(FlangerStepMode.Gate1);
        // Prime delay buffer
        var prime = MakeSine(4096);
        fx.Process(prime.AsSpan(), 2);
        fx.ResetPhase(); // restart at ON
        int half = (int)(SR / 2.0f / 2f);
        var buf = MakeSine(half);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.01f),
            "Flanger Gate1 ON must have output.");
    }

    static float[] MakeSine(int frames) {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * 440f * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
    static float Peak(float[] buf) {
        float p = 0f;
        foreach (var s in buf) { float a = MathF.Abs(s); if (a > p) p = a; }
        return p;
    }
}
