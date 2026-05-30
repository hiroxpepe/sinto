// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Signo.Core.Effects;


namespace Signo.Tests.Effects;

[TestFixture]
public class ChorusTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new Chorus(44100));

    [Test] public void Process_SilentInput_ProducesSilentOrNearSilentOutput()
    {
        var c = new Chorus(44100);
        c.mix = 1.0f; c.enabled = true;
        var buf = new float[1024];
        c.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(MathF.Abs(s), Is.LessThan(0.01f));
    }

    [Test] public void Process_OutputIsFinite()
    {
        var c = new Chorus(44100);
        c.mix = 0.5f; c.enabled = true;
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.1f) * 0.5f;
        c.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False);
    }

    [Test] public void InstanceBuffer_NotShared_BetweenInstances()
    {
        var c1 = new Chorus(44100);
        var c2 = new Chorus(44100);
        c1.mix = 1.0f; c1.enabled = true;
        c2.mix = 1.0f; c2.enabled = true;
        var buf1 = new float[512];
        var buf2 = new float[512];
        for (int i = 0; i < 512; i++) buf1[i] = 0.5f;
        c1.Process(buf1.AsSpan(), 2);
        c2.Process(buf2.AsSpan(), 2);
        bool c2IsSilent = buf2.All(s => MathF.Abs(s) < 0.05f);
        Assert.That(c2IsSilent, Is.True,
            "Chorus instances must have independent buffers (not static).");
    }

    [Test] public void Reset_ClearsBuffer()
    {
        var c = new Chorus(44100);
        c.mix = 1.0f; c.enabled = true;
        var fillBuf = new float[1024];
        for (int i = 0; i < fillBuf.Length; i++) fillBuf[i] = 0.8f;
        c.Process(fillBuf.AsSpan(), 2);
        c.Reset();
        var silentBuf = new float[1024];
        c.Process(silentBuf.AsSpan(), 2);
        foreach (float s in silentBuf)
            Assert.That(MathF.Abs(s), Is.LessThan(0.01f),
                "After Reset(), Chorus must output silence for silent input.");
    }
}

[TestFixture]
public class ReverbTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new Reverb());

    [Test] public void Process_SilentInput_ProducesSilentOutput()
    {
        var r = new Reverb();
        r.mix = 1.0f; r.enabled = true;
        var buf = new float[1024];
        r.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(MathF.Abs(s), Is.LessThan(0.01f));
    }

    [Test] public void Process_OutputIsFinite()
    {
        var r = new Reverb();
        r.roomSize = 0.9f; r.damping = 0.5f; r.mix = 0.5f; r.enabled = true;
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.05f) * 0.3f;
        r.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False);
    }

    [Test] public void Reset_DoesNotContinueReverbTail()
    {
        var r = new Reverb();
        r.roomSize = 0.9f; r.mix = 1.0f; r.enabled = true;
        var fillBuf = new float[44100];
        for (int i = 0; i < fillBuf.Length; i++) fillBuf[i] = 0.5f;
        r.Process(fillBuf.AsSpan(), 2);
        r.Reset();
        var silentBuf = new float[1024];
        r.Process(silentBuf.AsSpan(), 2);
        foreach (float s in silentBuf)
            Assert.That(MathF.Abs(s), Is.LessThan(0.05f),
                "After Reset(), Freeverb must not continue reverb tail.");
    }
}

[TestFixture]
public class EffectsTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new Signo.Core.Effects.Effects(44100));

    [Test] public void Process_DoesNotThrow()
    {
        var ec = new Signo.Core.Effects.Effects(44100);
        var buf = new float[1024];
        Assert.DoesNotThrow(() => ec.Process(buf.AsSpan(), 2));
    }

    [Test] public void ApplySoftClip_OutputStaysWithinPlusMinusOne()
    {
        var ec = new Signo.Core.Effects.Effects(44100);
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = 10.0f;
        ec.ApplySoftClip(buf.AsSpan());
        foreach (float s in buf)
            Assert.That(s, Is.InRange(-1.0f - 1e-4f, 1.0f + 1e-4f));
    }

    [Test] public void Retro_PS1Mode_SampleHold_MaintainsBufferLength_Mono()
    {
        var ec = new Signo.Core.Effects.Effects(44100);
        ec.Retro.mode    = Signo.Core.Synth.RetroMode.PS1;
        ec.Retro.enabled = true;
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.1f);
        ec.Process(buf.AsSpan(), 1);
        for (int i = 0; i < buf.Length - 3; i += 4) {
            Assert.That(buf[i+1], Is.EqualTo(buf[i]).Within(1e-6f), $"PS1 S&H mono: [{i+1}]!=[{i}]");
            Assert.That(buf[i+2], Is.EqualTo(buf[i]).Within(1e-6f), $"PS1 S&H mono: [{i+2}]!=[{i}]");
            Assert.That(buf[i+3], Is.EqualTo(buf[i]).Within(1e-6f), $"PS1 S&H mono: [{i+3}]!=[{i}]");
        }
    }

    [Test] public void Retro_PS1Mode_SampleHold_Stereo_LRIndependent()
    {
        // STEREO: L and R must each be independently held every 4 frames.
        // buf: [L0,R0, L1,R1, L2,R2, ...] interleaved stereo
        const int Channels = 2;
        const int Frames   = 1024;
        var ec = new Signo.Core.Effects.Effects(44100);
        ec.Retro.mode    = Signo.Core.Synth.RetroMode.PS1;
        ec.Retro.enabled = true;
        var buf = new float[Frames * Channels];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.07f) * 0.5f;
        ec.Process(buf.AsSpan(), Channels);
        for (int f = 0; f < Frames - 4; f += 4) {
            float L0 = buf[f * Channels];
            float R0 = buf[f * Channels + 1];
            for (int k = 1; k < 4; k++) {
                Assert.That(buf[(f+k)*Channels],   Is.EqualTo(L0).Within(1e-6f),
                    $"PS1 S&H stereo: L at frame {f+k} != L at frame {f}.");
                Assert.That(buf[(f+k)*Channels+1], Is.EqualTo(R0).Within(1e-6f),
                    $"PS1 S&H stereo: R at frame {f+k} != R at frame {f}.");
            }
        }
    }
}
