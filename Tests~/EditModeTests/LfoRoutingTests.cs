// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// LFO routing to DCF cutoff and DCA amplitude. The LFO DSP already modulates
/// these targets inside Voice; this covers the VAEngine API that sets LFO rate,
/// depth and destination so the modulation actually takes effect.
/// </summary>
[TestFixture]
public class LfoRoutingTests
{
    const int SR = 44100;

    static void Render(VAEngine e, float[] buf, int times)
    {
        for (int i = 0; i < times; i++) e.ProcessAudioCallback(buf.AsSpan());
    }

    [Test]
    public void NoLfo_CutoffStaysConstant()
    {
        var e = new VAEngine(SR, 2, 32, 512);
        e.SetFilterParams(0.5f, 0.2f, FilterKind.Moog);
        e.SetLfoToCutoff(0f, 5f); // depth 0
        var buf = new float[512];
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        Render(e, buf, 2);
        float c1 = e.GetVoiceEffectiveCutoff(60, 2);
        Render(e, buf, 20);
        float c2 = e.GetVoiceEffectiveCutoff(60, 2);
        Assert.That(MathF.Abs(c2 - c1), Is.LessThan(0.02f),
            "With LFO depth 0 the cutoff should not wander.");
    }

    [Test]
    public void LfoToCutoff_MakesCutoffVaryOverTime()
    {
        var e = new VAEngine(SR, 2, 32, 512);
        e.SetFilterParams(0.5f, 0.2f, FilterKind.Moog);
        e.SetLfoToCutoff(1f, 8f); // strong depth, 8 Hz
        var buf = new float[512];
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < 120; i++) {
            e.ProcessAudioCallback(buf.AsSpan());
            float c = e.GetVoiceEffectiveCutoff(60, 2);
            if (c < min) min = c;
            if (c > max) max = c;
        }
        Assert.That(max - min, Is.GreaterThan(0.05f),
            "LFO routed to cutoff must make it sweep over time.");
    }

    [Test]
    public void LfoToAmp_IsAcceptedWithoutError()
    {
        var e = new VAEngine(SR, 2, 32, 512);
        e.SetLfoToAmp(0.5f, 5f);
        var buf = new float[512];
        Assert.DoesNotThrow(() => {
            e.SendNoteOn(60, 0.8f, 2, 5, 0);
            Render(e, buf, 10);
        });
        Assert.That(e.activeVoices, Is.GreaterThan(0));
    }

    [Test]
    public void SetLfoToCutoff_ClampsDepthToValidRange()
    {
        var e = new VAEngine(SR, 2, 32, 512);
        Assert.DoesNotThrow(() => e.SetLfoToCutoff(5f, 8f));   // over-range depth
        Assert.DoesNotThrow(() => e.SetLfoToCutoff(-1f, 8f));  // negative depth
    }
}
