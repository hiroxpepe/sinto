// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Effects;

namespace Sinto.Tests.Effects;

[TestFixture]
public class DelayTests
{
    [Test] public void Constructor_DoesNotThrow()
        => Assert.DoesNotThrow(() => new Delay(44100));

    [Test] public void Process_DoesNotThrow()
    {
        var d = new Delay(44100);
        d.Time = 0.5f; d.Feedback = 0.5f; d.Mix = 0.5f; d.Enabled = true;
        var buf = new float[1024];
        Assert.DoesNotThrow(() => d.Process(buf.AsSpan(), 2));
    }

    [Test] public void Feedback_ClampedTo0_95()
    {
        // Feedback >= 1.0 → infinite loop → buffer overflow → app death
        var d = new Delay(44100);
        d.Time = 0.25f; d.Mix = 0.5f; d.Enabled = true;
        d.Feedback = 1.0f; // must be clamped to 0.95 internally
        var buf = new float[1024];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0.5f;
        Assert.DoesNotThrow(() => {
            for (int frame = 0; frame < 100; frame++)
                d.Process(buf.AsSpan(), 2);
        }, "Feedback = 1.0 must not cause infinite feedback runaway.");
        foreach (float s in buf)
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                "Output must be finite even with feedback = 1.0 (clamped to 0.95).");
    }

    [Test] public void TimeChange_DoesNotProduceClickNoise()
    {
        // Instantaneous read pointer warp on time change causes a "pop" click.
        // Fractional delay must interpolate the pointer smoothly.
        // Verify: output samples immediately after time change are within [-1, 1]
        // and do not jump by more than 0.5 per sample (click threshold).
        var d = new Delay(44100);
        d.Time = 0.5f; d.Feedback = 0.3f; d.Mix = 0.5f; d.Enabled = true;

        // Feed a steady sine-like signal for 1 second to fill the delay buffer
        var buf = new float[512];
        for (int i = 0; i < buf.Length; i++) buf[i] = MathF.Sin(i * 0.1f) * 0.5f;
        for (int frame = 0; frame < 88; frame++)
            d.Process(buf.AsSpan(), 2);

        // Change time abruptly
        d.Time = 0.1f;

        // Capture output immediately after time change
        var afterChange = new float[512];
        for (int i = 0; i < afterChange.Length; i++) afterChange[i] = 0.3f;
        d.Process(afterChange.AsSpan(), 2);

        // No sample should be NaN/Inf
        foreach (float s in afterChange)
            Assert.That(float.IsNaN(s) || float.IsInfinity(s), Is.False,
                "Output after time change must be finite (no pointer warp NaN).");

        // No abrupt jump > 0.5 per sample (click detection)
        float prev = afterChange[0];
        for (int i = 1; i < afterChange.Length; i++) {
            float delta = MathF.Abs(afterChange[i] - prev);
            Assert.That(delta, Is.LessThan(0.5f),
                $"Sample jump of {delta} at index {i} after time change. " +
                "Fractional delay smoothing may not be implemented.");
            prev = afterChange[i];
        }
    }

    [Test] public void Reset_ClearsBuffer()
    {
        var d = new Delay(44100);
        d.Time = 0.25f; d.Feedback = 0.5f; d.Mix = 1.0f; d.Enabled = true;
        var buf = new float[512];
        for (int i = 0; i < buf.Length; i++) buf[i] = 1.0f;
        d.Process(buf.AsSpan(), 2);
        d.Reset();
        var silent = new float[512]; // all zeros
        d.Process(silent.AsSpan(), 2);
        foreach (float s in silent)
            Assert.That(MathF.Abs(s), Is.LessThan(0.01f),
                "After Reset(), delay buffer must be cleared.");
    }

    [Test] public void InstanceBuffer_NotStatic_NoDataRaceBetweenEngines()
    {
        // static buffer = data race when BGM engine and SFX engine both have Delay.
        // Verify: two separate instances have independent buffers.
        var d1 = new Delay(44100);
        var d2 = new Delay(44100);
        d1.Time = 0.5f; d1.Mix = 1.0f; d1.Enabled = true;
        d2.Time = 0.25f; d2.Mix = 1.0f; d2.Enabled = true;

        var buf1 = new float[512];
        var buf2 = new float[512];
        for (int i = 0; i < 512; i++) buf1[i] = 1.0f;
        // buf2 stays silent

        d1.Process(buf1.AsSpan(), 2);
        d2.Process(buf2.AsSpan(), 2);

        // d2 should output near-zero since it received silence
        // If buffers are shared (static), d2 would output d1's audio
        bool d2IsSilent = true;
        foreach (float s in buf2)
            if (MathF.Abs(s) > 0.1f) { d2IsSilent = false; break; }

        Assert.That(d2IsSilent, Is.True,
            "d2 received silent input but output is non-zero — delay buffers may be static (shared).");
    }
}
