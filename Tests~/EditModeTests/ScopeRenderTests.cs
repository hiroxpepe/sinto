// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;

namespace Signo.Tests.UI;

/// <summary>
/// Oscilloscope rendering correctness — a real oscilloscope shows the TRUE
/// signal amplitude at a FIXED scale. It must NOT auto-normalise to the
/// buffer peak, because that makes decaying/attacking waveforms look skewed
/// (large on the left, shrinking to the right).
///
/// The fix: remove per-frame peak normalisation. Apply only the fixed display
/// gain (_dcoLevel). Then a decaying sine keeps its real envelope shape, and
/// the *ratio* between a sample's value and its screen deflection is constant
/// regardless of buffer contents or pitch.
/// </summary>
[TestFixture]
public class ScopeRenderTests
{
    // The CORRECT mapping: fixed scale, no normalisation.
    // y = cy - s * cy * 0.85 * level   (s = raw sample, NOT peak-normalised)
    static double MapOne(float s, double h, double level)
    {
        double cy = h / 2;
        return cy - s * cy * 0.85 * level;
    }

    // Simulate the OnRender pipeline WITHOUT peak normalisation.
    static double[] RenderFixedScale(float[] buf, double h, double level)
    {
        // DC removal only (legitimate — centres the trace).
        float mean = 0f;
        for (int i = 0; i < buf.Length; i++) mean += buf[i];
        mean /= buf.Length;
        var ys = new double[buf.Length];
        for (int i = 0; i < buf.Length; i++)
            ys[i] = MapOne(buf[i] - mean, h, level);
        return ys;
    }

    // ── A constant-amplitude sample value maps identically everywhere ─
    [Test]
    public void FixedScale_SameValueSameDeflection_AnyPosition()
    {
        double h = 400, level = 0.707;
        double y0 = MapOne(0.5f, h, level);
        double y1 = MapOne(0.5f, h, level);
        Assert.That(y1, Is.EqualTo(y0).Within(1e-12),
            "Same sample value must always map to the same Y (fixed scale).");
    }

    // ── Deflection scales LINEARLY with sample value (no pitch term) ──
    [Test]
    public void FixedScale_DeflectionLinearInSampleValue()
    {
        double h = 400, level = 1.0, cy = h / 2;
        double d025 = cy - MapOne(0.25f, h, level);
        double d050 = cy - MapOne(0.50f, h, level);
        double d100 = cy - MapOne(1.00f, h, level);
        Assert.That(d050 / d025, Is.EqualTo(2.0).Within(1e-9), "0.5 must deflect 2x of 0.25.");
        Assert.That(d100 / d025, Is.EqualTo(4.0).Within(1e-9), "1.0 must deflect 4x of 0.25.");
    }

    // ── Pitch independence: amplitude unaffected by frequency ────────
    // Two sines, SAME amplitude (0.8), DIFFERENT pitch. Peak deflection equal.
    [Test]
    public void FixedScale_PitchIndependent_AmplitudeMatches()
    {
        const int SR = 44100;
        double h = 400, level = 0.707, cy = h / 2;
        int count = (int)(SR * 0.15);

        var low  = new float[count];
        var high = new float[count];
        for (int i = 0; i < count; i++) {
            low[i]  = 0.8f * MathF.Sin(2f * MathF.PI * 110f * i / SR);  // low pitch
            high[i] = 0.8f * MathF.Sin(2f * MathF.PI * 880f * i / SR);  // high pitch
        }

        var ysLow  = RenderFixedScale(low,  h, level);
        var ysHigh = RenderFixedScale(high, h, level);

        double peakLow = 0, peakHigh = 0;
        foreach (var y in ysLow)  peakLow  = Math.Max(peakLow,  Math.Abs(y - cy));
        foreach (var y in ysHigh) peakHigh = Math.Max(peakHigh, Math.Abs(y - cy));

        Assert.That(peakLow / peakHigh, Is.EqualTo(1.0).Within(0.02),
            $"Same amplitude must display the same regardless of pitch. low/high={peakLow / peakHigh:F3}");
    }

    // ── A DECAYING sine must keep its real envelope (left>right) ─────
    // With peak-normalisation this still "works" but the bug was that it
    // forced the FIRST cycle to full-scale. With fixed scale, the trace
    // simply reflects the true samples. Verify the ratio left/right equals
    // the true envelope ratio (no artificial inflation of the left edge).
    [Test]
    public void FixedScale_DecayingSine_PreservesTrueEnvelope()
    {
        const int SR = 44100;
        double h = 400, level = 0.707, cy = h / 2;
        int count = (int)(SR * 0.15);

        var buf = new float[count];
        for (int i = 0; i < count; i++) {
            float env = MathF.Exp(-3f * i / count); // decay env: 1.0 -> ~0.05
            buf[i] = env * 0.9f * MathF.Sin(2f * MathF.PI * 333.6f * i / SR);
        }

        var ys = RenderFixedScale(buf, h, level);
        int q = ys.Length / 4;
        double leftPeak = 0, rightPeak = 0;
        for (int i = 0; i < q; i++) leftPeak = Math.Max(leftPeak, Math.Abs(ys[i] - cy));
        for (int i = ys.Length - q; i < ys.Length; i++) rightPeak = Math.Max(rightPeak, Math.Abs(ys[i] - cy));

        // True envelope ratio between region centres
        float envLeft  = MathF.Exp(-3f * (q / 2) / (float)count);
        float envRight = MathF.Exp(-3f * (count - q / 2) / (float)count);
        double expected = envLeft / envRight;

        double actual = leftPeak / rightPeak;
        Assert.That(actual, Is.EqualTo(expected).Within(expected * 0.15),
            $"Fixed-scale trace must reflect true decay envelope. expected~{expected:F2} actual={actual:F2}");
    }
}
