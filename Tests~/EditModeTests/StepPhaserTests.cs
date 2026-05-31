// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// SE-70 Stereo Phaser Step Mode (preset No.58 Step Phaser).
/// Step=On: LFO phase is quantised to discrete steps → phaser sweeps in a staircase.
/// Normal phaser (Step=Off) must be unaffected.
/// </summary>
[TestFixture]
public class StepPhaserTests
{
    const int SR = 44100;

    [Test]
    public void Phaser_Normal_ProcessDoesNotSilence()
    {
        // Regression: normal phaser must not silence the signal.
        var fx = new Phaser(SR);
        fx.SetParams(1.0f, 0.8f, 0.5f, 1f);
        var buf = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.1f),
            "Normal phaser must not silence the signal.");
    }

    [Test]
    public void Phaser_Normal_OutputDiffersFromDry()
    {
        // Normal phaser changes the signal.
        var fx = new Phaser(SR);
        fx.SetParams(1.0f, 0.9f, 0.5f, 1f);
        var wet = MakeSine(440f, 4096);
        var dry = MakeSine(440f, 4096);
        fx.Process(wet.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < wet.Length; i++) diff += MathF.Abs(wet[i] - dry[i]);
        Assert.That(diff, Is.GreaterThan(1f), "Phaser must change the signal.");
    }

    [Test]
    public void Phaser_Step_On_DiffersFromStepOff()
    {
        // Step=On must produce different output than Step=Off.
        var fxOn  = new Phaser(SR); fxOn.SetParams(2f, 0.9f, 0.3f, 1f);
        var fxOff = new Phaser(SR); fxOff.SetParams(2f, 0.9f, 0.3f, 1f);
        fxOn.SetStep(true);
        fxOn.SetStepRate(10); // coarse steps = large difference
        var bOn  = MakeSine(440f, 4096);
        var bOff = MakeSine(440f, 4096);
        fxOn.Process(bOn.AsSpan(), 2);
        fxOff.Process(bOff.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < bOn.Length; i++) diff += MathF.Abs(bOn[i] - bOff[i]);
        Assert.That(diff, Is.GreaterThan(0.5f),
            "Step=On must produce different output from Step=Off.");
    }

    [Test]
    public void Phaser_Step_On_DoesNotSilence()
    {
        // Step mode must not silence the signal.
        var fx = new Phaser(SR);
        fx.SetParams(1.0f, 0.9f, 0.5f, 1f);
        fx.SetStep(true);
        fx.SetStepRate(50);
        var buf = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.1f),
            "Step Phaser must not silence the signal.");
    }

    [Test]
    public void Phaser_StepRate_HigherValue_ProducesMoreTransitions()
    {
        // Higher Step Rate → finer steps → more transitions per render window.
        int fine   = CountTransitions(stepRate: 80);
        int coarse = CountTransitions(stepRate: 10);
        Assert.That(fine, Is.GreaterThan(coarse),
            "Higher Step Rate must produce more step transitions.");
    }

    // ── helpers ──────────────────────────────────────────────────────────

    static float[] MakeSine(float freq, int frames)
    {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * freq * i / SR);
            buf[i * 2] = buf[i * 2 + 1] = s;
        }
        return buf;
    }

    static float Peak(float[] buf)
    {
        float p = 0f;
        foreach (var s in buf) { float a = MathF.Abs(s); if (a > p) p = a; }
        return p;
    }

    static int CountTransitions(int stepRate)
    {
        var fx = new Phaser(SR);
        fx.SetParams(2f, 0.9f, 0.3f, 1f);
        fx.SetStep(true);
        fx.SetStepRate(stepRate);
        var buf = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        int n = 0;
        for (int i = 2; i < buf.Length - 2; i += 2)
            if (MathF.Abs(buf[i] - buf[i - 2]) > 0.0001f) n++;
        return n;
    }
}
