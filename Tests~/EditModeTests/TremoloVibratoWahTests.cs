// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// Tremolo (TR-2): LFO volume modulation, Triangle/Square wave.
/// Vibrato (VB-2): BBD-style pitch modulation via short delay.
/// AutoWah (AW-3): Envelope follower → Biquad BPF sweep, Up/Down mode.
/// All implement IInsertEffect.
/// </summary>
[TestFixture]
public class TremoloTests
{
    const int SR = 44100;

    [Test]
    public void Tremolo_Implements_IInsertEffect()
        => Assert.That(new Tremolo(SR), Is.InstanceOf<IInsertEffect>());

    [Test]
    public void Tremolo_ProcessDoesNotSilence()
    {
        var fx = new Tremolo(SR);
        fx.SetParams(rateHz: 4f, depth: 0.8f);
        fx.Send = 1f;
        var buf = MakeSine(4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.01f));
    }

    [Test]
    public void Tremolo_ModulatesAmplitude()
    {
        // Output must vary in amplitude over time (LFO effect).
        var fx = new Tremolo(SR);
        fx.SetParams(rateHz: 4f, depth: 1.0f);
        fx.Send = 1f;
        var buf = MakeSine(SR); // 1 second
        fx.Process(buf.AsSpan(), 2);
        float maxPeak = 0f, minPeak = float.MaxValue;
        int block = SR / 20; // 50ms blocks
        for (int b = 0; b < SR / block; b++) {
            float p = 0f;
            for (int i = b*block*2; i < (b+1)*block*2; i++) {
                float a = MathF.Abs(buf[i]); if (a > p) p = a;
            }
            if (p > maxPeak) maxPeak = p;
            if (p < minPeak) minPeak = p;
        }
        Assert.That(maxPeak - minPeak, Is.GreaterThan(0.1f),
            "Tremolo must produce amplitude variation over time.");
    }

    [Test]
    public void Tremolo_SquareWave_ProducesSharpOnOff()
    {
        var fx = new Tremolo(SR);
        fx.SetParams(rateHz: 2f, depth: 1.0f);
        fx.SetWaveform(TremoloWaveform.Square);
        fx.Send = 1f;
        fx.Reset();
        int half = SR / 4;
        var bufOn  = MakeSineFrames(half);
        var bufOff = MakeSineFrames(half);
        fx.Process(bufOn.AsSpan(), 2);   // phase 0→0.5 = ON
        fx.Process(bufOff.AsSpan(), 2);  // phase 0.5→1 = OFF
        float peakOn  = Peak(bufOn);
        float peakOff = Peak(bufOff);
        Assert.That(peakOn,  Is.GreaterThan(0.1f),  "Square ON must pass signal.");
        Assert.That(peakOff, Is.LessThan(peakOn * 0.2f), "Square OFF must be much quieter than ON.");
    }

    static float[] MakeSine(int frames) => MakeSineFrames(frames);
    static float[] MakeSineFrames(int frames) {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * 440f * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
    static float Peak(float[] buf) { float p=0f; foreach(var s in buf){float a=MathF.Abs(s);if(a>p)p=a;} return p; }
}

[TestFixture]
public class VibratoTests
{
    const int SR = 44100;

    [Test]
    public void Vibrato_Implements_IInsertEffect()
        => Assert.That(new Vibrato(SR), Is.InstanceOf<IInsertEffect>());

    [Test]
    public void Vibrato_ProcessDoesNotSilence()
    {
        var fx = new Vibrato(SR);
        fx.SetParams(rateHz: 4f, depth: 0.8f);
        fx.Send = 1f;
        var buf = MakeSine(4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.01f));
    }

    [Test]
    public void Vibrato_OutputDiffersFromDry()
    {
        var fx = new Vibrato(SR);
        fx.SetParams(rateHz: 4f, depth: 1.0f);
        fx.Send = 1f;
        var wet = MakeSine(4096);
        var dry = MakeSine(4096);
        fx.Process(wet.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < wet.Length; i++) diff += MathF.Abs(wet[i] - dry[i]);
        Assert.That(diff, Is.GreaterThan(1f), "Vibrato must alter the signal.");
    }

    static float[] MakeSine(int frames) {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * 440f * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
    static float Peak(float[] buf) { float p=0f; foreach(var s in buf){float a=MathF.Abs(s);if(a>p)p=a;} return p; }
}

[TestFixture]
public class AutoWahTests
{
    const int SR = 44100;

    [Test]
    public void AutoWah_Implements_IInsertEffect()
        => Assert.That(new AutoWah(SR), Is.InstanceOf<IInsertEffect>());

    [Test]
    public void AutoWah_ProcessDoesNotSilence()
    {
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 0.7f, freq: 0.5f, peak: 0.7f);
        fx.Send = 1f;
        var buf = MakeSine(4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.01f));
    }

    [Test]
    public void AutoWah_OutputDiffersFromDry()
    {
        var fx = new AutoWah(SR);
        fx.SetParams(sensitivity: 0.9f, freq: 0.5f, peak: 0.8f);
        fx.Send = 1f;
        var wet = MakeSine(4096);
        var dry = MakeSine(4096);
        fx.Process(wet.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < wet.Length; i++) diff += MathF.Abs(wet[i] - dry[i]);
        Assert.That(diff, Is.GreaterThan(0.5f), "AutoWah must alter the signal.");
    }

    [Test]
    public void AutoWah_UpMode_And_DownMode_ProduceDifferentOutput()
    {
        var up   = new AutoWah(SR); up.SetParams(0.9f, 0.5f, 0.8f); up.SetMode(WahMode.Up);   up.Send = 1f;
        var down = new AutoWah(SR); down.SetParams(0.9f, 0.5f, 0.8f); down.SetMode(WahMode.Down); down.Send = 1f;
        var bUp   = MakeSine(4096);
        var bDown = MakeSine(4096);
        up.Process(bUp.AsSpan(), 2);
        down.Process(bDown.AsSpan(), 2);
        float diff = 0f;
        for (int i = 0; i < bUp.Length; i++) diff += MathF.Abs(bUp[i] - bDown[i]);
        Assert.That(diff, Is.GreaterThan(0.5f), "Up and Down modes must produce different output.");
    }

    static float[] MakeSine(int frames) {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * 440f * i / SR);
            buf[i*2] = buf[i*2+1] = s;
        }
        return buf;
    }
    static float Peak(float[] buf) { float p=0f; foreach(var s in buf){float a=MathF.Abs(s);if(a>p)p=a;} return p; }
}
