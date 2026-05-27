// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

public sealed class VoiceScaler {
    private static readonly int[] Tiers = { 32, 24, 16 };
    private const float DownThreshold         = 0.70f;
    private const int   ConsecutiveOverloadRequired = 3; // Must exceed threshold N times in a row
    // Single-frame spikes (GC, OS scheduler, touch event) must NOT trigger TierDown.
    // Mobile: even a simple screen touch can cause a 1-frame audio callback delay.
    // Solution: only TierDown after N consecutive overloads (default 3 = ~35ms at 512/44100).
    private const float UpThreshold      = 0.40f;
    private const int   CooldownCallbacks = 64;
    private const int   HeadroomCallbacks = 300;

    private readonly VoiceManager  _voiceManager;
    private readonly ITimeProvider _time;   // injected for testability
    private int  _currentTierIndex;
    private int  _cooldownRemaining;
    private int  _consecutiveOverload;  // consecutive frames above DownThreshold
    private int  _consecutiveHeadroom;
    private long _callbackStartTick;

    public int CurrentMaxVoices => throw new System.NotImplementedException();
    public int CurrentTierIndex => _currentTierIndex;

    /// <param name="voiceManager">The voice manager to scale.</param>
    /// <param name="timeProvider">Time source — use null for production (SystemTimeProvider).</param>
    public VoiceScaler(VoiceManager voiceManager, ITimeProvider? timeProvider = null)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCallbackBegin()
        => throw new System.NotImplementedException();

    // OnCallbackEnd implementation MUST decrement cooldown first:
    //   if (_cooldownRemaining > 0) { _cooldownRemaining--; return; }
    // Without this decrement, cooldown never expires → dynamic scaling fires once then dies.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void OnCallbackEnd(int bufferLength, int sampleRate = 44100)
        => throw new System.NotImplementedException();

    public void ForceSetTier(int tierIndex)
        => throw new System.NotImplementedException();
}
