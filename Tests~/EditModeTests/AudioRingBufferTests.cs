// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sinto.Core.Audio;

namespace Sinto.Tests.Audio;

[TestFixture]
public class AudioRingBufferTests
{
    // ── Constructor Guard ────────────────────────────────────────

    [TestCase(2)] [TestCase(4)] [TestCase(512)] [TestCase(1024)] [TestCase(2048)]
    public void Constructor_PowerOfTwo_DoesNotThrow(int capacity)
        => Assert.DoesNotThrow(() => new AudioRingBuffer<int>(capacity));

    [TestCase(0)] [TestCase(1)] [TestCase(-1)] [TestCase(3)] [TestCase(1000)] [TestCase(1023)] [TestCase(1025)]
    public void Constructor_NotPowerOfTwo_ThrowsArgumentException(int capacity)
        => Assert.Throws<ArgumentException>(() => new AudioRingBuffer<int>(capacity));

    [Test]
    public void Constructor_NotPowerOfTwo_ExceptionMessageContainsValue()
    {
        var ex = Assert.Throws<ArgumentException>(() => new AudioRingBuffer<int>(1000));
        Assert.That(ex!.Message, Does.Contain("1000"));
    }

    // ── Basic Enqueue / Dequeue ──────────────────────────────────

    [Test]
    public void Dequeue_WhenEmpty_ReturnsFalse()
    {
        var buf = new AudioRingBuffer<int>(16);
        Assert.That(buf.TryDequeue(out _), Is.False);
    }

    [Test]
    public void Dequeue_WhenEmpty_ReturnsDefault()
    {
        var buf = new AudioRingBuffer<int>(16);
        buf.TryDequeue(out int val);
        Assert.That(val, Is.EqualTo(default(int)));
    }

    [Test]
    public void Enqueue_WhenEmpty_ReturnsTrue()
    {
        var buf = new AudioRingBuffer<int>(16);
        Assert.That(buf.TryEnqueue(42), Is.True);
    }

    [Test]
    public void EnqueueDequeue_ReturnsCorrectItem()
    {
        var buf = new AudioRingBuffer<int>(16);
        buf.TryEnqueue(42);
        buf.TryDequeue(out int val);
        Assert.That(val, Is.EqualTo(42));
    }

