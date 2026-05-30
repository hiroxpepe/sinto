// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>Square-wave pulse width (PWM) on the engine.</summary>
[TestFixture]
public class PulseWidthTests
{
    const int SR = 44100;

    static float[] RenderSquare(float pw)
    {
        var e = new Engine(SR, 1, 16, 2048);
        e.SetOscWaves(WaveType.Square, WaveType.Square);
        e.SetPulseWidth(pw, pw);
        e.SendNoteOn(57, 0.9f, 2, 5, 0); // A3
        var buf = new float[2048];
        e.ProcessAudioCallback(buf.AsSpan());
        return buf;
    }

    [Test]
    public void DifferentPulseWidths_ProduceDifferentOutput()
    {
        var a = RenderSquare(0.5f);  // symmetric square
        var b = RenderSquare(0.2f);  // narrow pulse
        float diff = 0f;
        for (int i = 0; i < a.Length; i++) diff += MathF.Abs(a[i] - b[i]);
        Assert.That(diff, Is.GreaterThan(1f),
            "Changing pulse width should change the square-wave output.");
    }

    [Test]
    public void SetPulseWidth_Accepted()
    {
        var e = new Engine(SR, 1, 16, 1024);
        Assert.DoesNotThrow(() => e.SetPulseWidth(0.3f, 0.7f));
        // out-of-range values are clamped, not thrown
        Assert.DoesNotThrow(() => e.SetPulseWidth(-1f, 5f));
    }
}
