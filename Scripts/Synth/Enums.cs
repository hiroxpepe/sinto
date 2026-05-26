// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Synth;

public enum WaveType     : byte { Sine=0, Saw=1, Triangle=2, Square=3, Noise=4 }
public enum InterpMode   : byte { Linear=0, NearestNeighbor=1 }
public enum VoiceState   : byte { Free=0, Attack=1, Decay=2, Sustain=3, Release=4, QuickRelease=5 }
public enum RetroMode    : byte { Clean=0, N64=1, PS1=2 }
public enum LFOWave      : byte { Sine=0, Triangle=1, Square=2, SH=3 }

[System.Flags]
public enum LFODestination : byte {
    None         = 0,
    OSC1Pitch    = 1 << 0,
    OSC2Pitch    = 1 << 1,
    OSC1PWM      = 1 << 2,
    OSC2PWM      = 1 << 3,
    FilterCutoff = 1 << 4,
    Amp          = 1 << 5,
}
