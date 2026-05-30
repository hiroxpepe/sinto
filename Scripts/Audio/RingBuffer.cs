// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Signo.Core.Audio;

/// <summary>Single-producer single-consumer lock-free ring buffer.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public sealed class RingBuffer<T> where T : struct
{
    readonly T[]  _buffer;
    readonly int  _mask;
    long _p1, _p2, _p3, _p4, _p5, _p6, _p7;
    int  _head;
    long _p8, _p9, _p10, _p11, _p12, _p13, _p14;
    int  _tail;

    public RingBuffer(int capacityPow2 = 1024) {
        // Capacity=1 wastes all slots (SPSC sacrifices 1 slot → 0 usable). Minimum is 2.
        if (capacityPow2 < 2 || (capacityPow2 & (capacityPow2 - 1)) != 0)
            throw new ArgumentException(
                $"Capacity must be a positive power of 2. Got: {capacityPow2}");
        _buffer = new T[capacityPow2];
        _mask   = capacityPow2 - 1;
    }

    // Diagnostic properties: read _head and _tail into locals ONCE before computing.
    // Reading _tail twice (once for subtraction, once for comparison) creates a Torn Read:
    //   another thread may update _tail between the two reads, yielding impossible values.
    public int Count {
        get { int h = Volatile.Read(ref _head); int t = Volatile.Read(ref _tail);
              return (t - h) & _mask; }
    }
    public bool IsEmpty {
        get { int h = Volatile.Read(ref _head); int t = Volatile.Read(ref _tail);
              return h == t; }
    }
    public bool IsFull {
        get { int h = Volatile.Read(ref _head); int t = Volatile.Read(ref _tail);
              return ((t + 1) & _mask) == h; }
    }

    public bool TryEnqueue(in T item) {
        int tail = Volatile.Read(ref _tail);
        int next = (tail + 1) & _mask;
        if (next == Volatile.Read(ref _head)) return false;
        _buffer[tail] = item;
        Volatile.Write(ref _tail, next);
        return true;
    }

    public bool TryDequeue(out T item) {
        int head = Volatile.Read(ref _head);
        if (head == Volatile.Read(ref _tail)) { item = default; return false; }
        item = _buffer[head];
        Volatile.Write(ref _head, (head + 1) & _mask);
        return true;
    }
}
