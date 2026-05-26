// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using NUnit.Framework;
using Sinto.Core.Audio;

namespace Sinto.Tests.Audio;

[TestFixture]
public class AudioRingBufferTests
{
    [Test]
    public void Constructor_PowerOfTwo_DoesNotThrow()
        => Assert.DoesNotThrow(() => new AudioRingBuffer<int>(1024));

    [Test]
    public void Constructor_NotPowerOfTwo_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => new AudioRingBuffer<int>(1000));

    [Test]
    public void Dequeue_WhenEmpty_ReturnsFalse()
    {
        var buf = new AudioRingBuffer<int>(16);
        Assert.That(buf.TryDequeue(out _), Is.False);
    }

    [Test]
    public void EnqueueDequeue_ReturnsCorrectItem()
    {
        var buf = new AudioRingBuffer<int>(16);
        buf.TryEnqueue(42);
        buf.TryDequeue(out int val);
        Assert.That(val, Is.EqualTo(42));
    }
}
