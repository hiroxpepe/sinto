// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// Flanger Gate1 mode via Engine: gate OFF half must be complete silence
/// even when voices are playing. Tests the full Engine pipeline.
/// </summary>
[TestFixture]
public class FlangerGateEngineTests
{
    const int SR = 44100;

    [Test]
    public void Engine_FlangerGate1_OffHalf_IsCompleteSilence()
    {
        // Setup: Engine with Flanger Gate1, a note playing.
        var engine = new Engine(SR, 2, 8, 512);
        engine.SetChorusType(ChorusType.Flanger);
        engine.SetChorusSend(1.0f);
        engine.SetFlangerParams(2.0f, 0.9f, 0.3f);
        engine.SetFlangerStepMode(FlangerStepMode.Gate1);

        // Play a note
        engine.SendNoteOn(60, 0.9f, 1, 1, 0);

        // Render until gate phase reaches OFF half (> half cycle of 2Hz = 0.25s)
        int halfCycleSamples = (int)(SR / 2.0f / 2f); // 11025 frames
        var buf = new float[512 * 2];

        // Skip through ON half
        for (int i = 0; i < halfCycleSamples / 512 + 1; i++)
            engine.ProcessAudioCallback(buf.AsSpan());

        // Now in OFF half: output must be silent
        var offBuf = new float[512 * 2];
        engine.ProcessAudioCallback(offBuf.AsSpan());

        float peak = 0f;
        foreach (var s in offBuf) { float a = MathF.Abs(s); if (a > peak) peak = a; }
        Assert.That(peak, Is.LessThan(0.05f),
            "Engine Flanger Gate1 OFF: output must be near-silent even with voices playing.");
    }

    [Test]
    public void Engine_FlangerGate1_OnHalf_HasOutput()
    {
        var engine = new Engine(SR, 2, 8, 512);
        engine.SetChorusType(ChorusType.Flanger);
        engine.SetChorusSend(1.0f);
        engine.SetFlangerParams(2.0f, 0.9f, 0.3f);
        engine.SetFlangerStepMode(FlangerStepMode.Gate1);
        engine.SendNoteOn(60, 0.9f, 1, 1, 0);

        // ON half: output must be non-silent
        var buf = new float[512 * 2];
        engine.ProcessAudioCallback(buf.AsSpan());

        float peak = 0f;
        foreach (var s in buf) { float a = MathF.Abs(s); if (a > peak) peak = a; }
        Assert.That(peak, Is.GreaterThan(0.01f),
            "Engine Flanger Gate1 ON: output must be non-silent.");
    }
}
