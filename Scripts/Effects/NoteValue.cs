// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Signo.Core.Effects;

/// <summary>
/// Musical note value for BPM sync. Used by Flanger, Phaser and Delay.
/// ToMs converts a note value to milliseconds at a given BPM.
/// </summary>
public enum NoteValue { DottedQuarter, Quarter, DottedEighth, Eighth, Sixteenth }

/// <summary>LFO waveform shape for Flanger and Phaser.</summary>
public enum LfoWaveform { Sine, Square }

public static class NoteValueExt
{
    /// <summary>Beat ratio relative to a quarter note.</summary>
    static float Ratio(NoteValue n) => n switch {
        NoteValue.DottedQuarter => 1.500f,
        NoteValue.Quarter       => 1.000f,
        NoteValue.DottedEighth  => 0.750f,
        NoteValue.Eighth        => 0.500f,
        NoteValue.Sixteenth     => 0.250f,
        _                       => 1.000f,
    };

    /// <summary>Convert note value to milliseconds at the given BPM.</summary>
    public static float ToMs(this NoteValue n, float bpm)
        => 60_000f / bpm * Ratio(n);

    /// <summary>Convert note value to LFO frequency (Hz) at the given BPM.</summary>
    public static float ToHz(this NoteValue n, float bpm)
        => bpm / 60f / Ratio(n);
}

/// <summary>SE-70 Stereo Flanger Step Mode.</summary>
public enum FlangerStepMode { Off, Step, Gate1, Gate2, Gate3 }

/// <summary>TR-2 Tremolo LFO waveform.</summary>
public enum TremoloWaveform { Triangle, Square }

/// <summary>AW-3 Auto Wah filter direction.</summary>
public enum WahMode { Up, Down }
