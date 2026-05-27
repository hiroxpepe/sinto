// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

/// <summary>ADSR envelope state machine. Zero-attack bypass for SFX.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Envelope {
#nullable enable
    private float     _level;
    private PlayState _phase;
    private float     _attack_rate;
    private float     _decay_rate;
    private float     _release_rate;
    private float     _sustain_level;
    private float     _quick_release_rate;

    public float     Level => _level;
    public PlayState Phase => _phase;
    public bool      IsDone => _phase == PlayState.Free;

    public void NoteOn(in EnvParams p, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        // Near-zero attack bypass — instant level, jump to Decay (≤1 sample)
        // Test: EnvParams clamps 0 → 0.001 in constructor.
        // Here: treat any attack so short it would complete in <2 samples as instant.
        if (p.Attack <= 1f / sampleRate || p.Attack <= 0.001f) {
            _level = 1f;
            _phase = PlayState.Decay;
            _attack_rate = 1f;
        } else {
            _level = 0f;
            _phase = PlayState.Attack;
            _attack_rate = 1f / (p.Attack * sampleRate);
        }
        // Defensive clamps for default(EnvParams) which bypasses constructor
        float decay   = p.Decay   <= 0f ? 0.001f : p.Decay;
        float release = p.Release <= 0f ? 0.001f : p.Release;
        _decay_rate    = 1f / (decay   * sampleRate);
        _release_rate  = 1f / (release * sampleRate);
        _sustain_level = p.Sustain;
        _quick_release_rate = 0f;
    }

    public void NoteOff() {
        if (_phase != PlayState.Free && _phase != PlayState.QuickRelease) {
            _phase = PlayState.Release;
        }
    }

    public void StartQuickRelease(int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        // 4ms quick release to prevent click on voice steal (~176 samples at 44.1kHz)
        _quick_release_rate = 1f / (0.004f * sampleRate);
        _phase = PlayState.QuickRelease;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick() {
        switch (_phase) {
            case PlayState.Attack:
                _level += _attack_rate;
                if (_level >= 1f) { _level = 1f; _phase = PlayState.Decay; }
                break;
            case PlayState.Decay:
                _level -= _decay_rate * (1f - _sustain_level);
                if (_level <= _sustain_level) {
                    _level = _sustain_level;
                    _phase = _sustain_level > 0f ? PlayState.Sustain : PlayState.Free;
                }
                break;
            case PlayState.Sustain:
                _level = _sustain_level;
                break;
            case PlayState.Release:
                _level -= _release_rate * _level;
                if (_level <= 0.0001f) { _level = 0f; _phase = PlayState.Free; }
                break;
            case PlayState.QuickRelease:
                _level -= _quick_release_rate;
                if (_level <= 0f) { _level = 0f; _phase = PlayState.Free; }
                break;
            default:
                _level = 0f;
                break;
        }
        return _level;
    }
}
