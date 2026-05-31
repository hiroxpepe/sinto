// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Wave shape (SHP): phase-distortion-style compression of the waveform cycle.
/// Shape 0.5 = neutral (no change), 0.0 = maximum compress-front,
/// 1.0 = maximum compress-back.
/// Applies to SAW, TRI, SIN. SQR uses PulseWidth instead.
/// </summary>
[TestFixture]
public class ShapeTests
{
    const int SR = 44100;

    static float[] Render(WaveType wave, float shape, int frames = 2048)
    {
        var e = new Engine(SR, 1, 16, frames);
        e.SetWave(wave);
        e.SetShape(0.5f, 0.5f); // default neutral first
        e.SetShape(shape, shape);
        e.SendNoteOn(57, 0.9f, 2, 5, 0); // A3 = 220Hz
        var buf = new float[frames];
        e.ProcessAudioCallback(buf.AsSpan());
        return buf;
    }

    [Test]
    public void Saw_NeutralShape_MatchesDefaultOutput()
    {
        var neutral = Render(WaveType.Saw, 0.5f);
        var also_neutral = Render(WaveType.Saw, 0.5f);
        float diff = 0f;
        for (int i = 0; i < neutral.Length; i++) diff += MathF.Abs(neutral[i] - also_neutral[i]);
        Assert.That(diff, Is.LessThan(0.001f), "Two neutral renders must be identical.");
    }

    [Test]
    public void Saw_ShapeChangesWaveform()
    {
        var neutral = Render(WaveType.Saw, 0.5f);
        var shaped  = Render(WaveType.Saw, 0.2f);
        float diff = 0f;
        for (int i = 0; i < neutral.Length; i++) diff += MathF.Abs(neutral[i] - shaped[i]);
        Assert.That(diff, Is.GreaterThan(1f), "Shape 0.2 must change SAW output.");
    }

    [Test]
    public void Tri_ShapeChangesWaveform()
    {
        var neutral = Render(WaveType.Triangle, 0.5f);
        var shaped  = Render(WaveType.Triangle, 0.8f);
        float diff = 0f;
        for (int i = 0; i < neutral.Length; i++) diff += MathF.Abs(neutral[i] - shaped[i]);
        Assert.That(diff, Is.GreaterThan(1f), "Shape 0.8 must change TRI output.");
    }

    [Test]
    public void Sin_ShapeChangesWaveform()
    {
        var neutral = Render(WaveType.Sine, 0.5f);
        var shaped  = Render(WaveType.Sine, 0.15f);
        float diff = 0f;
        for (int i = 0; i < neutral.Length; i++) diff += MathF.Abs(neutral[i] - shaped[i]);
        Assert.That(diff, Is.GreaterThan(1f), "Shape 0.15 must change SIN output.");
    }

    [Test]
    public void Sqr_ShapeHasNoEffect()
    {
        // SQR uses PulseWidth, not Shape. Shape changes must not alter SQR output.
        var neutral = Render(WaveType.Square, 0.5f);
        var shaped  = Render(WaveType.Square, 0.2f);
        float diff = 0f;
        for (int i = 0; i < neutral.Length; i++) diff += MathF.Abs(neutral[i] - shaped[i]);
        Assert.That(diff, Is.LessThan(0.001f), "Shape must not affect SQR (use PW instead).");
    }

    // ── Naive generator tests (polyBLEP is skipped when shape is active) ──

    [Test]
    public void Saw_NeverSilentAtAnyShape()
    {
        // GenSawNaive must keep producing audio for any shape value.
        foreach (float s in new[] { 0.05f, 0.1f, 0.2f, 0.3f, 0.7f, 0.8f, 0.9f, 0.95f }) {
            var buf = Render(WaveType.Saw, s, 4096);
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++) { float a = MathF.Abs(buf[i]); if (a > peak) peak = a; }
            Assert.That(peak, Is.GreaterThan(0.02f), $"SAW shape={s} must not be silent.");
        }
    }

    [Test]
    public void Tri_NeverSilentAtAnyShape()
    {
        // GenTriNaive must keep producing audio for any shape value (the bug was silence here).
        foreach (float s in new[] { 0.05f, 0.1f, 0.2f, 0.3f, 0.7f, 0.8f, 0.9f, 0.95f }) {
            var buf = Render(WaveType.Triangle, s, 4096);
            float peak = 0f;
            for (int i = 0; i < buf.Length; i++) { float a = MathF.Abs(buf[i]); if (a > peak) peak = a; }
            Assert.That(peak, Is.GreaterThan(0.05f), $"TRI shape={s} must not be silent.");
        }
    }

    [Test]
    public void Saw_NaiveOutputWithinRange()
    {
        // GenSawNaive output must stay within [-1, +1] at extreme shape values.
        foreach (float s in new[] { 0.02f, 0.5f, 0.98f }) {
            var buf = Render(WaveType.Saw, s, 4096);
            float max = float.MinValue, min = float.MaxValue;
            for (int i = 0; i < buf.Length; i++) { if (buf[i] > max) max = buf[i]; if (buf[i] < min) min = buf[i]; }
            Assert.That(max, Is.LessThanOrEqualTo(1.05f), $"SAW shape={s} exceeded +1.");
            Assert.That(min, Is.GreaterThanOrEqualTo(-1.05f), $"SAW shape={s} exceeded -1.");
        }
    }

    [Test]
    public void Tri_NaiveOutputWithinRange()
    {
        // GenTriNaive output must stay within [-1, +1] at extreme shape values.
        foreach (float s in new[] { 0.02f, 0.5f, 0.98f }) {
            var buf = Render(WaveType.Triangle, s, 4096);
            float max = float.MinValue, min = float.MaxValue;
            for (int i = 0; i < buf.Length; i++) { if (buf[i] > max) max = buf[i]; if (buf[i] < min) min = buf[i]; }
            Assert.That(max, Is.LessThanOrEqualTo(1.05f), $"TRI shape={s} exceeded +1.");
            Assert.That(min, Is.GreaterThanOrEqualTo(-1.05f), $"TRI shape={s} exceeded -1.");
        }
    }
}
