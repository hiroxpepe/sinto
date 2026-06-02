// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;
using Signo.Core.Effects;
using Signo.Core.Signal;

namespace Signo.Tests.Synth;

/// <summary>
/// Send/return FX via EffectBus (VS-880EX style): Reverb / Chorus / Delay are shared
/// send busses. VAEngine generates voices; EffectBus applies send-return.
/// FX ownership moved from VAEngine to EffectBus (ARCHITECTURE.md Phase 3).
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

    static (VAEngine engine, Channel channel, EffectBus bus) Play(
        float reverbSend, float chorusSend, float delaySend)
    {
        var engine  = new VAEngine(SR, 2, 32, 1024);
        var channel = new Channel(SR);
        var bus     = new EffectBus(SR);
        channel.ReverbSend = reverbSend;
        channel.ChorusSend = chorusSend;
        channel.DelaySend  = delaySend;
        engine.SendNoteOn(60, 0.9f, 2, 5, 0);
        return (engine, channel, bus);
    }

    static float[] Render(VAEngine engine, Channel channel, EffectBus bus, int frames = 1024)
    {
        var buf = new float[frames];
        var send = new float[frames];
        engine.ProcessAudioCallback(buf.AsSpan());
        channel.Process(buf.AsSpan(), 2);
        Array.Copy(buf, send, frames);
        bus.ProcessSend(buf.AsSpan(), send, channel.ReverbSend, channel.DelaySend, channel.ChorusSend);
        return buf;
    }

    [Test]
    public void AllSendsZero_OutputEqualsDry()
    {
        var (engine, channel, bus) = Play(0f, 0f, 0f);
        Render(engine, channel, bus);
        engine.SendNoteOff(60, 2, 0);
        float tail = 0f;
        for (int i = 0; i < 200; i++)
            tail = Rms(Render(engine, channel, bus).AsSpan());
        Assert.That(tail, Is.LessThan(0.001f), "No send => no FX tail.");
    }

    [Test]
    public void ReverbSend_ProducesTailAfterNoteOff()
    {
        var (engine, channel, bus) = Play(1f, 0f, 0f);
        bus.Reverb.roomSize = 0.9f; bus.Reverb.damping = 0.2f;
        Render(engine, channel, bus);
        engine.SendNoteOff(60, 2, 0);
        for (int i = 0; i < 30; i++) Render(engine, channel, bus);
        var tail = Render(engine, channel, bus);
        Assert.That(Rms(tail.AsSpan()), Is.GreaterThan(0.0005f),
            "Reverb send should leave a ringing tail after note off.");
    }

    [Test]
    public void DelaySend_ProducesEchoAfterDry()
    {
        var (engine, channel, bus) = Play(0f, 0f, 1f);
        bus.Delay.time = 0.05f; bus.Delay.feedback = 0.5f;
        Render(engine, channel, bus);
        engine.SendNoteOff(60, 2, 0);
        for (int i = 0; i < 10; i++) Render(engine, channel, bus);
        var echo = Render(engine, channel, bus);
        Assert.That(Rms(echo.AsSpan()), Is.GreaterThan(0.0005f),
            "Delay send should produce an echo after the dry note stops.");
    }

    [Test]
    public void ChorusSend_AltersSignal()
    {
        var dryEngine = new VAEngine(SR, 2, 32, 1024);
        var dryCh = new Channel(SR);
        var dryBus = new EffectBus(SR);
        dryEngine.SendNoteOn(60, 0.9f, 2, 5, 0);
        float[] bdry = new float[1024];
        for (int i = 0; i < 4; i++) bdry = Render(dryEngine, dryCh, dryBus);

        var (wetEngine, wetCh, wetBus) = Play(0f, 1f, 0f);
        wetBus.Chorus.enabled = true;
        float[] bwet = new float[1024];
        for (int i = 0; i < 4; i++) bwet = Render(wetEngine, wetCh, wetBus);

        float diff = 0f;
        for (int i = 0; i < bdry.Length; i++) diff += MathF.Abs(bwet[i] - bdry[i]);
        Assert.That(diff, Is.GreaterThan(0.5f), "Chorus send should change the signal.");
    }

    [Test]
    public void Params_AreAcceptedWithoutError()
    {
        var bus = new EffectBus(SR);
        Assert.DoesNotThrow(() => {
            bus.Reverb.roomSize = 0.5f; bus.Reverb.damping = 0.5f;
            bus.Chorus.rate = 0.6f; bus.Chorus.depth = 0.4f;
            bus.Delay.time = 0.3f; bus.Delay.feedback = 0.4f;
        });
    }
}
