// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// AutoWah v2: SVF (State Variable Filter) + envelope follower.
/// - SVF is stable under fast coefficient changes (no click noise).
/// - Envelope follower: rectifier + 1-pole LP (MusicDSP standard).
/// - Output: dry + bandpass * send (wah character).
/// - Up mode: louder → cutoff sweeps up. Down: louder → cutoff sweeps down.
/// </summary>
[TestFixture]
public class AutoWahV2Tests
{
    const int SR = 44100;

    [Test]
    public void AutoWah_SVF_ProcessDoesNotSilence()
    {
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 0.7f, freq: 0.3f, peak: 0.7f);
        fx.Send = 1f;
        var buf = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.1f),
            "AutoWah must not silence the signal.");
    }

    [Test]
    public void AutoWah_SVF_OutputDiffersFromDry()
    {
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 0.9f, freq: 0.3f, peak: 0.8f);
        fx.Send = 1f;
        var wet = MakeSine(440f, 8192);
        var dry = MakeSine(440f, 8192);
        fx.Process(wet.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < wet.Length; i++) diff += MathF.Abs(wet[i] - dry[i]);
        Assert.That(diff, Is.GreaterThan(1f), "AutoWah must alter the signal.");
    }

    [Test]
    public void AutoWah_SVF_NoCrashWithHighQ()
    {
        // SVF must remain stable at high resonance (no NaN/Inf).
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 1.0f, freq: 0.5f, peak: 1.0f);
        fx.Send = 1f;
        var buf = MakeSine(440f, 8192);
        fx.Process(buf.AsSpan(), 2);
        foreach (var s in buf)
            Assert.That(float.IsFinite(s), Is.True, "SVF must not produce NaN/Inf.");
    }

    [Test]
    public void AutoWah_SVF_EnvelopeFollowsAmplitude()
    {
        // Loud input → envelope rises → filter sweeps.
        // Output must differ when input amplitude changes.
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 1.0f, freq: 0.2f, peak: 0.7f);
        fx.Send = 1f;

        // Quiet input
        var quiet = MakeSine(440f, 2048, amp: 0.05f);
        fx.Process(quiet.AsSpan(), 2);
        float peakQuiet = Peak(quiet);

        fx.Reset();

        // Loud input
        var loud = MakeSine(440f, 2048, amp: 0.5f);
        fx.Process(loud.AsSpan(), 2);
        float peakLoud = Peak(loud);

        Assert.That(peakLoud, Is.GreaterThan(peakQuiet * 1.5f),
            "Louder input must produce more pronounced wah effect.");
    }

    [Test]
    public void AutoWah_SVF_UpAndDownModesDiffer()
    {
        var up   = new AutoWah(SR); up.SetParams(0.9f, 0.3f, 0.7f);   up.SetMode(WahMode.Up);   up.Send = 1f;
        var down = new AutoWah(SR); down.SetParams(0.9f, 0.3f, 0.7f); down.SetMode(WahMode.Down); down.Send = 1f;
        var bUp   = MakeSine(440f, 4096);
        var bDown = MakeSine(440f, 4096);
        up.Process(bUp.AsSpan(), 2);
        down.Process(bDown.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < bUp.Length; i++) diff += MathF.Abs(bUp[i] - bDown[i]);
        Assert.That(diff, Is.GreaterThan(0.5f), "Up and Down modes must produce different output.");
    }

    [Test]
    public void AutoWah_SVF_Send0_PassesDryUnchanged()
    {
        var fx = new AutoWah(SR);
        fx.SetParams(0.9f, 0.5f, 0.8f);
        fx.Send = 0f;
        var buf = MakeSine(440f, 1024);
        var dry = MakeSine(440f, 1024);
        fx.Process(buf.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < buf.Length; i++) diff += MathF.Abs(buf[i] - dry[i]);
        Assert.That(diff, Is.LessThan(0.001f), "Send=0 must pass dry unchanged.");
    }

    static float[] MakeSine(float freq, int frames, float amp = 0.5f)
    {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = amp * MathF.Sin(2f * MathF.PI * freq * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
    static float Peak(float[] buf) { float p=0f; foreach(var s in buf){float a=MathF.Abs(s);if(a>p)p=a;} return p; }
}

[TestFixture]
public class AutoWahSafetyTests
{
    const int SR = 44100;

    [Test]
    public void AutoWah_NeverExceedsInputAmplitude_AtMaxSettings()
    {
        // SAFETY: output must never exceed input amplitude * 2 (no blow-up).
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 1.0f, freq: 1.0f, peak: 1.0f);
        fx.Send = 1f;
        var buf = MakeSine(440f, 8192, amp: 0.5f);
        float inputMax = 0.5f;
        fx.Process(buf.AsSpan(), 2);
        float outputMax = 0f;
        foreach (var s in buf) { float a = MathF.Abs(s); if (a > outputMax) outputMax = a; }
        Assert.That(outputMax, Is.LessThan(inputMax * 2f),
            $"Output ({outputMax:F3}) must not exceed 2x input amplitude ({inputMax*2f:F3}).");
    }

    [Test]
    public void AutoWah_NoNanOrInf_UnderAllConditions()
    {
        float[] sensitivities = { 0f, 0.5f, 1.0f };
        float[] freqs         = { 0f, 0.5f, 1.0f };
        float[] peaks         = { 0f, 0.5f, 1.0f };
        foreach (var s in sensitivities)
        foreach (var f in freqs)
        foreach (var p in peaks) {
            var fx = new AutoWah(SR);
            fx.SetParams(s, f, p);
            fx.Send = 1f;
            var buf = MakeSine(440f, 2048, amp: 0.5f);
            fx.Process(buf.AsSpan(), 2);
            foreach (var sample in buf)
                Assert.That(float.IsFinite(sample), Is.True,
                    $"NaN/Inf at sens={s} freq={f} peak={p}");
        }
    }

    [Test]
    public void AutoWah_NoNanOrInf_WithSilentInput()
    {
        var fx = new AutoWah(SR);
        fx.SetParams(1.0f, 1.0f, 1.0f);
        fx.Send = 1f;
        var buf = new float[4096]; // all zeros
        fx.Process(buf.AsSpan(), 2);
        foreach (var s in buf)
            Assert.That(float.IsFinite(s), Is.True, "Silent input must not produce NaN/Inf.");
    }

    static float[] MakeSine(float freq, int frames, float amp = 0.5f) {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = amp * MathF.Sin(2f * MathF.PI * freq * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
}
