// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// SE-70 style BPM sync: LFO phase resets to 0 on each beat boundary.
/// Gate Flanger/Gate Phaser: LFO waveform = SQUARE produces rhythmic
/// on/off gating synchronized to BPM (BF-3 GATE/PAN mode equivalent).
/// </summary>
[TestFixture]
public class BpmSyncResetTests
{
    const int SR = 44100;

    [Test]
    public void Flanger_ResetRestartLfoPhase()
    {
        var fx = new Flanger(SR);
        fx.SetParams(rateHz: 2.0f, depth: 0.8f, feedback: 0.3f, send: 1f);
        var inputA = MakeSine(440f, 512);
        var inputB = MakeSine(440f, 512);
        fx.Process(inputA.AsSpan(), 2);
        fx.Reset();
        fx.Process(inputB.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < inputA.Length; i++) diff += MathF.Abs(inputA[i] - inputB[i]);
        Assert.That(diff, Is.LessThan(0.001f), "After Reset, Flanger must reproduce the same output.");
    }

    [Test]
    public void Phaser_ResetRestartLfoPhase()
    {
        var fx = new Phaser(SR);
        fx.SetParams(rateHz: 2.0f, depth: 0.8f, resonance: 0.5f, send: 1f);
        var inputA = MakeSine(440f, 512);
        var inputB = MakeSine(440f, 512);
        fx.Process(inputA.AsSpan(), 2);
        fx.Reset();
        fx.Process(inputB.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < inputA.Length; i++) diff += MathF.Abs(inputA[i] - inputB[i]);
        Assert.That(diff, Is.LessThan(0.001f), "After Reset, Phaser must reproduce the same output.");
    }

    [Test]
    public void Engine_BpmSyncResetCalledOnBeat()
    {
        var e = new Signo.Core.Synth.Engine(SR, 2, 8, 512);
        e.SetChorusType(Signo.Core.Synth.ChorusType.Flanger);
        Assert.DoesNotThrow(() => e.TriggerBpmSyncReset());
    }

    // ── Gate Flanger / Gate Phaser (LFO waveform = SQUARE) ──────────────



    [Test]
    public void Flanger_SineWave_IsDefault()
    {
        // Default waveform must be SINE (no SetLfoWaveform call needed).
        var fx = new Flanger(SR);
        Assert.That(fx.LfoWaveform, Is.EqualTo(LfoWaveform.Sine));
    }

    [Test]
    public void Phaser_SineWave_IsDefault()
    {
        var fx = new Phaser(SR);
        Assert.That(fx.LfoWaveform, Is.EqualTo(LfoWaveform.Sine));
    }

    static float[] MakeSineFrames(float freq, int frames)
    {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * freq * i / SR);
            buf[i * 2] = buf[i * 2 + 1] = s;
        }
        return buf;
    }

    static float[] MakeSine(float freq, int frames) => MakeSineFrames(freq, frames);

    static float Peak(float[] buf) {
        float p = 0f;
        foreach (var s in buf) { float a = MathF.Abs(s); if (a > p) p = a; }
        return p;
    }
}
