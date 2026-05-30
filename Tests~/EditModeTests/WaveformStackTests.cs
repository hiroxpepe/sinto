// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Waveform stacking: WaveType is a [Flags] enum so an oscillator can output
/// several waveforms summed together (Juno-106-style DCO). A single bit behaves
/// exactly like before; multiple bits sum and are normalised by active count.
/// </summary>
[TestFixture]
public class WaveformStackTests
{
    const int SR = 44100;

    static float[] Render(WaveType wave, int n = 2048, float hz = 220f)
    {
        var osc = new Oscillator();
        osc.SetFrequency(hz, SR);
        var p = new OscParams(wave);
        var outBuf = new float[n];
        for (int i = 0; i < n; i++) outBuf[i] = osc.Tick(p);
        return outBuf;
    }

    static float Rms(float[] b)
    {
        double s = 0; foreach (var v in b) s += v * (double)v;
        return (float)Math.Sqrt(s / b.Length);
    }

    [Test]
    public void FlagsEnum_HasPowerOfTwoValues()
    {
        // Single-bit values so they can combine.
        Assert.That((int)WaveType.Sine,     Is.EqualTo(1));
        Assert.That((int)WaveType.Saw,      Is.EqualTo(2));
        Assert.That((int)WaveType.Triangle, Is.EqualTo(4));
        Assert.That((int)WaveType.Square,   Is.EqualTo(8));
        Assert.That((int)WaveType.Noise,    Is.EqualTo(16));
    }

    [Test]
    public void SingleWaveform_StillProducesOutput()
    {
        var saw = Render(WaveType.Saw);
        Assert.That(Rms(saw), Is.GreaterThan(0.1f));
    }

    [Test]
    public void TwoWaveforms_DifferFromEither()
    {
        var saw   = Render(WaveType.Saw);
        var sqr   = Render(WaveType.Square);
        var stack = Render(WaveType.Saw | WaveType.Square);
        // Stacked output must not be identical to either single waveform.
        float dSaw = 0, dSqr = 0;
        for (int i = 0; i < stack.Length; i++) {
            dSaw += MathF.Abs(stack[i] - saw[i]);
            dSqr += MathF.Abs(stack[i] - sqr[i]);
        }
        Assert.That(dSaw, Is.GreaterThan(1f), "Stack must differ from saw alone.");
        Assert.That(dSqr, Is.GreaterThan(1f), "Stack must differ from square alone.");
    }

    [Test]
    public void TwoWaveforms_StayWithinRange()
    {
        var stack = Render(WaveType.Saw | WaveType.Square);
        foreach (var v in stack)
            Assert.That(MathF.Abs(v), Is.LessThanOrEqualTo(1.01f),
                "Summed waveform must be normalised to stay in range.");
    }

    [Test]
    public void NoneFlag_IsZero()
    {
        Assert.That((int)WaveType.None, Is.EqualTo(0));
    }
}
