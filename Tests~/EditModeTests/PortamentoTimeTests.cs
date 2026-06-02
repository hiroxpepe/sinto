// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Full coverage for SetPortamentoTime on both Voices and VAEngine (the VAEngine
/// method delegates to Voices). Verifies the time value reaches a voice on the
/// next NoteOn and drives gliding: time = 0 snaps instantly, time &gt; 0 glides.
/// </summary>
[TestFixture]
public class PortamentoTimeTests
{
    const int SR = 44100;

    static Note MakeNote(int midi, int track = 2, int priority = 5)
        => new(midi, 0.8f, track, priority);

    static OscParams Osc() => new(WaveType.Sine);

    static void NoteOn(Voices vm, int midi, int track = 2)
        => vm.NoteOn(MakeNote(midi, track), Osc(), Osc(),
                     EnvParams.Default, EnvParams.Default, EnvParams.Default);

    static float FreqOf(int midi) => 440f * System.MathF.Pow(2f, (midi - 69) / 12f);

    // ── Voices.SetPortamentoTime ────────────────────────────────────────

    [Test]
    public void Voices_DefaultTimeZero_FirstNote_NotGliding()
    {
        var vm = new Voices(32, SR);
        NoteOn(vm, 60);
        // With the default (0s) time, the voice should not be gliding.
        Assert.That(vm.GetVoiceIsGliding(60, 2), Is.False);
    }

    [Test]
    public void Voices_SetZero_NoteOn_SnapsToTargetImmediately()
    {
        var vm = new Voices(32, SR);
        vm.SetPortamentoTime(0f);
        NoteOn(vm, 69); // A4 = 440
        Assert.That(vm.GetVoiceCurrentFrequency(69, 2), Is.EqualTo(440f).Within(0.5f));
        Assert.That(vm.GetVoiceIsGliding(69, 2), Is.False);
    }

    [Test]
    public void Voices_SetNonZero_SecondNote_IsGliding()
    {
        var vm = new Voices(32, SR);
        vm.SetPortamentoTime(0.5f);
        // First note establishes a starting frequency on a voice.
        NoteOn(vm, 60); // C4
        // Re-using the same track/priority, a new note on a fresh voice still
        // starts gliding from its own previous frequency only if the voice
        // carried one; to observe gliding deterministically we drive a single
        // voice: NoteOn at 72 with a non-zero time must begin below target.
        var vm2 = new Voices(1, SR); // single voice, forces reuse
        vm2.SetPortamentoTime(0.5f);
        NoteOn(vm2, 60);
        NoteOn(vm2, 72); // same voice reused, should glide from ~C4 toward C5
        Assert.That(vm2.GetVoiceIsGliding(72, 2), Is.True,
            "A non-zero portamento time must make the reused voice glide.");
        // Current frequency should be below the C5 target while gliding.
        Assert.That(vm2.GetVoiceCurrentFrequency(72, 2), Is.LessThan(FreqOf(72)));
    }

    [Test]
    public void Voices_SetNonZero_ThenZero_NextNoteSnaps()
    {
        var vm = new Voices(1, SR);
        vm.SetPortamentoTime(0.5f);
        NoteOn(vm, 60);
        vm.SetPortamentoTime(0f);   // change back to instant
        NoteOn(vm, 72);             // reused voice, but time=0 -> snap
        Assert.That(vm.GetVoiceIsGliding(72, 2), Is.False);
        Assert.That(vm.GetVoiceCurrentFrequency(72, 2), Is.EqualTo(FreqOf(72)).Within(0.5f));
    }

    [Test]
    public void Voices_NegativeTime_TreatedAsInstant()
    {
        var vm = new Voices(1, SR);
        vm.SetPortamentoTime(-1f); // negative <= 0 -> snap
        NoteOn(vm, 60);
        NoteOn(vm, 72);
        Assert.That(vm.GetVoiceIsGliding(72, 2), Is.False);
    }

    // ── VAEngine.SetPortamentoTime (delegation) ───────────────────────────

    [Test]
    public void VAEngine_SetZero_NoteOn_SnapsImmediately()
    {
        var e = new VAEngine(SR, 2, 32, 1024);
        e.SetPortamentoTime(0f);
        e.SendNoteOn(69, 0.8f, 2, 5, 0);
        var buf = new float[1024];
        e.ProcessAudioCallback(buf.AsSpan()); // drain event + render
        Assert.That(e.GetVoiceCurrentFrequency(69, 2), Is.EqualTo(440f).Within(1f));
        Assert.That(e.GetVoiceIsGliding(69, 2), Is.False);
    }

    [Test]
    public void VAEngine_SetNonZero_ReusedVoice_Glides()
    {
        var e = new VAEngine(SR, 2, 1, 1024); // single voice forces reuse
        e.SetPortamentoTime(0.5f);
        var buf = new float[64];
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        e.ProcessAudioCallback(buf.AsSpan());
        e.SendNoteOn(72, 0.8f, 2, 5, 0);
        e.ProcessAudioCallback(buf.AsSpan());
        Assert.That(e.GetVoiceIsGliding(72, 2), Is.True,
            "VAEngine.SetPortamentoTime must delegate so the reused voice glides.");
    }

    [Test]
    public void VAEngine_DelegatesValue_GlideReachesTargetAfterEnoughTime()
    {
        var e = new VAEngine(SR, 2, 1, 8192);
        e.SetPortamentoTime(0.1f); // short glide (~4410 samples)
        var small = new float[64];
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        e.ProcessAudioCallback(small.AsSpan());
        e.SendNoteOn(72, 0.8f, 2, 5, 0);
        e.ProcessAudioCallback(small.AsSpan());
        // Render well beyond 0.1s: 4 x 8192 frames = 32768 samples >> 4410.
        var big = new float[8192 * 2]; // 8192 frames (stereo)
        for (int i = 0; i < 4; i++) e.ProcessAudioCallback(big.AsSpan());
        Assert.That(e.GetVoiceCurrentFrequency(72, 2), Is.EqualTo(FreqOf(72)).Within(2f),
            "After enough render time the glide must reach the target frequency.");
        Assert.That(e.GetVoiceIsGliding(72, 2), Is.False);
    }

    [Test]
    public void VAEngine_NoActiveVoice_FrequencyQueryReturnsMinusOne()
    {
        var e = new VAEngine(SR, 2, 32, 1024);
        Assert.That(e.GetVoiceCurrentFrequency(60, 2), Is.EqualTo(-1f));
        Assert.That(e.GetVoiceIsGliding(60, 2), Is.False);
    }
}
