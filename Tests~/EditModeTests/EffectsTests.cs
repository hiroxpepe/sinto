// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Sinto.Core.Effects;

namespace Sinto.Tests.Effects;

[TestFixture]
public class BBDChorusTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new BBDChorus(44100));

    [Test] public void Process_SilentInput_ProducesSilentOrNearSilentOutput()
    {
        var c = new BBDChorus(44100);
        c.Mix = 1.0f; c.Enabled = true;
        var buf = new float[1024]; // all zeros
        c.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(MathF.Abs(s), Is.LessThan(0.01f),
                "Silent input through chorus must produce near-silent output.");
    }

    [Test] public void Process_OutputIsFinite()
    {
        var c = new BBDChorus(44100);
        c.Mix = 0.5f; c.Enabled = true;
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.1f) * 0.5f;
        c.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False);
    }

    [Test] public void InstanceBuffer_NotShared_BetweenInstances()
    {
        var c1 = new BBDChorus(44100);
        var c2 = new BBDChorus(44100);
        c1.Mix = 1.0f; c1.Enabled = true;
        c2.Mix = 1.0f; c2.Enabled = true;

        var buf1 = new float[512];
        var buf2 = new float[512];
        for (int i = 0; i < 512; i++) buf1[i] = 0.5f;
        c1.Process(buf1.AsSpan(), 2);
        c2.Process(buf2.AsSpan(), 2); // silent

        bool c2IsSilent = buf2.All(s => MathF.Abs(s) < 0.05f);
        Assert.That(c2IsSilent, Is.True,
            "BBDChorus instances must have independent buffers (not static).");
    }
}

[TestFixture]
public class FreeverbReverbTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new FreeverbReverb());

    [Test] public void Process_SilentInput_ProducesSilentOutput()
    {
        var r = new FreeverbReverb();
        r.Mix = 1.0f; r.Enabled = true;
        var buf = new float[1024];
        r.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(MathF.Abs(s), Is.LessThan(0.01f),
                "Silent input through reverb must produce near-silent output.");
    }

    [Test] public void Process_OutputIsFinite()
    {
        var r = new FreeverbReverb();
        r.RoomSize = 0.9f; r.Damping = 0.5f; r.Mix = 0.5f; r.Enabled = true;
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.05f) * 0.3f;
        r.Process(buf.AsSpan(), 2);
        foreach (float s in buf)
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False);
    }
}

[TestFixture]
public class EffectsChainTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new EffectsChain(44100));

    [Test] public void Process_DoesNotThrow()
    {
        var ec = new EffectsChain(44100);
        var buf = new float[1024];
        Assert.DoesNotThrow(() => ec.Process(buf.AsSpan(), 2));
    }

    [Test] public void ApplySoftClip_OutputStaysWithinPlusMinusOne()
    {
        // Master soft clipper prevents 32-voice clipping.
        var ec = new EffectsChain(44100);
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = 10.0f; // way above 1.0
        ec.ApplySoftClip(buf.AsSpan());
        foreach (float s in buf)
            Assert.That(s, Is.InRange(-1.0f - 1e-4f, 1.0f + 1e-4f),
                "SoftClip must bring all samples within [-1, 1].");
    }

    [Test] public void RetroFilter_PS1Mode_SampleHold_MaintainsBufferLength()
    {
        // PS1 mode downsamples 44100→11025 (factor 4).
        // Must use Sample & Hold — NOT buffer shrinking.
        // Every group of 4 samples must have identical values (held sample).
        var ec = new EffectsChain(44100);
        ec.Retro.Mode    = Sinto.Core.Synth.RetroMode.PS1;
        ec.Retro.Enabled = true;

        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.1f);
        ec.Process(buf.AsSpan(), 1);

        // After PS1 processing, every 4 consecutive mono samples must be identical
        for (int i = 0; i < buf.Length - 3; i += 4) {
            Assert.That(buf[i+1], Is.EqualTo(buf[i]).Within(1e-6f),
                $"PS1 Sample&Hold: buf[{i+1}] != buf[{i}]. Buffer may have been shrunk.");
            Assert.That(buf[i+2], Is.EqualTo(buf[i]).Within(1e-6f),
                $"PS1 Sample&Hold: buf[{i+2}] != buf[{i}].");
            Assert.That(buf[i+3], Is.EqualTo(buf[i]).Within(1e-6f),
                $"PS1 Sample&Hold: buf[{i+3}] != buf[{i}].");
        }
    }
}
