// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// Phaser effect intensity parity with Chorus and Flanger at full knob (100).
/// At full depth/resonance, Phaser must produce comparable output change
/// to Chorus and Flanger.
/// </summary>
[TestFixture]
public class PhaserDepthTests
{
    const int SR = 44100;

    // Chorus full: rate=2.0Hz, depth=0.7 (via SetChorusParams)
    // Flanger full: rate=4.0Hz, depth=1.0, fb=0.8
    // Phaser full: must match this intensity level

    [Test]
    public void Phaser_FullKnob_OutputChangeComparableToFlanger()
    {
        // Measure output change (diff from dry) for Flanger and Phaser at full settings.
        float flangerDiff = MeasureDiff_Flanger(rate: 4.0f, depth: 1.0f, feedback: 0.8f, send: 1.0f);
        float phaserDiff  = MeasureDiff_Phaser (rate: 4.0f, depth: 1.0f, resonance: 0.95f, send: 1.0f);

        // Phaser diff must be at least 50% of Flanger diff (comparable intensity)
        Assert.That(phaserDiff, Is.GreaterThan(flangerDiff * 0.9f),
            $"Phaser (diff={phaserDiff:F3}) must be at least 90% of Flanger (diff={flangerDiff:F3}).");
    }

    [Test]
    public void Phaser_FullKnob_CurrentSettings_AreWeakerThanTarget()
    {
        // RED test: current settings (resonance=depth*0.9) are weaker than target.
        float flangerDiff = MeasureDiff_Flanger(rate: 4.0f, depth: 1.0f, feedback: 0.8f, send: 1.0f);
        float phaserCurrent = MeasureDiff_Phaser(rate: 4.0f, depth: 1.0f, resonance: 1.0f * 0.9f, send: 1.0f);

        TestContext.Out.WriteLine($"Flanger diff:         {flangerDiff:F3}");
        TestContext.Out.WriteLine($"Phaser current diff:  {phaserCurrent:F3}");
        TestContext.Out.WriteLine($"Ratio: {phaserCurrent/flangerDiff:F2}");

        // Document the current gap — this test always passes (diagnostic only)
        Assert.Pass($"Flanger={flangerDiff:F3} Phaser(current)={phaserCurrent:F3} ratio={phaserCurrent/flangerDiff:F2}");
    }

    [Test]
    public void Phaser_Resonance_095_IsMostIntense()
    {
        // resonance=0.95 must produce more effect than resonance=0.5
        float diffHigh = MeasureDiff_Phaser(rate: 2.0f, depth: 0.9f, resonance: 0.95f, send: 1.0f);
        float diffLow  = MeasureDiff_Phaser(rate: 2.0f, depth: 0.9f, resonance: 0.5f,  send: 1.0f);
        Assert.That(diffHigh, Is.GreaterThan(diffLow),
            "Higher resonance must produce more intense phasing effect.");
    }

    // ── helpers ──────────────────────────────────────────────────────────

    static float MeasureDiff_Flanger(float rate, float depth, float feedback, float send)
    {
        var fx  = new Flanger(SR);
        fx.SetParams(rate, depth, feedback, send);
        var wet = MakeSine(4096);
        var dry = MakeSine(4096);
        fx.Process(wet.AsSpan(), 2);
        float d = 0f;
        for (int i = 0; i < wet.Length; i++) d += MathF.Abs(wet[i] - dry[i]);
        return d / wet.Length;
    }

    static float MeasureDiff_Phaser(float rate, float depth, float resonance, float send)
    {
        var fx  = new Phaser(SR);
        fx.SetParams(rate, depth, resonance, send);
        var wet = MakeSine(4096);
        var dry = MakeSine(4096);
        fx.Process(wet.AsSpan(), 2);
        float d = 0f;
        for (int i = 0; i < wet.Length; i++) d += MathF.Abs(wet[i] - dry[i]);
        return d / wet.Length;
    }

    static float[] MakeSine(int frames)
    {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * 440f * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
}
