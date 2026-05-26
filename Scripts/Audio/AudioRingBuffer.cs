// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Sinto.Core.Audio;

[StructLayout(LayoutKind.Sequential)]
public sealed class AudioRingBuffer<T> where T : struct
{
    private readonly T[]  _buffer;
    private readonly int  _mask;
    private long _p1, _p2, _p3, _p4, _p5, _p6, _p7;
    private int  _head;
    private long _p8, _p9, _p10, _p11, _p12, _p13, _p14;
    private int  _tail;

    public AudioRingBuffer(int capacityPow2 = 1024) {
        if (capacityPow2 <= 0 || (capacityPow2 & (capacityPow2 - 1)) != 0)
            throw new ArgumentException(
                $"Capacity must be a positive power of 2. Got: {capacityPow2}");
        _buffer = new T[capacityPow2];
        _mask   = capacityPow2 - 1;
    }

    public int  Count   => (_tail - _head) & _mask;
    public bool IsEmpty => Volatile.Read(ref _head) == Volatile.Read(ref _tail);
    public bool IsFull  => ((_tail + 1) & _mask) == Volatile.Read(ref _head);

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
