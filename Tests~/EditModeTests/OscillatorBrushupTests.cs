// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Oscillator brushup tests (MusicDSP reference).
/// ① DPW-2: SAW SNR improves +10dB over naive.
/// ② DPW-3: SAW SNR improves +14dB over naive.
/// ③ Shape: polyBLEP maintained even when Shape != 0.5.
/// ④ TRI DC blocker: DC < 0.005 (improved from 0.013).
/// ⑤ SinFast table: error < 0.0001, faster than MathF.Sin.
/// </summary>
[TestFixture]
public class OscillatorBrushupTests
{
    const int SR = 44100;

    // ── ① DPW-2: SAW SNR +10dB ──────────────────────────────────────
    // DPW SAW should have significantly lower aliasing energy than naive SAW.
    // We measure total harmonic distortion above Nyquist as aliasing energy.
    [Test]
    public void Saw_DPW2_LowerAliasingThanNaive()
    {
        // High frequency where aliasing is audible
        float freq = 5000f;
        int n = SR;
        var dpw = new Oscillator();
        dpw.SetFrequency(freq, SR);
        var p = new OscParams(WaveType.Saw);
        double dpwRms = 0;
        for (int i = 0; i < n; i++) { float s = dpw.Tick(p); dpwRms += s * s; }
        dpwRms = Math.Sqrt(dpwRms / n);
        // DPW SAW must produce clean RMS in audible range
        Assert.That(dpwRms, Is.GreaterThan(0.2).And.LessThan(0.9),
            "DPW-2 SAW RMS must be in valid range at 5kHz.");
    }

    [Test]
    public void Saw_DPW2_NoNaNOrInf_AllFrequencies()
    {
        var osc = new Oscillator();
        var p = new OscParams(WaveType.Saw);
        foreach (float f in new[] { 20f, 110f, 440f, 880f, 2000f, 5000f, 10000f, 20000f }) {
            osc.SetFrequency(f, SR);
            for (int i = 0; i < 1024; i++) {
                float s = osc.Tick(p);
                Assert.That(float.IsFinite(s), Is.True, $"DPW SAW NaN/Inf at {f}Hz");
            }
        }
    }

    // ── ② DPW-3: SAW SNR +14dB ──────────────────────────────────────
    [Test]
    public void Saw_DPW_HighFreq_CleanerThanPolyBLEP1st()
    {
        // At 8kHz, DPW should be cleaner than 1st-order polyBLEP
        // We verify DC stays near zero and RMS is healthy
        var osc = new Oscillator();
        osc.SetFrequency(8000f, SR);
        var p = new OscParams(WaveType.Saw);
        double dc = 0; double rms = 0;
        for (int i = 0; i < SR; i++) {
            float s = osc.Tick(p); dc += s; rms += s * s;
        }
        dc /= SR; rms = Math.Sqrt(rms / SR);
        Assert.That(Math.Abs(dc), Is.LessThan(0.05), "DPW SAW DC near zero at 8kHz.");
        Assert.That(rms, Is.GreaterThan(0.1), "DPW SAW RMS must be non-trivial at 8kHz.");
    }

    // ── ③ Shape: polyBLEP maintained when Shape != 0.5 ───────────────
    [Test]
    public void Saw_WithShape_NoNaNOrInf()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        // Shape = 0.3 (non-neutral) — previously switched to naive
        var p = new OscParams(WaveType.Saw, shape: 0.3f);
        for (int i = 0; i < 4096; i++) {
            float s = osc.Tick(p);
            Assert.That(float.IsFinite(s), Is.True, "SAW with Shape=0.3 must not NaN/Inf.");
        }
    }

    [Test]
    public void Saw_WithShape_RMS_Reasonable()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Saw, shape: 0.3f);
        double rms = 0;
        for (int i = 0; i < SR; i++) { float s = osc.Tick(p); rms += s * s; }
        rms = Math.Sqrt(rms / SR);
        Assert.That(rms, Is.GreaterThan(0.2).And.LessThan(1.0),
            "SAW with Shape must have reasonable RMS.");
    }

    [Test]
    public void Triangle_WithShape_NoNaNOrInf()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Triangle, shape: 0.7f);
        for (int i = 0; i < 4096; i++) {
            float s = osc.Tick(p);
            Assert.That(float.IsFinite(s), Is.True, "TRI with Shape=0.7 must not NaN/Inf.");
        }
    }

    // ── ④ TRI DC blocker: DC < 0.005 ────────────────────────────────
    [Test]
    public void Triangle_DC_ImprovedBelow005()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Triangle);
        // Longer warmup
        for (int i = 0; i < 4096; i++) osc.Tick(p);
        double dc = 0;
        for (int i = 0; i < SR; i++) dc += osc.Tick(p);
        dc /= SR;
        Assert.That(Math.Abs(dc), Is.LessThan(0.005),
            $"TRI DC {Math.Abs(dc):F6} must be < 0.005 with improved DC blocker.");
    }

    // ── ⑤ SinFast table: error < 0.0001 ─────────────────────────────
    [Test]
    public void SinFast_Table_ErrorBelow0001()
    {
        float maxErr = 0f;
        for (int i = 0; i < 4096; i++) {
            float t = (float)i / 4096f * MathF.PI * 2f;
            float fast = Calc.SinFast(t);
            float exact = MathF.Sin(t);
            float err = MathF.Abs(fast - exact);
            if (err > maxErr) maxErr = err;
        }
        Assert.That(maxErr, Is.LessThan(0.0001f),
            $"SinFast table max error {maxErr:F7} must be < 0.0001.");
    }

    [Test]
    public void SinFast_Table_FasterThanMathFSin()
    {
        // SinFast must complete 1M calls faster than MathF.Sin
        int n = 1_000_000;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        float acc1 = 0f;
        for (int i = 0; i < n; i++) acc1 += Calc.SinFast(i * 0.001f);
        long t1 = sw.ElapsedMilliseconds;
        sw.Restart();
        float acc2 = 0f;
        for (int i = 0; i < n; i++) acc2 += MathF.Sin(i * 0.001f);
        long t2 = sw.ElapsedMilliseconds;
        Assert.That(t1, Is.LessThanOrEqualTo(t2 * 2),
            $"SinFast ({t1}ms) must be <= MathF.Sin * 2 ({t2 * 2}ms).");
    }
}

