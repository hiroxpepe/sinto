// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// High-pass filter placed before the main filter (DCO -> HPF -> DCF -> DCA).
/// Raising HPF frequency attenuates low frequencies while preserving highs.
/// Implemented as a resonance-less 1st-order high-pass (HighPassFilter struct).
/// </summary>
[TestFixture]
public class HpfTests
{
    const int SR = 44100;

    static float Rms(ReadOnlySpan<float> b)
    {
        double s = 0; for (int i = 0; i < b.Length; i++) s += b[i] * (double)b[i];
        return (float)Math.Sqrt(s / b.Length);
    }

    // Feed a pure sine of `hz` through a HighPassFilter at cutoff `cutoffHz`,
    // return output RMS.
    static float FilteredRms(float hz, float cutoffHz)
    {
        var hpf = new HighPassFilter();
        hpf.SetCutoff(cutoffHz, SR);
        int n = SR / 4; // 0.25s
        var outBuf = new float[n];
        double phase = 0, inc = 2 * Math.PI * hz / SR;
        for (int i = 0; i < n; i++) {
            float x = (float)Math.Sin(phase); phase += inc;
            outBuf[i] = hpf.Process(x);
        }
        // skip the first 1000 samples (settling)
        return Rms(outBuf.AsSpan(1000));
    }

    [Test]
    public void CutoffZero_PassesEverything()
    {
        var hpf = new HighPassFilter();
        hpf.SetCutoff(0f, SR); // off = pass-all
        float x = 0.7f;
        // DC-ish: with cutoff 0 the output should track the input closely.
        float y = hpf.Process(x);
        Assert.That(MathF.Abs(y), Is.GreaterThan(0.5f));
    }

    [Test]
    public void HighCutoff_AttenuatesLowFrequencyStrongly()
    {
        // 50 Hz tone through a 1 kHz high-pass should lose most of its energy.
        float low = FilteredRms(50f, 1000f);
        Assert.That(low, Is.LessThan(0.2f), "Low frequency must be strongly attenuated.");
    }

    [Test]
    public void HighCutoff_PreservesHighFrequency()
    {
        // 5 kHz tone through a 1 kHz high-pass should pass largely intact.
        float high = FilteredRms(5000f, 1000f);
        Assert.That(high, Is.GreaterThan(0.5f), "High frequency must pass.");
    }

    [Test]
    public void RaisingCutoff_ReducesLowFrequencyOutput()
    {
        float at200  = FilteredRms(100f, 200f);
        float at2000 = FilteredRms(100f, 2000f);
        Assert.That(at2000, Is.LessThan(at200),
            "Raising the cutoff must reduce a 100 Hz tone's output further.");
    }

    [Test]
    public void Engine_SetHpf_AttenuatesOutputLowEnd()
    {
        // End-to-end: with HPF high, a low note's output RMS should drop versus HPF off.
        var e1 = new Engine(SR, 2, 32, 1024);
        e1.SetHpf(0f);
        var b1 = new float[SR / 2];
        e1.SendNoteOn(36, 0.9f, 2, 5, 0); // low C2
        e1.ProcessAudioCallback(b1.AsSpan());
        float rmsOff = Rms(b1.AsSpan(2000));

        var e2 = new Engine(SR, 2, 32, 1024);
        e2.SetHpf(100f); // strong high-pass (0..100 scale)
        var b2 = new float[SR / 2];
        e2.SendNoteOn(36, 0.9f, 2, 5, 0);
        e2.ProcessAudioCallback(b2.AsSpan());
        float rmsHigh = Rms(b2.AsSpan(2000));

        Assert.That(rmsHigh, Is.LessThan(rmsOff),
            "A strong HPF must reduce a low note's output energy.");
    }
}
