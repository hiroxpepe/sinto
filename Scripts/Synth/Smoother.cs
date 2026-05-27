// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;
using Sinto.Core.Audio;

namespace Sinto.Core.Synth;

public struct Smoother {
    private float _current;
    private float _target;
    private float _coeff;
    private long  _sampleIndex; // for Denormal parity — Smoother is a 1-pole IIR

    public float Current => _current;
    public float Target  => _target;

    public Smoother(float initialValue, float smoothingHz = 20f, int sampleRate = 44100)
        => throw new System.NotImplementedException();

    public void SetTarget(float target)
        => throw new System.NotImplementedException();

    // Implementation must apply Denormal or Sleep optimization:
    //   if (MathF.Abs(_target - _current) < 1e-5f) { _current = _target; return _current; }
    //   else { _current += (_target - _current) * _coeff + Denormal.Protect(0f, _sampleIndex++); }
    // Without this, the IIR difference (target-current) enters subnormal range after
    // parameter changes, causing 100x CPU spike (ARM Denormal trap).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick()
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SnapToTarget()
        => throw new System.NotImplementedException();

    public void SetSampleRate(int sampleRate, float smoothingHz = 20f)
        => throw new System.NotImplementedException();
}