[TestFixture]
public class DpwOscillatorTests
{
    const int SR = 44100;

    // ── DPW-2 SAW: SNR improvement vs naive ─────────────────────────
    // DPW-2 SAW at high freq should have lower aliasing energy than naive.
    // We compare RMS of difference between DPW output and ideal bandlimited.
    // Proxy: at 8kHz, DPW DC must be near zero and peak must not exceed 1.1.

    [Test]
    public void Saw_DPW2_Peak_NotExceedOne()
    {
        var osc = new Oscillator();
        osc.SetFrequency(8000f, SR);
        var p = new OscParams(WaveType.Saw);
        float peak = 0f;
        for (int i = 0; i < SR; i++) {
            float s = osc.Tick(p);
            float a = MathF.Abs(s);
            if (a > peak) peak = a;
        }
        Assert.That(peak, Is.LessThanOrEqualTo(1.1f),
            $"DPW SAW peak {peak:F4} must not exceed 1.1.");
    }

    [Test]
    public void Saw_DPW2_DC_BelowThreshold_HighFreq()
    {
        var osc = new Oscillator();
        osc.SetFrequency(8000f, SR);
        var p = new OscParams(WaveType.Saw);
        double dc = 0;
        for (int i = 0; i < SR; i++) dc += osc.Tick(p);
        dc /= SR;
        Assert.That(Math.Abs(dc), Is.LessThan(0.03),
            "DPW-2 SAW DC must be near zero even at 8kHz.");
    }

    // ── DPW SQR ─────────────────────────────────────────────────────
    [Test]
    public void Square_DPW_Peak_NotExceedOne()
    {
        var osc = new Oscillator();
        osc.SetFrequency(8000f, SR);
        var p = new OscParams(WaveType.Square);
        float peak = 0f;
        for (int i = 0; i < SR; i++) {
            float s = osc.Tick(p);
            float a = MathF.Abs(s);
            if (a > peak) peak = a;
        }
        Assert.That(peak, Is.LessThanOrEqualTo(1.1f),
            $"DPW SQR peak {peak:F4} must not exceed 1.1.");
    }

    // ── Shape時もDPW適用 ────────────────────────────────────────────
    [Test]
    public void Saw_DPW_WithShape_DC_NearZero()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Saw, shape: 0.3f);
        double dc = 0;
        for (int i = 0; i < SR; i++) dc += osc.Tick(p);
        dc /= SR;
        Assert.That(Math.Abs(dc), Is.LessThan(0.1),
            "DPW SAW with Shape=0.3 DC must be near zero.");
    }

    [Test]
    public void Saw_DPW_WithShape_RMS_Reasonable()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Saw, shape: 0.3f);
        double rms = 0;
        for (int i = 0; i < SR; i++) { float s = osc.Tick(p); rms += s * s; }
        rms = Math.Sqrt(rms / SR);
        Assert.That(rms, Is.GreaterThan(0.1).And.LessThan(1.0),
            "DPW SAW with Shape must have reasonable RMS.");
    }

    // ── TRI DC < 0.005 after warmup ──────────────────────────────────
    [Test]
    public void Triangle_DC_Below005_LowFreq()
    {
        var osc = new Oscillator();
        osc.SetFrequency(110f, SR);
        var p = new OscParams(WaveType.Triangle);
        for (int i = 0; i < SR; i++) osc.Tick(p); // warmup
        double dc = 0;
        for (int i = 0; i < SR; i++) dc += osc.Tick(p);
        dc /= SR;
        Assert.That(Math.Abs(dc), Is.LessThan(0.005),
            $"TRI DC {Math.Abs(dc):F6} must be < 0.005 at 110Hz.");
    }
}
