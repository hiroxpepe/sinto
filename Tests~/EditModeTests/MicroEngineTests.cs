// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class MicroEngineTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new MicroEngine(44100));

    [Test] public void IsActive_Initially_IsFalse()
    {
        var m = new MicroEngine(44100);
        Assert.That(m.isActive, Is.False);
    }

    [Test] public void RenderMono_Stereo_DuplicatesLandR()
    {
        // Unity OnAudioFilterRead provides interleaved stereo: [L0, R0, L1, R1, ...]
        // Mono source must write the SAME sample to both L and R.
        // If not: L gets frame 0, R gets frame 1 → 2× speed + broken 3D positioning.
        const int Frames = 256;
        const int Channels = 2;
        var m = new MicroEngine(44100);
        m.NoteOn(60, 1.0f,
            new OscParams(WaveType.Sine),
            new OscParams(WaveType.Sine),
            new EnvParams(0.001f, 0.1f, 1.0f, 0.1f));

        var buf = new float[Frames * Channels];
        m.RenderMono(buf.AsSpan(), Channels);

        // After NoteOn, voice should be active and producing audio
        // Check L == R for every frame
        bool foundNonZero = false;
        for (int f = 0; f < Frames; f++) {
            float L = buf[f * 2];
            float R = buf[f * 2 + 1];
            if (MathF.Abs(L) > 0.001f) foundNonZero = true;
            Assert.That(L, Is.EqualTo(R).Within(1e-6f),
                $"Frame {f}: L={L} != R={R}. Mono source must duplicate to both channels.");
        }
        // Verify audio is actually being produced (not silent)
        Assert.That(foundNonZero, Is.True,
            "RenderMono produced no audio — voice may not have started.");
    }

    [Test] public void RenderMono_MonoChannel_DoesNotThrow()
    {
        var m = new MicroEngine(44100);
        var buf = new float[512];
        Assert.DoesNotThrow(() => m.RenderMono(buf.AsSpan(), 1));
    }
}
