// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class VoiceTests
{
    const int SR = 44100;

    static OscillatorParams DefaultOsc()  => new(WaveType.Sine);
    static EnvelopeParams   DefaultEnv()  => EnvelopeParams.Default;
    static LFOParams        DefaultLFO()  => new(LFOWave.Sine, 1.0f, 0.0f, false);
    static Note             MakeNote(int midi = 60)
        => new(midi, 0.8f, 2, 5);

    [Test] public void Voice_IsValueType()
        => Assert.That(typeof(Voice).IsValueType, Is.True);

    [Test] public void Initial_State_IsFree()
    {
        var v = new Voice();
        Assert.That(v.State, Is.EqualTo(VoiceState.Free));
    }

    [Test] public void NoteOn_StateBecomesAttack()
    {
        var v = new Voice();
        v.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        Assert.That(v.State, Is.Not.EqualTo(VoiceState.Free),
            "After NoteOn, voice must not be Free.");
    }

    [Test] public void NoteOn_ActiveNote_IsStored()
    {
        var v = new Voice();
        var note = MakeNote(69);
        v.NoteOn(note, DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        Assert.That(v.ActiveNote.MidiNote, Is.EqualTo(69));
    }

    [Test] public void NoteOn_SnapsAllSmoothedParameters()
    {
        // After NoteOn, SmoothedParameter.Current must equal Target immediately.
        // If SnapToTarget() is not called, previous voice state leaks → "pyun" artifact.
        var v = new Voice();
        v.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        // SmoothedCutoff.Current must equal SmoothedCutoff.Target (snapped)
        Assert.That(v.SmoothedCutoff.Current,
            Is.EqualTo(v.SmoothedCutoff.Target).Within(1e-4f),
            "SmoothedCutoff must be snapped on NoteOn.");
    }

    [Test] public void NoteOff_StateBecomesRelease()
    {
        var v = new Voice();
        v.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        v.NoteOff();
        Assert.That(v.State, Is.EqualTo(VoiceState.Release));
    }

    [Test] public void StartQuickRelease_StateBecomesQuickRelease()
    {
        var v = new Voice();
        v.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        v.StartQuickRelease(SR);
        Assert.That(v.State, Is.EqualTo(VoiceState.QuickRelease));
    }

    [Test] public void Tick_ProducesFiniteOutput()
    {
        var v = new Voice();
        v.NoteOn(MakeNote(), DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        float result = v.Tick(0f, 0f, 0.8f, 0.3f, DefaultLFO(), DefaultLFO());
        Assert.That(float.IsNaN(result),      Is.False, "Voice.Tick must not return NaN.");
        Assert.That(float.IsInfinity(result), Is.False, "Voice.Tick must not return Infinity.");
    }

    [Test] public void Tick_WhenFree_ReturnsZero()
    {
        var v = new Voice();
        // Free state (no NoteOn) must produce silence
        float result = v.Tick(0f, 0f, 0.8f, 0.3f, DefaultLFO(), DefaultLFO());
        Assert.That(result, Is.EqualTo(0f).Within(1e-6f),
            "Free voice must produce silence.");
    }

    [Test] public void CurrentAmplitude_Initially_IsZero()
    {
        var v = new Voice();
        Assert.That(v.CurrentAmplitude, Is.EqualTo(0f).Within(1e-6f));
    }

    [Test] public void NoteOn_PortamentoZero_SnapsFrequency()
    {
        // portamentoTime = 0 → instant frequency (no glide)
        var v = new Voice();
        v.NoteOn(MakeNote(69), DefaultOsc(), DefaultOsc(),
            DefaultEnv(), DefaultEnv(), DefaultEnv(), 0f, SR);
        // After NoteOn with zero portamento, frequency must be at target immediately
        Assert.That(v.Portamento.IsGliding, Is.False,
            "portamentoTime=0 must not start glide.");
    }
}
