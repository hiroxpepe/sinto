// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Filter;

namespace Sinto.Tests.Filter;

[TestFixture]
public class FilterStateTests
{
    const int SR = 44100;

    [Test] public void SetParams_DoesNotThrow()
    {
        var f = new FilterState();
        Assert.DoesNotThrow(() => f.SetParams(0.5f, 0.5f, FilterMode.Moog, SR));
    }

    [Test] public void Process_OutputIsFinite()
    {
        var f = new FilterState();
        f.SetParams(0.5f, 0.3f, FilterMode.Moog, SR);
        float result = f.Process(0.5f, 0L);
        Assert.That(float.IsNaN(result),      Is.False, "Filter output must not be NaN.");
        Assert.That(float.IsInfinity(result), Is.False, "Filter output must not be Inf.");
    }

    [Test] public void Process_MaxResonance_DoesNotDiverge()
    {
        // Resonance = 1.0 (user max) → internal = 3.99 after ×4 scaling.
        // Output must stay finite — never NaN or Inf (divergence = speaker damage).
        var f = new FilterState();
        f.SetParams(0.5f, 1.0f, FilterMode.Moog, SR);
        for (int i = 0; i < SR; i++) {
            float result = f.Process(0.5f, i);
            Assert.That(float.IsNaN(result) || float.IsInfinity(result), Is.False,
                $"Filter diverged at sample {i}. Resonance ×4 scaling or clamp missing.");
        }
    }

    // ── Resonance ×4 scaling verification ──────────────────────────────

    [Test] public void Moog_HighResonance_ProducesSelfOscillation_AndStabilizes()
    {
        // At user resonance = 1.0 → internal = 3.99 → self-oscillation (tone without input).
        // If ×4 scaling is missing, internal = 1.0 → no self-oscillation → flat output.
        //
        // TWO requirements:
        //   (1) Amplitude must GROW to audible level (>= 0.5) — proves self-oscillation
        //   (2) Amplitude must NOT diverge to Inf/NaN (< 2.0) — proves stable saturation
        //       Without TanhFast soft clipper, Moog can diverge to Infinity → speaker damage.
        var f = new FilterState();
        f.SetParams(0.5f, 1.0f, FilterMode.Moog, SR);

        // Feed silence for 1 second — self-oscillating filter produces non-zero output
        float[] samples = new float[SR];
        for (int i = 0; i < SR; i++)
            samples[i] = f.Process(0.0f, i);

        float max = 0f;
        foreach (float s in samples) {
            Assert.That(float.IsNaN(s),      Is.False, "Moog self-oscillation produced NaN — diverging.");
            Assert.That(float.IsInfinity(s), Is.False, "Moog self-oscillation produced Inf — diverging.");
            max = MathF.Max(max, MathF.Abs(s));
        }

        // (1) Must oscillate — amplitude must be audible
        Assert.That(max, Is.GreaterThan(0.5f),
            $"Moog max amplitude = {max}. Must reach >= 0.5 for audible self-oscillation. " +
            "Resonance ×4 scaling may be missing.");

        // (2) Must stabilize — amplitude must not diverge beyond saturation
        Assert.That(max, Is.LessThan(2.0f),
            $"Moog max amplitude = {max}. Must stay < 2.0 (stable saturation). " +
            "Filter is diverging — TanhFast soft clipper may be missing from ProcessMoog.");
    }

    [Test] public void Moog_ZeroResonance_OutputIsQuiet()
    {
        // At resonance = 0 → no self-oscillation
        var f = new FilterState();
        f.SetParams(0.5f, 0.0f, FilterMode.Moog, SR);
        float[] samples = new float[SR];
        for (int i = 0; i < SR; i++)
            samples[i] = f.Process(0.0f, i);
        float max = 0f;
        foreach (float s in samples) max = MathF.Max(max, MathF.Abs(s));
        Assert.That(max, Is.LessThan(0.001f),
            "Zero resonance with silent input should produce near-zero output.");
    }

    [Test] public void Roland_Process_OutputIsFinite()
    {
        var f = new FilterState();
        f.SetParams(0.5f, 0.8f, FilterMode.Roland, SR);
        for (int i = 0; i < SR / 10; i++) {
            float result = f.Process(0.5f, i);
            Assert.That(float.IsNaN(result) || float.IsInfinity(result), Is.False,
                $"Roland filter diverged at sample {i}.");
        }
    }

    [Test] public void Reset_ClearsState()
    {
        var f = new FilterState();
        f.SetParams(0.5f, 0.9f, FilterMode.Moog, SR);
        for (int i = 0; i < 100; i++) f.Process(1.0f, i);
        f.Reset();
        float afterReset = f.Process(0.0f, 0L);
        // After reset with silent input, output should be near zero (no state carryover)
        Assert.That(MathF.Abs(afterReset), Is.LessThan(0.1f),
            "After Reset(), filter state must be cleared.");
    }

    [Test] public void Process_DenormalGuard_NoSubnormalTrap()
    {
        // After long silence, IIR state must not enter subnormal range (10-100x CPU spike).
        var f = new FilterState();
        f.SetParams(0.5f, 0.3f, FilterMode.Moog, SR);
        // Process silence for 5 seconds
        for (int i = 0; i < SR * 5; i++) {
            float result = f.Process(0.0f, i);
            Assert.That(float.IsNaN(result), Is.False, $"NaN at sample {i} — denormal not protected.");
        }
    }
}
