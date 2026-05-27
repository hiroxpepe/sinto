#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class CalcTests
{
    [SetUp] public void SetUp() => Calc.Initialize();

    [Test] public void SinFast_Zero_ReturnsZero()
        => Assert.That(Calc.SinFast(0.0), Is.EqualTo(0f).Within(0.01f));

    [Test] public void SinFast_HalfPi_ReturnsOne()
        => Assert.That(Calc.SinFast(Math.PI / 2), Is.EqualTo(1f).Within(0.01f));

    [Test] public void SinFast_Pi_ReturnsZero()
        => Assert.That(Calc.SinFast(Math.PI), Is.EqualTo(0f).Within(0.01f));

    [Test] public void SinFast_ThreeHalfPi_ReturnsMinusOne()
        => Assert.That(Calc.SinFast(3 * Math.PI / 2), Is.EqualTo(-1f).Within(0.01f));

    [Test] public void SinFast_TwoPi_ReturnsZero()
        => Assert.That(Calc.SinFast(2 * Math.PI), Is.EqualTo(0f).Within(0.01f));

    [Test] public void TanhFast_Zero_ReturnsZero()
        => Assert.That(Calc.TanhFast(0f), Is.EqualTo(0f).Within(0.01f));

    [Test] public void TanhFast_One_ReturnsApprox0762()
        => Assert.That(Calc.TanhFast(1f), Is.EqualTo(0.762f).Within(0.05f));

    [Test] public void TanhFast_Three_IsNearOne()
        => Assert.That(Calc.TanhFast(3f), Is.EqualTo(1f).Within(0.01f));

    [Test] public void TanhFast_MinusThree_IsNearMinusOne()
        => Assert.That(Calc.TanhFast(-3f), Is.EqualTo(-1f).Within(0.01f));

    [Test] public void PitchRatioFast_Zero_ReturnsOne()
        => Assert.That(Calc.PitchRatioFast(0f), Is.EqualTo(1f).Within(0.001f));

    [Test] public void PitchRatioFast_Twelve_ReturnsTwo()
        => Assert.That(Calc.PitchRatioFast(12f), Is.EqualTo(2f).Within(0.01f));

    [Test] public void PitchRatioFast_MinusTwelve_ReturnsHalf()
        => Assert.That(Calc.PitchRatioFast(-12f), Is.EqualTo(0.5f).Within(0.01f));

    [Test] public void PitchRatioFast_FractionalSemitone_IsSmooth()
    {
        // LFO pitch modulation uses fractional semitones (e.g. +0.5, +1.34).
        // Integer-only LUT would produce staircase output (zipper noise).
        // Verify that consecutive fractional inputs produce monotonically increasing output.
        Calc.Initialize();
        float prev = Calc.PitchRatioFast(0f);
        for (float s = 0.01f; s <= 2.0f; s += 0.01f) {
            float curr = Calc.PitchRatioFast(s);
            Assert.That(curr, Is.GreaterThan(prev),
                $"PitchRatioFast({s:F2}) must be > PitchRatioFast({s-0.01f:F2}). " +
                $"Staircase output indicates integer-only LUT (zipper noise risk).");
            prev = curr;
        }
    }

    [Test] public void PitchRatioFast_FractionalSemitone_Precision()
    {
        // +0.5 semitones ≈ 2^(0.5/12) ≈ 1.02930
        Calc.Initialize();
        float expected = (float)Math.Pow(2.0, 0.5 / 12.0);
        Assert.That(Calc.PitchRatioFast(0.5f), Is.EqualTo(expected).Within(0.0001f),
            "Fractional semitone must be computed continuously, not rounded to integer.");
    }

    // ── Extreme value safety ──────────────────────────────────────

    [TestCase(-100f)]
    [TestCase(-50f)]
    [TestCase(-25f)]
    [TestCase(25f)]
    [TestCase(50f)]
    [TestCase(100f)]
    [TestCase(float.MaxValue)]
    [TestCase(float.MinValue)]
    public void PitchRatioFast_ExtremeValues_DoNotThrow(float semitones)
    {
        // LFO + Pitch Envelope can produce semitone values far outside [-24, +24].
        // LUT implementation with fixed size WILL throw IndexOutOfRangeException
        // if input is not clamped before array access → audio thread instant death.
        Calc.Initialize();
        Assert.DoesNotThrow(() => Calc.PitchRatioFast(semitones),
            $"PitchRatioFast({semitones}) must not throw. " +
            "LUT must clamp index or fall back to polynomial for out-of-range input.");
    }

    [TestCase(-100f)]
    [TestCase(100f)]
    public void PitchRatioFast_ExtremeValues_ReturnFinitePositiveResult(float semitones)
    {
        Calc.Initialize();
        float result = Calc.PitchRatioFast(semitones);
        Assert.That(float.IsNaN(result),      Is.False, $"PitchRatioFast({semitones}) returned NaN.");
        Assert.That(float.IsInfinity(result), Is.False, $"PitchRatioFast({semitones}) returned Inf.");
        Assert.That(result, Is.GreaterThan(0f),          $"PitchRatioFast({semitones}) must be > 0.");
    }
}
