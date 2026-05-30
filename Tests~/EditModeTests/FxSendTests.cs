// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Send/return FX on the Engine (QY70-style): Reverb / Chorus / Delay are shared
/// send busses. Each part sends a controllable amount; the dry signal is always
/// present and the wet returns are mixed back in. Main parameters are live.
/// </summary>
[TestFixture]
public class FxSendTests
{
    const int SR = 44100;

    static float Rms(ReadOnlySpan<float> b)
    {
        double s = 0; for (int i = 0; i < b.Length; i++) s += b[i] * (double)b[i];
        return (float)Math.Sqrt(s / b.Length);
    }

    static Engine Play(float reverbSend, float chorusSend, float delaySend)
    {
        var e = new Engine(SR, 2, 32, 1024);
        e.SetReverbSend(reverbSend);
        e.SetChorusSend(chorusSend);
        e.SetDelaySend(delaySend);
        e.SendNoteOn(60, 0.9f, 2, 5, 0);
        return e;
    }

    [Test]
    public void AllSendsZero_OutputEqualsDry()
    {
        // With every send at 0, the FX must not alter the signal: rendering a
        // short note then silence should decay to silence (no reverb tail).
        var e = Play(0f, 0f, 0f);
        var buf = new float[1024];
        e.ProcessAudioCallback(buf.AsSpan());
        e.SendNoteOff(60, 2, 0);
        // render a few seconds of silence
        float tail = 0f;
        for (int i = 0; i < 200; i++) {
            var b = new float[1024];
            e.ProcessAudioCallback(b.AsSpan());
            tail = Rms(b.AsSpan());
        }
        Assert.That(tail, Is.LessThan(0.001f), "No send => no FX tail.");
    }

    [Test]
    public void ReverbSend_ProducesTailAfterNoteOff()
    {
        var e = Play(1f, 0f, 0f);
        e.SetReverbParams(0.9f, 0.2f); // big room, low damping
        var buf = new float[1024];
        e.ProcessAudioCallback(buf.AsSpan());
        e.SendNoteOff(60, 2, 0);
        // let the note's amp die, then measure: reverb should still ring
        for (int i = 0; i < 30; i++) e.ProcessAudioCallback(new float[1024].AsSpan());
        var tailBuf = new float[1024];
        e.ProcessAudioCallback(tailBuf.AsSpan());
        Assert.That(Rms(tailBuf.AsSpan()), Is.GreaterThan(0.0005f),
            "Reverb send should leave a ringing tail after note off.");
    }

    [Test]
    public void DelaySend_ProducesEchoAfterDry()
    {
        var e = new Engine(SR, 2, 32, 1024);
        e.SetDelaySend(1f);
        e.SetDelayParams(0.05f, 0.5f); // 50ms, moderate feedback
        e.SendNoteOn(60, 0.9f, 2, 5, 0);
        var b1 = new float[1024];
        e.ProcessAudioCallback(b1.AsSpan());
        e.SendNoteOff(60, 2, 0);
        for (int i = 0; i < 10; i++) e.ProcessAudioCallback(new float[1024].AsSpan());
        var echo = new float[1024];
        e.ProcessAudioCallback(echo.AsSpan());
        Assert.That(Rms(echo.AsSpan()), Is.GreaterThan(0.0005f),
            "Delay send should produce an echo after the dry note stops.");
    }

    [Test]
    public void ChorusSend_AltersSignal()
    {
        // Chorus is delay-based (~15ms), so compare a block after the delay line
        // has filled rather than the very first block.
        var dry = new Engine(SR, 2, 32, 1024);
        dry.SendNoteOn(60, 0.9f, 2, 5, 0);
        var bdry = new float[1024];
        for (int i = 0; i < 4; i++) dry.ProcessAudioCallback(bdry.AsSpan());

        var wet = Play(0f, 1f, 0f);
        var bwet = new float[1024];
        for (int i = 0; i < 4; i++) wet.ProcessAudioCallback(bwet.AsSpan());

        float diff = 0f;
        for (int i = 0; i < bdry.Length; i++) diff += MathF.Abs(bwet[i] - bdry[i]);
        Assert.That(diff, Is.GreaterThan(0.5f), "Chorus send should change the signal.");
    }

    [Test]
    public void Params_AreAcceptedWithoutError()
    {
        var e = new Engine(SR, 2, 32, 1024);
        Assert.DoesNotThrow(() => {
            e.SetReverbParams(0.5f, 0.5f);
            e.SetChorusParams(0.6f, 0.4f);
            e.SetDelayParams(0.3f, 0.4f);
            e.SetReverbSend(0.5f);
            e.SetChorusSend(0.5f);
            e.SetDelaySend(0.5f);
        });
    }
}
