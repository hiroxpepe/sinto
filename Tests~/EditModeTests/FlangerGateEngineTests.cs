// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;
using Signo.Core.Effects;
using Signo.Core.Signal;

namespace Signo.Tests.Effects;

/// <summary>
/// Flanger Gate1 mode via Channel: gate OFF half must be complete silence
/// even when voices are playing. Tests VAEngine → Channel pipeline.
/// FX ownership moved from VAEngine to Channel (ARCHITECTURE.md Phase 3).
/// </summary>
[TestFixture]
public class FlangerGateEngineTests
{
    const int SR = 44100;

    static (VAEngine engine, Channel channel, Flanger flanger) Setup()
    {
        var engine  = new VAEngine(SR, 2, 8, 512);
        var channel = new Channel(SR);
        var flanger = new Flanger(SR);
        flanger.enabled = true;
        channel.AddInsert(flanger);
        return (engine, channel, flanger);
    }

    static float[] RenderWithChannel(VAEngine engine, Channel channel, int frames)
    {
        var buf = new float[frames * 2];
        engine.ProcessAudioCallback(buf.AsSpan());
        channel.Process(buf.AsSpan(), 2);
        return buf;
    }

    [Test]
    public void Channel_FlangerGate1_OffHalf_IsCompleteSilence()
    {
        var (engine, channel, flanger) = Setup();
        engine.SetChorusType(ChorusType.Flanger);
        flanger.SetParams(2.0f, 0.9f, 0.3f, 1.0f);
        flanger.SetStepMode(FlangerStepMode.Gate1);

        engine.SendNoteOn(60, 0.9f, 1, 1, 0);

        int halfCycleSamples = (int)(SR / 2.0f / 2f);
        // Skip through ON half
        for (int i = 0; i < halfCycleSamples / 512 + 1; i++)
            RenderWithChannel(engine, channel, 512);

        // Now in OFF half
        var offBuf = RenderWithChannel(engine, channel, 512);
        float peak = 0f;
        foreach (var s in offBuf) { float a = MathF.Abs(s); if (a > peak) peak = a; }
        Assert.That(peak, Is.LessThan(0.05f),
            "Channel Flanger Gate1 OFF: output must be near-silent even with voices playing.");
    }

    [Test]
    public void Channel_FlangerGate1_OnHalf_HasOutput()
    {
        var (engine, channel, flanger) = Setup();
        engine.SetChorusType(ChorusType.Flanger);
        flanger.SetParams(2.0f, 0.9f, 0.3f, 1.0f);
        flanger.SetStepMode(FlangerStepMode.Gate1);
        engine.SendNoteOn(60, 0.9f, 1, 1, 0);

        // Render into ON half
        var buf = RenderWithChannel(engine, channel, 512);
        float peak = 0f;
        foreach (var s in buf) { float a = MathF.Abs(s); if (a > peak) peak = a; }
        Assert.That(peak, Is.GreaterThan(0.01f),
            "Channel Flanger Gate1 ON: output must be non-silent.");
    }
}
