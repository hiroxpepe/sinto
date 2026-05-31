#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

[TestFixture]
public class OscillatorTests
{
    const int SR = 44100;

    static OscParams MakeParams(WaveType wave,
        Interpolation interp = Interpolation.Linear, float pw = 0.5f, float shape = 0.5f)
        => new(wave, interp, 0f, pw, shape, 1f);

    [Test] public void Sine_OutputRangeAndAmplitudeAreValid()
    {
        // InRange(-1,+1) alone passes if Tick() always returns 0.0f (silent bug).
        // Also verify peak amplitude (Max >= 0.9, Min <= -0.9) to prove audio is actually generated.
        Calc.Initialize();
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Sine);
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = osc.Tick(p);

        Assert.That(samples.Max(), Is.GreaterThanOrEqualTo(0.9f),
            "Sine wave peak must reach >= 0.9 (not silent).");
        Assert.That(samples.Min(), Is.LessThanOrEqualTo(-0.9f),
            "Sine wave trough must reach <= -0.9 (not silent).");
        Assert.That(samples.Max(), Is.LessThanOrEqualTo(1.0f + 1e-3f),
            "Sine wave must not exceed +1.0.");
        Assert.That(samples.Min(), Is.GreaterThanOrEqualTo(-1.0f - 1e-3f),
            "Sine wave must not go below -1.0.");
    }

    [Test] public void Saw_OutputRangeAndAmplitudeAreValid()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Saw);
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = osc.Tick(p);
        Assert.That(samples.Max(), Is.GreaterThanOrEqualTo(0.9f), "Saw peak must reach >= 0.9.");
        Assert.That(samples.Min(), Is.LessThanOrEqualTo(-0.9f), "Saw trough must reach <= -0.9.");
        Assert.That(samples.Max(), Is.LessThanOrEqualTo(1.0f + 1e-3f));
        Assert.That(samples.Min(), Is.GreaterThanOrEqualTo(-1.0f - 1e-3f));
    }

    [Test] public void Square_OutputApproximatelyPlusOrMinusOne_AndBothValuesAppear()
    {
        // polyBLEP-corrected square: bandwidth-limited so values near transitions deviate
        // slightly from ±1. This is correct band-limited behavior (analog-like).
        // Most samples are very close to ±1; only ~2 samples per cycle differ.
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Square, pw: 0.5f);
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) {
            samples[i] = osc.Tick(p);
            // Allow up to ±1.5 range (polyBLEP adds correction near discontinuities)
            Assert.That(samples[i], Is.InRange(-1.5f, 1.5f),
                $"Square wave output {samples[i]} far out of range at sample {i}");
        }
        // Both +1 and -1 must appear (not always 0)
        Assert.That(samples.Any(s => s > 0.9f), Is.True, "Square wave never reached +1.");
        Assert.That(samples.Any(s => s < -0.9f), Is.True, "Square wave never reached -1.");
    }

    [Test] public void Noise_OutputRangeAndAmplitudeAreValid()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Noise);
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = osc.Tick(p);
        // Noise RMS should be around 0.577 for uniform distribution
        double rms = Math.Sqrt(samples.Average(s => (double)s * s));
        Assert.That(rms, Is.GreaterThan(0.1), "Noise RMS must be > 0.1 (not silent).");
        Assert.That(samples.Max(), Is.LessThanOrEqualTo(1.0f + 1e-3f));
        Assert.That(samples.Min(), Is.GreaterThanOrEqualTo(-1.0f - 1e-3f));
    }

    [Test] public void SetFrequency_Zero_ClampedToMinimum()
    {
        var osc = new Oscillator();
        osc.SetFrequency(0f, SR); // should clamp to 1Hz
        var p = MakeParams(WaveType.Sine);
        Assert.DoesNotThrow(() => osc.Tick(p));
    }

    [Test] public void PulseWidth_Zero_ClampedTo0_01()
    {
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Square, pw: 0.0f); // should clamp to 0.01
        Assert.DoesNotThrow(() => osc.Tick(p));
    }

    [Test] public void Phase_WrapsAround_After2Pi()
    {
        // _phase must wrap to [0, 2π) each cycle.
        // Without wrap: _phase grows to millions → float precision loss → pitch drift.
        // Verify: 1 second of 440Hz produces 440 complete cycles without drift.
        Calc.Initialize();
        var osc  = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Sine);

        // Run for 1 second (44100 samples = 440 cycles)
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = osc.Tick(p);

        // Count zero crossings (positive-going) ≈ 440 for 440Hz
        int crossings = 0;
        for (int i = 1; i < samples.Length; i++)
            if (samples[i - 1] < 0f && samples[i] >= 0f) crossings++;

        Assert.That(crossings, Is.InRange(438, 442),
            $"Expected ~440 zero crossings for 440Hz, got {crossings}. " +
            "Phase wrap may not be implemented, causing pitch drift.");
    }

    [Test] public void Phase_LongDuration_NoPrecisionLoss()
    {
        // After 10 minutes at 440Hz, phase must still produce correct frequency.
        Calc.Initialize();
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Sine);

        // Skip 10 minutes of samples
        for (int i = 0; i < SR * 600; i++) osc.Tick(p);

        // Now measure frequency over 1 second
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = osc.Tick(p);

        int crossings = 0;
        for (int i = 1; i < samples.Length; i++)
            if (samples[i - 1] < 0f && samples[i] >= 0f) crossings++;

        Assert.That(crossings, Is.InRange(435, 445),
            $"After 10 min, expected ~440 crossings, got {crossings}. Phase drift detected.");
    }

    [Test] public void NearestNeighbor_ProducesOutput()
    {
        Calc.Initialize();
        var osc = new Oscillator();
        osc.SetFrequency(440f, SR);
        var p = MakeParams(WaveType.Sine, Interpolation.NearestNeighbor);
        float s = osc.Tick(p);
        Assert.That(float.IsNaN(s), Is.False);
    }
}
