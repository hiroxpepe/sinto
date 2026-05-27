#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class LfoTests
{
    const int SR = 44100;

    static LfoParams MakeParams(LfoWave wave, float rate = 1.0f, bool tempoSync = false)
        => new(wave, rate, 0.5f, tempoSync, LfoTarget.FilterCutoff);

    [Test] public void Initialize_DoesNotThrow()
    {
        var lfo = new Lfo();
        Assert.DoesNotThrow(() => lfo.Initialize(MakeParams(LfoWave.Sine), SR));
    }

    [Test] public void Tick_Sine_OutputRangeIsMinusOneToOne()
    {
        var lfo = new Lfo();
        lfo.Initialize(MakeParams(LfoWave.Sine, 1.0f), SR);
        var p = MakeParams(LfoWave.Sine, 1.0f);
        for (int i = 0; i < SR; i++) {
            float v = lfo.Tick(p);
            Assert.That(v, Is.InRange(-1.0f - 1e-3f, 1.0f + 1e-3f),
                $"Sine LFO output {v} out of range at sample {i}.");
        }
    }

    [Test] public void Tick_Sine_CompletesOneCycleIn44100Samples()
    {
        var lfo = new Lfo();
        var p = MakeParams(LfoWave.Sine, 1.0f); // 1 Hz
        lfo.Initialize(p, SR);
        var samples = new float[SR];
        for (int i = 0; i < SR; i++) samples[i] = lfo.Tick(p);

        int crossings = 0;
        for (int i = 1; i < samples.Length; i++)
            if (samples[i-1] < 0f && samples[i] >= 0f) crossings++;

        Assert.That(crossings, Is.InRange(1, 2),
            $"1Hz Sine LFO should complete ~1 cycle in {SR} samples. Crossings: {crossings}");
    }

    [Test] public void Tick_Triangle_OutputRangeIsValid()
    {
        var lfo = new Lfo();
        var p = MakeParams(LfoWave.Triangle, 2.0f);
        lfo.Initialize(p, SR);
        for (int i = 0; i < SR; i++) {
            float v = lfo.Tick(p);
            Assert.That(v, Is.InRange(-1.0f - 1e-3f, 1.0f + 1e-3f));
        }
    }

    [Test] public void Tick_Square_OutputIsPlusOneOrMinusOne()
    {
        var lfo = new Lfo();
        var p = MakeParams(LfoWave.Square, 1.0f);
        lfo.Initialize(p, SR);
        for (int i = 0; i < SR; i++) {
            float v = lfo.Tick(p);
            bool valid = MathF.Abs(v - 1f) < 1e-3f || MathF.Abs(v + 1f) < 1e-3f;
            Assert.That(valid, Is.True, $"Square LFO output {v} is not ±1 at sample {i}.");
        }
    }

    [Test] public void Tick_SH_OutputChangesOnCycleBoundary()
    {
        // S&H must hold value within a cycle and jump on wrap
        var lfo = new Lfo();
        var p = MakeParams(LfoWave.SH, 10.0f); // 10Hz = faster cycle for shorter test
        lfo.Initialize(p, SR);
        var samples = new float[SR / 10]; // 0.1 second
        for (int i = 0; i < samples.Length; i++) samples[i] = lfo.Tick(p);

        // Within a hold period, consecutive values must be identical
        // Count distinct values — should equal ~number of cycles
        int changes = 0;
        for (int i = 1; i < samples.Length; i++)
            if (MathF.Abs(samples[i] - samples[i-1]) > 1e-4f) changes++;

        Assert.That(changes, Is.GreaterThan(0), "S&H LFO must produce value changes.");
        Assert.That(changes, Is.LessThan(samples.Length / 2),
            "S&H LFO must hold values between cycles (not change every sample).");
    }

    [Test] public void SetBPM_TempoSync_DoesNotThrow()
    {
        var lfo = new Lfo();
        var p = MakeParams(LfoWave.Sine, 0.25f, tempoSync: true);
        lfo.Initialize(p, SR, 120f);
        Assert.DoesNotThrow(() => lfo.SetBPM(140f, p, SR));
    }

    [Test] public void Tick_AfterSetBPM_OutputRemainsValid()
    {
        var lfo = new Lfo();
        var p = MakeParams(LfoWave.Sine, 0.25f, tempoSync: true);
        lfo.Initialize(p, SR, 120f);
        lfo.SetBPM(140f, p, SR);
        for (int i = 0; i < 1000; i++) {
            float v = lfo.Tick(p);
            Assert.That(float.IsNaN(v), Is.False, "LFO Tick after SetBPM must not return NaN.");
        }
    }
}
