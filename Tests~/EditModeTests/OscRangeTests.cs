// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Per-oscillator range (16'/8'/4' = octave -1/0/+1). OSC1 and OSC2 can pick
/// independent octaves; the oscillator frequency scales by 2^octave.
/// </summary>
[TestFixture]
public class OscRangeTests
{
    const int SR = 44100;

    static void NoteOn(VAEngine e, int midi)
    {
        e.SendNoteOn(midi, 0.8f, 2, 5, 0);
        var buf = new float[256];
        e.ProcessAudioCallback(buf.AsSpan());
    }

    static float Note(int midi) => 440f * MathF.Pow(2f, (midi - 69) / 12f);

    [Test]
    public void DefaultRange_BothOscAtBaseFrequency()
    {
        var e = new VAEngine(SR, 2, 32, 1024);
        e.SetOscRange(0, 0); // 8' both
        NoteOn(e, 69);       // A4 = 440
        Assert.That(e.GetVoiceOscFrequency(69, 2, 0), Is.EqualTo(440f).Within(1f));
        Assert.That(e.GetVoiceOscFrequency(69, 2, 1), Is.EqualTo(440f).Within(1f));
    }

    [Test]
    public void Osc1Down16_Osc2Base_Osc1IsHalfFrequency()
    {
        var e = new VAEngine(SR, 2, 32, 1024);
        e.SetOscRange(-1, 0); // OSC1 = 16' (one octave down), OSC2 = 8'
        NoteOn(e, 69);
        Assert.That(e.GetVoiceOscFrequency(69, 2, 0), Is.EqualTo(220f).Within(1f));
        Assert.That(e.GetVoiceOscFrequency(69, 2, 1), Is.EqualTo(440f).Within(1f));
    }

    [Test]
    public void Osc2Up4_IsDoubleFrequency()
    {
        var e = new VAEngine(SR, 2, 32, 1024);
        e.SetOscRange(0, 1); // OSC2 = 4' (one octave up)
        NoteOn(e, 69);
        Assert.That(e.GetVoiceOscFrequency(69, 2, 1), Is.EqualTo(880f).Within(2f));
    }

    [Test]
    public void Ranges_AreIndependentBetweenOscillators()
    {
        var e = new VAEngine(SR, 2, 32, 1024);
        e.SetOscRange(-1, 1); // OSC1 down, OSC2 up = two octaves apart
        NoteOn(e, 60);        // C4
        float f1 = e.GetVoiceOscFrequency(60, 2, 0);
        float f2 = e.GetVoiceOscFrequency(60, 2, 1);
        Assert.That(f2 / f1, Is.EqualTo(4f).Within(0.05f));
    }
}
