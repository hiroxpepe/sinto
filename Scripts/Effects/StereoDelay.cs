// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Effects;

public sealed class StereoDelay : IEffect
{
    // FRACTIONAL DELAY DESIGN:
    // When delay time changes (e.g. 500ms → 250ms), the read pointer must NOT
    // jump instantly. Abrupt pointer warp causes waveform discontinuity → "pop" click noise.
    //
    // Correct implementation:
    //   _targetReadPos = currentWritePos - (newDelayTime * sampleRate)
    //   per sample: _readPos += (_targetReadPos - _readPos) * smoothCoeff
    //   (smoothCoeff ≈ 1e-3 gives ~2ms crossfade — imperceptible)
    //
    // Also: Feedback clamped to 0.95 maximum to prevent infinite feedback runaway.

    // Instance buffer (NOT static — multiple StereoDelay instances must not share buffers)
    private readonly float[] _delayBufL;
    private readonly float[] _delayBufR;
    private int   _writePos;
    private float _readPosL;      // fractional read position L
    private float _readPosR;      // fractional read position R
    private float _targetTimeSec; // smooth target for dynamic time change

    public float Time      { get; set; }  // [0.001, 2.0] seconds
    public float Feedback  { get; set; }  // [0.0, 0.95] — hard capped
    public float Mix       { get; set; }  // [0.0, 1.0]
    public bool  TempoSync { get; set; }
    public float Bpm       { get; set; }
    public float SyncNote  { get; set; }
    public bool  Enabled   { get; set; }

    public StereoDelay(int sampleRate = 44100) {
        // 2 seconds max delay at 44100Hz per channel
        _delayBufL = new float[sampleRate * 2];
        _delayBufR = new float[sampleRate * 2];
    }

    public void Process(Span<float> buffer, int channels)
        => throw new NotImplementedException();

    public void Reset()
        => throw new NotImplementedException();

    /// <summary>
    /// Update BPM for tempo sync. Recalculates target delay time.
    /// Does NOT jump the read pointer — uses fractional crossfade.
    /// </summary>
    public void SetBPM(float bpm)
        => throw new NotImplementedException();
}