    [Test]
    public void EnqueueDequeue_PreservesFIFOOrder()
    {
        var buf = new AudioRingBuffer<int>(16);
        buf.TryEnqueue(1); buf.TryEnqueue(2); buf.TryEnqueue(3);
        buf.TryDequeue(out int a); buf.TryDequeue(out int b); buf.TryDequeue(out int c);
        Assert.That(new[] { a, b, c }, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    // ── Capacity / Full / Empty ──────────────────────────────────

    /// <summary>
    /// SPSC ring buffer sacrifices 1 slot to distinguish Full from Empty.
    /// Capacity = 4 → usable slots = 3.
    /// After 3 successful Enqueues, the 4th must return false AND IsFull must be true.
    /// </summary>
    [Test]
    public void IsFull_After3Enqueues_Into4Capacity_IsTrue()
    {
        var buf = new AudioRingBuffer<int>(4);
        Assert.That(buf.TryEnqueue(1), Is.True);
        Assert.That(buf.TryEnqueue(2), Is.True);
        Assert.That(buf.TryEnqueue(3), Is.True);
        // 4th enqueue must fail (buffer full — 1 slot sacrificed)
        Assert.That(buf.TryEnqueue(4), Is.False);
        Assert.That(buf.IsFull, Is.True);
    }

    [Test]
    public void IsEmpty_Initially_IsTrue()
    {
        var buf = new AudioRingBuffer<int>(16);
        Assert.That(buf.IsEmpty, Is.True);
    }

    [Test]
    public void IsEmpty_AfterEnqueue_IsFalse()
    {
        var buf = new AudioRingBuffer<int>(16);
        buf.TryEnqueue(1);
        Assert.That(buf.IsEmpty, Is.False);
    }

    [Test]
    public void IsEmpty_AfterEnqueueAndDequeue_IsTrue()
    {
        var buf = new AudioRingBuffer<int>(16);
        buf.TryEnqueue(1);
        buf.TryDequeue(out _);
        Assert.That(buf.IsEmpty, Is.True);
    }

    [Test]
    public void Enqueue_1023Items_Into1024Buffer_AllSucceed()
    {
        var buf = new AudioRingBuffer<int>(1024);
        for (int i = 0; i < 1023; i++)
            Assert.That(buf.TryEnqueue(i), Is.True);
    }

    [Test]
    public void Enqueue_1024thItem_Into1024Buffer_ReturnsFalse()
    {
        var buf = new AudioRingBuffer<int>(1024);
        for (int i = 0; i < 1023; i++) buf.TryEnqueue(i);
        Assert.That(buf.TryEnqueue(9999), Is.False);
    }

    [Test]
    public void WrapAround_MaintainsCorrectness()
    {
        var buf = new AudioRingBuffer<int>(4); // usable = 3
        for (int i = 0; i < 3; i++) buf.TryEnqueue(i);
        for (int i = 0; i < 3; i++) { buf.TryDequeue(out int v); Assert.That(v, Is.EqualTo(i)); }
        for (int i = 10; i < 13; i++) buf.TryEnqueue(i);
        for (int i = 10; i < 13; i++) { buf.TryDequeue(out int v); Assert.That(v, Is.EqualTo(i)); }
    }

    // ── False Sharing Prevention ─────────────────────────────────

    [Test]
    public void GenericClass_DoesNotThrow_TypeLoadException()
    {
        // TypeLoadException would fire here if LayoutKind.Explicit were applied to generic class
        Assert.DoesNotThrow(() => {
            var t = typeof(AudioRingBuffer<int>);
            _ = t.FullName;
        });
    }

    [Test]
    public void FieldLayout_HeadAndTail_AreAtLeast64BytesApart()
    {
        // Marshal.OffsetOf<T>() only works on unmanaged or explicitly-laid-out value types.
        // AudioRingBuffer<T> is a generic CLASS (reference type) — Marshal.OffsetOf would throw.
        // Alternative: use unsafe code / reflection metadata to verify padding fields exist.
        //
        // Verify indirectly: count long padding fields between _head and _tail.
        // The struct layout declares 7 × long (56 bytes) padding before _head
        // and 7 × long (56 bytes) padding before _tail.
        // We verify the padding fields exist by name in the type.
        var type = typeof(AudioRingBuffer<int>);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // Padding fields _p1.._p7 before _head
        for (int i = 1; i <= 7; i++) {
            var f = type.GetField($"_p{i}", flags);
            Assert.That(f, Is.Not.Null, $"Padding field _p{i} not found — False Sharing prevention may be missing.");
            Assert.That(f!.FieldType, Is.EqualTo(typeof(long)), $"_p{i} must be long (8 bytes).");
        }
        // Padding fields _p8.._p14 before _tail
        for (int i = 8; i <= 14; i++) {
            var f = type.GetField($"_p{i}", flags);
            Assert.That(f, Is.Not.Null, $"Padding field _p{i} not found.");
            Assert.That(f!.FieldType, Is.EqualTo(typeof(long)), $"_p{i} must be long (8 bytes).");
        }
        // _head and _tail both exist
        Assert.That(type.GetField("_head", flags), Is.Not.Null);
        Assert.That(type.GetField("_tail", flags), Is.Not.Null);
    }

    // ── GC Zero ──────────────────────────────────────────────────

    [Test]
    public void TryEnqueueDequeue_DoesNotTriggerGC()
    {
        var buf = new AudioRingBuffer<int>(1024);
        for (int i = 0; i < 100; i++) { buf.TryEnqueue(i); buf.TryDequeue(out _); }
        int gen0Before = GC.CollectionCount(0);
        for (int i = 0; i < 100_000; i++) { buf.TryEnqueue(i); buf.TryDequeue(out _); }
        Assert.That(GC.CollectionCount(0), Is.EqualTo(gen0Before));
    }

    // ── SPSC Concurrency ─────────────────────────────────────────

    [Test]
    [Timeout(5000)] // 5s timeout — prevents CI freeze if TryEnqueue/Dequeue deadlocks
    public void SPSC_100k_Operations_ZeroLoss()
    {
        const int COUNT = 100_000;
        var buf = new AudioRingBuffer<int>(4096);
        var received = new List<int>(COUNT);
        var producer = Task.Run(() => {
            for (int i = 0; i < COUNT; i++) {
                while (!buf.TryEnqueue(i)) Thread.SpinWait(10);
            }
        });
        var consumer = Task.Run(() => {
            int count = 0;
            while (count < COUNT) {
                if (buf.TryDequeue(out int v)) { received.Add(v); count++; }
                else Thread.SpinWait(10);
            }
        });
        Task.WaitAll(producer, consumer);
        Assert.That(received.Count, Is.EqualTo(COUNT));
        for (int i = 0; i < COUNT; i++)
            Assert.That(received[i], Is.EqualTo(i));
    }
}
