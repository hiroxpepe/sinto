// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Two noise colours: White (flat spectrum, the existing Noise) and Pink
/// (-3 dB/oct, more low-frequency energy). Pink is a separate waveform flag
/// (Paul Kellet approximation, SH-101 precedent).
/// </summary>
[TestFixture]
public class NoiseColourTests
{
    const int SR = 44100;

    static float[] Render(WaveType wave, int n = 16384)
    {
        var osc = new Oscillator();
        osc.SetFrequency(220f, SR);
        var p = new OscParams(wave);
        var outBuf = new float[n];
        for (int i = 0; i < n; i++) outBuf[i] = osc.Tick(p);
        return outBuf;
    }

    // crude low/high band energy split via a one-pole low-pass at ~500 Hz.
    static (float lowRms, float highRms) BandSplit(float[] b)
    {
        float a = 0.93f; // ~500 Hz one-pole
        float lp = 0; double low = 0, high = 0;
        for (int i = 0; i < b.Length; i++) {
            lp = a * lp + (1 - a) * b[i];
            float hp = b[i] - lp;
            low  += lp * (double)lp;
            high += hp * (double)hp;
        }
        return ((float)Math.Sqrt(low / b.Length), (float)Math.Sqrt(high / b.Length));
    }

    [Test]
    public void Pink_IsASeparateFlag()
    {
        Assert.That((int)WaveType.Pink, Is.EqualTo(32));
        Assert.That(WaveType.Noise, Is.Not.EqualTo(WaveType.Pink));
    }

    [Test]
    public void WhiteNoise_ProducesOutput()
    {
        var w = Render(WaveType.Noise);
        var (lo, hi) = BandSplit(w);
        Assert.That(lo + hi, Is.GreaterThan(0.05f));
    }

    [Test]
    public void PinkNoise_ProducesOutput()
    {
        var p = Render(WaveType.Pink);
        var (lo, hi) = BandSplit(p);
        Assert.That(lo + hi, Is.GreaterThan(0.05f));
    }

    [Test]
    public void Pink_HasMoreLowEnergyRatioThanWhite()
    {
        var (wLo, wHi) = BandSplit(Render(WaveType.Noise));
        var (pLo, pHi) = BandSplit(Render(WaveType.Pink));
        float whiteRatio = wLo / (wHi + 1e-6f);
        float pinkRatio  = pLo / (pHi + 1e-6f);
        Assert.That(pinkRatio, Is.GreaterThan(whiteRatio),
            "Pink noise must have a higher low/high energy ratio than white.");
    }
}
