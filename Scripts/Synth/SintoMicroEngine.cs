// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Sinto.Core.Audio;
using Sinto.Core.Effects;

namespace Sinto.Core.Synth;

/// <summary>
/// Lightweight single-voice SFX processor for per-GameObject emitters.
/// Memory: ~50KB vs SintoEngine ~750KB.
/// MUST include minimal SPSC ring buffer (4 slots) to prevent main/audio thread data race.
/// MonoCompatible = always true (Unity AudioSource handles 3D positioning).
/// </summary>
public sealed class SintoMicroEngine
{
    // Minimal SPSC — even SFX needs thread safety
    private readonly AudioRingBuffer<ControlEvent> _events;
    private Voice          _voice;
    private readonly BBDChorus   _chorus;
    private readonly RetroFilter _retro;
    private readonly int   _sampleRate;

    public bool IsActive => throw new NotImplementedException();

    public SintoMicroEngine(int sampleRate = 44100) {
        _events    = new AudioRingBuffer<ControlEvent>(4); // minimal SPSC
        _chorus    = new BBDChorus();
        _retro     = new RetroFilter();
        _sampleRate = sampleRate;
    }

    // Main thread
    public void NoteOn(int midiNote, float velocity, in OscillatorParams osc1,
        in OscillatorParams osc2, in EnvelopeParams amp)
        => throw new NotImplementedException();

    public void NoteOff()
        => throw new NotImplementedException();

    /// <summary>
    /// Render mono output into interleaved stereo buffer.
    /// MONO DUPLICATE RULE: each computed sample is written to BOTH L and R channels.
    ///   buf[i*2]   = sample  (L)
    ///   buf[i*2+1] = sample  (R)  ← same value
    /// Without duplication: sample goes to L only, R gets next frame → 2x speed + broken 3D.
    /// </summary>
    public void RenderMono(Span<float> buffer, int channels)
        => throw new NotImplementedException();
}
