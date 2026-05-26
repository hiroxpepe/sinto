// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Sinto.Core.Audio;

namespace Sinto.Core.Synth;

public sealed class SintoEngine : IDisposable {
    private readonly AudioRingBuffer<ControlEvent> _eventQueue;
    private readonly VoiceManager                  _voiceManager;
    private readonly VoiceScaler                   _voiceScaler;
    private readonly int                           _sampleRate;
    private readonly int                           _channels;
    private int   _paused;
    private float _currentBpm;

    public int   ActiveVoices     => throw new NotImplementedException();
    public int   CurrentMaxVoices => throw new NotImplementedException();
    public bool  IsPaused         => throw new NotImplementedException();
    public float CurrentBpm       => _currentBpm;
    /// <summary>Total samples rendered (advances only when not paused). For test verification.</summary>
    public long  DspTimeSamples      => throw new NotImplementedException();

    public SintoEngine(int sampleRate = 44100, int channels = 2,
        int maxVoices = 32, int bufferSize = 1024)
        => throw new NotImplementedException();

    public bool SendNoteOn(int midiNote, float velocity, int trackId,
        int priority, ushort offsetFrames)
        => throw new NotImplementedException();

    public bool SendNoteOff(int midiNote, int trackId, ushort offsetFrames)
        => throw new NotImplementedException();

    public void Pause()  => throw new NotImplementedException();
    public void Resume() => throw new NotImplementedException();
    public void SetBPM(float bpm) => throw new NotImplementedException();

    public void RequestPresetSwap(object newPreset)
        => throw new NotImplementedException();

    public void ProcessAudioCallback(Span<float> buffer)
        => throw new NotImplementedException();

    public void Dispose() => throw new NotImplementedException();

    // ── SintoMicroEngine design note ─────────────────────────────
    // SintoMicroEngine (SFX emitter) must include a minimal SPSC ring buffer
    // even though it saves memory by omitting full StereoDelay/Reverb.
    // Reason: Unity main thread calls TriggerNote() → NoteOn() directly,
    // while audio thread calls RenderMono() on the same Voice struct.
    // Without SPSC: data race on Voice.State/Envelopes → crashes.
    // Solution: AudioRingBuffer<ControlEvent>(4) — 4 slots is sufficient for SFX.


}
