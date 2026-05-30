// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Audition;

/// <summary>
/// Thread-safe ring buffer shared between the audio thread (writer) and the
/// oscilloscope UI thread (reader). The audio thread pushes blocks via Push();
/// the UI thread takes a snapshot via GetSnapshot() without blocking either side.
/// </summary>
public sealed class ScopeBuffer
{
    const int CAPACITY = 44100 / 2;   // 0.5 sec of mono samples
    readonly float[] _ring = new float[CAPACITY];
    int _writePos;
    bool _hasData;

    // Called from the audio thread: copy a block of interleaved stereo PCM,
    // downmix to mono, and write into the ring buffer.
    public void Push(float[] buffer, int offset, int count, int channels)
    {
        int frames = count / channels;
        for (int f = 0; f < frames; f++) {
            float mono = buffer[offset + f * channels]; // left channel
            _ring[_writePos] = mono;
            _writePos = (_writePos + 1) % CAPACITY;
        }
        _hasData = true;
    }

    // Called from the UI thread: copy the most recent `count` samples into a
    // fresh array, ordered chronologically (oldest first).
    public float[] GetSnapshot(int count)
    {
        if (!_hasData) return Array.Empty<float>();
        count = Math.Min(count, CAPACITY);
        var snap = new float[count];
        int start = (_writePos - count + CAPACITY) % CAPACITY;
        for (int i = 0; i < count; i++)
            snap[i] = _ring[(start + i) % CAPACITY];
        return snap;
    }
}
