// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Safety tests for all waveforms under all shapes and combinations.
/// - No NaN/Inf for any wave at any shape.
/// - Peak must not exceed 1.1 (no blow-up).
/// - RMS must be non-trivial (not silent).
/// - Composite waveforms (SAW+SQR etc) must also be stable.
/// </summary>
[TestFixture]
public class WaveformSafetyTests
{
    const int SR = 44100;
    static readonly float[] SHAPES  = { 0.05f, 0.1f, 0.3f, 0.5f, 0.7f, 0.9f, 0.95f };
    static readonly float[] FREQS   = { 55f, 220f, 440f, 880f, 4000f, 10000f };

    static (float peak, float rms, bool hasNaN) Measure(WaveType w, float shape, float freq, float pw = 0.5f)
    {
        var osc = new Oscillator();
        osc.SetFrequency(freq, SR);
        var p = new OscParams(w, pulseWidth: pw, shape: shape);
        float peak = 0f; double rmsAcc = 0; bool nan = false;
        for (int i = 0; i < 2048; i++) {
            float s = osc.Tick(p);
            if (!float.IsFinite(s)) { nan = true; break; }
            float a = MathF.Abs(s);
            if (a > peak) peak = a;
            rmsAcc += s * s;
        }
        return (peak, (float)Math.Sqrt(rmsAcc / 2048), nan);
    }

    // ── 単体波形 × 全Shape × 全周波数 ───────────────────────────────
    [Test]
    public void Saw_AllShapes_AllFreqs_StableAndAudible()
    {
        foreach (float shape in SHAPES)
        foreach (float freq  in FREQS) {
            var (peak, rms, nan) = Measure(WaveType.Saw, shape, freq);
            Assert.That(nan,  Is.False,          $"SAW nan shape={shape} freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SAW blow-up shape={shape} freq={freq} peak={peak:F3}");
            Assert.That(rms,  Is.GreaterThan(0.05f), $"SAW silent shape={shape} freq={freq}");
        }
    }

    [Test]
    public void Square_AllShapes_AllFreqs_Stable()
    {
        foreach (float shape in SHAPES)
        foreach (float freq  in FREQS) {
            var (peak, rms, nan) = Measure(WaveType.Square, shape, freq);
            Assert.That(nan,  Is.False,          $"SQR nan shape={shape} freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SQR blow-up shape={shape} freq={freq}");
            Assert.That(rms,  Is.GreaterThan(0.05f), $"SQR silent shape={shape} freq={freq}");
        }
    }

    [Test]
    public void Triangle_AllShapes_AllFreqs_Stable()
    {
        foreach (float shape in SHAPES)
        foreach (float freq  in FREQS) {
            var (peak, rms, nan) = Measure(WaveType.Triangle, shape, freq);
            Assert.That(nan,  Is.False,          $"TRI nan shape={shape} freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"TRI blow-up shape={shape} freq={freq}");
            Assert.That(rms,  Is.GreaterThan(0.05f), $"TRI silent shape={shape} freq={freq}");
        }
    }

    [Test]
    public void Sine_AllShapes_AllFreqs_Stable()
    {
        foreach (float shape in SHAPES)
        foreach (float freq  in FREQS) {
            var (peak, rms, nan) = Measure(WaveType.Sine, shape, freq);
            Assert.That(nan,  Is.False,          $"SIN nan shape={shape} freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SIN blow-up shape={shape} freq={freq}");
            Assert.That(rms,  Is.GreaterThan(0.05f), $"SIN silent shape={shape} freq={freq}");
        }
    }

    [Test]
    public void Noise_AllFreqs_Stable()
    {
        foreach (float freq in FREQS) {
            var (peak, rms, nan) = Measure(WaveType.Noise, 0.5f, freq);
            Assert.That(nan,  Is.False,          $"NOISE nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"NOISE blow-up freq={freq}");
        }
    }

    [Test]
    public void Pink_AllFreqs_Stable()
    {
        foreach (float freq in FREQS) {
            var (peak, rms, nan) = Measure(WaveType.Pink, 0.5f, freq);
            Assert.That(nan,  Is.False,          $"PINK nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"PINK blow-up freq={freq}");
        }
    }

    // ── 複合波形 ────────────────────────────────────────────────────
    [Test]
    public void Composite_SawSqr_Stable()
    {
        var w = WaveType.Saw | WaveType.Square;
        foreach (float freq in FREQS) {
            var (peak, rms, nan) = Measure(w, 0.5f, freq);
            Assert.That(nan,  Is.False,          $"SAW+SQR nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SAW+SQR blow-up freq={freq} peak={peak:F3}");
            Assert.That(rms,  Is.GreaterThan(0.05f), $"SAW+SQR silent freq={freq}");
        }
    }

    [Test]
    public void Composite_SawTri_Stable()
    {
        var w = WaveType.Saw | WaveType.Triangle;
        foreach (float freq in FREQS) {
            var (peak, rms, nan) = Measure(w, 0.5f, freq);
            Assert.That(nan,  Is.False,          $"SAW+TRI nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SAW+TRI blow-up freq={freq} peak={peak:F3}");
        }
    }

    [Test]
    public void Composite_SawSqrTri_Stable()
    {
        var w = WaveType.Saw | WaveType.Square | WaveType.Triangle;
        foreach (float freq in FREQS) {
            var (peak, rms, nan) = Measure(w, 0.5f, freq);
            Assert.That(nan,  Is.False,          $"SAW+SQR+TRI nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SAW+SQR+TRI blow-up freq={freq} peak={peak:F3}");
        }
    }

    [Test]
    public void Composite_SinSaw_Stable()
    {
        var w = WaveType.Sine | WaveType.Saw;
        foreach (float freq in FREQS) {
            var (peak, rms, nan) = Measure(w, 0.5f, freq);
            Assert.That(nan,  Is.False,          $"SIN+SAW nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SIN+SAW blow-up freq={freq} peak={peak:F3}");
        }
    }

    [Test]
    public void Composite_AllWaves_WithShape_Stable()
    {
        // 全波形同時選択 × extreme shape
        var w = WaveType.Saw | WaveType.Square | WaveType.Triangle | WaveType.Sine;
        foreach (float shape in new[] { 0.05f, 0.5f, 0.95f })
        foreach (float freq  in new[] { 440f, 4000f }) {
            var (peak, rms, nan) = Measure(w, shape, freq);
            Assert.That(nan,  Is.False,          $"ALL nan shape={shape} freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"ALL blow-up shape={shape} freq={freq} peak={peak:F3}");
        }
    }

    // ── PulseWidth変化 ───────────────────────────────────────────────
    [Test]
    public void Square_ExtremePW_Stable()
    {
        foreach (float pw in new[] { 0.01f, 0.1f, 0.5f, 0.9f, 0.99f })
        foreach (float freq in new[] { 440f, 4000f }) {
            var (peak, rms, nan) = Measure(WaveType.Square, 0.5f, freq, pw);
            Assert.That(nan,  Is.False,          $"SQR PW={pw} nan freq={freq}");
            Assert.That(peak, Is.LessThan(1.2f), $"SQR PW={pw} blow-up freq={freq}");
        }
    }
}
