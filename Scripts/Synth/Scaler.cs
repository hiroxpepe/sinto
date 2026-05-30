// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Signo.Core.Synth;

/// <summary>Dynamic voice scaler. Tier-based reduction under load.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Scaler {
    static readonly int[] TIERS = { 32, 24, 16 };
    const float DOWN_THRESHOLD                 = 0.70f;
    const int   CONSECUTIVE_OVERLOAD_REQUIRED  = 3;
    const float UP_THRESHOLD                   = 0.40f;
    const int   COOLDOWN_CALLBACKS             = 64;
    const int   HEADROOM_CALLBACKS             = 300;

    readonly Voices _voices;
    readonly ITimer _time;
    int  _current_tier_index;
    int  _cooldown_remaining;
    int  _consecutive_overload;
    int  _consecutive_headroom;
    long _callback_start_tick;

    public int currentMaxVoices => TIERS[_current_tier_index];
    public int currentTierIndex => _current_tier_index;

    public Scaler(Voices voices, ITimer? timeProvider = null) {
        _voices = voices ?? throw new System.ArgumentNullException(nameof(voices));
        _time   = timeProvider ?? SystemTimer.Instance;
        _current_tier_index  = 0;
        _cooldown_remaining  = 0;
        _consecutive_overload = 0;
        _consecutive_headroom = 0;
        _callback_start_tick = 0L;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCallbackBegin() {
        _callback_start_tick = _time.GetTimestamp();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCallbackEnd(int bufferLength, int sampleRate = 44100) {
        // Decrement cooldown first
        if (_cooldown_remaining > 0) {
            _cooldown_remaining--;
            return;
        }
        long elapsed_ticks = _time.GetTimestamp() - _callback_start_tick;
        double elapsed_sec = (double)elapsed_ticks / _time.frequency;
        double buffer_duration = (double)bufferLength / sampleRate;
        double usage = elapsed_sec / buffer_duration;
        if (usage >= DOWN_THRESHOLD) {
            _consecutive_overload++;
            _consecutive_headroom = 0;
            if (_consecutive_overload >= CONSECUTIVE_OVERLOAD_REQUIRED &&
                _current_tier_index < TIERS.Length - 1) {
                _current_tier_index++;
                _voices.SetMaxVoices(TIERS[_current_tier_index]);
                _cooldown_remaining = COOLDOWN_CALLBACKS;
                _consecutive_overload = 0;
            }
        } else if (usage <= UP_THRESHOLD) {
            _consecutive_overload = 0;
            _consecutive_headroom++;
            if (_consecutive_headroom >= HEADROOM_CALLBACKS &&
                _current_tier_index > 0) {
                _current_tier_index--;
                _voices.SetMaxVoices(TIERS[_current_tier_index]);
                _cooldown_remaining  = COOLDOWN_CALLBACKS;
                _consecutive_headroom = 0;
            }
        } else {
            _consecutive_overload = 0;
            _consecutive_headroom = 0;
        }
    }

    public void ForceSetTier(int tierIndex) {
        if (tierIndex < 0) tierIndex = 0;
        if (tierIndex >= TIERS.Length) tierIndex = TIERS.Length - 1;
        _current_tier_index = tierIndex;
        _voices.SetMaxVoices(TIERS[tierIndex]);
    }
}
