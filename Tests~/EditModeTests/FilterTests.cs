// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;
using Filter = Sinto.Core.Synth.Filter;

namespace Sinto.Tests.Synth;

[TestFixture]
public class FilterTests
{
    const int SR = 44100;

    [Test] public void SetParams_DoesNotThrow()
    {
        var f = new Filter();
        Assert.DoesNotThrow(() => f.SetParams(0.5f, 0.5f, FilterKind.Moog, SR));
    }

    [Test] public void Process_FiniteOutput()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.3f, FilterKind.Roland, SR);
        for (int i = 0; i < 1000; i++) {
            float r = f.Process(MathF.Sin(i * 0.1f), i);
            Assert.That(float.IsNaN(r) || float.IsInfinity(r), Is.False);
        }
    }

    [Test] public void Reset_ClearsState()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.5f, FilterKind.Roland, SR);
        for (int i = 0; i < 100; i++) f.Process(1.0f, i);
        f.Reset();
        float after = f.Process(0.0f, 200);
        Assert.That(MathF.Abs(after), Is.LessThan(0.01f),
            "After Reset, silent input must produce near-zero output.");
    }

    [Test] public void Moog_HighResonance_ProducesSelfOscillation_AndStabilizes()
    {
        var f = new Filter();
        f.SetParams(0.5f, 1.0f, FilterKind.Moog, SR);
        float[] samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = f.Process(0.0f, i);
        float max = 0f;
        foreach (float s in samples) {
            Assert.That(float.IsNaN(s),      Is.False, "Moog self-oscillation produced NaN.");
            Assert.That(float.IsInfinity(s), Is.False, "Moog self-oscillation produced Inf.");
            if (MathF.Abs(s) > max) max = MathF.Abs(s);
        }
        Assert.That(max, Is.GreaterThan(0.5f), $"Moog max={max}. Must self-oscillate >= 0.5.");
        Assert.That(max, Is.LessThan(2.0f),    $"Moog max={max}. Must stabilize < 2.0.");
    }

    [Test] public void Moog_ZeroResonance_OutputIsQuiet()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.0f, FilterKind.Moog, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            float s = f.Process(0.0f, i);
            if (MathF.Abs(s) > max) max = MathF.Abs(s);
        }
        Assert.That(max, Is.LessThan(0.001f),
            "Zero resonance with silent input must produce near-zero output.");
    }

    [Test] public void Roland_Process_OutputIsFinite()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.8f, FilterKind.Roland, SR);
        for (int i = 0; i < SR / 10; i++) {
            float result = f.Process(0.5f, i);
            Assert.That(float.IsNaN(result) || float.IsInfinity(result), Is.False,
                $"Roland infinite at sample {i}");
        }
    }

    [Test] public void Moog_Denormal_NoNaNAfterLongSilence()
    {
        var f = new Filter();
        f.SetParams(0.5f, 0.3f, FilterKind.Moog, SR);
        for (int i = 0; i < SR * 5; i++) {
            float result = f.Process(0.0f, i);
            Assert.That(float.IsNaN(result), Is.False, $"NaN at sample {i}.");
        }
    }

    // ── Cutoff scaling per mode ───────────────────────────────────────────

    [Test] public void Roland_FullCutoff_IsLowerHz_ThanMoog_FullCutoff()
    {
        var roland = new Filter();
        var moog   = new Filter();
        roland.SetParams(1.0f, 0f, FilterKind.Roland, SR);
        moog  .SetParams(1.0f, 0f, FilterKind.Moog,   SR);
        var rng = new Random(42);
        float rms_roland = 0f, rms_moog = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = roland.Process(noise, i); rms_roland += r * r;
            float m = moog  .Process(noise, i); rms_moog   += m * m;
        }
        Assert.That(rms_moog, Is.GreaterThan(rms_roland),
            "Moog at full cutoff must pass more energy than Roland at full cutoff.");
    }

    [Test] public void Roland_MidCutoff_ApproximatelyMatchesMoog_Brightness()
    {
        var roland = new Filter();
        var moog   = new Filter();
        roland.SetParams(0.5f, 0f, FilterKind.Roland, SR);
        moog  .SetParams(0.5f, 0f, FilterKind.Moog,   SR);
        var rng = new Random(99);
        float rms_roland = 0f, rms_moog = 0f;
        for (int i = 0; i < 4096; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = roland.Process(noise, i); rms_roland += r * r;
            float m = moog  .Process(noise, i); rms_moog   += m * m;
        }
        float ratio = rms_moog > 0f ? rms_roland / rms_moog : 0f;
        Assert.That(ratio, Is.InRange(0.2f, 5.0f),
            $"Roland/Moog RMS ratio at mid cutoff = {ratio:F2}. Should be comparable brightness.");
    }

    [Test] public void Roland_ZeroCutoff_ProducesNearSilence()
    {
        var f = new Filter();
        f.SetParams(0f, 0f, FilterKind.Roland, SR);
        float sum = 0f;
        var rng = new Random(7);
        for (int i = 0; i < 2048; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            sum += MathF.Abs(f.Process(noise, i));
        }
        Assert.That(sum / 2048f, Is.LessThan(0.1f),
            "Roland at cutoff=0 must strongly attenuate signal.");
    }

    // ── Roland resonance / self-oscillation ──────────────────────────────
    // RED: Roland filter must resonate and self-oscillate at high resonance.
    // Juno-106 IR3109 is famous for its "plasticky, acidy" resonance character.

    [Test] public void Roland_HighResonance_ProducesSelfOscillation()
    {
        // Self-oscillation test: feed a brief impulse to excite the filter,
        // then check sustained oscillation in silence.
        var f = new Filter();
        f.SetParams(0.5f, 1.0f, FilterKind.Roland, SR);
        float max = 0f;
        for (int i = 0; i < SR; i++) {
            // Single impulse at sample 0 to excite the filter
            float input = (i == 0) ? 1.0f : 0.0f;
            float s = f.Process(input, i);
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                $"Roland self-oscillation produced NaN/Inf at sample {i}.");
            if (MathF.Abs(s) > max) max = MathF.Abs(s);
        }
        Assert.That(max, Is.GreaterThan(0.5f),
            $"Roland max={max}. Must self-oscillate at high resonance (>= 0.5). " +
            "Feedback path or resonance coefficient is too weak.");
        Assert.That(max, Is.LessThan(2.0f),
            $"Roland max={max}. Must stabilize < 2.0 (no runaway divergence).");
    }

    [Test] public void Roland_MidResonance_ProducesAudibleResonancePeak()
    {
        // Resonant filter must boost energy around cutoff vs flat filter.
        var f_res  = new Filter();
        var f_flat = new Filter();
        f_res .SetParams(0.4f, 0.7f, FilterKind.Roland, SR);
        f_flat.SetParams(0.4f, 0.0f, FilterKind.Roland, SR);
        var rng = new Random(42);
        float rms_res = 0f, rms_flat = 0f;
        for (int i = 0; i < 8192; i++) {
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float r = f_res .Process(noise, i); rms_res  += r * r;
            float n = f_flat.Process(noise, i); rms_flat += n * n;
        }
        Assert.That(rms_res, Is.GreaterThan(rms_flat),
            $"Roland resonant RMS={rms_res:F4} must exceed non-resonant RMS={rms_flat:F4}. " +
            "Resonance peak not visible — feedback coefficient too weak.");
    }
}
