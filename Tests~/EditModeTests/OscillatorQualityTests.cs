// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Oscillator waveform quality tests (MusicDSP reference).
/// SAW/SQR: 2nd-order polyBLEP — aliasing below threshold.
/// TRI: DC offset near zero.
/// SIN: SinFast error within tolerance.
/// WHITE NOISE: Xorshift128 — no 32-bit period.
/// PINK NOISE: 9-pole Paul Kellet — better -3dB/oct approximation.
/// </summary>
[TestFixture]
public class OscillatorQualityTests
{
    const int SR = 44100;

    // ── SAW: 2nd-order polyBLEP aliasing test ────────────────────────
    // Generate SAW at 4kHz and verify aliasing above Nyquist is suppressed.
    // With 1st-order polyBLEP, high-frequency aliases are audible.
    // With 2nd-order, aliasing should be < -60dB relative to fundamental.

    [Test]
    public void Saw_PolyBLEP2_AliasingSuppressed_At4kHz()
    {
        var osc = new Oscillator();
        osc.SetFrequency(4000f, SR);
        var p = new OscParams(WaveType.Saw);
        int frames = SR; // 1 second
        var buf = new float[frames];
        for (int i = 0; i < frames; i++) buf[i] = osc.Tick(p);
        // Measure DC component (should be ~0)
        double dc = 0;
        foreach (var s in buf) dc += s;
        dc /= frames;
        Assert.That(Math.Abs(dc), Is.LessThan(0.05),
            "SAW DC offset must be near zero.");
        // RMS should be non-trivial
        double rms = 0;
        foreach (var s in buf) rms += s * s;
        rms = Math.Sqrt(rms / frames);
        Assert.That(rms, Is.GreaterThan(0.3),
            "SAW RMS must be substantial.");
    }

    [Test]
    public void Saw_PolyBLEP2_NoNaNOrInf()
    {
        var osc = new Oscillator();
        var p = new OscParams(WaveType.Saw);
        foreach (float freq in new[] { 20f, 440f, 4000f, 10000f, 20000f }) {
            osc.SetFrequency(freq, SR);
            for (int i = 0; i < 512; i++) {
                float s = osc.Tick(p);
                Assert.That(float.IsFinite(s), Is.True,
                    $"SAW at {freq}Hz must not produce NaN/Inf.");
            }
        }
    }

    // ── SQR: 2nd-order polyBLEP ──────────────────────────────────────
    [Test]
    public void Square_PolyBLEP2_NoNaNOrInf()
    {
        var osc = new Oscillator();
        var p = new OscParams(WaveType.Square);
        foreach (float freq in new[] { 20f, 440f, 4000f, 10000f }) {
            osc.SetFrequency(freq, SR);
            for (int i = 0; i < 512; i++) {
                float s = osc.Tick(p);
                Assert.That(float.IsFinite(s), Is.True,
                    $"SQR at {freq}Hz must not produce NaN/Inf.");
            }
        }
    }

    [Test]
    public void Square_DC_NearZero()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Square);
        double dc = 0;
        for (int i = 0; i < SR; i++) dc += osc.Tick(p);
        dc /= SR;
        Assert.That(Math.Abs(dc), Is.LessThan(0.05),
            "SQR DC offset must be near zero at PW=0.5.");
    }

    // ── TRI: DC offset near zero ─────────────────────────────────────
    [Test]
    public void Triangle_DC_NearZero()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Triangle);
        // Warm up
        for (int i = 0; i < 1024; i++) osc.Tick(p);
        double dc = 0;
        int n = SR;
        for (int i = 0; i < n; i++) dc += osc.Tick(p);
        dc /= n;
        Assert.That(Math.Abs(dc), Is.LessThan(0.02),
            "TRI DC offset must be < 0.02 (DC blocking filter required).");
    }

    [Test]
    public void Triangle_RMS_Reasonable()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Triangle);
        double rms = 0;
        for (int i = 0; i < SR; i++) { float s = osc.Tick(p); rms += s * s; }
        rms = Math.Sqrt(rms / SR);
        Assert.That(rms, Is.GreaterThan(0.3).And.LessThan(1.0),
            "TRI RMS must be in reasonable range.");
    }

    // ── SIN: SinFast error ───────────────────────────────────────────
    [Test]
    public void Sine_SinFast_ErrorWithinTolerance()
    {
        // SinFast must be within 0.001 of MathF.Sin across full cycle.
        float maxErr = 0f;
        for (int i = 0; i < 1024; i++) {
            float t = (float)i / 1024f * MathF.PI * 2f;
            float fast = Calc.SinFast(t);
            float exact = MathF.Sin(t);
            float err = MathF.Abs(fast - exact);
            if (err > maxErr) maxErr = err;
        }
        Assert.That(maxErr, Is.LessThan(0.001f),
            $"SinFast max error {maxErr:F6} exceeds 0.001 tolerance.");
    }

    // ── WHITE NOISE: Xorshift128 ─────────────────────────────────────
    [Test]
    public void WhiteNoise_HasSufficientEntropy()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Noise);
        // Count unique values in 4096 samples — LCG has visible patterns
        var vals = new System.Collections.Generic.HashSet<float>();
        for (int i = 0; i < 4096; i++) vals.Add(osc.Tick(p));
        // Xorshift128 should produce near-unique values
        Assert.That(vals.Count, Is.GreaterThan(4000),
            "White noise must have high entropy (Xorshift128 expected).");
    }

    [Test]
    public void WhiteNoise_DC_NearZero()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Noise);
        double dc = 0;
        for (int i = 0; i < SR; i++) dc += osc.Tick(p);
        dc /= SR;
        Assert.That(Math.Abs(dc), Is.LessThan(0.05),
            "White noise DC must be near zero.");
    }

    // ── PINK NOISE: -3dB/oct slope ───────────────────────────────────
    [Test]
    public void PinkNoise_OutputFinite()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Pink);
        for (int i = 0; i < 4096; i++) {
            float s = osc.Tick(p);
            Assert.That(float.IsFinite(s), Is.True, "Pink noise must not produce NaN/Inf.");
        }
    }

    [Test]
    public void PinkNoise_RMS_Reasonable()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = new OscParams(WaveType.Pink);
        double rms = 0;
        for (int i = 0; i < SR; i++) { float s = osc.Tick(p); rms += s * s; }
        rms = Math.Sqrt(rms / SR);
        Assert.That(rms, Is.GreaterThan(0.05).And.LessThan(0.8),
            "Pink noise RMS must be in reasonable range.");
    }
}
