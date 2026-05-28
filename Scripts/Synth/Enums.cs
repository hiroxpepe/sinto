// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Synth;

/// <summary>Primary oscillator waveform types.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public enum WaveType : byte { Sine=0, Saw=1, Triangle=2, Square=3, Noise=4 }

/// <summary>Oscillator interpolation strategies.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public enum Interpolation : byte { Linear=0, NearestNeighbor=1 }

/// <summary>Voice lifecycle states for envelope-driven playback.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public enum PlayState : byte { Free=0, Attack=1, Decay=2, Sustain=3, Release=4, QuickRelease=5 }

/// <summary>Retro post-processing modes.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public enum RetroMode : byte { Clean=0, N64=1, PS1=2 }

/// <summary>Available low-frequency oscillator waveforms.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public enum LfoWave : byte { Sine=0, Triangle=1, Square=2, SH=3 }

/// <summary>Bitmask destinations for LFO modulation routing.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[System.Flags]
public enum LfoTarget : byte {
    None         = 0,
    OSC1Pitch    = 1 << 0,
    OSC2Pitch    = 1 << 1,
    OSC1PWM      = 1 << 2,
    OSC2PWM      = 1 << 3,
    FilterCutoff = 1 << 4,
    Amp          = 1 << 5,
}
